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
		public static double? IndoorTemperature { get; set; }

	}
}
