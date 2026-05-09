using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal class Current
	{

		// Current values

		public static double THWIndex = 0;
		public static double THSWIndex = 0;

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

	}
}
