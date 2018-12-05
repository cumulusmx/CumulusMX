using System;
using System.Runtime.Serialization;

namespace CumulusMX
{
	[DataContract]
	public class DataStruct // The annotations on this class are so it can be serialised as JSON
	{
		private Cumulus cumulus;
		
		public DataStruct(Cumulus cumulus, double outdoorTemp, int outdoorHum, double avgTempToday, double indoorTemp, double outdoorDewpoint, double windChill, int indoorHum, double pressure, double windLatest, double windAverage, double recentmaxgust, double windRunToday, int bearing, int avgbearing, double rainToday, double rainYesterday, double rainMonth, double rainYear, double rainRate, double rainLastHour, double heatIndex, double humidex, double appTemp, double tempTrend, double pressTrend, double highGustToday, string highGustTodayTime, double highWindToday, int highGustBearingToday, string windUnit, int bearingRangeFrom10, int bearingRangeTo10, string windRoseData, double highTempToday, double lowTempToday, string highTempTodayToday, string lowTempTodayTime, double highPressToday, double lowPressToday, string highPressTodayTime, string lowPressTodayTime, double highRainRateToday, string highRainRateTodayTime, int highHumToday, int lowHumToday, string highHumTodayTime, string lowHumTodayTime, string pressUnit, string tempUnit, string rainUnit, double highDewpointToday, double lowDewpointToday, string highDewpointTodayTime, string lowDewpointTodayTime, double lowWindChillToday, string lowWindChillTodayTime, int solarRad, int highSolarRadToday, string highSolarRadTodayTime, double uvindex, double highUVindexToday, string highUVindexTodayTime, string forecast, string sunrise, string sunset, string moonrise, string moonset, double highHeatIndexToday, string highHeatIndexTodayTime, double highAppTempToday, double lowAppTempToday, string highAppTempTodayTime, string lowAppTempTodayTime, int currentSolarMax, double alltimeHighPressure, double alltimeLowPressure, double sunshineHours, string domWindDir, string lastRainTipISO, double highHourlyRainToday, string highHourlyRainTodayTime, string highBeaufortToday, string beaufort, string beaufortDesc, string lastDataRead, bool dataStopped, double stormRain, string stormRainStart, int cloudbase, string cloudbaseUnit, double last24hourRain)
		{
			this.cumulus = cumulus;
			OutdoorTemp = outdoorTemp;
			HighTempToday = highTempToday;
			LowTempToday = lowTempToday;
			HighTempTodayTime = highTempTodayToday;
			LowTempTodayTime = lowTempTodayTime;
			OutdoorHum = outdoorHum;
			AvgTempToday = avgTempToday;
			IndoorTemp = indoorTemp;
			OutdoorDewpoint = outdoorDewpoint;
			WindChill = windChill;
			LowWindChillToday = lowWindChillToday;
			LowWindChillTodayTime = lowWindChillTodayTime;
			IndoorHum = indoorHum;
			Pressure = pressure;
			HighPressToday = highPressToday;
			LowPressToday = lowPressToday;
			HighPressTodayTime = highPressTodayTime;
			LowPressTodayTime = lowPressTodayTime;
			WindLatest = windLatest;
			WindAverage = windAverage;
			Recentmaxgust = recentmaxgust;
			WindRunToday = windRunToday;
			Bearing = bearing;
			Avgbearing = avgbearing;
			RainToday = rainToday;
			RainYesterday = rainYesterday;
			RainMonth = rainMonth;
			RainYear = rainYear;
			RainRate = rainRate;
			HighRainRateToday = highRainRateToday;
			HighRainRateTodayTime = highRainRateTodayTime;
			HighHourlyRainToday = highHourlyRainToday;
			HighHourlyRainTodayTime = highHourlyRainTodayTime;
			RainLastHour = rainLastHour;
			RainLast24Hour = last24hourRain;
			HeatIndex = heatIndex;
			HighHeatIndexToday = highHeatIndexToday; 
			HighHeatIndexTodayTime = highHeatIndexTodayTime;
			Humidex = humidex;
			AppTemp = appTemp; 
			HighAppTempToday = highAppTempToday;
			LowAppTempToday = lowAppTempToday;
			HighAppTempTodayTime = highAppTempTodayTime;
			LowAppTempTodayTime = lowAppTempTodayTime;
			TempTrend = tempTrend;
			PressTrend = pressTrend;
			HighGustToday = highGustToday;
			HighGustTodayTime = highGustTodayTime;
			HighWindToday = highWindToday;
			HighGustBearingToday = highGustBearingToday;
			BearingRangeFrom10 = bearingRangeFrom10;
			BearingRangeTo10 = bearingRangeTo10;
			WindRoseData = windRoseData;
			WindUnit = windUnit;
			PressUnit = pressUnit;
			TempUnit = tempUnit;
			RainUnit = rainUnit;
			HighHumToday = highHumToday;
			LowHumToday = lowHumToday;
			HighHumTodayTime = highHumTodayTime;
			LowHumTodayTime = lowHumTodayTime;
			HighDewpointToday = highDewpointToday;
			LowDewpointToday = lowDewpointToday;
			HighDewpointTodayTime = highDewpointTodayTime;
			LowDewpointTodayTime = lowDewpointTodayTime;
			SolarRad = solarRad;
			HighSolarRadToday = highSolarRadToday;
			HighSolarRadTodayTime = highSolarRadTodayTime;
			CurrentSolarMax = currentSolarMax;
			UVindex = uvindex;
			HighUVindexToday = highUVindexToday;
			HighUVindexTodayTime = highUVindexTodayTime;
			AlltimeHighPressure = alltimeHighPressure;
			AlltimeLowPressure = alltimeLowPressure;
			SunshineHours = sunshineHours;
			Forecast = forecast;
			Sunrise = sunrise;
			Sunset = sunset;
			Moonrise = moonrise;
			Moonset = moonset;
			DominantWindDirection = domWindDir;
			LastRainTipISO = lastRainTipISO;
			HighBeaufortToday = highBeaufortToday;
			Beaufort = beaufort;
			BeaufortDesc = beaufortDesc;
			LastDataRead = lastDataRead;
			DataStopped = dataStopped;
			StormRain = stormRain;
			StormRainStart = stormRainStart;
			Cloudbase = cloudbase;
			CloudbaseUnit = cloudbaseUnit;
		}

