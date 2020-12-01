using System;

namespace CumulusMX
{
	class SunriseSunset
	{
		//
		//   This module contains a user defined spreadsheet function called
		//   sunevent(year, month, day, glong, glat, tz,event) that returns the
		//   time of day of sunrise, sunset, or the beginning and end one of
		//   three kinds of twilight
		//
		//   The method used here is adapted from Montenbruck and Pfleger's
		//   Astronomy on the Personal Computer, 3rd Ed, section 3.8
		//
		//   the arguments for the function are as follows...
		//   year, month, day - your date in zone time
		//   glong - your longitude in degrees, west negative
		//   glat - your latitude in degrees, north positive
		//   tz - your time zone in decimal hours, west or 'behind' Greenwich negative
		//   event - a code integer representing the event you want as follows
		//       positive integers mean rising events
		//       negative integers mean setting events
		//       1 = sunrise                 -1  = sunset
		//       2 = begin civil twilight    -2  = end civil twilight
		//       3 = begin nautical twi      -3  = end nautical twi
		//       4 = begin astro twi         -4  = end astro twi
		//
		//   the results are returned as a variant with either a time of day
		//   in zone time or a string reporting an 'event not occuring' condition
		//   event not occuring can be one of the following
		//       .....    always below horizon, so no rise or set
		//       *****    always above horizon, so no rise or set
		//       -----    the particular rise or set event does not occur on that day
		//
		//   The function will produce meaningful results at all latitudes
		//   but there will be a small range of latitudes around 67.43 degrees North or South
		//   when the function might indicate a sunrise very close to noon (or a sunset
		//   very soon after noon) where in fact the Sun is below the horizon all day
		//   this behaviour relates to the approximate Sun position formulas in use
		//
		//   As always, the sunrise / set times relate to an earth which is smooth
		//   and has no large obstructions on the horizon - you might get a close
		//   approximation to this at sea but rarely on land. Accuracy more than 1 min
		//   of time is not worth striving for - atmospheric refraction alone can
		//   alter observed rise times by minutes
		//
		//   The module also defines a series of 'named funtions' based on sunevent()
		//   as follows
		//   astrotwilightstarts(date, tz, glong, glat)
		//   nauticaltwilightstarts(date, tz, glong, glat)
		//   civiltwilightstarts(date, tz, glong, glat)
		//   sunrise(date, tz, glong, glat)
		//   sunset(date, tz, glong, glat)
		//   civiltwilightends(date, tz, glong, glat)
		//   nauticaltwilightends(date, tz, glong, glat)
		//   astrotwilightends(date, tz, glong, glat)
		//
		//   these functions  take a date in Excel date format and return times in
		//   Excel time format (ie a day fraction) if there is an event on the day
		//   If there isn't an event on the day, then you get #VALUE!
		//   These functions lend themselves to plotting sunrise and set times on
		//   charts - just multiply output in 'number' format by 24 to get the decimal
		//   hours for the event
		//

		private const int CSunrise = 1;
		private const int CBeginCivilTwilight = -2;
		private const int CBeginNautTwilight = -3;
		private const int CBeginAstroTwilight = -4;
		private const int CSunset = -1;
		private const int CEndCivilTwilight = 2;
		private const int CEndNautTwilight = 3;
		private const int CEndAstroTwilight = 4;

		public static DateTime RoundToMinute(DateTime dt)
		{
			var dtRounded = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
			if (dt.Second >= 30) dtRounded = dtRounded.AddMinutes(1);
			return dtRounded;
		}

		private static double ModifiedJulianDay(int year, int month, int day)
		{
			//
			//   takes the year, month and day as a Gregorian calendar date
			//   and returns the modified julian day number
			//
			double a;
			double b;

			if (month <= 2)
			{
				month += 12;
				year--;
			}

			b = (int)Math.Floor(year / 400.0) - (int)Math.Floor(year / 100.0) + (int)Math.Floor(year / 4.0);
			a = 365.0 * year - 679004.0;
			return a + b + (int)Math.Floor(30.6001 * (month + 1)) + day;
		}

		private static double Frac(double x)
		{
			//
			//  returns the fractional part of x as used in minimoon and minisun
			//
			double a;
			a = x - (int)Math.Floor(x);
			return a;
		}

