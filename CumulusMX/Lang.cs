using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX
{
	internal class Lang
	{
		public Lang()
		{
			zForecast = new string[]
			{
				"Settled fine", "Fine weather", "Becoming fine", "Fine, becoming less settled", "Fine, possible showers", "Fairly fine, improving",
				"Fairly fine, possible showers early", "Fairly fine, showery later", "Showery early, improving", "Changeable, mending",
				"Fairly fine, showers likely", "Rather unsettled clearing later", "Unsettled, probably improving", "Showery, bright intervals",
				"Showery, becoming less settled", "Changeable, some rain", "Unsettled, short fine intervals", "Unsettled, rain later", "Unsettled, some rain",
				"Mostly very unsettled", "Occasional rain, worsening", "Rain at times, very unsettled", "Rain at frequent intervals", "Rain, very unsettled",
				"Stormy, may improve", "Stormy, much rain"
			};

			compassp = new string[16];

			AirQualityCaptions =  new string[] { "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4" };
			AirQualityAvgCaptions = new string[] { "Sensor Avg 1", "Sensor Avg 2", "Sensor Avg 3", "Sensor Avg 4" };

			LeafWetnessCaptions = new string[] { "Wetness 1", "Wetness 2", "Wetness 3", "Wetness 4", "Wetness 5", "Wetness 6", "Wetness 7", "Wetness 8" };

			UserTempCaptions = new string[] { "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8" };

			ExtraTempCaptions = new string[] { "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };

			ExtraHumCaptions = new string[] { "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };

			ExtraDPCaptions = new string[] { "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };

			SoilTempCaptions = new string[] { "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10", "Sensor 11", "Sensor 12", "Sensor 13", "Sensor 14", "Sensor 15", "Sensor 16" };

			SoilMoistureCaptions = new string[] {"Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10", "Sensor 11", "Sensor 12", "Sensor 13", "Sensor 14", "Sensor 15", "Sensor 16" };

			DavisForecast1 = new string[]
			{
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
				"Mostly clear and warmer. ", "Mostly clear for 12 to 24 hours and cooler.",
				"Mostly clear for 12 hours with little temperature change.",
				"Mostly clear with little temperature change.",
				"Mostly clear and cooler.",
				"Partially cloudy, Rain and/or snow possible or continuing.",
				"Partially cloudy, Snow possible or continuing.",
				"Partially cloudy, Rain possible or continuing.",
				"Mostly cloudy, Rain and/or snow possible or continuing.",
				"Mostly cloudy, Snow possible or continuing.",
				"Mostly cloudy, Rain possible or continuing.",
				"Mostly cloudy. ", "Partially cloudy.",
				"Mostly clear.",
				"Partly cloudy with little temperature change.",
				"Partly cloudy and cooler.",
				"Unknown forecast rule."
			};

			DavisForecast2 = new string[]
			{
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
			};

			DavisForecast3 = new string[]
			{
				"",
				"Windy with possible wind shift to the W, SW, or S.",
				"Possible wind shift to the W, SW, or S.",
				"Windy with possible wind shift to the W, NW, or N.",
				"Possible wind shift to the W, NW, or N.",
				"Windy.",
				"Increasing winds."
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
		public string WaningGibbous  { get; set; }
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
		// WH45 CO2 sensor captions
		public string CO2_CurrentCaption { get; set; }
		public string CO2_24HourCaption { get; set; }
		public string CO2_pm2p5Caption { get; set; }
		public string CO2_pm2p5_24hrCaption { get; set; }
		public string CO2_pm10Caption { get; set; }
		public string CO2_pm10_24hrCaption { get; set; }
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
	}
}
