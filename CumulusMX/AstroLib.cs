using System;

namespace CumulusMX
{
	internal class AstroLib
	{
		public static double BrasSolar(double el, double erv, double nfac)
		{
			// el      solar elevation deg from horizon
			// erv     distance from earth to sun in AU
			// nfac    atmospheric turbidity parameter (2=clear, 4-5=smoggy)

			double sinal = Math.Sin(DegToRad(el)); // Sine of the solar elevation angle

			if (sinal < 0)
				return 0;

			// solar radiation on horizontal surface at top of atmosphere
			double i0 = (1367 / (erv * erv)) * sinal;

			// optical air mass
			double m = 1 / (sinal + (0.15 * Math.Pow(el + 3.885, -1.253)));

			// molecular scattering coefficient
			double al = 0.128 - (0.054 * Math.Log(m) / Math.Log(10));

			// clear-sky solar radiation at earth surface on horizontal surface (W/m^2)
			return i0 * Math.Exp(-nfac * al * m);
		}

		public static double RyanStolzSolar(double el, double erv, double atc, double z)
		{
			// el      solar elevation deg from horizon
			// erv     distance from earth to sun in AU
			// atc     atmospheric transmission coefficient
			// z       elevation, metres

			double sinal = Math.Sin(DegToRad(el)); // Sine of the solar elevation angle

			if (sinal < 0)
				return 0;

			double al = Math.Asin(sinal);
			double a0 = RadToDeg(al); // convert the radians to degree

			double rm = Math.Pow(((288.0 - 0.0065 * z) / 288.0), 5.256) / (sinal + 0.15 * Math.Pow((a0 + 3.885), (-1.253)));

			double rsToa = 1360 * sinal / (erv * erv); // RS on the top of atmosphere

			return rsToa * Math.Pow(atc, rm); //RS on the ground
		}

		private static double DegToRad(double angle)
		{
			return Math.PI * angle / 180.0;
		}

		private static double RadToDeg(double angle)
		{
			return angle * (180.0 / Math.PI);
		}

		public static double SolarMax(DateTime timestamp, double longitude, double latitude, double altitude,
									  out double solarelevation, SolarOptions options)
		{
			double factor = 0;
			if (options.SolarCalc == 0)
			{
				factor = GetFactor(timestamp, options.RStransfactorJun, options.RStransfactorDec);
			}
			if (options.SolarCalc == 1)
			{
				factor = GetFactor(timestamp, options.BrasTurbidityJun, options.BrasTurbidityDec);
			}
			return SolarMax(timestamp, longitude, latitude, altitude, out solarelevation, factor, options.SolarCalc);
		}


		private static double SolarMax(DateTime timestamp, double longitude, double latitude, double altitude,
									  out double solarelevation, double factor, int method)
		{
			DateTime utctime = timestamp.ToUniversalTime();

			CalculateSunPosition(utctime, latitude, longitude, out solarelevation, out _);
			var dEpoch = new DateTime(1990, 1, 1, 0, 0, 0);
			double erv = CalcSunDistance(utctime, dEpoch);

			//cumulus.LogMessage(utctime+" lat="+latitude+" lon="+longitude+" sun elev="+solarelevation);
			if (method == 0)
			{
				return RyanStolzSolar(solarelevation, erv, factor, altitude);
			}
			if (method == 1)
			{
				return BrasSolar(solarelevation, erv, factor);
			}

			return 0;
		}

		// Calculate the interpolated factor using a cosine function
		private static double GetFactor(DateTime timestamp, double jun, double dec)
		{
			var range = jun - dec;
			var doy = timestamp.DayOfYear;
			return dec + Math.Cos((doy - 172) / 183.0 * Math.PI / 2) * range;
		}



		// Uses the VBA from NOAA solrad_ver16 spreadsheet
		#region solrad calculations

