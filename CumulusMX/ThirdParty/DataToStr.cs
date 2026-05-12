using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CumulusMX
{
	internal static class DataToStr
	{
		/// <summary>
		/// Converts a wind value from user units to a formatted miles-per-hour string.
		/// </summary>
		/// <remarks>Uses ConvertUnits.UserWindToMPH for unit conversion and Math.Round when rounding is requested;
		/// formatting uses CultureInfo.InvariantCulture.</remarks>
		/// <param name="wind">Wind speed in user-configured units.</param>
		/// <param name="round">True to round to the nearest whole mile per hour (no decimal places); false to include one decimal place.</param>
		/// <returns>String representing the wind speed in miles per hour formatted with invariant culture.</returns>
		public static string WindMPHStr(double wind, bool round)
		{
			var windMPH = ConvertUnits.UserWindToMPH(wind);
			if (round)
			{
				windMPH = Math.Round(windMPH);
				return windMPH.ToString("F0", CultureInfo.InvariantCulture);
			}

			return windMPH.ToString("F1", CultureInfo.InvariantCulture);
		}

		public static string WindMSStr(double wind, bool round)
		{
			var windMS = ConvertUnits.UserWindToMS(wind);
			if (round)
			{
				windMS = Math.Round(windMS);
				return windMS.ToString("F0", CultureInfo.InvariantCulture);
			}

			return windMS.ToString("F1", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert rain in user units to inches for WU etc
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		public static string RainINstr(double rain)
		{
			return ConvertUnits.UserRainToIN(rain).ToString("F2", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert rain in user units to mm for APIs etc
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		public static string RainMMstr(double rain)
		{
			return ConvertUnits.UserRainToMM(rain).ToString("F2", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert temp in user units to F for WU etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		public static string TempFstr(double temp)
		{
			return ConvertUnits.UserTempToF(temp).ToFixed("F1");
		}

		/// <summary>
		/// Convert temp in user units to C for APIs etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		public static string TempCstr(double temp)
		{
			return ConvertUnits.UserTempToC(temp).ToFixed("F1");
		}

		public static string PressINstr(double pressure)
		{
			return ConvertUnits.UserPressToIN(pressure).ToString("F3", CultureInfo.InvariantCulture);
		}

		public static string PressPAstr(double pressure)
		{
			// return value to 100 * hPa
			return (ConvertUnits.UserPressToMB(pressure) * 100).ToString("F0", CultureInfo.InvariantCulture);
		}
	}
}