		private static double Range(double x)
		{
			//
			//   returns an angle in degrees in the range 0 to 360
			//   used to condition the arguments for the Sun's orbit
			//   in function minisun below
			//
			double a;
			double b;
			b = x / 360;
			a = 360 * (b - (int)Math.Floor(b));
			if (a < 0)
			{
				a += 360;
			}
			return a;
		}

		/*
		private static string hrsmin(double t)
		{
			//
			//   takes a time as a decimal number of hours between 0 and 23.9999...
			//   and returns a string with the time in hhmm format
			//
			double hour;
			double min;

			hour = (int)Math.Floor(t);
			min = (int)Math.Floor((t - hour) * 60 + 0.5);
			return (hour.ToString("00") + min.ToString("00"));
			//return "0000";
		}
		*/

		private static string HrsMinSec(double t)
		{
			//
			//   takes a time as a decimal number of hours between 0 and 23.9999...
			//   and returns a string with the time in hhmmss format
			//
			TimeSpan ts = TimeSpan.FromHours(t);
			var hour = ts.Hours;
			var min = ts.Minutes;
			var sec = ts.Seconds;

			//double hour = (int)Math.Floor(t);
			//double min = (int)Math.Floor((t - hour) * 60 + 0.5);
			return (hour.ToString("00") + min.ToString("00") + sec.ToString("00"));
			//return "0000";
		}

		private static double LocalMeanSiderealTime(double mjd, double glong)
		{
			//
			//  Takes the mjd and the longitude (west negative) and then returns
			//  the local sidereal time in hours. Im using Meeus formula 11.4
			//  instead of messing about with UTo and so on
			//
			var d = mjd - 51544.5;
			var t = d / 36525.0;
			var lst = Range(280.46061837 + 360.98564736629 * d + 0.000387933 * t * t - t * t * t / 38710000.0);
			return lst / 15.0 + glong / 15.0;
		}

		private static void MiniSun(double t, ref double ra, ref double dec)
		{
			//
			//   takes t (julian centuries since J2000.0) and empty variables ra and dec
			//   sets ra and dec to the value of the Sun coordinates at t
			//
			//   positions claimed to be within 1 arc min by Montenbruck and Pfleger
			//
			const double p2 = 6.283185307;
			const double coseps = 0.91748;
			const double sineps = 0.39778;

			var m = p2 * Frac(0.993133 + 99.997361 * t);
			var DL = 6893.0 * System.Math.Sin(m) + 72.0 * System.Math.Sin(2 * m);
			var l = p2 * Frac(0.7859453 + m / p2 + (6191.2 * t + DL) / 1296000.0);
			var SL = System.Math.Sin(l);
			var x = System.Math.Cos(l);
			var y = coseps * SL;
			var z = sineps * SL;
			var rho = System.Math.Sqrt(1 - z * z);
			dec = (360.0 / p2) * System.Math.Atan(z / rho);
			ra = (48.0 / p2) * System.Math.Atan(y / (x + rho));
			if (ra < 0)
			{
				ra += 24;
			}
		}

		private static void Quadrant(double ym, double yz, double yp, ref int nz, ref double z1, ref double z2, ref double xe, ref double ye)
		{
			//
			//  finds the parabola throuh the three points (-1,ym), (0,yz), (1, yp)
			//  and sets the coordinates of the max/min (if any) xe, ye
			//  the values of x where the parabola crosses zero (z1, z2)
			//  and the nz number of roots (0, 1 or 2) within the interval [-1, 1]
			//

			nz = 0;
			var a = 0.5 * (ym + yp) - yz;
			var b = 0.5 * (yp - ym);
			var c = yz;
			xe = -b / (2 * a);
			ye = (a * xe + b) * xe + c;
			var dis = b * b - 4.0 * a * c;

			if (!(dis > 0)) return;

			var dx = 0.5 * System.Math.Sqrt(dis) / System.Math.Abs(a);
			z1 = xe - dx;
			z2 = xe + dx;
			if (System.Math.Abs(z1) <= 1.0)
			{
				nz++;
			}
			if (System.Math.Abs(z2) <= 1.0)
			{
				nz++;
			}
			if (z1 < -1.0)
			{
				z1 = z2;
			}
		}