		/*
		private static void CalculateSunPosition(DateTime dateTime, double latitude, double lon, out double altitude, out double azimuth)
		{
			// change sign convention for longitude from negative to positive in western hemisphere
			var longitude = lon * -1.0;

			if (latitude > 89.8)
				latitude = 89.8;

			if (latitude < -89.8)
				latitude = -89.8;

			// timenow is GMT time for calculation in hours since 0Z
			//var timenow = dateTime.TimeOfDay.TotalHours;

			double julianDate = 367.0 * dateTime.Year -
				(int)((7.0 / 4.0) * (dateTime.Year + (int)((dateTime.Month + 9.0) / 12.0))) +
				(int)((275.0 * dateTime.Month) / 9.0) +
				dateTime.Day - 730531.5;

			double t = julianDate / 36525.0;
			//double r = calcSunRadVector(t);
			//double alpha = calcSunRtAscension(t);
			double solardec = calcSunDeclination(t);
			double eqtime = calcEquationOfTime(t);

			double zone = TimeZoneInfo.Local.BaseUtcOffset.Hours;
			zone = -7;

			double solarTimeFix = eqtime - 4.0 * longitude + 60.0 * zone;
			double trueSolarTime = dateTime.Hour * 60 + dateTime.Minute + dateTime.Second / 60.0 + solarTimeFix;  // in minutes

			while (trueSolarTime > 1440)
			{
				trueSolarTime -= 1440;
			}

			double hourangle = trueSolarTime / 4.0 - 180;

			if (hourangle < -180)
				hourangle += 360;

			double harad = DegToRad(hourangle);

			double csz = Math.Sin(DegToRad(latitude)) * Math.Sin(DegToRad(solardec)) + Math.Cos(DegToRad(latitude)) * Math.Cos(DegToRad(solardec)) * Math.Cos(harad);

			if (csz > 1)
				csz = 1;
			else if (csz < -1)
				csz = -1;

			double zenith = RadToDeg(Math.Acos(csz));
			double azDenom = Math.Cos(DegToRad(latitude)) * Math.Sin(DegToRad(zenith));

			double azRad;

			if (Math.Abs(azDenom) > 0.001)
			{
				azRad = (Math.Sin(DegToRad(latitude) * Math.Cos(DegToRad(zenith))) - Math.Sin(DegToRad(solardec))) / azDenom;

				if (Math.Abs(azRad) > 1)
				{
					if (azRad < 0)
						azRad = -1;
					else
						azRad = 1;
				}

				azimuth = 180.0 - RadToDeg(Math.Acos(azRad));

				if (hourangle > 0)
					azimuth = -azimuth;
			}
			else
			{
				if (latitude > 0)
					azimuth = 180;
				else
					azimuth = 0;

			}

			if (azimuth < 0)
				azimuth += 360;

			var exoatmElevation = 90.0 - zenith;
			double refractionCorrection;

			if (exoatmElevation > 85)
			{
				refractionCorrection = 0;
			}
			else
			{

				double te = Math.Tan(DegToRad(exoatmElevation));

				if (exoatmElevation > 5)
				{
					refractionCorrection = 58.1 / te - 0.07 / (te * te * te) + 0.000086 / (te * te * te * te * te);
				}
				else if (exoatmElevation > -0.575)
				{
					double step1 = -12.79 + exoatmElevation * 0.711;
					double step2 = 103.4 + exoatmElevation * step1;
					double step3 = -518.2 + exoatmElevation * step2;

					refractionCorrection = 1735.0 + exoatmElevation * step3;
				}
				else
				{
					refractionCorrection = -20.774 / te;
				}

				refractionCorrection = refractionCorrection / 3600.0;
			}

			double solarzen = zenith - refractionCorrection;

			altitude = 90.0 - solarzen;
		}



		private static double calcSunRadVector(double t)
		{
			// ***********************************************************************
			// Name:    calcSunRadVector (not used by sunrise, solarnoon, sunset)
			// Type:    Function
			// Purpose: calculate the distance to the sun in AU
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   sun radius vector in AUs
			// ***********************************************************************

			double v = calcSunTrueAnomaly(t);

			double e = calcEccentricityEarthOrbit(t);

			return 1.000001018 * (1 - e * e) / (1 + e * Math.Cos(DegToRad(v)));
		}

		private static double calcSunTrueAnomaly(double t)
		{
			// ***********************************************************************
			// Name:    calcSunTrueAnomaly (not used by sunrise, solarnoon, sunset)
			// Type:    Function
			//Purpose: calculate the true anamoly of the sun
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   sun's true anamoly in degrees
			// ***********************************************************************

			double m = calcGeomMeanAnomalySun(t);

			double c = calcSunEqOfCenter(t);

			return m + c;
		}

		private static double calcGeomMeanAnomalySun(double t)
		{
			// ***********************************************************************
			// Name:    calGeomAnomalySun
			// Type:    Function
			// Purpose: calculate the Geometric Mean Anomaly of the Sun
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   the Geometric Mean Anomaly of the Sun in degrees
			// ***********************************************************************

			return 357.52911 + t * (35999.05029 - 0.0001537 * t);
		}

		private static double calcSunEqOfCenter(double t)
		{
			// ***********************************************************************
			// Name:    calcSunEqOfCenter
			// Type:    Function
			// Purpose: calculate the equation of center for the sun
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   in degrees
			// ***********************************************************************

			double m = calcGeomMeanAnomalySun(t);

			double mrad = DegToRad(m);
			double sinm = Math.Sin(mrad);
			double sin2m = Math.Sin(mrad + mrad);
			double sin3m = Math.Sin(mrad + mrad + mrad);

			return sinm * (1.914602 - t * (0.004817 + 0.000014 * t)) + sin2m * (0.019993 - 0.000101 * t) + sin3m * 0.000289;
		}

		private static double calcEccentricityEarthOrbit(double t)
		{
			// ***********************************************************************
			// Name:    calcEccentricityEarthOrbit
			// Type:    Function
			// Purpose: calculate the eccentricity of earth's orbit
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   the unitless eccentricity
			// ***********************************************************************

			return 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
		}


		private static double calcSunRtAscension(double t)
		{
			// ***********************************************************************
			// Name:    calcSunRtAscension (not used by sunrise, solarnoon, sunset)
			// Type:    Function
			// Purpose: calculate the right ascension of the sun
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   sun's right ascension in degrees
			// ***********************************************************************

			double e = calcObliquityCorrection(t);

			double lambda = calcSunApparentLong(t);
			double tananum = Math.Cos(DegToRad(e)) * Math.Sin(DegToRad(lambda));
			double tanadenom = Math.Cos(DegToRad(lambda));

			//original NOAA code using javascript Math.Atan2(y,x) convention:
			//        var alpha = radToDeg(Math.atan2(tananum, tanadenom));
			//        alpha = radToDeg(Application.WorksheetFunction.Atan2(tananum, tanadenom))
			//
			//translated using Excel VBA Application.WorksheetFunction.Atan2(x,y) convention:

			return RadToDeg(Math.Atan2(tanadenom, tananum));
		}

		private static double calcSunDeclination(double t)
		{
			// ***********************************************************************
			// Name:    calcSunDeclination
			// Type:    Function
			// Purpose: calculate the declination of the sun
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   sun's declination in degrees
			// ***********************************************************************

			double e = calcObliquityCorrection(t);

			double lambda = calcSunApparentLong(t);
			double sint = Math.Sin(DegToRad(e)) * Math.Sin(DegToRad(lambda));

			return RadToDeg(Math.Asin(sint));
		}

		private static double calcSunApparentLong(double t)
		{
			// ***********************************************************************
			// Name:    calcSunApparentLong (not used by sunrise, solarnoon, sunset)
			// Type:    Function
			// Purpose: calculate the apparent longitude of the sun
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   sun's apparent longitude in degrees
			// ***********************************************************************

			double O = calcSunTrueLong(t);

			double omega = 125.04 - 1934.136 * t;

			return O - 0.00569 - 0.00478 * Math.Sin(DegToRad(omega));
		}

		private static double calcSunTrueLong(double t)
		{
			// ***********************************************************************
			// Name:    calcSunTrueLong
			// Type:    Function
			// Purpose: calculate the true longitude of the sun
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   sun's true longitude in degrees
			// ***********************************************************************

			double l0 = calcGeomMeanLongSun(t);

			double c = calcSunEqOfCenter(t);

			return l0 + c;
		}

		private static double calcGeomMeanLongSun(double t)
		{
			// ***********************************************************************
			// Name:    calGeomMeanLongSun
			// Type:    Function
			// Purpose: calculate the Geometric Mean Longitude of the Sun
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   the Geometric Mean Longitude of the Sun in degrees
			// ***********************************************************************

			double l0 = 280.46646 + t * (36000.76983 + 0.0003032 * t);

			do
			{
				if (l0 <= 360 && l0 >= 0)
					break;

				if (l0 > 360)
					l0 -= 360;

				if (l0 < 0)
					l0 += 360;
			} while (true);

			return l0;
		}

		private static double calcEquationOfTime(double t)
		{
			// ***********************************************************************
			// Name:    calcEquationOfTime
			// Type:    Function
			// Purpose: calculate the difference between true solar time and mean
			//     solar time
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   equation of time in minutes of time
			// ***********************************************************************

			double epsilon = calcObliquityCorrection(t);
			double l0 = calcGeomMeanLongSun(t);
			double e = calcEccentricityEarthOrbit(t);
			double m = calcGeomMeanAnomalySun(t);

			double y = Math.Tan(DegToRad(epsilon) / 2.0);
			y = y * y;

			double sin2l0 = Math.Sin(2.0 * DegToRad(l0));
			double sinm = Math.Sin(DegToRad(m));
			double cos2l0 = Math.Cos(2.0 * DegToRad(l0));
			double sin4l0 = Math.Sin(4.0 * DegToRad(l0));
			double sin2m = Math.Sin(2.0 * DegToRad(m));

			double Etime = y * sin2l0 - 2.0 * e * sinm + 4.0 * e * y * sinm * cos2l0 - 0.5 * y * y * sin4l0 - 1.25 * e * e * sin2m;

			return RadToDeg(Etime) * 4.0;
		}

		private static double calcMeanObliquityOfEcliptic(double t)
		{
			// ***********************************************************************
			// Name:    calcMeanObliquityOfEcliptic
			// Type:    Function
			// Purpose: calculate the mean obliquity of the ecliptic
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   mean obliquity in degrees
			// ***********************************************************************

			double seconds = 21.448 - t * (46.815 + t * (0.00059 - t * (0.001813)));

			return 23.0 + (26.0 + (seconds / 60.0)) / 60.0;
		}

		private static double calcObliquityCorrection(double t)
		{
			// ***********************************************************************
			// Name:    calcObliquityCorrection
			// Type:    Function
			// Purpose: calculate the corrected obliquity of the ecliptic
			// Arguments:
			//   t : number of Julian centuries since J2000.0
			// Return value:
			//   corrected obliquity in degrees
			// ***********************************************************************

			double e0 = calcMeanObliquityOfEcliptic(t);

			double omega = 125.04 - 1934.136 * t;
			
			return e0 + 0.00256 * Math.Cos(DegToRad(omega));
		}

		*/