		[IgnoreDataMember]
		public double StormRain { get; set; }

		[DataMember(Name = "StormRain")]
		public string StormRainRounded
		{
			get { return StormRain.ToString(cumulus.RainFormat); }
			set { }
		}

		[DataMember]
		public string StormRainStart { get; set; }
		
		[DataMember]
		public int CurrentSolarMax { get; set; }

		[IgnoreDataMember]
		public double HighHeatIndexToday { get; set; }

		[DataMember(Name = "HighHeatIndexToday")]
		public string HighHeatIndexTodayRounded
		{
			get { return HighHeatIndexToday.ToString(cumulus.TempFormat); }
			set { }
		}

		[DataMember]
		public string HighHeatIndexTodayTime { get; set; }

		[DataMember]
		public string Sunrise { get; set; }

		[DataMember]
		public string Sunset { get; set; }

		[DataMember]
		public string Moonrise { get; set; }

		[DataMember]
		public string Moonset { get; set; }


		[DataMember]
		public string Forecast { get; set; }

		[IgnoreDataMember]
		public double UVindex { get; set; }

		[DataMember(Name = "UVindex")]
		public string UVindexRounded
		{
			get { return UVindex.ToString(cumulus.UVFormat); }
			set { }   
		}

		[IgnoreDataMember]
		public double HighUVindexToday { get; set; }

		[DataMember(Name = "HighUVindexToday")]
		public string HighUVindexTodayRounded
		{
			get { return HighUVindexToday.ToString(cumulus.UVFormat); }
			set { }
		}

		[DataMember]
		public string HighUVindexTodayTime { get; set; }

		[DataMember]
		public string HighSolarRadTodayTime { get; set; }

		[DataMember]
		public int HighSolarRadToday { get; set; }

		[DataMember]
		public int SolarRad { get; set; }

		[IgnoreDataMember]
		public double IndoorTemp;

