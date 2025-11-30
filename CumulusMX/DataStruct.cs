using System;
using System.Collections.Generic;

namespace CumulusMX
{
	// The annotations on this class are so it can be serialised as JSON
	public class DataStruct(
		Cumulus cumulus, double outdoorTemp, int outdoorHum, double avgTempToday, double? indoorTemp, double outdoorDewpoint, double windChill,
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

		double _stormRain = stormRain;

		public string StormRain
		{
			get => _stormRain.ToString(cumulus.RainFormat);
		}

		public string StormRainStart { get; } = stormRainStart;

		public int CurrentSolarMax { get; } = currentSolarMax;

		double _highHeatIndexToday = highHeatIndexToday;

		public string HighHeatIndexToday
		{
			get => _highHeatIndexToday.ToString(cumulus.TempFormat);
		}

		public string HighHeatIndexTodayTime { get; } = highHeatIndexTodayTime;

		public string Sunrise { get; } = sunrise;

		public string Sunset { get; } = sunset;

		public string Moonrise { get; } = moonrise;

		public string Moonset { get; } = moonset;

		public string Forecast { get; } = forecast;

		double? _uVindex { get; } = uvindex;

		public string UVindex
		{
			get => _uVindex.HasValue ? _uVindex.Value.ToString(cumulus.UVFormat) : "-";
		}

		double _highUVindexToday = highUVindexToday;

		public string HighUVindexToday
		{
			get => _highUVindexToday.ToString(cumulus.UVFormat);
		}

		public string HighUVindexTodayTime { get; } = highUVindexTodayTime;

		public string HighSolarRadTodayTime { get; } = highSolarRadTodayTime;

		public int HighSolarRadToday { get; } = highSolarRadToday;

		int? _SolarRad = solarRad;

		public string SolarRad
		{
			get => _SolarRad.HasValue ? _SolarRad.ToString() : "-";
		}

		double? _indoorTemp = indoorTemp;

		public string IndoorTemp
		{
			get => _indoorTemp.HasValue ? _indoorTemp.Value.ToString(cumulus.TempFormat) : "-";
		}

		double _outdoorDewpoint = outdoorDewpoint;

		public string OutdoorDewpoint
		{
			get => _outdoorDewpoint.ToString(cumulus.TempFormat);
		}

		double _lowDewpointToday = lowDewpointToday;

		public string LowDewpointToday
		{
			get => _lowDewpointToday.ToString(cumulus.TempFormat);
		}

		double _highDewpointToday = highDewpointToday;

		public string HighDewpointToday
		{
			get => _highDewpointToday.ToString(cumulus.TempFormat);
		}

		public string LowDewpointTodayTime { get; } = lowDewpointTodayTime;

		public string HighDewpointTodayTime { get; } = highDewpointTodayTime;

		double _windChill = windChill;

		public string WindChill
		{
			get => _windChill.ToString(cumulus.TempFormat);
		}

		double _lowWindChillToday = lowWindChillToday;

		public string LowWindChillToday
		{
			get => _lowWindChillToday.ToString(cumulus.TempFormat);
		}

		public string LowWindChillTodayTime { get; } = lowWindChillTodayTime;

		public string WindUnit { get; } = windUnit;

		public string WindRunUnit { get; } = windRunUnit;

		public string RainUnit { get; } = rainUnit;

		public string TempUnit { get; } = tempUnit;

		public string PressUnit { get; } = pressUnit;

		public string CloudbaseUnit { get; } = cloudbaseUnit;

		public int Cloudbase { get; } = cloudbase;

		public string LowHumTodayTime { get; } = lowHumTodayTime;

		public string HighHumTodayTime { get; } = highHumTodayTime;

		public int LowHumToday { get; } = lowHumToday;

		public int HighHumToday { get; } = highHumToday;

		public string HighRainRateTodayTime { get; } = highRainRateTodayTime;

		double _highRainRateToday = highRainRateToday;

		public string HighRainRateToday
		{
			get => _highRainRateToday.ToString(cumulus.RainFormat);
		}

		public string HighHourlyRainTodayTime { get; } = highHourlyRainTodayTime;

		double _highHourlyRainToday = highHourlyRainToday;

		public string HighHourlyRainToday
		{
			get => _highHourlyRainToday.ToString(cumulus.RainFormat);
		}

		public string LowPressTodayTime { get; } = lowPressTodayTime;

		public string HighPressTodayTime { get; } = highPressTodayTime;

		double _lowPressToday = lowPressToday;

		public string LowPressToday
		{
			get => _lowPressToday.ToString(cumulus.PressFormat);
		}

		double _highPressToday = highPressToday;

		public string HighPressToday
		{
			get => _highPressToday.ToString(cumulus.PressFormat);
		}

		public string LowTempTodayTime { get; } = lowTempTodayTime;

		public string HighTempTodayTime { get; } = highTempTodayToday;

		double _lowTempToday = lowTempToday;

		public string LowTempToday
		{
			get => _lowTempToday.ToString(cumulus.TempFormat);
		}

		double _highTempToday = highTempToday;

		public string HighTempToday
		{
			get => _highTempToday.ToString(cumulus.TempFormat);
		}

		public string WindRoseData { get; } = windRoseData;

		public int BearingRangeTo10 { get; } = bearingRangeTo10;

		public int BearingRangeFrom10 { get; } = bearingRangeFrom10;

		public int HighGustBearingToday { get; } = highGustBearingToday;

		double _highWindToday = highWindToday;

		public string HighWindToday
		{
			get => _highWindToday.ToString(cumulus.WindAvgFormat);
		}

		public string HighGustTodayTime { get; } = highGustTodayTime;

		double _highGustToday = highGustToday;

		public string HighGustToday
		{
			get => _highGustToday.ToString(cumulus.WindFormat);
		}

		double _outdoorTemp = outdoorTemp;

		public string OutdoorTemp
		{
			get => _outdoorTemp.ToString(cumulus.TempFormat);
		}

		public int OutdoorHum { get; } = outdoorHum;

		double _avgTempToday = avgTempToday;

		public string AvgTempToday
		{
			get => _avgTempToday.ToString(cumulus.TempFormat);
		}

		int? _indoorHum = indoorHum;

		public string IndoorHum
		{
			get => _indoorHum.HasValue ? _indoorHum.ToString() : "-";
		}

		double _pressure = pressure;

		public string Pressure
		{
			get => _pressure.ToString(cumulus.PressFormat);
		}

		double _alltimeHighPressure = alltimeHighPressure;

		public string AlltimeHighPressure
		{
			get => _alltimeHighPressure.ToString(cumulus.PressFormat);
		}

		double _alltimeLowPressure = alltimeLowPressure;

		public string AlltimeLowPressure
		{
			get => _alltimeLowPressure.ToString(cumulus.PressFormat);
		}

		double _windLatest = windLatest;

		public string WindLatest
		{
			get => _windLatest.ToString(cumulus.WindFormat);
		}

		double _windAverage = windAverage;

		public string WindAverage
		{
			get => _windAverage.ToString(cumulus.WindAvgFormat);
		}

		double _recentmaxgust = recentmaxgust;

		public string Recentmaxgust
		{
			get => _recentmaxgust.ToString(cumulus.WindFormat);
		}

		double _windRunToday = windRunToday;

		public string WindRunToday
		{
			get => _windRunToday.ToString(cumulus.WindRunFormat);
		}

		public int Bearing { get; } = bearing;

		public int Avgbearing { get; } = avgbearing;

		double _rainToday = rainToday;

		public string RainToday
		{
			get => _rainToday.ToString(cumulus.RainFormat);
		}

		double _rainYesterday = rainYesterday;

		public string RainYesterday
		{
			get => _rainYesterday.ToString(cumulus.RainFormat);
		}

		double _rainWeek = rainWeek;

		public string RainWeek
		{
			get => _rainWeek.ToString(cumulus.RainFormat);
		}

		double _rainMonth = rainMonth;

		public string RainMonth
		{
			get => _rainMonth.ToString(cumulus.RainFormat);
		}

		double _rainYear = rainYear;

		public string RainYear
		{
			get => _rainYear.ToString(cumulus.RainFormat);
		}

		double _rainRate = rainRate;

		public string RainRate
		{
			get => _rainRate.ToString(cumulus.RainFormat);
		}

		double _rainLastHour = rainLastHour;

		public string RainLastHour
		{
			get => _rainLastHour.ToString(cumulus.RainFormat);
		}

		double _rainLast24Hour = last24hourRain;

		public string RainLast24Hour
		{
			get => _rainLast24Hour.ToString(cumulus.RainFormat);
		}

		double _heatIndex = heatIndex;

		public string HeatIndex
		{
			get => _heatIndex.ToString(cumulus.TempFormat);
		}

		double _humidex = humidex;

		public string Humidex
		{
			get => _humidex.ToString(cumulus.TempFormat);
		}

		public string HighHumidexTodayTime { get; } = highHumidexTodayTime;

		double _highHumidexToday = highHumidexToday;

		public string HighHumidexToday
		{
			get => _highHumidexToday.ToString(cumulus.TempFormat);
		}

		double _appTemp = appTemp;

		public string AppTemp
		{
			get => _appTemp.ToString(cumulus.TempFormat);
		}

		public string LowAppTempTodayTime { get; } = lowAppTempTodayTime;

		public string HighAppTempTodayTime { get; } = highAppTempTodayTime;

		double _lowAppTempToday = lowAppTempToday;

		public string LowAppTempToday
		{
			get => _lowAppTempToday.ToString(cumulus.TempFormat);
		}

		double _highAppTempToday = highAppTempToday;

		public string HighAppTempToday
		{
			get => _highAppTempToday.ToString(cumulus.TempFormat);
		}

		double _feelsLike = feelsLike;

		public string FeelsLike
		{
			get => _feelsLike.ToString(cumulus.TempFormat);
		}

		public string LowFeelsLikeTodayTime { get; } = lowFeelsLikeTodayTime;

		public string HighFeelsLikeTodayTime { get; } = highFeelsLikeTodayTime;

		double _lowFeelsLikeToday = lowFeelsLikeToday;

		public string LowFeelsLikeToday
		{
			get => _lowFeelsLikeToday.ToString(cumulus.TempFormat);
		}

		double _highFeelsLikeToday = highFeelsLikeToday;

		public string HighFeelsLikeToday
		{
			get => _highFeelsLikeToday.ToString(cumulus.TempFormat);
		}

		double _tempTrend = tempTrend;

		public string TempTrend
		{
			get => _tempTrend.ToString(cumulus.TempTrendFormat);
		}

		double _pressTrend = pressTrend;

		public string PressTrend
		{
			get => _pressTrend.ToString(cumulus.PressTrendFormat);
		}

		double _sunshineHours = sunshineHours;

		public string SunshineHours
		{
			get => _sunshineHours.ToString(cumulus.SunFormat);
		}

		public string Version
		{
			get => cumulus.Version;
		}

		public string Build
		{
			get => cumulus.Build;
		}

		public string DominantWindDirection { get; } = domWindDir;

		public string LastRainTipISO { get; } = lastRainTipISO;

		public string HighBeaufortToday { get; } = highBeaufortToday;

		public string Beaufort { get; } = beaufort;

		public string BeaufortDesc { get; } = beaufortDesc;

		public string LastDataRead { get; } = lastDataRead.ToLocalTime().ToString(cumulus.ProgramOptions.TimeFormatLong);

		public string LastDataReadDate
		{
			get => lastDataRead.ToLocalTime().ToString("d");
		}

		public bool DataStopped { get; } = dataStopped;

		public List<DashboardAlarms> Alarms { get; } = alarms;
	}
}