		#endregion



		// From the NOAA_SolarCalculations_day spreadsheet
		// https://gml.noaa.gov/grad/solcalc/calcdetails.html
		#region NOAA_Solar

		private static void CalculateSunPosition(DateTime dateTime, double latitude, double longitude, out double altitude, out double azimuth)
		{
			// We will use DateTime.ToOADate() which automatically includes the TZ
			var zone = 0.0;

			var julianDate = dateTime.ToOADate() + 2415018.5;
			var julianCentry = (julianDate - 2451545 ) / 36525.0;
			var geoMeanLongSun = PutIn360Deg(280.46646 + julianCentry * (36000.76983 + julianCentry * 0.0003032));
			var geoMeanAnomSun = 357.52911 + julianCentry * (35999.05029 - 0.0001537 * julianCentry);
			var eccEarthOrbit = 0.016708634 - julianCentry * (0.000042037 + 0.0000001267 * julianCentry);
			var sunEqCentre = Math.Sin(DegToRad(geoMeanAnomSun)) * (1.914602 - julianCentry * (0.004817 + 0.000014 * julianCentry)) + Math.Sin(DegToRad(2 * geoMeanAnomSun)) * (0.019993 - 0.000101 * julianCentry) + Math.Sin(DegToRad(3 * geoMeanAnomSun)) * 0.000289;
			var sunTrueLong = geoMeanLongSun + sunEqCentre;
			//var sunTrueAnom = geoMeanAnomSun + sunEqCentre;
			//var sunRadVector = (1.000001018 * (1 - eccEarthOrbit * eccEarthOrbit)) / (1 + eccEarthOrbit * Math.Cos(DegToRad(sunTrueAnom)));
			var sunAppLong = sunTrueLong - 0.00569 - 0.00478 * Math.Sin(DegToRad(125.04 - 1934.136 * julianCentry));
			var meanObliqEcplitic = 23 + (26 + ((21.448 - julianCentry * (46.815 + julianCentry * (0.00059 - julianCentry * 0.001813)))) / 60.0) / 60.0;
			var obliqCorr = meanObliqEcplitic + 0.00256 * Math.Cos(DegToRad(125.04 - 1934.136 * julianCentry));
			//var sunRA = RadToDeg(Math.Atan2(Math.Cos(DegToRad(sunAppLong)), Math.Cos(DegToRad(obliqCorr)) * Math.Sin(DegToRad(obliqCorr))));
			var sunDec = RadToDeg(Math.Asin(Math.Sin(DegToRad(obliqCorr)) * Math.Sin(DegToRad(sunAppLong))));
			var varY = Math.Tan(DegToRad(obliqCorr / 2.0)) * Math.Tan(DegToRad(obliqCorr / 2.0));
			var eqOfTime = 4 * RadToDeg(varY * Math.Sin(2 * DegToRad(geoMeanLongSun)) - 2 * eccEarthOrbit * Math.Sin(DegToRad(geoMeanAnomSun)) + 4 * eccEarthOrbit * varY * Math.Sin(DegToRad(geoMeanAnomSun)) * Math.Cos(2 * DegToRad(geoMeanLongSun)) - 0.5 * varY * varY * Math.Sin(4 * DegToRad(geoMeanLongSun)) - 1.25 * eccEarthOrbit * eccEarthOrbit * Math.Sin(2 * DegToRad(geoMeanAnomSun)));
			//var haSunRise = RadToDeg(Math.Acos(Math.Cos(DegToRad(90.833)) / (Math.Cos(DegToRad(latitude)) * Math.Cos(DegToRad(sunDec))) - Math.Tan(DegToRad(latitude)) * Math.Tan(DegToRad(sunDec))));
			//var solarNoonLst = (720.0 - 4 * longitude - eqOfTime + zone * 60.0) / 1440.0;
			//var sunriseTimeLst = solarNoonLst - haSunRise * 4 / 1440.0;
			//var sunsetTimeLst = solarNoonLst + haSunRise * 4 / 1440.0;
			//var sunlightDurationMins = 8 * haSunRise; 
			var trueSolarTime = PutInRange(dateTime.TimeOfDay.TotalMinutes + eqOfTime + 4 * longitude - 60 * zone, 1440);
			var hourAngle = trueSolarTime / 4.0 < 0 ? trueSolarTime / 4.0 + 180 : trueSolarTime / 4.0 - 180;
			var solarZenithAngle = RadToDeg(Math.Acos(Math.Sin(DegToRad(latitude)) * Math.Sin(DegToRad(sunDec)) + Math.Cos(DegToRad(latitude)) * Math.Cos(DegToRad(sunDec)) * Math.Cos(DegToRad(hourAngle))));
			var solarElevation = 90 - solarZenithAngle;

			double refraction;

			if (solarElevation > 85)
				refraction = 0;
			else if (solarElevation > 5)
				refraction = 58.1 / Math.Tan(DegToRad(solarElevation)) - 0.07 / Math.Pow(Math.Tan(DegToRad(solarElevation)), 3) + 0.000086 / Math.Pow(Math.Tan(DegToRad(solarElevation)), 5);
			else if (solarElevation > -0.575)
				refraction = 1735.0 + solarElevation * (-518.2 + solarElevation * (103.4 + solarElevation * (-12.79 + solarElevation * 0.711)));
			else
				refraction  = -20.772 / Math.Tan(DegToRad(solarElevation));

			altitude = solarElevation + refraction / 3600.0;

			if (hourAngle > 0)
				azimuth = PutIn360Deg(DegToRad(Math.Acos(((Math.Sin(DegToRad(latitude)) * Math.Cos(DegToRad(solarZenithAngle))) - Math.Sin(DegToRad(sunDec))) / (Math.Sin(DegToRad(latitude)) * Math.Sin(DegToRad(solarZenithAngle))))) + 180);
			else
				azimuth = PutIn360Deg(540 - DegToRad(Math.Acos(((Math.Sin(DegToRad(latitude)) * Math.Cos(DegToRad(solarZenithAngle))) - Math.Sin(DegToRad(sunDec))) / (Math.Cos(DegToRad(latitude)) * Math.Sin(DegToRad(solarZenithAngle))))));

		}


		#endregion


		// http://guideving.blogspot.co.uk/2010/08/sun-position-in-c.html
		#region sun-position-in-c

