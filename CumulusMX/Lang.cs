using System.Collections.Generic;

namespace CumulusMX
{
	internal class Lang
	{
		public Lang()
		{
			zForecast =
			[
				"Settled fine",
				"Fine weather",
				"Becoming fine",
				"Fine, becoming less settled",
				"Fine, possible showers",
				"Fairly fine, improving",
				"Fairly fine, possible showers early",
				"Fairly fine, showery later",
				"Showery early, improving",
				"Changeable, mending",
				"Fairly fine, showers likely",
				"Rather unsettled clearing later",
				"Unsettled, probably improving",
				"Showery, bright intervals",
				"Showery, becoming less settled",
				"Changeable, some rain",
				"Unsettled, short fine intervals",
				"Unsettled, rain later",
				"Unsettled, some rain",
				"Mostly very unsettled",
				"Occasional rain, worsening",
				"Rain at times, very unsettled",
				"Rain at frequent intervals",
				"Rain, very unsettled",
				"Stormy, may improve",
				"Stormy, much rain"
			];

			compassp = new string[16];

			AirQualityCaptions = new string[4];
			AirQualityAvgCaptions = new string[4];
			AirQuality10Captions = new string[4];
			AirQuality10AvgCaptions = new string[4];

			LaserCaptions = new string[4];
			LeafWetnessCaptions = new string[8];
			UserTempCaptions = new string[8];
			ExtraTempCaptions = new string[16];
			ExtraHumCaptions = new string[16];
			ExtraDPCaptions = new string[16];
			SoilTempCaptions = new string[16];
			SoilMoistureCaptions = new string[16];
			SoilEcCaptions = new string[16];

			DavisForecast1 =
			[
				"FORECAST REQUIRES 3 HRS. OF RECENT DATA",
				"Mostly cloudy with little temperature change.",
				"Mostly cloudy and cooler.",
				"Clearing, cooler and windy.",
				"Clearing and cooler.",
				"Increasing clouds and cooler.",
				"Increasing clouds with little temperature change.",
				"Increasing clouds and warmer.",
				"Mostly clear for 12 to 24 hours with little temperature change.",
				"Mostly clear for 6 to 12 hours with little temperature change.",
				"Mostly clear and warmer. ",
				"Mostly clear for 12 to 24 hours and cooler.",
				"Mostly clear for 12 hours with little temperature change.",
				"Mostly clear with little temperature change.",
				"Mostly clear and cooler.",
				"Partially cloudy, Rain and/or snow possible or continuing.",
				"Partially cloudy, Snow possible or continuing.",
				"Partially cloudy, Rain possible or continuing.",
				"Mostly cloudy, Rain and/or snow possible or continuing.",
				"Mostly cloudy, Snow possible or continuing.",
				"Mostly cloudy, Rain possible or continuing.",
				"Mostly cloudy. ",
				"Partially cloudy.",
				"Mostly clear.",
				"Partly cloudy with little temperature change.",
				"Partly cloudy and cooler.",
				"Unknown forecast rule."
			];

			DavisForecast2 =
			[
				"",
				"Precipitation possible within 48 hours.",
				"Precipitation possible within 24 to 48 hours.",
				"Precipitation possible within 24 hours.",
				"Precipitation possible within 12 to 24 hours.",
				"Precipitation possible within 12 hours, possibly heavy at times.",
				"Precipitation possible within 12 hours.",
				"Precipitation possible within 6 to 12 hours. ",
				"Precipitation possible within 6 to 12 hours, possibly heavy at times.",
				"Precipitation possible and windy within 6 hours.",
				"Precipitation possible within 6 hours.",
				"Precipitation ending in 12 to 24 hours.",
				"Precipitation possibly heavy at times and ending within 12 hours.",
				"Precipitation ending within 12 hours.",
				"Precipitation ending within 6 hours.",
				"Precipitation likely, possibly heavy at times.",
				"Precipitation likely.",
				"Precipitation continuing, possibly heavy at times.",
				"Precipitation continuing."
			];

			DavisForecast3 =
			[
				"",
				"Windy with possible wind shift to the W, SW, or S.",
				"Possible wind shift to the W, SW, or S.",
				"Windy with possible wind shift to the W, NW, or N.",
				"Possible wind shift to the W, NW, or N.",
				"Windy.",
				"Increasing winds."
			];

			DataCaptions = new Dictionary<string, string>
			{
				{ "Temp", "Temperature" },
				{ "TempIn", "Indoor Temp" },
				{ "HiTemp", "High Temperature" },
				{ "LoTemp", "Low Temperature" },
				{ "HiTempRange", "High Daily Temp Range" },
				{ "LoTempRange", "Low Daily Temp Range" },
				{ "TempRange", "Temperature Range" },
				{ "AvgTemp", "Average Temp" },
				{ "HiMinTemp", "High Minimum Temp" },
				{ "LoMaxTemp", "Low Maximum Temp" },
				{ "WindChill", "WindChill" },
				{ "LoWindChill", "Low Wind Chill" },
				{ "AppTemp", "Apparent Temp" },
				{ "HiAppTemp", "High Apparent Temp" },
				{ "LoAppTemp", "Low Apparent Temp" },
				{ "HeatInd", "Heat Index" },
				{ "HiHeatInd", "High Heat Index" },
				{ "DewPnt", "Dew Point" },
				{ "HiDewPnt", "High Dew Point" },
				{ "LoDewPnt", "Low Dew Point" },
				{ "FeelsLike", "Feels Like" },
				{ "HiFeelsLike", "High Feels Like" },
				{ "LoFeelsLike", "Low Feels Like" },
				{ "Humidex", "Humidex" },
				{ "HiHumidex", "High Humidex" },
				{ "Hum", "Humidity" },
				{ "HumIn", "Indoor Humidity" },
				{ "HiHum", "High Humidity" },
				{ "LoHum", "Low Humidity" },
				{ "TotalRain", "Total Rain" },
				{ "RainRate", "Rainfall Rate" },
				{ "HiRainRate", "High Rain Rate" },
				{ "HiHourlyRain", "High Hourly Rain" },
				{ "HiMonthRain", "High Monthly Rainfall" },
				{ "HiDailyRain", "High Daily Rain" },
				{ "Hi24hRain", "High 24-Hour Rain" },
				{ "LongDryPeriod", "Longest Dry Period" },
				{ "LongWetPeriod", "Longest Wet Period" },
				{ "Press", "Pressure" },
				{ "LoPress", "Low Pressure" },
				{ "HiPress", "High Pressure" },
				{ "WindGust", "Wind Gust" },
				{ "HiGust", "High Gust" },
				{ "WindSpeed", "WindSpeed" },
				{ "HiWindSpeed", "High Wind Speed" },
				{ "LatestBearing", "Latest Bearing" },
				{ "AvgBearing", "Average Bearing" },
				{ "HiWindDailyRun", "High Daily Windrun" },
				{ "WindRun", "Wind Run" },
				{ "DomDir", "Dominant Direction" },
				{ "HiBGT", "High BGT" },
				{ "HiWBGT", "High WBGT" },
				{ "HiSolar", "High Solar Irradiation" },
				{ "SolarIrrad", "Solar Irradiation" },
				{ "SunshineHrs", "Sunshine Hours" },
				{ "HiUV", "High UV-Index" },
				{ "ChillHrs", "Chill Hours" }
			};
		}

