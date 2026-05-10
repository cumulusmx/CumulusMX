using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal class MetData
	{

		public static double THWIndex { get; set; } = 0;

		public static double THSWIndex { get; set; } = 0;

		/// <summary>
		/// Indoor relative humidity in %
		/// </summary>
		public static int? HumidityIn { get; set; }

		/// <summary>
		/// Outdoor relative humidity in %
		/// </summary>
		public static int Humidity { get; set; } = 0;

		/// <summary>
		/// Outdoor temp
		/// </summary>
		public static double Temperature { get; set; } = 0;

		/// <summary>
		/// Indoor temperature in C
		/// </summary>
		public static double? TemperatureIn { get; set; }

		/// <summary>
		/// Sea-level pressure
		/// </summary>
		public static double Pressure { get; set; } = 0;

		public static string PressTrendStr { get; set; }

		public static double StationPressure { get; set; } = 0;

		public static double AltimeterPressure { get; set; }

		/// <summary>
		/// Outdoor dew point
		/// </summary>
		public static double Dewpoint { get; set; } = 0;

		/// <summary>
		/// Wind chill
		/// </summary>
		public static double WindChill { get; set; } = 0;

		/// <summary>
		/// Apparent temperature
		/// </summary>
		public static double ApparentTemperature { get; set; }

		/// <summary>
		/// Heat index
		/// </summary>
		public static double HeatIndex { get; set; } = 0;

		/// <summary>
		/// Humidex
		/// </summary>
		public static double Humidex { get; set; } = 0;

		/// <summary>
		/// Feels like (JAG/TI)
		/// </summary>
		public static double FeelsLike { get; set; } = 0;

		/// <summary>
		/// Latest wind speed/gust
		/// </summary>
		public static double WindLatest { get; set; } = 0;

		/// <summary>
		/// Average wind speed
		/// </summary>
		public static double WindAverage { get; set; } = 0;

		/// <summary>
		/// Peak wind gust in last 10 minutes
		/// </summary>
		public static double RecentMaxGust { get; set; } = 0;

		/// <summary>
		/// Wind direction in degrees
		/// </summary>
		public static int Bearing { get; set; } = 0;


		/// <summary>
		/// Wind direction as compass points
		/// </summary>
		public static string BearingText { get; set; } = "---";

		/// <summary>
		/// Wind direction in degrees
		/// </summary>
		public static int AvgBearing { get; set; } = 0;

		/// <summary>
		/// Wind direction as compass points
		/// </summary>
		public static string AvgBearingText
		{
			get
			{
				return AvgBearing == 0 ? "-" : Program.cumulus.Trans.compassp[(AvgBearing * 100 + 1125) % 36000 / 2250];
			}
		}

		/// <summary>
		/// Wind run for today
		/// </summary>
		public static double WindRunToday { get; set; } = 0;

		/// <summary>
		/// Rainfall today
		/// </summary>
		public static double RainToday { get; set; } = 0;

		public static double RainSinceMidnight { get; set; }

		/// <summary>
		/// Solar Radiation in W/m2
		/// </summary>
		public static int? SolarRad { get; set; }

		/// <summary>
		/// UV index
		/// </summary>
		public static double? UV { get; set; }

		public static double ET { get; set; }

		/// <summary>
		/// Rain this month
		/// </summary>
		public static double RainWeek { get; set; } = 0;

		/// <summary>
		/// Rain this month
		/// </summary>
		public static double RainMonth { get; set; } = 0;

		/// <summary>
		/// Rain this year
		/// </summary>
		public static double RainYear { get; set; } = 0;

		/// <summary>
		/// rain rate
		/// </summary>
		public static double RainRate { get; set; } = 0;

		public static double MidnightRainCount { get; set; }

		public static int MidnightRainResetDay { get; set; }

		public static double? BlackGlobeTemp { get; set; }

		public static double? WetBulbGlobeTemp { get; set; }

		public static double LightValue { get; set; }

		public static double HeatingDegreeDays { get; set; }

		public static double CoolingDegreeDays { get; set; }

		public static double GrowingDegreeDaysThisYear1 { get; set; }

		public static double GrowingDegreeDaysThisYear2 { get; set; }

		public static double TempTotalToday { get; set; }

		public static double ChillHours { get; set; }

		public static double YestChillHours { get; set; }

		/// <summary>
		/// Extra Temps
		/// </summary>
		public static double?[] ExtraTemp { get; set; }

		/// <summary>
		/// User allocated Temps
		/// </summary>
		public static double?[] UserTemp { get; set; }

		/// <summary>
		/// Extra Humidity
		/// </summary>
		public static double?[] ExtraHum { get; set; }

		/// <summary>
		/// Extra dewpoint
		/// </summary>
		public static double?[] ExtraDewPoint { get; set; }

		/// <summary>
		/// Soil Temp 1-16 in C
		/// </summary>
		public static double?[] SoilTemp { get; set; }

	}
}