		private static double SinAltSun(double mjd0, double hour, double glong, double cglat, double sglat)
		{
			//
			//  this rather mickey mouse function takes a lot of
			//  arguments and then returns the sine of the altitude of
			//  the object labelled by iobj. iobj = 1 is moon, iobj = 2 is sun
			//
			double ra = 0;
			double dec = 0;
			const double rads = 0.0174532925;
			var mjd = mjd0 + hour / 24.0;
			var t = (mjd - 51544.5) / 36525.0;
			MiniSun(t, ref ra, ref dec);
			// hour angle of object
			var tau = 15.0 * (LocalMeanSiderealTime(mjd, glong) - ra);
			// sin(alt) of object using the conversion formulas
			var salt = sglat * System.Math.Sin(rads * dec) + cglat * System.Math.Cos(rads * dec) * System.Math.Cos(rads * tau);
			return salt;
		}

		//
		//   Worksheet functions below....
		//

		private static string SunEvent(int year, int month, int day, double tz, double glong, double glat, int EventType)
		{
			//
			//   This is the function that does most of the work
			//
			//			double sglong;
			double utrise = 0;
			double utset = 0;
			// 			int above;
			// 			double utset;
			// 			int above;
			// 			double utrise;
			// 			double utset;
			string OutString = "";
			// 			string AlwaysDown;
			// 			string OutString;
			// 			string NoEvent;
			double[] sinho = new double[6];
			const double rads = 0.0174532925;
			const string alwaysUp = "****";
			const string alwaysDown = "....";
			const string noEvent = "----";

			//
			//   Set up the array with the 4 values of sinho needed for the 4
			//   kinds of sun event
			//
			sinho[1] = System.Math.Sin(rads * -0.833); //sunset upper limb simple refraction
			sinho[2] = System.Math.Sin(rads * -6.0); //civil twi
			sinho[3] = System.Math.Sin(rads * -12.0); //nautical twi
			sinho[4] = System.Math.Sin(rads * -18.0); //astro twi
			var sglat = System.Math.Sin(rads * glat);
			var cglat = System.Math.Cos(rads * glat);
			var ddate = ModifiedJulianDay(year, month, day) - tz / 24.0;
			//
			//   main loop takes each value of sinho in turn and finds the rise/set
			//   events associated with that altitude of the Sun
			//
			var j = System.Math.Abs(EventType);
			var nz = 0;
			double z1 = 0;
			double z2 = 0;
			double xe = 0;
			double ye = 0;
			var rise = 0;
			var sett = 0;
			var above = 0;
			var hour = 1.0;
			var ym = SinAltSun(ddate, hour - 1.0, glong, cglat, sglat) - sinho[j];
			if (ym > 0.0)
			{
				above = 1;
			}
			//
			//  the while loop finds the sin(alt) for sets of three consecutive
			//  hours, and then tests for a single zero crossing in the interval
			//  or for two zero crossings in an interval or for a grazing event
			//  The flags rise and sett are set accordingly
			//
			while (hour < 25 && (sett == 0 || rise == 0))
			{
				var yz = SinAltSun(ddate, hour, glong, cglat, sglat) - sinho[j];
				var yp = SinAltSun(ddate, hour + 1.0, glong, cglat, sglat) - sinho[j];
				Quadrant(ym, yz, yp, ref nz, ref z1, ref z2, ref xe, ref ye);
				// case when one event is found in the interval
				if (nz == 1)
				{
					if (ym < 0.0)
					{
						utrise = hour + z1;
						rise = 1;
					}
					else
					{
						utset = hour + z1;
						sett = 1;
					}
				} // end of nz = 1 case
				//
				//   case where two events are found in this interval
				//   (rare but whole reason we are not using simple iteration)
				//
				if (nz == 2)
				{
					if (ye < 0.0)
					{
						utrise = hour + z2;
						utset = hour + z1;
					}
					else
					{
						utrise = hour + z1;
						utset = hour + z2;
					}
					rise = 1;
					sett = 1;
				}
				//
				//   set up the next search interval
				//
				ym = yp;
				hour += 2.0;

			} // end of while loop
			//
			// now search has completed, we compile the string to pass back
			// to the user. The string depends on several combinations
			// of the above flag (always above or always below) and the rise
			// and sett flags
			//
			if (rise == 1 || sett == 1)
			{
				if (rise == 1)
				{
					if (EventType > 0)
					{
						OutString = HrsMinSec(utrise);
					}
				}
				else
				{
					if (EventType > 0)
					{
						OutString = noEvent;
					}
				}
				if (sett == 1)
				{
					if (EventType < 0)
					{
						OutString = HrsMinSec(utset);
					}
				}
				else
				{
					if (EventType < 0)
					{
						OutString = noEvent;
					}
				}
			}
			else
			{
				if (above == 1)
				{
					OutString = alwaysUp;
				}
				else
				{
					OutString = alwaysDown;
				}
			}
			return OutString;
		}