		/*
		public static void CalculateSunPosition(DateTime dateTime, double latitude, double longitude, out double altitude, out double azimuth)
		{
			const double Deg2Rad = Math.PI / 180.0;
			const double Rad2Deg = 180.0 / Math.PI;
			// Number of days from J2000.0.
			double julianDate = 367 * dateTime.Year -
				(int)((7.0 / 4.0) * (dateTime.Year + (int)((dateTime.Month + 9.0) / 12.0))) +
				(int)((275.0 * dateTime.Month) / 9.0) +
				dateTime.Day - 730531.5;

			double julianCenturies = julianDate / 36525.0;

			// Sidereal Time
			double siderealTimeHours = 6.6974 + 2400.0513 * julianCenturies;

			double siderealTimeUT = siderealTimeHours +
				(366.2422 / 365.2422) * dateTime.TimeOfDay.TotalHours;

			double siderealTime = siderealTimeUT * 15 + longitude;

			// Refine to number of days (fractional) to specific time.
			julianDate += dateTime.TimeOfDay.TotalDays;
			julianCenturies = julianDate / 36525.0;

			// Solar Coordinates
			double meanLongitude = CorrectAngle(Deg2Rad *
				(280.466 + 36000.77 * julianCenturies));

			double meanAnomaly = CorrectAngle(Deg2Rad *
				(357.52911 + 35999.05029 * julianCenturies));

			double equationOfCenter = Deg2Rad * ((1.914602 - 0.004817 * julianCenturies) *
				Math.Sin(meanAnomaly) + 0.019993 * Math.Sin(2 * meanAnomaly));

			double elipticalLongitude =
				CorrectAngle(meanLongitude + equationOfCenter);

			double obliquity = (23.439 - 0.013 * julianCenturies) * Deg2Rad;

			// Right Ascension
			double rightAscension = Math.Atan2(
				Math.Cos(obliquity) * Math.Sin(elipticalLongitude),
				Math.Cos(elipticalLongitude));

			double declination = Math.Asin(
				Math.Sin(rightAscension) * Math.Sin(obliquity));

			// Horizontal Coordinates
			double hourAngle = CorrectAngle(siderealTime * Deg2Rad) - rightAscension;

			if (hourAngle > Math.PI)
			{
				hourAngle -= 2 * Math.PI;
			}

			altitude = Math.Asin(
				Math.Sin(latitude * Deg2Rad) *
				Math.Sin(declination) +
				Math.Cos(latitude * Deg2Rad) *
				Math.Cos(declination) * 
				Math.Cos(hourAngle)
			);

			// refraction correction
			// work in degrees
			altitude *= Rad2Deg;
			double refractionCorrection;
			if (altitude > 85.0 || altitude <= -0.575)
			{
				refractionCorrection = 0.0;
			}
			else
			{
				double te = Math.Tan(DegToRad(altitude));
				if (altitude > 5.0)
				{
					refractionCorrection = 58.1 / te - 0.07 / (te * te * te) + 0.000086 / (te * te * te * te * te);
				}
				else if (altitude > -0.575)
				{
					double step1 = -12.79 + altitude * 0.711;
					double step2 = 103.4 + altitude * step1;
					double step3 = -518.2 + altitude * step2;
					refractionCorrection = 1735.0 + altitude * step3;
				}
				else
				{
					refractionCorrection = -20.774 / te;
				}
				refractionCorrection /= 3600.0;
			}
			altitude += refractionCorrection;

			// Nominator and denominator for calculating Azimuth
			// angle. Needed to test which quadrant the angle is in.
			double aziNom = -Math.Sin(hourAngle);
			double aziDenom =
				Math.Tan(declination) * Math.Cos(latitude * Deg2Rad) -
				Math.Sin(latitude * Deg2Rad) * Math.Cos(hourAngle);

			azimuth = Math.Atan(aziNom / aziDenom);

			if (aziDenom < 0) // In 2nd or 3rd quadrant
			{
				azimuth += Math.PI;
			}
			else if (aziNom < 0) // In 4th quadrant
			{
				azimuth += 2 * Math.PI;
			}

			azimuth *= Rad2Deg;
		}


		//
		// \brief Corrects an angle.
		//
		// \param angleInRadians An angle expressed in radians.
		// \return An angle in the range 0 to 2*PI.
		//
		// http://guideving.blogspot.co.uk/2010/08/sun-position-in-c.html
		//

		private static double CorrectAngle(double angleInRadians)
		{
			if (angleInRadians < 0)
			{
				return 2*Math.PI - (Math.Abs(angleInRadians)%(2*Math.PI));
			}

			if (angleInRadians > 2*Math.PI)
			{
				return angleInRadians%(2*Math.PI);
			}

			return angleInRadians;
		}
		*/

		#endregion


		private static double CalcSunDistance(DateTime dDate, DateTime dEpoch)
		{
			const double fAcc = 0.0000001;
			double fD = GetDaysBetween(dDate, dEpoch);
			double fSolarMEL = GetSolarMEL(dEpoch, true);
			double fSolarPL = GetSolarPerigeeLong(dEpoch, true);
			double fSunEarthEcc = GetSunEarthEcc(dEpoch, true);
			//double fOblique = GetEarthObliquity(dEpoch, true);
			const double fSMA = 149598500.0;

			double fN = (360.0/365.242191)*fD;
			fN = PutIn360Deg(fN);
			double fM = fN + fSolarMEL - fSolarPL;
			fM = PutIn360Deg(fM);
			fM = DegToRad(fM);
			double fE = CalcEccentricAnomaly(fM, fM, fSunEarthEcc, fAcc);
			double fTanV2 = Math.Sqrt((1.0 + fSunEarthEcc)/(1.0 - fSunEarthEcc))*Math.Tan(fE/2.0);
			double fV = Math.Atan(fTanV2)*2.0;
			double fDistance = (fSMA*(1.0 - (fSunEarthEcc*fSunEarthEcc)))/(1.0 + (fSunEarthEcc*Math.Cos(fV)));

			// Convert from km to AU
			return fDistance/149597871.0;
		}

		private static double GetSunEarthEcc(DateTime dDate, bool b0Epoch)
		{
			//Returns the eccentricity of Earth's orbit around the sun for the specified date

			double fJD = GetJulianDay(dDate, 0);
			if (b0Epoch)
			{
				fJD -= 1.0;
			}
			double fT = (fJD - 2415020.0)/36525.0;
			double fEcc = 0.01675104 - (0.0000418*fT) - (0.000000126*fT*fT);
			return fEcc;
		}

		/*
		private static double GetEarthObliquity(DateTime dDate, bool b0Epoch)
		{
			//Returns the obliquity of Earth's orbit around the sun for the specified date

			double fJD = GetJulianDay(dDate, 0);
			if (b0Epoch)
			{
				fJD -= 1.0;
			}

			double fT = (fJD - 2415020.0)/36525.0;
			double fDeltaObl = (46.815*fT) + (0.0006*fT*fT) + (0.00181*fT*fT*fT);
			double fObl = 23.43929167 - (fDeltaObl/3600.0);
			return fObl;
		}
		*/