		// Forecasts
		public string ForecastNotAvailable { get; set; }
		public string Exceptional { get; set; }
		public string[] zForecast { get; set; }
		// moon phases
		public string NewMoon { get; set; }
		public string WaxingCrescent { get; set; }
		public string FirstQuarter { get; set; }
		public string WaxingGibbous { get; set; }
		public string FullMoon { get; set; }
		public string WaningGibbous { get; set; }
		public string LastQuarter { get; set; }
		public string WaningCrescent { get; set; }
		// Beaufort
		public string Calm { get; set; }
		public string Lightair { get; set; }
		public string Lightbreeze { get; set; }
		public string Gentlebreeze { get; set; }
		public string Moderatebreeze { get; set; }
		public string Freshbreeze { get; set; }
		public string Strongbreeze { get; set; }
		public string Neargale { get; set; }
		public string Gale { get; set; }
		public string Stronggale { get; set; }
		public string Storm { get; set; }
		public string Violentstorm { get; set; }
		public string Hurricane { get; set; }
		public string Unknown { get; set; }
		// trends
		public string Risingveryrapidly { get; set; }
		public string Risingquickly { get; set; }
		public string Rising { get; set; }
		public string Risingslowly { get; set; }
		public string Steady { get; set; }
		public string Fallingslowly { get; set; }
		public string Falling { get; set; }
		public string Fallingquickly { get; set; }
		public string Fallingveryrapidly { get; set; }
		// compass points
		public string[] compassp { get; set; }
		// air quality captions
		public string[] AirQualityCaptions { get; set; }
		public string[] AirQualityAvgCaptions { get; set; }
		public string[] AirQuality10Captions { get; set; }
		public string[] AirQuality10AvgCaptions { get; set; }
		// leaf wetness captions
		public string[] LeafWetnessCaptions { get; set; }
		// user temps
		public string[] UserTempCaptions { get; set; }
		// Extra temperature captions
		public string[] ExtraTempCaptions { get; set; }
		// Extra humidity captions
		public string[] ExtraHumCaptions { get; set; }
		// Extra dew point captions
		public string[] ExtraDPCaptions { get; set; }
		// soil temp captions
		public string[] SoilTempCaptions { get; set; }
		// soil moisture captions
		public string[] SoilMoistureCaptions { get; set; }
		// soil EC captions
		public string[] SoilEcCaptions { get; set; }
		// WH45 CO2 sensor captions
		public string CO2_CurrentCaption { get; set; }
		public string CO2_24HourCaption { get; set; }
		public string CO2_pm2p5Caption { get; set; }
		public string CO2_pm2p5_24hrCaption { get; set; }
		public string CO2_pm10Caption { get; set; }
		public string CO2_pm10_24hrCaption { get; set; }
		public string CO2_TemperatureCaption { get; set; }
		public string CO2_HumidityCaption { get; set; }
		// daylight
		public string thereWillBeMinSLessDaylightTomorrow { get; set; }
		public string thereWillBeMinSMoreDaylightTomorrow { get; set; }
		// Davis forecast
		public string[] DavisForecast1 { get; set; }
		public string[] DavisForecast2 { get; set; }
		public string[] DavisForecast3 { get; set; }
		// alarm emails
		public string AlarmEmailPreamble { get; set; }
		public string AlarmEmailSubject { get; set; }
		// web tag defaults
		public string WebTagGenTimeDate { get; set; }
		public string WebTagGenTime { get; set; }
		public string WebTagGenDate { get; set; }
		public string WebTagRecTimeDate { get; set; }
		public string WebTagRecDate { get; set; }
		public string WebTagRecDryWetDate { get; set; }
		public string WebTagElapsedTime { get; set; }
		// Snow
		public string SnowDepth { get; set; }
		public string Snow24h { get; set; }
		// Laser
		public string[] LaserCaptions { get; set; }
		// Lightning
		public string LightningDistance { get; set; }
		public string LightningTime { get; set; }
		public string LightningCount { get; set; }
		// Hi/Lo captions
		public Dictionary<string, string> DataCaptions { get; set; }
	}
}
