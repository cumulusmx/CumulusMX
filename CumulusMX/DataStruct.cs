using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CumulusMX
{
	// The annotations on this class are so it can be serialised as JSON
	[DataContract]
	public class DataStruct(Cumulus cumulus, double outdoorTemp, int outdoorHum, double avgTempToday, double? indoorTemp, double outdoorDewpoint, double windChill,
						int? indoorHum, double pressure, double windLatest, double windAverage, double recentmaxgust, double windRunToday, int bearing, int avgbearing,
						double rainToday, double rainYesterday, double rainWeek, double rainMonth, double rainYear, double rainRate, double rainLastHour, double heatIndex, double humidex,
						double appTemp, double tempTrend, double pressTrend, double highGustToday, string highGustTodayTime, double highWindToday, int highGustBearingToday,
						string windUnit, string windRunUnit, int bearingRangeFrom10, int bearingRangeTo10, string windRoseData, double highTempToday, double lowTempToday, string highTempTodayToday,
						string lowTempTodayTime, double highPressToday, double lowPressToday, string highPressTodayTime, string lowPressTodayTime, double highRainRateToday,
						string highRainRateTodayTime, int highHumToday, int lowHumToday, string highHumTodayTime, string lowHumTodayTime, string pressUnit, string tempUnit,
						string rainUnit, double highDewpointToday, double lowDewpointToday, string highDewpointTodayTime, string lowDewpointTodayTime, double lowWindChillToday,
						string lowWindChillTodayTime, int? solarRad, int highSolarRadToday, string highSolarRadTodayTime, double? uvindex, double highUVindexToday,
						string highUVindexTodayTime, string forecast, string sunrise, string sunset, string moonrise, string moonset, double highHeatIndexToday,
						string highHeatIndexTodayTime, double highAppTempToday, double lowAppTempToday, string highAppTempTodayTime, string lowAppTempTodayTime,
						int currentSolarMax, double alltimeHighPressure, double alltimeLowPressure, double sunshineHours, string domWindDir, string lastRainTipISO,
						double highHourlyRainToday, string highHourlyRainTodayTime, string highBeaufortToday, string beaufort, string beaufortDesc, DateTime lastDataRead,
						bool dataStopped, double stormRain, string stormRainStart, int cloudbase, string cloudbaseUnit, double last24hourRain,
						double feelsLike, double highFeelsLikeToday, string highFeelsLikeTodayTime, double lowFeelsLikeToday, string lowFeelsLikeTodayTime,
						double highHumidexToday, string highHumidexTodayTime, List<DashboardAlarms> alarms)
	{
		private readonly Cumulus cumulus = cumulus;

		[IgnoreDataMember]
		public double StormRain { get; } = stormRain;

		[DataMember(Name = "StormRain")]
		public string StormRainRounded
		{
			get => StormRain.ToString(cumulus.RainFormat);
		}

		[DataMember]
		public string StormRainStart { get; } = stormRainStart;

		[DataMember]
		public int CurrentSolarMax { get; } = currentSolarMax;

		[IgnoreDataMember]
		public double HighHeatIndexToday { get; } = highHeatIndexToday;

		[DataMember(Name = "HighHeatIndexToday")]
		public string HighHeatIndexTodayRounded
		{
			get => HighHeatIndexToday.ToString(cumulus.TempFormat);
		}

		[DataMember]
		public string HighHeatIndexTodayTime { get; } = highHeatIndexTodayTime;

		[DataMember]
		public string Sunrise { get; } = sunrise;

		[DataMember]
		public string Sunset { get; } = sunset;

		[DataMember]
		public string Moonrise { get; } = moonrise;

		[DataMember]
		public string Moonset { get; } = moonset;

		[DataMember]
		public string Forecast { get; } = forecast;

		[IgnoreDataMember]
		public double? UVindex { get; } = uvindex;

		[DataMember(Name = "UVindex")]
		public string UVindexRounded
		{
			get => UVindex.HasValue ? UVindex.Value.ToString(cumulus.UVFormat) : "-";
		}

		[IgnoreDataMember]
		public double HighUVindexToday { get; } = highUVindexToday;

		[DataMember(Name = "HighUVindexToday")]
		public string HighUVindexTodayRounded
		{
			get => HighUVindexToday.ToString(cumulus.UVFormat);
		}

		[DataMember]
		public string HighUVindexTodayTime { get; } = highUVindexTodayTime;

		[DataMember]
		public string HighSolarRadTodayTime { get; } = highSolarRadTodayTime;

		[DataMember]
		public int HighSolarRadToday { get; } = highSolarRadToday;

		[IgnoreDataMember]
		public int? SolarRad { get; } = solarRad;
		[DataMember(Name = "SolarRad")]
		public string SolarRadRounded
		{
			get => SolarRad.HasValue ? SolarRad.ToString() : "-";
		}

		[IgnoreDataMember]
		public double? IndoorTemp { get; } = indoorTemp;

		[DataMember(Name = "IndoorTemp")]
		public string IndoorTempRounded
		{
			get => IndoorTemp.HasValue ? IndoorTemp.Value.ToString(cumulus.TempFormat) : "-";
		}

		[IgnoreDataMember]
		public double OutdoorDewpoint { get; } = outdoorDewpoint;

		[DataMember(Name = "OutdoorDewpoint")]
		public string OutdoorDewpointRounded
		{
			get => OutdoorDewpoint.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double LowDewpointToday { get; } = lowDewpointToday;

		[DataMember(Name = "LowDewpointToday")]
		public string LowDewpointTodayRounded
		{
			get => LowDewpointToday.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double HighDewpointToday { get; } = highDewpointToday;

		[DataMember(Name = "HighDewpointToday")]
		public string HighDewpointTodayRounded
		{
			get => HighDewpointToday.ToString(cumulus.TempFormat);
		}

		[DataMember]
		public string LowDewpointTodayTime { get; } = lowDewpointTodayTime;

		[DataMember]
		public string HighDewpointTodayTime { get; } = highDewpointTodayTime;

		[IgnoreDataMember]
		public double WindChill { get; } = windChill;

		[DataMember(Name = "WindChill")]
		public string WindChillRounded
		{
			get => WindChill.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double LowWindChillToday { get; } = lowWindChillToday;

		[DataMember(Name = "LowWindChillToday")]
		public string LowWindChillTodayRounded
		{
			get => LowWindChillToday.ToString(cumulus.TempFormat);
		}

		[DataMember]
		public string LowWindChillTodayTime { get; } = lowWindChillTodayTime;

		[DataMember]
		public string WindUnit { get; } = windUnit;

		[DataMember]
		public string WindRunUnit { get; } = windRunUnit;

		[DataMember]
		public string RainUnit { get; } = rainUnit;

		[DataMember]
		public string TempUnit { get; } = tempUnit;

		[DataMember]
		public string PressUnit { get; } = pressUnit;

		[DataMember]
		public string CloudbaseUnit { get; } = cloudbaseUnit;

		[DataMember]
		public int Cloudbase { get; } = cloudbase;

		[DataMember]
		public string LowHumTodayTime { get; } = lowHumTodayTime;

		[DataMember]
		public string HighHumTodayTime { get; } = highHumTodayTime;

		[DataMember]
		public int LowHumToday { get; } = lowHumToday;

		[DataMember]
		public int HighHumToday { get; } = highHumToday;

		[DataMember]
		public string HighRainRateTodayTime { get; } = highRainRateTodayTime;

		[IgnoreDataMember]
		public double HighRainRateToday { get; } = highRainRateToday;

		[DataMember(Name = "HighRainRateToday")]
		public string HighRainRateTodayRounded
		{
			get => HighRainRateToday.ToString(cumulus.RainFormat);
		}

		[DataMember]
		public string HighHourlyRainTodayTime { get; } = highHourlyRainTodayTime;

		[IgnoreDataMember]
		public double HighHourlyRainToday { get; } = highHourlyRainToday;

		[DataMember(Name = "HighHourlyRainToday")]
		public string HighHourlyRainTodayRounded
		{
			get => HighHourlyRainToday.ToString(cumulus.RainFormat);
		}

		[DataMember]
		public string LowPressTodayTime { get; } = lowPressTodayTime;

		[DataMember]
		public string HighPressTodayTime { get; } = highPressTodayTime;

		[IgnoreDataMember]
		public double LowPressToday { get; } = lowPressToday;

		[DataMember(Name = "LowPressToday")]
		public string LowPressTodayRounded
		{
			get => LowPressToday.ToString(cumulus.PressFormat);
		}

		[IgnoreDataMember]
		public double HighPressToday { get; } = highPressToday;

		[DataMember(Name = "HighPressToday")]
		public string HighPressTodayRounded
		{
			get => HighPressToday.ToString(cumulus.PressFormat);
		}

		[DataMember]
		public string LowTempTodayTime { get; } = lowTempTodayTime;

		[DataMember]
		public string HighTempTodayTime { get; } = highTempTodayToday;

		[IgnoreDataMember]
		public double LowTempToday { get; } = lowTempToday;

		[DataMember(Name = "LowTempToday")]
		public string LowTempTodayRounded
		{
			get => LowTempToday.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double HighTempToday { get; } = highTempToday;

		[DataMember(Name = "HighTempToday")]
		public string HighTempTodayRounded
		{
			get => HighTempToday.ToString(cumulus.TempFormat);
		}

		[DataMember]
		public string WindRoseData { get; } = windRoseData;

		[DataMember]
		public int BearingRangeTo10 { get; } = bearingRangeTo10;

		[DataMember]
		public int BearingRangeFrom10 { get; } = bearingRangeFrom10;

		[DataMember]
		public int HighGustBearingToday { get; } = highGustBearingToday;

		[IgnoreDataMember]
		public double HighWindToday { get; } = highWindToday;

		[DataMember(Name = "HighWindToday")]
		public string HighWindTodayRounded
		{
			get => HighWindToday.ToString(cumulus.WindAvgFormat);
		}

		[DataMember]
		public string HighGustTodayTime { get; } = highGustTodayTime;

		[IgnoreDataMember]
		public double HighGustToday { get; } = highGustToday;

		[DataMember(Name = "HighGustToday")]
		public string HighGustTodayRounded
		{
			get => HighGustToday.ToString(cumulus.WindFormat);
		}

		[IgnoreDataMember]
		public double OutdoorTemp { get; } = outdoorTemp;

		[DataMember(Name = "OutdoorTemp")]
		public string OutdoorTempRounded
		{
			get => OutdoorTemp.ToString(cumulus.TempFormat);
		}

		[DataMember]
		public int OutdoorHum { get; } = outdoorHum;

		[IgnoreDataMember]
		public double AvgTempToday { get; } = avgTempToday;

		[DataMember(Name = "AvgTempToday")]
		public string AvgTempRounded
		{
			get => AvgTempToday.ToString(cumulus.TempFormat);
		}


		[IgnoreDataMember]
		public int? IndoorHum { get; } = indoorHum;

		[DataMember(Name = "IndoorHum")]
		public string IndoorHumNull
		{
			get => IndoorHum.HasValue ? IndoorHum.ToString() : "-";
		}

		[IgnoreDataMember]
		public double Pressure { get; } = pressure;

		[DataMember(Name = "Pressure")]
		public string PressureRounded
		{
			get => Pressure.ToString(cumulus.PressFormat);
		}

		[IgnoreDataMember]
		public double AlltimeHighPressure { get; } = alltimeHighPressure;

		[DataMember(Name = "AlltimeHighPressure")]
		public string AlltimeHighPressureRounded
		{
			get => AlltimeHighPressure.ToString(cumulus.PressFormat);
		}

		[IgnoreDataMember]
		public double AlltimeLowPressure { get; } = alltimeLowPressure;

		[DataMember(Name = "AlltimeLowPressure")]
		public string AlltimeLowPressureRounded
		{
			get => AlltimeLowPressure.ToString(cumulus.PressFormat);
		}

		[IgnoreDataMember]
		public double WindLatest { get; } = windLatest;

		[DataMember(Name = "WindLatest")]
		public string WindLatestRounded
		{
			get => WindLatest.ToString(cumulus.WindFormat);
		}

		[IgnoreDataMember]
		public double WindAverage { get; } = windAverage;

		[DataMember(Name = "WindAverage")]
		public string WindAverageRounded
		{
			get => WindAverage.ToString(cumulus.WindAvgFormat);
		}

		[IgnoreDataMember]
		public double Recentmaxgust { get; } = recentmaxgust;

		[DataMember(Name = "Recentmaxgust")]
		public string RecentmaxgustRounded
		{
			get => Recentmaxgust.ToString(cumulus.WindFormat);
		}

		[IgnoreDataMember]
		public double WindRunToday { get; } = windRunToday;

		[DataMember(Name = "WindRunToday")]
		public string WindRunTodayRounded
		{
			get => WindRunToday.ToString(cumulus.WindRunFormat);
		}

		[DataMember]
		public int Bearing { get; } = bearing;

		[DataMember]
		public int Avgbearing { get; } = avgbearing;

		[IgnoreDataMember]
		public double RainToday { get; } = rainToday;

		[DataMember(Name = "RainToday")]
		public string RainTodayRounded
		{
			get => RainToday.ToString(cumulus.RainFormat);
		}

		[IgnoreDataMember]
		public double RainYesterday { get; } = rainYesterday;

		[DataMember(Name = "RainYesterday")]
		public string RainYesterdayRounded
		{
			get => RainYesterday.ToString(cumulus.RainFormat);
		}

		[IgnoreDataMember]
		public double RainWeek { get; } = rainWeek;

		[DataMember(Name = "RainWeek")]
		public string RainWeekRounded
		{
			get => RainWeek.ToString(cumulus.RainFormat);
		}


		[IgnoreDataMember]
		public double RainMonth { get; } = rainMonth;

		[DataMember(Name = "RainMonth")]
		public string RainMonthRounded
		{
			get => RainMonth.ToString(cumulus.RainFormat);
		}

		[IgnoreDataMember]
		public double RainYear { get; } = rainYear;
		[DataMember(Name = "RainYear")]
		public string RainYearRounded
		{
			get => RainYear.ToString(cumulus.RainFormat);
		}

		[IgnoreDataMember]
		public double RainRate { get; } = rainRate;

		[DataMember(Name = "RainRate")]
		public string RainRateRounded
		{
			get => RainRate.ToString(cumulus.RainFormat);
		}

		[IgnoreDataMember]
		public double RainLastHour { get; } = rainLastHour;

		[DataMember(Name = "RainLastHour")]
		public string RainLastHourRounded
		{
			get => RainLastHour.ToString(cumulus.RainFormat);
		}

		[IgnoreDataMember]
		public double RainLast24Hour { get; } = last24hourRain;

		[DataMember(Name = "RainLast24Hour")]
		public string RainLast24HourRounded
		{
			get => RainLast24Hour.ToString(cumulus.RainFormat);
		}

		[IgnoreDataMember]
		public double HeatIndex { get; } = heatIndex;

		[DataMember(Name = "HeatIndex")]
		public string HeatIndexRounded
		{
			get => HeatIndex.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double Humidex { get; } = humidex;

		[DataMember(Name = "Humidex")]
		public string HumidexRounded
		{
			get => Humidex.ToString(cumulus.TempFormat);
		}

		[DataMember]
		public string HighHumidexTodayTime { get; } = highHumidexTodayTime;

		[IgnoreDataMember]
		public double HighHumidexToday { get; } = highHumidexToday;

		[DataMember(Name = "HighHumidexToday")]
		public string HighHumidexTodayRounded
		{
			get => HighHumidexToday.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double AppTemp { get; } = appTemp;

		[DataMember(Name = "AppTemp")]
		public string AppTempRounded
		{
			get => AppTemp.ToString(cumulus.TempFormat);
		}

		[DataMember]
		public string LowAppTempTodayTime { get; } = lowAppTempTodayTime;

		[DataMember]
		public string HighAppTempTodayTime { get; } = highAppTempTodayTime;

		[IgnoreDataMember]
		public double LowAppTempToday { get; } = lowAppTempToday;

		[DataMember(Name = "LowAppTempToday")]
		public string LowAppTempTodayRounded
		{
			get => LowAppTempToday.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double HighAppTempToday { get; } = highAppTempToday;

		[DataMember(Name = "HighAppTempToday")]
		public string HighAppTempTodayRounded
		{
			get => HighAppTempToday.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double FeelsLike { get; } = feelsLike;

		[DataMember(Name = "FeelsLike")]
		public string FeelsLikeRounded
		{
			get => FeelsLike.ToString(cumulus.TempFormat);
		}

		[DataMember]
		public string LowFeelsLikeTodayTime { get; } = lowFeelsLikeTodayTime;

		[DataMember]
		public string HighFeelsLikeTodayTime { get; } = highFeelsLikeTodayTime;

		[IgnoreDataMember]
		public double LowFeelsLikeToday { get; } = lowFeelsLikeToday;

		[DataMember(Name = "LowFeelsLikeToday")]
		public string LowFeelsLikeTodayRounded
		{
			get => LowFeelsLikeToday.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double HighFeelsLikeToday { get; } = highFeelsLikeToday;

		[DataMember(Name = "HighFeelsLikeToday")]
		public string HighFeelsLikeTodayRounded
		{
			get => HighFeelsLikeToday.ToString(cumulus.TempFormat);
		}

		[IgnoreDataMember]
		public double TempTrend { get; } = tempTrend;

		[DataMember(Name = "TempTrend")]
		public string TempTrendRounded
		{
			get => TempTrend.ToString(cumulus.TempTrendFormat);
		}

		[IgnoreDataMember]
		public double PressTrend { get; } = pressTrend;

		[DataMember(Name = "PressTrend")]
		public string PressTrendRounded
		{
			get => PressTrend.ToString(cumulus.PressTrendFormat);
		}

		[IgnoreDataMember]
		public double SunshineHours { get; } = sunshineHours;

		[DataMember(Name = "SunshineHours")]
		public string SunshineHoursRounded
		{
			get => SunshineHours.ToString(cumulus.SunFormat);
		}

		[DataMember]
		public string Version
		{
			get => cumulus.Version;
		}

		[DataMember]
		public string Build
		{
			get => cumulus.Build;
		}

		[DataMember]
		public string DominantWindDirection { get; } = domWindDir;

		[DataMember]
		public string LastRainTipISO { get; } = lastRainTipISO;

		[DataMember]
		public string HighBeaufortToday { get; } = highBeaufortToday;

		[DataMember]
		public string Beaufort { get; } = beaufort;

		[DataMember]
		public string BeaufortDesc { get; } = beaufortDesc;

		[DataMember]
		public string LastDataRead { get; } = lastDataRead.ToLocalTime().ToString(cumulus.ProgramOptions.TimeFormatLong);

		[DataMember]
		public string LastDataReadDate
		{
			get => lastDataRead.ToLocalTime().ToString("d");
		}


		[DataMember]
		public bool DataStopped { get; } = dataStopped;

		[DataMember]
		public List<DashboardAlarms> Alarms { get; } = alarms;
	}
}