		private static double CalcEccentricAnomaly(double fEGuess, double fMA, double fEcc, double fAcc)
		{
			//Calc Eccentric Anomaly to specified accuracy
			double fE;

			double fEG = fEGuess;

			double fDelta = fEG - (fEcc*Math.Sin(fEG)) - fMA;
			if (Math.Abs(fDelta) > fAcc)
			{
				double fDeltaE = (fDelta/(1.0 - (fEcc*Math.Cos(fEG))));
				double fETmp = fEG - fDeltaE;
				fE = CalcEccentricAnomaly(fETmp, fMA, fEcc, fAcc);
			}
			else
			{
				fE = fEGuess;
			}
			return fE;
		}

		public static long GetDaysBetween(DateTime dDate, DateTime dSecDate)
		{
			double fJD1 = GetJulianDay(dDate, 0);
			double fJD2 = GetJulianDay(dSecDate, 0);
			double fDays = (fJD1 - fJD2);

			if (fDays >= 0)
			{
				fDays = (long) Math.Floor(fDays);
			}
			else
			{
				fDays = (long) Math.Floor(Math.Abs(fDays))*-1;
			}
			return (long) Math.Floor(fDays);
		}

		private static double GetJulianDay(DateTime dDate, int iZone)
		{
			double iGreg;
			double fC;

			dDate = CalcUTFromZT(dDate, iZone);

			int iYear = dDate.Year;
			int iMonth = dDate.Month;
			int iDay = dDate.Day;
			int iHour = dDate.Hour;
			int iMinute = dDate.Minute;
			int iSecond = dDate.Second;
			double fFrac = iDay + ((iHour + (iMinute/60) + (iSecond/60/60))/24);
			iGreg = iYear < 1582 ? 0 : 1;
			if ((iMonth == 1) || (iMonth == 2))
			{
				iYear -= 1;
				iMonth += 12;
			}

			double fA = (long) Math.Floor(iYear / 100.0);
			double fB = (2 - fA + (long) Math.Floor(fA/4))*iGreg;
			if (iYear < 0)
			{
				fC = (int) Math.Floor((365.25*iYear) - 0.75);
			}
			else
			{
				fC = (int) Math.Floor(365.25*iYear);
			}
			double fD = (int) Math.Floor(30.6001*(iMonth + 1));
			double fJD = fB + fC + fD + 1720994.5;
			fJD += fFrac;
			return fJD;
		}

		private static DateTime CalcUTFromZT(DateTime dDate, int iZone)
		{
			if (iZone >= 0)
			{
				return dDate.Subtract(new TimeSpan(iZone, 0, 0));
			}

			return dDate.AddHours(Math.Abs(iZone));
		}

		private static double GetSolarMEL(DateTime dDate, bool b0Epoch)
		{
			//Returns the Sun's Mean Ecliptic Longitude for the specified date

			double fJD = GetJulianDay(dDate, 0);
			if (b0Epoch)
			{
				fJD -= 1.0;
			}

			double fT = (fJD - 2415020.0)/36525.0;
			double fLong = 279.6966778 + (36000.76892*fT) + (0.0003025*fT*fT);
			fLong = PutIn360Deg(fLong);
			return fLong;
		}

		private static double GetSolarPerigeeLong(DateTime dDate, bool b0Epoch)
		{
			//Returns the Sun's Perigee Longitude for the specified date

			double fJD = GetJulianDay(dDate, 0);

			if (b0Epoch)
			{
				fJD -= 1.0;
			}

			double fT = (fJD - 2415020.0)/36525.0;
			double fLong = 281.2208444 + (1.719175*fT) + (0.000452778*fT*fT);
			fLong = PutIn360Deg(fLong);
			return fLong;
		}

		private static double PutIn360Deg(double pfDeg)
		{
			return PutInRange(pfDeg, 360);
		}

		private static double PutInRange(double val, double range)
		{
			while (val >= range)
				val -= range;

			while (val < 0)
				val += range;

			return val;
		}

		//public static int cSunrise = 1;
		//public static int cBeginCivilTwilight = -2;
		//public static int cBeginNautTwilight = -3;
		//public static int cBeginAstroTwilight = -4;
		//public static int cSunset = -1;
		//public static int cEndCivilTwilight = 2;
		//public static int cEndNautTwilight = 3;
		//public static int cEndAstroTwilight = 4;

		/*
		private static double mjd(int year, int month, int day)
		{
			//
			//   takes the year, month and day as a Gregorian calendar date
			//   and returns the modified julian day number
			//
			double a;
			double b;

			if (month <= 2)
			{
				month = month + 12;
				year--;
			}

			b = (int)Math.Floor(year / 400.0) - (int)Math.Floor(year / 100.0) + (int)Math.Floor(year / 4.0);
			a = 365.0 * year - 679004.0;
			return a + b + (int)Math.Floor(30.6001 * (month + 1)) + day;
		}
		*/

		/*
		private static double frac(double x)
		{
			//
			//  returns the fractional part of x as used in minimoon and minisun
			//
			double a;
			a = x - (int)Math.Floor(x);
			return a;
		}
		*/

		/*
		private static double range(double x)
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
				a = a + 360;
			}
			return a;
		}
		*/

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

		/*
		private static double lmst(double mjd, double glong)
		{
			//
			//  Takes the mjd and the longitude (west negative) and then returns
			//  the local sidereal time in hours. Im using Meeus formula 11.4
			//  instead of messing about with UTo and so on
			//
			double lst;
			double t;
			double d;
			d = mjd - 51544.5;
			t = d / 36525.0;
			lst = range(280.46061837 + 360.98564736629 * d + 0.000387933 * t * t - t * t * t / 38710000.0);
			return lst / 15.0 + glong / 15.0;
		}
		*/

		/*
		private static void minisun(double t, ref double ra, ref double dec)
		{
			//
			//   takes t (julian centuries since J2000.0) and empty variables ra and dec
			//   sets ra and dec to the value of the Sun coordinates at t
			//
			//   positions claimed to be within 1 arc min by Montenbruck and Pfleger
			//
			double p2;
			double coseps;
			double sineps;
			double l;
			double m;
			double DL;
			double SL;
			double x;
			double y;
			double z;
			double rho;
			p2 = 6.283185307;
			coseps = 0.91748;
			sineps = 0.39778;

			m = p2 * frac(0.993133 + 99.997361 * t);
			DL = 6893.0 * System.Math.Sin(m) + 72.0 * System.Math.Sin(2 * m);
			l = p2 * frac(0.7859453 + m / p2 + (6191.2 * t + DL) / 1296000.0);
			SL = System.Math.Sin(l);
			x = System.Math.Cos(l);
			y = coseps * SL;
			z = sineps * SL;
			rho = System.Math.Sqrt(1 - z * z);
			dec = (360.0 / p2) * System.Math.Atan(z / rho);
			ra = (48.0 / p2) * System.Math.Atan(y / (x + rho));
			if (ra < 0)
			{
				ra = ra + 24;
			}
		}
		*/