		[DataMember(Name = "IndoorTemp")]
		public string IndoorTempRounded
		{
			get { return IndoorTemp.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double OutdoorDewpoint;

		[DataMember(Name = "OutdoorDewpoint")]
		public string OutdoorDewpointRounded
		{
			get { return OutdoorDewpoint.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double LowDewpointToday { get; set; }

		[DataMember(Name = "LowDewpointToday")]
		public string LowDewpointTodayRounded
		{
			get { return LowDewpointToday.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double HighDewpointToday { get; set; }

		[DataMember(Name = "HighDewpointToday")]
		public string HighDewpointTodayRounded
		{
			get { return HighDewpointToday.ToString(cumulus.TempFormat); }
			set { }
		}

		[DataMember]
		public string LowDewpointTodayTime { get; set; }

		[DataMember]
		public string HighDewpointTodayTime { get; set; }


		[IgnoreDataMember]
		public double WindChill;

		[DataMember(Name = "WindChill")]
		public string WindChillRounded
		{
			get { return WindChill.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double LowWindChillToday { get; set; }

		[DataMember(Name = "LowWindChillToday")]
		public string LowWindChillTodayRounded
		{
			get { return LowWindChillToday.ToString(cumulus.TempFormat); }
			set { }
		}

		[DataMember]
		public string LowWindChillTodayTime { get; set; }

		[DataMember]
		public string WindUnit;

		[DataMember]
		public string RainUnit { get; set; }

		[DataMember]
		public string TempUnit { get; set; }

		[DataMember]
		public string PressUnit { get; set; }

		[DataMember]
		public string CloudbaseUnit { get; set; }

		[DataMember]
		public int Cloudbase { get; set; }

		[DataMember]
		public string LowHumTodayTime { get; set; }

		[DataMember]
		public string HighHumTodayTime { get; set; }

		[DataMember]
		public int LowHumToday { get; set; }

		[DataMember]
		public int HighHumToday { get; set; }

		[DataMember]
		public string HighRainRateTodayTime { get; set; }

		[IgnoreDataMember]
		public double HighRainRateToday { get; set; }

		[DataMember(Name="HighRainRateToday")]
		public string HighRainRateTodayRounded
		{
			get { return HighRainRateToday.ToString(cumulus.RainFormat); }
			set { }
		}

		[DataMember] 
		public string HighHourlyRainTodayTime { get; set; }

		[IgnoreDataMember]
		public double HighHourlyRainToday { get; set; }

		[DataMember(Name = "HighHourlyRainToday")]
		public string HighHourlyRainTodayRounded
		{
			get { return HighHourlyRainToday.ToString(cumulus.RainFormat); }
			set { }
		}

		[DataMember]
		public string LowPressTodayTime { get; set; }

		[DataMember]
		public string HighPressTodayTime { get; set; }

		[IgnoreDataMember]
		public double LowPressToday { get; set; }

		[DataMember(Name = "LowPressToday")]
		public string LowPressTodayRounded
		{
			get { return LowPressToday.ToString(cumulus.PressFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double HighPressToday { get; set; }

		[DataMember(Name = "HighPressToday")]
		public string HighPressTodayRounded
		{
			get { return HighPressToday.ToString(cumulus.PressFormat); }
			set { }
		}

		[DataMember]
		public string LowTempTodayTime { get; set; }

		[DataMember]
		public string HighTempTodayTime { get; set; }

		[IgnoreDataMember]
		public double LowTempToday { get; set; }

		[DataMember(Name = "LowTempToday")]
		public string LowTempTodayRounded
		{
			get { return LowTempToday.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double HighTempToday { get; set; }

		[DataMember(Name = "HighTempToday")]
		public string HighTempTodayRounded
		{
			get { return HighTempToday.ToString(cumulus.TempFormat); }
			set { }
		}

		[DataMember]
		public string WindRoseData { get; set; }

		[DataMember]
		public int BearingRangeTo10 { get; set; }

		[DataMember]
		public int BearingRangeFrom10 { get; set; }

		[DataMember]
		public int HighGustBearingToday { get; set; }

		[IgnoreDataMember]
		public double HighWindToday { get; set; }

		[DataMember(Name="HighWindToday")]
		public string HighWindTodayRounded
		{
			get { return HighWindToday.ToString(cumulus.WindFormat); }
			set { }
		}

		[DataMember]
		public string HighGustTodayTime { get; set; }

		[IgnoreDataMember]
		public double HighGustToday { get; set; }

		[DataMember(Name = "HighGustToday")]
		public string HighGustTodayRounded
		{
			get { return HighGustToday.ToString(cumulus.WindFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double OutdoorTemp { get; set; }

		[DataMember(Name = "OutdoorTemp")]
		public string OutdoorTempRounded
		{
			get { return OutdoorTemp.ToString(cumulus.TempFormat); }
			set { }
		}

		[DataMember]
		public int OutdoorHum { get; set; }

		[DataMember]
		public double AvgTempToday { get; set; }

		[DataMember]
		public int IndoorHum { get; set; }

		[IgnoreDataMember]
		public double Pressure { get; set; }

		[DataMember(Name = "Pressure")]
		public String PressureRounded
		{
			get { return Pressure.ToString(cumulus.PressFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double AlltimeHighPressure { get; set; }

		[DataMember(Name = "AlltimeHighPressure")]
		public String AlltimeHighPressureRounded
		{
			get { return AlltimeHighPressure.ToString(cumulus.PressFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double AlltimeLowPressure { get; set; }

		[DataMember(Name = "AlltimeLowPressure")]
		public String AlltimeLowPressureRounded
		{
			get { return AlltimeLowPressure.ToString(cumulus.PressFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double WindLatest { get; set; }

		[DataMember(Name = "WindLatest")]
		public String WindLatestRounded
		{
			get { return WindLatest.ToString(cumulus.WindFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double WindAverage { get; set; }

		[DataMember(Name = "WindAverage")]
		public String WindAverageRounded
		{
			get { return WindAverage.ToString(cumulus.WindFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double Recentmaxgust { get; set; }

		[DataMember(Name = "Recentmaxgust")]
		public String RecentmaxgustRounded
		{
			get { return Recentmaxgust.ToString(cumulus.WindFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double WindRunToday { get; set; }

		[DataMember(Name="WindRunToday")]
		public String WindRunTodayRounded
		{
			get { return WindRunToday.ToString(cumulus.WindRunFormat); }
			set { }
		}

		[DataMember]
		public int Bearing { get; set; }

		[DataMember]
		public int Avgbearing { get; set; }

		[IgnoreDataMember]
		public double RainToday { get; set; }

		[DataMember(Name = "RainToday")]
		public String RainTodayRounded
		{
			get { return RainToday.ToString(cumulus.RainFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double RainYesterday { get; set; }

		[DataMember(Name = "RainYesterday")]
		public String RainYesterdayRounded
		{
			get { return RainYesterday.ToString(cumulus.RainFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double RainMonth { get; set; }

		[DataMember(Name = "RainMonth")]
		public String RainMonthRounded
		{
			get { return RainMonth.ToString(cumulus.RainFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double RainYear { get; set; }
		[DataMember(Name = "RainYear")]
		public String RainYearRounded
		{
			get { return RainYear.ToString(cumulus.RainFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double RainRate { get; set; }

		[DataMember(Name = "RainRate")]
		public String RainRateRounded
		{
			get { return RainRate.ToString(cumulus.RainFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double RainLastHour { get; set; }

		[DataMember(Name = "RainLastHour")]
		public String RainLastHourRounded
		{
			get { return RainLastHour.ToString(cumulus.RainFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double RainLast24Hour { get; set; }

		[DataMember(Name = "RainLast24Hour")]
		public String RainLast24HourRounded
		{
			get { return RainLast24Hour.ToString(cumulus.RainFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double HeatIndex { get; set; }

		[DataMember(Name = "HeatIndex")]
		public string HeatIndexRounded
		{
			get { return HeatIndex.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double Humidex { get; set; }

		[DataMember(Name = "Humidex")]
		public string HumidexRounded
		{
			get { return Humidex.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double AppTemp { get; set; }

		[DataMember(Name = "AppTemp")]
		public string AppTempRounded
		{
			get { return AppTemp.ToString(cumulus.TempFormat); }
			set { }
		}

		[DataMember]
		public string LowAppTempTodayTime { get; set; }

		[DataMember]
		public string HighAppTempTodayTime { get; set; }

		[IgnoreDataMember]
		public double LowAppTempToday { get; set; }

		[DataMember(Name = "LowAppTempToday")]
		public string LowAppTempTodayRounded
		{
			get { return LowAppTempToday.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double HighAppTempToday { get; set; }

		[DataMember(Name = "HighAppTempToday")]
		public string HighAppTempTodayRounded
		{
			get { return HighAppTempToday.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double TempTrend { get; set; }

		[DataMember(Name = "TempTrend")]
		public string TempTrendRounded
		{
			get { return TempTrend.ToString(cumulus.TempFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double PressTrend { get; set; }

		[DataMember(Name = "PressTrend")]
		public string PressTrendRounded
		{
			get { return PressTrend.ToString(cumulus.PressFormat); }
			set { }
		}

		[IgnoreDataMember]
		public double SunshineHours { get; set; }

		[DataMember(Name = "SunshineHours")]
		public string SunshineHoursRounded
		{
			get { return SunshineHours.ToString("F1"); }
			set { }
		}

		[DataMember]
		public string Version
		{
			get { return cumulus.Version; }
			set { }
		}

		[DataMember]
		public string Build
		{
			get { return cumulus.Build; }
			set { }
		}

		[DataMember]
		public string DominantWindDirection { get; set; }

		[DataMember]
		public string LastRainTipISO { get; set; }
		
		[DataMember]
		public string HighBeaufortToday { get; set; }

		[DataMember]
		public string Beaufort { get; set; }

		[DataMember]
		public string BeaufortDesc { get; set; }

		[DataMember]
		public string LastDataRead { get; set; }

		[DataMember]
		public bool DataStopped { get; set; }
	}
}