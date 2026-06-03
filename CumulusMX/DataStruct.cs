using System;
using System.Collections.Generic;
using System.Linq;

namespace CumulusMX
{
	// The annotations on this class are so it can be serialised as JSON
	public class DataStruct(Cumulus cumulus, string windRoseData, List<DashboardAlarms> alarms)
	{
		private readonly Cumulus cumulus = cumulus;

		public string StormRain { get => MetData.StormRain.ToString(cumulus.RainFormat); }

		public string StormRainStart { get => MetData.StartOfStorm == DateTime.MinValue ? "-----" : MetData.StartOfStorm.ToString("d"); }

		public int CurrentSolarMax { get => MetData.CurrentSolarMax; }

		public string HighHeatIndexToday { get => DailyHighLow.Today.HighHeatIndex.ToFixedLocal(cumulus.TempFormat); }

		public string HighHeatIndexTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighHeatIndexTime); }

		public string Sunrise { get => WeatherStation.GetTimeString(cumulus.SunRiseTime, cumulus.ProgramOptions.TimeFormat); }

		public string Sunset { get => WeatherStation.GetTimeString(cumulus.SunSetTime, cumulus.ProgramOptions.TimeFormat); }

		public string Moonrise { get => WeatherStation.GetTimeString(cumulus.MoonRiseTime, cumulus.ProgramOptions.TimeFormat); }

		public string Moonset { get => WeatherStation.GetTimeString(cumulus.MoonSetTime, cumulus.ProgramOptions.TimeFormat); }

		public string Forecast { get => MetData.ForecastStr; }

		public string UVindex { get => MetData.UV.ToFixedLocal(cumulus.UVFormat, "-"); }

		public string HighUVindexToday { get => DailyHighLow.Today.HighUv.ToString(cumulus.UVFormat); }
 
		public string HighUVindexTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighUvTime); }

		public string HighSolarRadTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighSolarTime); }

		public int HighSolarRadToday { get => DailyHighLow.Today.HighSolar; }

		public string SolarRad { get => MetData.SolarRad.ToText("-"); }

		public string IndoorTemp { get => MetData.TemperatureIn.ToFixedLocal(cumulus.TempFormat, "-"); }

		public string OutdoorDewpoint { get => MetData.Dewpoint.ToFixedLocal(cumulus.TempFormat); }

		public string LowDewpointToday { get => DailyHighLow.Today.LowDewPoint.ToFixedLocal(cumulus.TempFormat); }

		public string HighDewpointToday { get => DailyHighLow.Today.HighDewPoint.ToFixedLocal(cumulus.TempFormat); }

		public string LowDewpointTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.LowDewPointTime); }

		public string HighDewpointTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighDewPointTime); }

		public string WindChill { get => MetData.WindChill.ToFixedLocal(cumulus.TempFormat); }

		public string LowWindChillToday { get => DailyHighLow.Today.LowWindChill.ToFixedLocal(cumulus.TempFormat); }

		public string LowWindChillTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.LowWindChillTime); }

		public string BGT { get => MetData.BlackGlobeTemp.ToFixedLocal(cumulus.TempFormat, "-"); }

		public string WBGT { get => MetData.WetBulbGlobeTemp.ToFixedLocal(cumulus.TempFormat, "-"); }

		public string WindUnit { get => cumulus.Units.WindText; }

		public string WindRunUnit { get => cumulus.Units.WindRunText; }

		public string RainUnit { get => cumulus.Units.RainText; }

		public string TempUnit { get => cumulus.Units.TempText; }

		public string PressUnit { get => cumulus.Units.PressText; }

		public string CloudbaseUnit { get => cumulus.CloudBaseInFeet ? "ft" : "m"; }

		public int Cloudbase { get => MetData.CloudBase; }

		public string LowHumTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.LowHumidityTime); }

		public string HighHumTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighHumidityTime); }

		public int LowHumToday { get => DailyHighLow.Today.LowHumidity; }

		public int HighHumToday { get => DailyHighLow.Today.HighHumidity; }

		public string HighRainRateTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighRainRateTime); }

		public string HighRainRateToday { get => DailyHighLow.Today.HighRainRate.ToString(cumulus.RainFormat); }

		public string HighHourlyRainTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighHourlyRainTime); }

		public string HighHourlyRainToday { get => DailyHighLow.Today.HighHourlyRain.ToString(cumulus.RainFormat); }

		public string LowPressTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.LowPressTime); }

		public string HighPressTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighPressTime); }

		public string LowPressToday { get => DailyHighLow.Today.LowPress.ToString(cumulus.PressFormat); }

		public string HighPressToday { get => DailyHighLow.Today.HighPress.ToString(cumulus.PressFormat); }

		public string LowTempTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.LowTempTime); }

		public string HighTempTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighTempTime); }

		public string LowTempToday { get => DailyHighLow.Today.LowTemp.ToFixedLocal(cumulus.TempFormat); }

		public string HighTempToday { get => DailyHighLow.Today.HighTemp.ToFixedLocal(cumulus.TempFormat); }

		public string WindRoseData { get; } = windRoseData;

		public int BearingRangeTo10 { get => MetData.BearingRangeTo10; }

		public int BearingRangeFrom10 { get => MetData.BearingRangeFrom10; }

		public int HighGustBearingToday { get => DailyHighLow.Today.HighGustBearing; }

		public string HighWindToday { get => DailyHighLow.Today.HighWind.ToString(cumulus.WindAvgFormat); }

		public string HighGustTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighGustTime); }

		public string HighGustToday { get => DailyHighLow.Today.HighGust.ToString(cumulus.WindAvgFormat); }

		public string OutdoorTemp { get => MetData.Temperature.ToFixedLocal(cumulus.TempFormat); }

		public int OutdoorHum { get => MetData.Humidity; }

		public string AvgTempToday { get => MetData.AverageTemp.ToFixedLocal(cumulus.TempFormat); }

		public string IndoorHum { get => MetData.HumidityIn.ToText("-"); }

		public string Pressure { get => MetData.Pressure.ToFixedLocal(cumulus.PressFormat); }

		public string AlltimeHighPressure { get => Records.AllTime.HighPress.Val.ToString(cumulus.PressFormat); }

		public string AlltimeLowPressure { get => Records.AllTime.LowPress.Val.ToString(cumulus.PressFormat); }

		public string WindLatest { get => MetData.WindLatest.ToString(cumulus.WindFormat); }

		public string WindAverage { get => MetData.WindAverage.ToString(cumulus.WindAvgFormat); }

		public string Recentmaxgust { get => MetData.RecentMaxGust.ToString(cumulus.WindFormat); }

		public string WindRunToday { get => MetData.WindRunToday.ToString(cumulus.WindRunFormat); }

		public int Bearing { get => MetData.WindBearing; }

		public int Avgbearing { get => MetData.WindAvgBearing; }

		public string RainToday { get => MetData.RainToday.ToString(cumulus.RainFormat); }

		public string RainYesterday { get => MetData.RainYesterday.ToString(cumulus.RainFormat); }

		public string RainWeek { get => MetData.RainWeek.ToString(cumulus.RainFormat); }

		public string RainMonth { get => MetData.RainMonth.ToString(cumulus.RainFormat); }

		public string RainYear { get => MetData.RainYear.ToString(cumulus.RainFormat); }

		public string RainRate { get => MetData.RainRate.ToString(cumulus.RainFormat); }

		public string RainLastHour { get => MetData.RainLastHour.ToString(cumulus.RainFormat); }

		public string RainLast24Hour { get => MetData.RainLast24Hour.ToString(cumulus.RainFormat); }

		public string HeatIndex { get => MetData.HeatIndex.ToString(cumulus.TempFormat); }

		public string Humidex { get => MetData.Humidex.ToString(cumulus.TempFormat); }

		public string HighHumidexTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighHumidexTime); }

		public string HighHumidexToday { get => DailyHighLow.Today.HighHumidex.ToFixedLocal(cumulus.TempFormat); }

		public string AppTemp { get => MetData.ApparentTemperature.ToString(cumulus.TempFormat); }

		public string LowAppTempTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.LowAppTempTime); }

		public string HighAppTempTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighAppTempTime); }

		public string LowAppTempToday { get => DailyHighLow.Today.LowAppTemp.ToFixedLocal(cumulus.TempFormat); }

		public string HighAppTempToday { get => DailyHighLow.Today.HighAppTemp.ToFixedLocal(cumulus.TempFormat); }

		public string FeelsLike { get => MetData.FeelsLike.ToString(cumulus.TempFormat); }

		public string LowFeelsLikeTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.LowFeelsLikeTime); }

		public string HighFeelsLikeTodayTime { get => WeatherStation.GetTimeString(DailyHighLow.Today.HighFeelsLikeTime); }

		public string LowFeelsLikeToday { get => DailyHighLow.Today.LowFeelsLike.ToFixedLocal(cumulus.TempFormat); }

		public string HighFeelsLikeToday { get => DailyHighLow.Today.HighFeelsLike.ToFixedLocal(cumulus.TempFormat); }

		public string TempTrend { get => MetData.TempTrendVal.ToString(cumulus.TempTrendFormat); }

		public string PressTrend { get => MetData.PressTrendStr; }

		public string SunshineHours { get => MetData.SunshineHours.ToString(cumulus.SunFormat); }

		public string Version { get => cumulus.Version; }

		public string Build { get => cumulus.Build; }

		public string DominantWindDirection { get => cumulus.CompassPoint(MetData.DominantWindBearing); }

		public string LastRainTipISO { get => MetData.LastRainTip; }

		public string HighBeaufortToday { get => "F" + Cumulus.Beaufort(DailyHighLow.Today.HighWind); }

		public string Beaufort { get => "F" + Cumulus.Beaufort(MetData.WindAverage); }

		public string BeaufortDesc { get => cumulus.BeaufortDesc(MetData.WindAverage); }

		//public string LastDataRead { get => cumulus.Stations.Max(s => s.LastDataReadTimestamp).ToLocalTime().ToString(cumulus.ProgramOptions.TimeFormatLong); } 
		public string LastDataRead { get => cumulus.Stations[0].LastDataReadTimestamp.ToLocalTime().ToString(cumulus.ProgramOptions.TimeFormatLong); }

		//public string LastDataReadDate { get => cumulus.Stations.Max(s => s.LastDataReadTimestamp).ToLocalTime().ToString("d"); }
		public string LastDataReadDate { get => cumulus.Stations[0].LastDataReadTimestamp.ToLocalTime().ToString("d"); }

		public bool DataStopped { get => cumulus.Stations[0].DataStopped; }

		public List<DashboardAlarms> Alarms { get; } = alarms;
	}
}