		/*
		private static void quad(double ym, double yz, double yp, ref int nz, ref double z1, ref double z2, ref double xe, ref double ye)
		{
			//
			//  finds the parabola throuh the three points (-1,ym), (0,yz), (1, yp)
			//  and sets the coordinates of the max/min (if any) xe, ye
			//  the values of x where the parabola crosses zero (z1, z2)
			//  and the nz number of roots (0, 1 or 2) within the interval [-1, 1]
			//
			double a;
			double b;
			double c;
			double dis;
			double dx;

			nz = 0;
			a = 0.5 * (ym + yp) - yz;
			b = 0.5 * (yp - ym);
			c = yz;
			xe = -b / (2 * a);
			ye = (a * xe + b) * xe + c;
			dis = b * b - 4.0 * a * c;
			if (dis > 0)
			{
				dx = 0.5 * System.Math.Sqrt(dis) / System.Math.Abs(a);
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
		}
		*/

		/*
		private static double SinAltSun(double mjd0, double hour, double glong, double cglat, double sglat)
		{
			//
			//  this rather mickey mouse function takes a lot of
			//  arguments and then returns the sine of the altitude of
			//  the object labelled by iobj. iobj = 1 is moon, iobj = 2 is sun
			//
			double mjd;
			double t;
			double ra = 0;
			double dec = 0;
			double tau;
			double salt;
			double rads;
			double alt;
			double refrac;
			double te;
			double step1, step2, step3;
			rads = 0.0174532925;
			mjd = mjd0 + hour / 24.0;
			t = (mjd - 51544.5) / 36525.0;
			minisun(t, ref ra, ref dec);
			// hour angle of object
			tau = 15.0 * (lmst(mjd, glong) - ra);
			// sin(alt) of object using the conversion formulas
			salt = sglat * System.Math.Sin(rads * dec) + cglat * System.Math.Cos(rads * dec) * System.Math.Cos(rads * tau);
			// MC: Add a simplified atmospheric refraction correction
			alt = radToDeg(Math.Asin(salt));
			if (alt > 85.0 || alt <= -0.575)
			{
				refrac = 0;
			}
			else
			{
				te = Math.Tan(degToRad(alt));
				if (alt > 5.0)
				{
					refrac = 58.1 / te - 0.07 / (te * te * te) + 0.000086 / (te * te * te * te * te);
				}
				else if (alt > -0.575)
				{
					step1 = -12.79 + alt * 0.7111;
					step2 = 103.4 + alt * step1;
					step3 = -518.2 + alt * step2;
					refrac = 1735.0 + alt * step3;
				}
				else
				{
					refrac = -20.774 / te;
				}
				refrac /= 3600.0;
			}
			return Math.Sin(degToRad(alt + refrac));
		}
		*/

		//
		//   Worksheet functions below....
		//

		/*
		public static string sunevent(int year, int month, int day, double tz, double glong, double glat, int EventType)
		{
			//
			//   This is the function that does most of the work
			//
			//			double sglong;
			double sglat;
			double cglat;
			double ddate;
			double ym;
			double yz;
			int above;
			double utrise = 0;
			double utset = 0;
			// 			int above;
			utrise = 0;
			// 			double utset;
			// 			int above;
			// 			double utrise;
			// 			double utset;
			double yp;
			int nz;
			int rise;
			int sett;
			int j;
			double hour;
			double z1;
			double z2;
			double rads;
			double xe;
			double ye;
			string AlwaysUp;
			string AlwaysDown;
			string OutString = "";
			string NoEvent;
			// 			string AlwaysDown;
			// 			string OutString;
			// 			string NoEvent;
			double[] sinho = new double[6];
			rads = 0.0174532925;
			AlwaysUp = "****";
			AlwaysDown = "....";
			NoEvent = "----";

			//
			//   Set up the array with the 4 values of sinho needed for the 4
			//   kinds of sun event
			//
			sinho[1] = System.Math.Sin(rads * -0.833); //sunset upper limb simple refraction
			sinho[2] = System.Math.Sin(rads * -6.0); //civil twi
			sinho[3] = System.Math.Sin(rads * -12.0); //nautical twi
			sinho[4] = System.Math.Sin(rads * -18.0); //astro twi
			sglat = System.Math.Sin(rads * glat);
			cglat = System.Math.Cos(rads * glat);
			ddate = mjd(year, month, day) - tz / 24.0;
			//
			//   main loop takes each value of sinho in turn and finds the rise/set
			//   events associated with that altitude of the Sun
			//
			j = System.Math.Abs(EventType);
			nz = 0;
			z1 = 0;
			z2 = 0;
			xe = 0;
			ye = 0;
			rise = 0;
			sett = 0;
			above = 0;
			hour = 1.0;
			ym = SinAltSun(ddate, hour - 1.0, glong, cglat, sglat) - sinho[j];
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
				yz = SinAltSun(ddate, hour, glong, cglat, sglat) - sinho[j];
				yp = SinAltSun(ddate, hour + 1.0, glong, cglat, sglat) - sinho[j];
				quad(ym, yz, yp, ref nz, ref z1, ref z2, ref xe, ref ye);
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
				hour = hour + 2.0;

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
						OutString = hrsmin(utrise);
					}
				}
				else
				{
					if (EventType > 0)
					{
						OutString = NoEvent;
					}
				}
				if (sett == 1)
				{
					if (EventType < 0)
					{
						OutString = hrsmin(utset);
					}
				}
				else
				{
					if (EventType < 0)
					{
						OutString = NoEvent;
					}
				}
			}
			else
			{
				if (above == 1)
				{
					OutString = AlwaysUp;
				}
				else
				{
					OutString = AlwaysDown;
				}
			}
			return OutString;
		}
		*/

		/*
		public static string sunrise(DateTime ddate, double tz, double glong, double glat)
		{
			//
			//   simple way of calling sunevent() using the Excel date format
			//   returns just the sunrise time or NULL if no event
			//   I used the day(), month() and year() functions in excel to allow
			//   portability to the MAC (different date serial numbers)
			//
			string EventTime;
			string sOut;
			sOut = sunevent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, cSunrise);
			if (sOut == "....")
			{
				EventTime = "Always Down";
			}
			else if (sOut == "----")
			{
				EventTime = "No event";
			}
			else if (sOut == "****")
			{
				EventTime = "Always Up";
			}
			else
			{
				EventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
			}
			return EventTime;
		}
		*/

		/*
		public static string sunset(DateTime ddate, double tz, double glong, double glat)
		{
			//
			//   simple way of calling sunevent() using the Excel date format
			//   returns just the sunset time or ****, ...., ---- as appropriate in a string
			//   I used the day(), month() and year() functions in excel to allow
			//   portability to the MAC (different date serial number base)
			//
			string EventTime;
			string sOut;
			sOut = sunevent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, cSunset);
			if (sOut == "....")
			{
				EventTime = "Always Down";
			}
			else if (sOut == "----")
			{
				EventTime = "No event";
			}
			else if (sOut == "****")
			{
				EventTime = "Always Up";
			}
			else
			{
				EventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
			}
			return EventTime;
		}
		*/