		public static string SunRise(DateTime ddate, double tz, double glong, double glat)
		{
			//
			//   simple way of calling sunevent() using the Excel date format
			//   returns just the sunrise time or NULL if no event
			//   I used the day(), month() and year() functions in excel to allow
			//   portability to the MAC (different date serial numbers)
			//
			string EventTime;
			var sOut = SunEvent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, CSunrise);
			switch (sOut)
			{
				case "....":
					EventTime = "Always Down";
					break;
				case "----":
					EventTime = "No event";
					break;
				case "****":
					EventTime = "Always Up";
					break;
				default:
					EventTime = sOut;
					break;
			}
			return EventTime;
		}

		public static string SunSet(DateTime ddate, double tz, double glong, double glat)
		{
			//
			//   simple way of calling sunevent() using the Excel date format
			//   returns just the sunset time or ****, ...., ---- as approptiate in a string
			//   I used the day(), month() and year() functions in excel to allow
			//   portability to the MAC (different date serial number base)
			//
			string eventTime;
			var sOut = SunEvent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, CSunset);
			switch (sOut)
			{
				case "....":
					eventTime = "Always Down";
					break;
				case "----":
					eventTime = "No event";
					break;
				case "****":
					eventTime = "Always Up";
					break;
				default:
					eventTime = sOut;
					break;
			}
			return eventTime;
		}

		public static string CivilTwilightStarts(DateTime ddate, double tz, double glong, double glat)
		{
			//
			//   simple way of calling sunevent() using the Excel date format
			//   returns just the start of civil twilight time or ****, ...., ---- as approptiate
			//   I used the day(), month() and year() functions in excel to allow
			//   portability to the MAC (different date serial numbers)
			//
			string eventTime;
			var sOut = SunEvent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, CBeginCivilTwilight);
			switch (sOut)
			{
				case "....":
					eventTime = "Always Down";
					break;
				case "----":
					eventTime = "No event";
					break;
				case "****":
					eventTime = "Always Up";
					break;
				default:
					eventTime = sOut;
					break;
			}
			return eventTime;
		}

		public static string CivilTwilightEnds(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string eventTime;
			var sOut = SunEvent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, CEndCivilTwilight);
			switch (sOut)
			{
				case "....":
					eventTime = "Always Down";
					break;
				case "----":
					eventTime = "No event";
					break;
				case "****":
					eventTime = "Always Up";
					break;
				default:
					eventTime = sOut;
					break;
			}
			return eventTime;
		}

		public static string NauticalTwilightStarts(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string eventTime;
			var sOut = SunEvent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, CBeginNautTwilight);
			switch (sOut)
			{
				case "....":
					eventTime = "Always Down";
					break;
				case "----":
					eventTime = "No event";
					break;
				case "****":
					eventTime = "Always Up";
					break;
				default:
					eventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
					break;
			}
			return eventTime;
		}

		public static string NauticalTwilightEnds(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string eventTime;
			var sOut = SunEvent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, CEndNautTwilight);
			switch (sOut)
			{
				case "....":
					eventTime = "Always Down";
					break;
				case "----":
					eventTime = "No event";
					break;
				case "****":
					eventTime = "Always Up";
					break;
				default:
					eventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
					break;
			}
			return eventTime;
		}

		public static string AstroTwilightStarts(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string eventTime;
			var sOut = SunEvent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, CBeginAstroTwilight);
			switch (sOut)
			{
				case "....":
					eventTime = "Always Down";
					break;
				case "----":
					eventTime = "No event";
					break;
				case "****":
					eventTime = "Always Up";
					break;
				default:
					eventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
					break;
			}
			return eventTime;
		}

		public static string AstroTwilightEnds(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string eventTime;
			var sOut = SunEvent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, CEndAstroTwilight);
			switch (sOut)
			{
				case "....":
					eventTime = "Always Down";
					break;
				case "----":
					eventTime = "No event";
					break;
				case "****":
					eventTime = "Always Up";
					break;
				default:
					eventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
					break;
			}
			return eventTime;
		}
	}
}
