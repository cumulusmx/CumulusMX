namespace CumulusMX
{
	internal static class ConvertUnits
	{
		/// <summary>
		///  Convert temp supplied in C to units in use
		/// </summary>
		/// <param name="value">Temp in C</param>
		/// <returns>Temp in configured units</returns>
		public static double TempCToUser(double value)
		{
			if (Program.cumulus.Units.Temp == 1)
			{
				return MeteoLib.CToF(value);
			}
			else
			{
				// C
				return value;
			}
		}

		/// <summary>
		///  Convert temp supplied in F to units in use
		/// </summary>
		/// <param name="value">Temp in F</param>
		/// <returns>Temp in configured units</returns>
		public static double TempFToUser(double value)
		{
			if (Program.cumulus.Units.Temp == 0)
			{
				return MeteoLib.FtoC(value);
			}
			else
			{
				// F
				return value;
			}
		}

		/// <summary>
		///  Convert temp supplied in user units to C
		/// </summary>
		/// <param name="value">Temp in configured units</param>
		/// <returns>Temp in C</returns>
		public static double UserTempToC(double value)
		{
			if (Program.cumulus.Units.Temp == 1)
			{
				return MeteoLib.FtoC(value);
			}
			else
			{
				// C
				return value;
			}
		}

		/// <summary>
		///  Convert temp supplied in user units to F
		/// </summary>
		/// <param name="value">Temp in configured units</param>
		/// <returns>Temp in F</returns>
		public static double UserTempToF(double value)
		{
			if (Program.cumulus.Units.Temp == 1)
			{
				return value;
			}
			else
			{
				// C
				return MeteoLib.CToF(value);
			}
		}

		/// <summary>
		///  Converts wind supplied in m/s to user units
		/// </summary>
		/// <param name="value">Wind in m/s</param>
		/// <returns>Wind in configured units</returns>
		public static double WindMSToUser(double value)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0:
					return value;
				case 1:
					return value * 2.23693629;
				case 2:
					return value * 3.6;
				case 3:
					return value * 1.94384449;
				default:
					return 0;
			}
		}

		/// <summary>
		///  Converts wind supplied in mph to user units
		/// </summary>
		/// <param name="value">Wind in mph</param>
		/// <returns>Wind in configured units</returns>
		public static double WindMPHToUser(double value)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0:
					return value * 0.44704;
				case 1:
					return value;
				case 2:
					return value * 1.60934;
				case 3:
					return value * 0.868976;
				default:
					return 0;
			}
		}

		/// <summary>
		/// Converts wind in user units to m/s
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static double WindToMS(double value)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0:
					return value;
				case 1:
					return value / 2.23693629;
				case 2:
					return value / 3.6F;
				case 3:
					return value / 1.94384449;
				default:
					return 0;
			}
		}

		/// <summary>
		/// Converts value in kilometres to distance unit based on users configured wind units
		/// </summary>
		/// <param name="val"></param>
		/// <returns>Wind in configured units</returns>
		public static double KmtoUserUnits(double val)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0: // m/s
				case 2: // km/h
					return val;
				case 1: // mph
					return val * 0.621371;
				case 3: // knots
					return val * 0.539957;
			}
			return val;
		}

		/// <summary>
		///  Converts windrun supplied in user units to km
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in km</returns>
		public static double WindRunToKm(double value)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0: // m/s
				case 2: // km/h
					return value;
				case 1: // mph
					return value / 0.621371192;
				case 3: // knots
					return value / 0.539957;
				default:
					return 0;
			}
		}

		/// <summary>
		///  Converts windrun supplied in user units to miles
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in mi</returns>
		public static double WindRunToMi(double value)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0: // m/s
				case 2: // km/h
					return value * 0.621371192;
				case 1: // mph
					return value;
				case 3: // knots
					return value / 0.8689762;
				default:
					return 0;
			}
		}

		/// <summary>
		///  Converts windrun supplied in user units to nautical miles
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in Nm</returns>
		public static double WindRunToNm(double value)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0: // m/s
				case 2: // km/h
					return value * 0.539956803;
				case 1: // mph
					return value * 0.8689762;
				case 3: // knots
					return value;
				default:
					return 0;
			}
		}

		public static double UserWindToKPH(double wind) // input is in Units.Wind units, convert to km/h
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0: // m/s
					return wind * 3.6;
				case 1: // mph
					return wind * 1.609344;
				case 2: // kph
					return wind;
				case 3: // knots
					return wind * 1.852;
				default:
					return wind;
			}
		}

		public static double UserWindToMS(double wind) // input is in Units.Wind units, convert to m/s
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0: // m/s
					return wind;
				case 1: // mph
					return wind * 0.44704;
				case 2: // kph
					return wind * 0.2777778;
				case 3: // knots
					return wind * 0.5144444;
				default:
					return wind;
			}
		}

		public static double UserWindToMPH(double value)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0:
					return value * 2.23693629;
				case 1:
					return value;
				case 2:
					return value * 0.621371;
				case 3:
					return value * 1.15077945;
				default:
					return 0;
			}
		}

		public static double UserWindToKnots(double value)
		{
			switch (Program.cumulus.Units.Wind)
			{
				case 0:
					return value * 1.943844;
				case 1:
					return value * 0.8689758;
				case 2:
					return value * 0.5399565;
				case 3:
					return value;
				default:
					return 0;
			}
		}


		/// <summary>
		/// Converts rain in mm to units in use
		/// </summary>
		/// <param name="value">Rain in mm</param>
		/// <returns>Rain in configured units</returns>
		public static double RainMMToUser(double value)
		{
			return Program.cumulus.Units.Rain == 1 ? value * 0.0393700787 : value;
		}

		/// <summary>
		/// Converts rain in inches to units in use
		/// </summary>
		/// <param name="value">Rain in mm</param>
		/// <returns>Rain in configured units</returns>
		public static double RainINToUser(double value)
		{
			return Program.cumulus.Units.Rain == 1 ? value : value * 25.4;
		}

		/// <summary>
		/// Converts rain in units in use to mm
		/// </summary>
		/// <param name="value">Rain in configured units</param>
		/// <returns>Rain in mm</returns>
		public static double UserRainToMM(double value)
		{
			return Program.cumulus.Units.Rain == 1 ? value / 0.0393700787 : value;
		}

		public static double UserRainToIN(double rain)
		{
			return Program.cumulus.Units.Rain == 0 ? rain * 0.0393700787 : rain;
		}


		/// <summary>
		/// Convert pressure in mb to units in use
		/// </summary>
		/// <param name="value">pressure in mb</param>
		/// <returns>pressure in configured units</returns>
		public static double PressMBToUser(double value)
		{
			return Program.cumulus.Units.Press == 2 ? value * 0.0295333727 : value;
		}

		/// <summary>
		/// Convert pressure in inHg to units in use
		/// </summary>
		/// <param name="value">pressure in mb</param>
		/// <returns>pressure in configured units</returns>
		public static double PressINHGToUser(double value)
		{
			return Program.cumulus.Units.Press == 2 ? value : value * 33.8638866667;
		}

		/// <summary>
		/// Convert pressure in units in use to mb
		/// </summary>
		/// <param name="value">pressure in configured units</param>
		/// <returns>pressure in mb</returns>
		public static double UserPressToMB(double value)
		{
			return Program.cumulus.Units.Press == 2 ? value / 0.0295333727 : value;
		}

		/// <summary>
		/// Convert pressure from user units to hPa
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static double UserPressureToHPa(double value)
		{
			return Program.cumulus.Units.Press == 2 ? value / 0.0295333727 : value;
		}

		/// <summary>
		/// Convert pressure in units in use to inHg
		/// </summary>
		/// <param name="value">pressure in configured units</param>
		/// <returns>pressure in mb</returns>
		public static double UserPressToIN(double value)
		{
			return Program.cumulus.Units.Press == 2 ? value : value * 0.0295333727;
		}

		/// <summary>
		/// Takes speed in user units, returns Bft number
		/// </summary>
		/// <param name="windspeed"></param>
		/// <returns></returns>
		public static int Beaufort(double speed)
		{
			double windspeedMS = UserWindToMS(speed);
			if (windspeedMS < 0.3)
				return 0;
			else if (windspeedMS < 1.6)
				return 1;
			else if (windspeedMS < 3.4)
				return 2;
			else if (windspeedMS < 5.5)
				return 3;
			else if (windspeedMS < 8.0)
				return 4;
			else if (windspeedMS < 10.8)
				return 5;
			else if (windspeedMS < 13.9)
				return 6;
			else if (windspeedMS < 17.2)
				return 7;
			else if (windspeedMS < 20.8)
				return 8;
			else if (windspeedMS < 24.5)
				return 9;
			else if (windspeedMS < 28.5)
				return 10;
			else if (windspeedMS < 32.7)
				return 11;
			else return 12;
		}

	}
}