		/*
		public static string CivilTwilightStarts(DateTime ddate, double tz, double glong, double glat)
		{
			//
			//   simple way of calling sunevent() using the Excel date format
			//   returns just the start of civil twilight time or ****, ...., ---- as appropriate
			//   I used the day(), month() and year() functions in excel to allow
			//   portability to the MAC (different date serial numbers)
			//
			string EventTime;
			string sOut;
			sOut = sunevent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, cBeginCivilTwilight);
			if (sOut == "....")
			{
				EventTime = "Always Down";
			}
			else if (sOut == "----")
			{
				EventTime = "No event";
			}
			else if (sOut == "****")
			{
				EventTime = "Always Up";
			}
			else
			{
				EventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
			}
			return EventTime;
		}
		*/

		/*
		public static string CivilTwilightEnds(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string EventTime;
			string sOut;
			sOut = sunevent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, cEndCivilTwilight);
			if (sOut == "....")
			{
				EventTime = "Always Down";
			}
			else if (sOut == "----")
			{
				EventTime = "No event";
			}
			else if (sOut == "****")
			{
				EventTime = "Always Up";
			}
			else
			{
				EventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
			}
			return EventTime;
		}
		*/

		/*
		public static string NauticalTwilightStarts(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string EventTime;
			string sOut;
			sOut = sunevent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, cBeginNautTwilight);
			if (sOut == "....")
			{
				EventTime = "Always Down";
			}
			else if (sOut == "----")
			{
				EventTime = "No event";
			}
			else if (sOut == "****")
			{
				EventTime = "Always Up";
			}
			else
			{
				EventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
			}
			return EventTime;
		}
		*/

		/*
		public static string NauticalTwilightEnds(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string EventTime;
			string sOut;
			sOut = sunevent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, cEndNautTwilight);
			if (sOut == "....")
			{
				EventTime = "Always Down";
			}
			else if (sOut == "----")
			{
				EventTime = "No event";
			}
			else if (sOut == "****")
			{
				EventTime = "Always Up";
			}
			else
			{
				EventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
			}
			return EventTime;
		}
		*/

		/*
		public static string AstroTwilightStarts(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string EventTime;
			string sOut;
			sOut = sunevent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, cBeginAstroTwilight);
			if (sOut == "....")
			{
				EventTime = "Always Down";
			}
			else if (sOut == "----")
			{
				EventTime = "No event";
			}
			else if (sOut == "****")
			{
				EventTime = "Always Up";
			}
			else
			{
				EventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
			}
			return EventTime;
		}
		*/

		/*
		public static string AstroTwilightEnds(DateTime ddate, double tz, double glong, double glat)
		{
			//
			string EventTime;
			string sOut;
			sOut = sunevent(ddate.Year, ddate.Month, ddate.Day, tz, glong, glat, cEndAstroTwilight);
			if (sOut == "....")
			{
				EventTime = "Always Down";
			}
			else if (sOut == "----")
			{
				EventTime = "No event";
			}
			else if (sOut == "****")
			{
				EventTime = "Always Up";
			}
			else
			{
				EventTime = sOut.Substring(0, 2) + "h " + sOut.Substring(2, 2) + "m";
			}
			return EventTime;
		}
		*/

		/*
		public static void CalcMoonPos(DateTime dDate, DateTime dEpoch, double fMEpochLong, double fMPeriLong, double fMAscNode, double fMIncl, double fMEcc, double fSEpochEclLong, double fSPeriEclLong, double fSEcc, ref double fMRA, ref double fMDecl)
		{
			double fN, fSM, fSE, fSLambda;
			double fL, fMM, fMN, fME, fAE, fMEC, fA3, fA4, fMV, fMM1, fL1, fL2, fN1, fX, fY;
			double fT, fMLambda, fMBeta, fJD1, fJD2, fDays;

			fJD1 = GetJulianDay(dDate, 0);
			fJD2 = GetJulianDay(dEpoch, 0);
			fDays = (fJD1 - fJD2);
			fDays += 1;

			fN = (360.0 / 365.242191) * fDays;
			fN = Trig.PutIn360Deg(fN);
			fSM = fN + fSEpochEclLong - fSPeriEclLong;
			fSM = Trig.PutIn360Deg(fSM);

			fSE = (360.0 / Math.PI) * fSEcc * Math.Sin(Trig.DegToRad(fSM));
			fSLambda = fN + fSE + fSEpochEclLong;

			fL = (13.176396 * fDays) + fMEpochLong;
			fL = Trig.PutIn360Deg(fL);

			fMM = fL - (0.111404 * fDays) - fMPeriLong;
			fMM = Trig.PutIn360Deg(fMM);

			fMN = fMAscNode - (0.0529539 * fDays);
			fMN = Trig.PutIn360Deg(fMN);

			fME = 1.2739 * Trig.Sin((2.0 * (fL - fSLambda)) - fMM);
			fAE = 0.1858 * Trig.Sin(fSM);
			fA3 = 0.37 * Trig.Sin(fSM);

			fMM1 = fMM + fME - fAE + fA3;

			fMEC = 6.2886 * Trig.Sin(fMM1);
			fA4 = 0.214 * Trig.Sin(2.0 * fMM1);
			fL1 = fL + fME + fMEC - fAE + fA4;

			fMV = 0.6583 * Trig.Sin(2.0 * (fL1 - fSLambda));
			fL2 = fL1 + fMV;

			fN1 = fMN - (0.16 * Trig.Sin(fSM));
			fY = Trig.Sin(fL2 - fN1) * Trig.Cos(fMIncl);
			fX = Trig.Cos(fL2 - fN1);

			fT = Trig.Atan(fY / fX);

			fT = Trig.TanQuadrant(fX, fY, fT);

			fMLambda = fT + fN1;
			fMBeta = Trig.Asin(Trig.Sin(fL2 - fN1) * Trig.Sin(fMIncl));
			ConvEclToEqu(23.441884, fMLambda, fMBeta, ref fMRA, ref fMDecl);
		}
		*/

		/*
		public static void ConvEclToEqu(double fOblique, double fELong, double fELat, ref double fRA, ref double fDecl)
		{
			double fX;
			double fY;
			double fSinDecl;

			fELong = Trig.DegToRad(fELong);
			fELat = Trig.DegToRad(fELat);
			fOblique = Trig.DegToRad(fOblique);
			fSinDecl = (Math.Sin(fELat) * Math.Cos(fOblique)) + (Math.Cos(fELat) * Math.Sin(fOblique) * Math.Sin(fELong));
			fDecl = Math.Asin(fSinDecl);
			fY = (Math.Sin(fELong) * Math.Cos(fOblique)) - (Math.Tan(fELat) * Math.Sin(fOblique));
			fX = Math.Cos(fELong);
			fRA = Math.Atan(fY / fX);
			fRA = Trig.RadToDeg(fRA);
			fDecl = Trig.RadToDeg(fDecl);
			fRA = Trig.TanQuadrant(fX, fY, fRA);
			fRA = fRA / 15.0;
		}
		*/

