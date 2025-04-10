using System;

namespace CumulusMX
{

	internal static class ConvertUnits
	{
		private const double inHg2hPa = 33.8638866667;
		private const double inHg2kPa = 3.38638866667;
		private const int kPa2hPa = 10;
		private const double mm2in = 1 / 25.4;
		private const double in2mm = 25.4;

		/// <summary>
		///  Convert temp supplied in C to units in use
		/// </summary>
		/// <param name="value">Temp in C</param>
		/// <returns>Temp in configured units</returns>
		public static double TempCToUser(double value)
		{
			return Program.cumulus.Units.Temp == 1 ? MeteoLib.CToF(value) : value;
		}

		/// <summary>
		///  Convert temp supplied in F to units in use
		/// </summary>
		/// <param name="value">Temp in F</param>
		/// <returns>Temp in configured units</returns>
		public static double TempFToUser(double value)
		{
			return Program.cumulus.Units.Temp == 0 ? MeteoLib.FtoC(value) : value;
		}

		/// <summary>
		///  Convert temp supplied in user units to C
		/// </summary>
		/// <param name="value">Temp in configured units</param>
		/// <returns>Temp in C</returns>
		public static double UserTempToC(double value)
		{
			return Program.cumulus.Units.Temp == 1 ? MeteoLib.FtoC(value) : value;
		}

		/// <summary>
		///  Convert temp supplied in user units to F
		/// </summary>
		/// <param name="value">Temp in configured units</param>
		/// <returns>Temp in F</returns>
		public static double UserTempToF(double value)
		{
			return Program.cumulus.Units.Temp == 1 ? value : MeteoLib.CToF(value);
		}

		/// <summary>
		///  Converts wind supplied in m/s to user units
		/// </summary>
		/// <param name="value">Wind in m/s</param>
		/// <returns>Wind in configured units</returns>
		public static double WindMSToUser(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => value,
				1 => value * 2.23693629,
				2 => value * 3.6,
				3 => value * 1.94384449,
				_ => 0,
			};
		}

		public static double? WindMSToUser(double? value)
		{
			return value.HasValue ? WindMSToUser(value.Value) : null;
		}

		public static decimal? WindMSToUser(decimal? value)
		{
			return value.HasValue ? (decimal) WindMSToUser((double) value.Value) : null;
		}