		/*
		public static void CalcMoonPhase(DateTime dDate, DateTime dEpoch, double fMEpochLong, double fMPeriLong, double fMAscNode, double fMIncl, double fMEcc, double fSEpochEclLong, double fSPeriEclLong, double fSEcc, ref double fMPhase)
		{
			double fN, fSM, fSE, fSLambda;
			double fL, fMM, fMN, fME, fAE, fMEC, fA3, fA4, fMV, fMM1, fL1, fL2;
			double fJD1, fJD2, fDays, fMD;

			fJD1 = GetJulianDay(dDate, 0);
			fJD2 = GetJulianDay(dEpoch, 0);
			fDays = (fJD1 - fJD2);
			fDays += 1;

			fN = (360.0 / 365.242191) * fDays;
			fN = Trig.PutIn360Deg(fN);
			fSM = fN + fSEpochEclLong - fSPeriEclLong;
			fSM = Trig.PutIn360Deg(fSM);

			fSE = (360.0 / Math.PI) * fSEcc * Math.Sin(Trig.DegToRad(fSM));
			fSLambda = fN + fSE + fSEpochEclLong;

			fL = (13.176396 * fDays) + fMEpochLong;
			fL = Trig.PutIn360Deg(fL);

			fMM = fL - (0.111404 * fDays) - fMPeriLong;
			fMM = Trig.PutIn360Deg(fMM);

			fMN = fMAscNode - (0.0529539 * fDays);
			fMN = Trig.PutIn360Deg(fMN);

			fME = 1.2739 * Trig.Sin((2.0 * (fL - fSLambda)) - fMM);
			fAE = 0.1858 * Trig.Sin(fSM);
			fA3 = 0.37 * Trig.Sin(fSM);

			fMM1 = fMM + fME - fAE + fA3;

			fMEC = 6.2886 * Trig.Sin(fMM1);
			fA4 = 0.214 * Trig.Sin(2.0 * fMM1);
			fL1 = fL + fME + fMEC - fAE + fA4;

			fMV = 0.6583 * Trig.Sin(2.0 * (fL1 - fSLambda));
			fL2 = fL1 + fMV;

			fMD = fL2 - fSLambda;
			fMPhase = 0.5 * (1.0 - Trig.Cos(fMD));
		}
		*/

		/*
		public static void CalcMoonDistance(DateTime dDate, DateTime dEpoch, double fMEpochLong, double fMPeriLong, double fMAscNode, double fMIncl, double fMEcc, double fSEpochEclLong, double fSPeriEclLong, double fSEcc, double fMSMA, ref double fMDistance)
		{
			var fJD1 = GetJulianDay(dDate, 0);
			var fJD2 = GetJulianDay(dEpoch, 0);
			var fDays = (fJD1 - fJD2);
			fDays += 1;

			var fN = (360.0 / 365.242191) * fDays;
			fN = Trig.PutIn360Deg(fN);
			var fSM = fN + fSEpochEclLong - fSPeriEclLong;
			fSM = Trig.PutIn360Deg(fSM);

			var fSE = (360.0 / Math.PI) * fSEcc * Math.Sin(Trig.DegToRad(fSM));
			var fSLambda = fN + fSE + fSEpochEclLong;

			var fL = (13.176396 * fDays) + fMEpochLong;
			fL = Trig.PutIn360Deg(fL);

			var fMM = fL - (0.111404 * fDays) - fMPeriLong;
			fMM = Trig.PutIn360Deg(fMM);

			//fMN = fMAscNode - (0.0529539 * fDays);
			//fMN = Trig.PutIn360Deg(fMN);

			var fME = 1.2739 * Trig.Sin((2.0 * (fL - fSLambda)) - fMM);
			var fAE = 0.1858 * Trig.Sin(fSM);
			var fA3 = 0.37 * Trig.Sin(fSM);

			var fMM1 = fMM + fME - fAE + fA3;

			var fMEC = 6.2886 * Trig.Sin(fMM1);

			fMDistance = fMSMA * ((1.0 - (fMEcc * fMEcc)) / (1.0 + (fMEcc * Trig.Cos(fMM1 + fMEC))));
		}
		*/

		/*
		public static void CalcMoonDiam(DateTime dDate, DateTime dEpoch, double fMEpochLong, double fMPeriLong, double fMAscNode, double fMIncl, double fMEcc, double fSEpochEclLong, double fSPeriEclLong, double fSEcc, double fMSMA, double fVAngDiam, ref double fMAngDiam)
		{
			double fRho;

			fRho = 0;
			CalcMoonDistance(dDate, dEpoch, fMEpochLong, fMPeriLong, fMAscNode, fMIncl, fMEcc, fSEpochEclLong, fSPeriEclLong, fSEcc, fMSMA, ref fRho);

			fMAngDiam = fVAngDiam / (fRho / fMSMA);
		}
		*/

		/*
		public static void CalcMoonParallax(DateTime dDate, DateTime dEpoch, double fMEpochLong, double fMPeriLong, double fMAscNode, double fMIncl, double fMEcc, double fSEpochEclLong, double fSPeriEclLong, double fSEcc, double fMSMA, double fVParallax, ref double fMParallax)
		{
			double fRho;

			fRho = 0;
			CalcMoonDistance(dDate, dEpoch, fMEpochLong, fMPeriLong, fMAscNode, fMIncl, fMEcc, fSEpochEclLong, fSPeriEclLong, fSEcc, fMSMA, ref fRho);

			fMParallax = fVParallax / (fRho / fMSMA);
		}
		*/

		/*
		public static double CalcMoonBrightLimb(double fSunRA, double fSunDecl, double fMRA, double fMDecl)
		{
			double fX, fY, fT, fDeltaRA;

			fSunRA = fSunRA * 15;
			fMRA = fMRA * 15;
			fDeltaRA = fSunRA - fMRA;

			fY = Trig.Cos(fSunDecl) * Trig.Sin(fDeltaRA);
			fX = (Trig.Cos(fMDecl) * Trig.Sin(fSunDecl)) - (Trig.Sin(fMDecl) * Trig.Cos(fSunDecl) * Trig.Cos(fDeltaRA));
			fT = Trig.Atan(fY / fX);
			return Trig.TanQuadrant(fX, fY, fT);
		}
		*/

		/*
		public static double CalcMoonAge(DateTime dDate, int iZone)
		{
			double fJD, fIP, fAge;

			fJD = GetJulianDay(dDate, iZone);
			fIP = Normalize((fJD - 2451550.1) / 29.530588853);
			fAge = fIP * 29.530588853;
			return fAge;
		}
		*/

		/*
		public static string GetMoonStage(double fAge)
		{
			string sStage;

			if (fAge < 1.84566)
			{
				sStage = "New";
			}
			else if (fAge < 5.53699)
			{
				sStage = "Waxing Crescent";
			}
			else if (fAge < 9.22831)
			{
				sStage = "First Quarter";
			}
			else if (fAge < 12.91963)
			{
				sStage = "Waxing Gibbous";
			}
			else if (fAge < 16.61096)
			{
				sStage = "Full";
			}
			else if (fAge < 20.30228)
			{
				sStage = "Waning Gibbous";
			}
			else if (fAge < 23.9931)
			{
				sStage = "Last Quarter";
			}
			else if (fAge < 27.68493)
			{
				sStage = "Waning Crescent";
			}
			else
			{
				sStage = "New";
			}

			return sStage;
		}
		*/

		/*
		private static double Normalize(double fN)
		{
			fN -= Math.Floor(fN);
			if (fN < 0)
			{
				fN += 1;
			}
			return fN;
		}
		*/
	}
}