		/// <summary>
		///  Converts wind supplied in mph to user units
		/// </summary>
		/// <param name="value">Wind in mph</param>
		/// <returns>Wind in configured units</returns>
		public static double WindMPHToUser(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => value * 0.44704,
				1 => value,
				2 => value * 1.60934,
				3 => value * 0.868976,
				_ => 0,
			};
		}

		public static double WindMPHToUser(int value)
		{
			return WindMPHToUser((double) value);
		}

		public static double? WindMPHToUser(double? value)
		{
			return value.HasValue ? WindMPHToUser(value.Value) : null;
		}

		public static decimal? WindMPHToUser(decimal? value)
		{
			return value.HasValue ? (decimal) WindMPHToUser((double) value.Value) : null;
		}

		/// <summary>
		///  Converts wind supplied in knots to user units
		/// </summary>
		/// <param name="value">Wind in knots</param>
		/// <returns>Wind in configured units</returns>
		public static double WindKnotsToUser(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => value * 0.5144444,
				1 => value * 1.150779,
				2 => value * 1.852,
				3 => value,
				_ => 0,
			};
		}

		public static double? WindKnotsToUser(double? value)
		{
			return value.HasValue ? WindKnotsToUser(value.Value) : null;
		}

		public static decimal? WindKnotsToUser(decimal? value)
		{
			return value.HasValue ? (decimal) WindKnotsToUser((double) value.Value) : null;
		}

		/// <summary>
		///  Converts wind supplied in kph to user units
		/// </summary>
		/// <param name="value">Wind in kph</param>
		/// <returns>Wind in configured units</returns>
		public static double WindKPHToUser(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => value * 0.2777778,
				1 => value * 0.6213712,
				2 => value,
				3 => value * 0.5399568,
				_ => 0,
			};
		}

		public static double? WindKPHToUser(double? value)
		{
			return value.HasValue ? WindKPHToUser(value.Value) : null;
		}

		public static decimal? WindKPHToUser(decimal? value)
		{
			return value.HasValue ? (decimal) WindKPHToUser((double) value.Value) : null;
		}

		/// <summary>
		/// Converts wind in user units to m/s
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static double WindToMS(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => value,
				1 => value / 2.23693629,
				2 => value / 3.6F,
				3 => value / 1.94384449,
				_ => 0,
			};
		}

		/// <summary>
		/// Converts value in kilometres to distance unit based on users configured wind units
		/// </summary>
		/// <param name="val"></param>
		/// <returns>Wind in configured units</returns>
		public static double KmtoUserUnits(double val)
		{
			return Program.cumulus.Units.Wind switch
			{
				// m/s
				0 or 2 => val,
				// mph
				1 => val * 0.621371,
				// knots
				3 => val * 0.539957,
				_ => val,
			};
		}

		/// <summary>
		/// Converts value in kilometres to distance unit based on users configured wind units
		/// </summary>
		/// <param name="val"></param>
		/// <returns>Wind in configured units</returns>
		public static double MilestoUserUnits(double val)
		{
			return Program.cumulus.Units.Wind switch
			{

				0 or 2 => val * 1.609344,   // m/s or km/h
				1 or 3 => val,              // mph or knots
				_ => val,
			};
		}

		/// <summary>
		///  Converts windrun supplied in user units to km
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in km</returns>
		public static double WindRunToKm(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 or 2 => value,			// m/s or km/h
				1 => value / 0.621371192,   // mph
				3 => value / 0.539957,      // knots
				_ => 0,
			};
		}

		/// <summary>
		///  Converts windrun supplied in user units to miles
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in mi</returns>
		public static double WindRunToMi(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 or 2 => value * 0.621371192,  // m/s or km/h
				1 => value,                     // mph
				3 => value / 0.8689762,         // knots
				_ => 0,
			};
		}

		/// <summary>
		///  Converts windrun supplied in user units to nautical miles
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in Nm</returns>
		public static double WindRunToNm(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 or 2 => value * 0.539956803,	// m/s, km/h
				1 => value * 0.8689762,			// mph
				3 => value,						// knots
				_ => 0,
			};
		}

		public static double UserWindToKPH(double wind) // input is in Units.Wind units, convert to km/h
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => wind * 3.6,        // m/s
				1 => wind * 1.609344,   // mph
				2 => wind,              // kph
				3 => wind * 1.852,      // knots
				_ => wind,
			};
		}

		public static double UserWindToMS(double wind) // input is in Units.Wind units, convert to m/s
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => wind,              // m/s
				1 => wind * 0.44704,    // mph
				2 => wind * 0.2777778,  // kph
				3 => wind * 0.5144444,  // knots
				_ => wind,
			};
		}

		public static double UserWindToMPH(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => value * 2.23693629,
				1 => value,
				2 => value * 0.621371,
				3 => value * 1.15077945,
				_ => 0,
			};
		}

		public static double UserWindToKnots(double value)
		{
			return Program.cumulus.Units.Wind switch
			{
				0 => value * 1.943844,
				1 => value * 0.8689758,
				2 => value * 0.5399565,
				3 => value,
				_ => 0,
			};
		}


		/// <summary>
		/// Converts rain in mm to units in use
		/// </summary>
		/// <param name="value">Rain in mm</param>
		/// <returns>Rain in configured units</returns>
		public static double RainMMToUser(double value)
		{
			return Program.cumulus.Units.Rain == 0 ? value : value * mm2in;
		}

		public static double? RainMMToUser(double? value)
		{
			return value.HasValue ? RainMMToUser(value.Value) : null;
		}

		public static decimal? RainMMToUser(decimal? value)
		{
			return value.HasValue ? (decimal) RainMMToUser((double) value.Value) : null;
		}

		/// <summary>
		/// Converts rain in inches to units in use
		/// </summary>
		/// <param name="value">Rain in mm</param>
		/// <returns>Rain in configured units</returns>
		public static double RainINToUser(double value)
		{
			return Program.cumulus.Units.Rain == 0 ? value * in2mm : value;
		}

		public static double? RainINToUser(double? value)
		{
			return value.HasValue ? RainINToUser(value.Value) : null;
		}

		public static decimal? RainINToUser(decimal? value)
		{
			return value.HasValue ? (decimal) RainINToUser((double) value.Value) : null;
		}

		/// <summary>
		/// Converts rain in units in use to mm
		/// </summary>
		/// <param name="value">Rain in configured units</param>
		/// <returns>Rain in mm</returns>
		public static double UserRainToMM(double value)
		{
			return Program.cumulus.Units.Rain == 0 ? value : value * in2mm;
		}

		public static double UserRainToIN(double rain)
		{
			return Program.cumulus.Units.Rain == 0 ? rain * mm2in : rain;
		}


		/// <summary>
		/// Convert pressure in mb to units in use
		/// </summary>
		/// <param name="value">pressure in mb</param>
		/// <returns>pressure in configured units</returns>
		public static double PressMBToUser(double value)
		{
			return Program.cumulus.Units.Press switch
			{
				0 or 1 => value,
				2 => value / inHg2hPa,
				3 => value / kPa2hPa,
				_ => 0
			};
		}

		public static double? PressMBToUser(double? value)
		{
			return value.HasValue ? PressMBToUser(value.Value) : null;
		}

		public static decimal? PressMBToUser(decimal? value)
		{
			return value.HasValue ? (decimal) PressMBToUser((double) value.Value) : null;
		}

		/// <summary>
		/// Convert pressure in kPa to units in use
		/// </summary>
		/// <param name="value">pressure in kPa</param>
		/// <returns>pressure in configured units</returns>
		public static double PressKPAToUser(double value)
		{
			return Program.cumulus.Units.Press switch
			{
				0 or 1 => value * kPa2hPa,
				2 => value / inHg2kPa,
				3 => value,
				_ => 0
			};
		}
		public static double? PressKPAToUser(double? value)
		{
			return value.HasValue ? PressKPAToUser(value.Value) : null;
		}

		/// <summary>
		/// Convert pressure in inHg to units in use
		/// </summary>
		/// <param name="value">pressure in mb</param>
		/// <returns>pressure in configured units</returns>
		public static double PressINHGToUser(double value)
		{
			return Program.cumulus.Units.Press switch
			{
				0 or 1 => value * inHg2hPa,
				2 => value,
				3 => value * inHg2kPa,
				_ => 0
			};
		}
		public static double? PressINHGToUser(double? value)
		{
			return value.HasValue ? PressINHGToUser(value.Value) : null;
		}

		public static decimal? PressINHGToUser(decimal? value)
		{
			return value.HasValue ? (decimal) PressINHGToUser((double) value.Value) : null;
		}

		/// <summary>
		/// Convert pressure in inHg to hPa
		/// </summary>
		/// <param name="value">pressure in inHg</param>
		/// <returns>pressure in hPa</returns>
		public static double PressINHGToHpa(double value)
		{
			return value * inHg2hPa;
		}

		/// <summary>
		/// Convert pressure in mmHg to units in use
		/// </summary>
		/// <param name="value">pressure in mmHg</param>
		/// <returns>pressure in configured units</returns>
		public static double PressMMHGToUser(double value)
		{
			return PressINHGToHpa(value * mm2in);
		}

		/// <summary>
		/// Convert pressure in units in use to mb
		/// </summary>
		/// <param name="value">pressure in configured units</param>
		/// <returns>pressure in mb</returns>
		public static double UserPressToMB(double value)
		{
			return Program.cumulus.Units.Press switch
			{
				0 or 1 => value,
				2 => value * inHg2hPa,
				3 => value * kPa2hPa,
				_ => 0
			};
		}

		/// <summary>
		/// Convert pressure from user units to hPa
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static double UserPressToHpa(double value)
		{
			return Program.cumulus.Units.Press switch
			{
				0 or 1 => value,
				2 => value * inHg2hPa,
				3 => value * kPa2hPa,
				_ => 0
			};
		}

		/// <summary>
		/// Convert pressure from user units to kPa
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static double UserPressToKpa(double value)
		{
			return Program.cumulus.Units.Press switch
			{
				0 or 1 => value / kPa2hPa,
				2 => value * inHg2kPa,
				3 => value,
				_ => 0
			};
		}

		/// <summary>
		/// Convert pressure in units in use to inHg
		/// </summary>
		/// <param name="value">pressure in configured units</param>
		/// <returns>pressure in mb</returns>
		public static double UserPressToIN(double value)
		{
			return Program.cumulus.Units.Press switch
			{
				0 or 1 => value / inHg2hPa,
				2 => value,
				3 => value / inHg2kPa,
				_ => 0
			};
		}

		public static decimal LaserMmToUser(decimal value)
		{
			return Program.cumulus.Units.LaserDistance switch
			{
				0 => Math.Round(value * (decimal) 0.1, 1),
				1 => Math.Round(value * (decimal) 0.03937008, 2),
				2 => value,
				_ => 0,
			};
		}

		public static decimal? LaserMmToUser(decimal? value)
		{
			if (!value.HasValue)
			{
				return null;
			}

			return Program.cumulus.Units.LaserDistance switch
			{
				0 => Math.Round(value.Value * (decimal) 0.1, 1),
				1 => Math.Round(value.Value * (decimal) 0.03937008, 2),
				2 => value,
				_ => 0,
			};
		}


		public static decimal LaserInchesToUser(decimal value)
		{
			return Program.cumulus.Units.LaserDistance switch
			{
				0 => Math.Round(value * (decimal) 2.54, 1),
				1 => Math.Round(value, 2),
				2 => Math.Round(value *	(decimal) 25.4, 0),
				_ => 0,
			};
		}

		public static decimal? LaserInchesToUser(decimal? value)
		{
			if (!value.HasValue)
			{
				return null;
			}

			return Program.cumulus.Units.LaserDistance switch
			{
				0 => Math.Round(value.Value * (decimal) 2.54, 1),
				1 => Math.Round(value.Value, 2),
				2 => Math.Round(value.Value * (decimal) 25.4, 0),
				_ => 0,
			};
		}

		public static decimal LaserToSnow(decimal value)
		{
			if (Program.cumulus.Units.SnowDepth == Program.cumulus.Units.LaserDistance)
			{
				return value;
			}

			if (Program.cumulus.Units.SnowDepth == 0)
			{
				// snow depth = cm
				decimal mult = Program.cumulus.Units.LaserDistance switch
				{
					0 => 1,
					1 => (decimal) 2.54,
					2 => (decimal) 0.1,
					_ => 0
				};
				return value * mult;
			}
			else
			{
				// snow depth = inches
				decimal mult = Program.cumulus.Units.LaserDistance switch
				{
					0 => (decimal) 0.3937008,
					1 => 1,
					2 => (decimal) 0.03937008,
					_ => 0
				};
				return value * mult;
			}

		}

		/// <summary>
		/// Takes speed in user units, returns Bft number
		/// </summary>
		/// <param name="windspeed"></param>
		/// <returns></returns>
		public static int Beaufort(double speed)
		{
			double windspeedMS = UserWindToMS(speed);
			return windspeedMS switch
			{
				< 0.3 => 0,
				< 1.6 => 1,
				< 3.4 => 2,
				< 5.5 => 3,
				< 8.0 => 4,
				< 10.8 => 5,
				< 13.9 => 6,
				< 17.2 => 7,
				< 20.8 => 8,
				< 24.5 => 9,
				< 28.5 => 10,
				< 32.7 => 11,
				_ => 12
			};
		}
	}
}
