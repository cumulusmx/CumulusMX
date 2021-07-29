using System;

namespace CumulusMX
{
	internal class AirQualityIndices
	{
		/*
		 * US AQI - United States Environmental Protection Agency (EPA)
		 * https://www.airnow.gov/sites/default/files/2018-05/aqi-technical-assistance-document-may2016.pdf
		 */

		public static int US_EPApm2p5(double pmVal)
		{
			double retVal;

			if (pmVal >= 500.4)
				return 500;

			if (pmVal >= 350.4)
			{
				//Clow = 350.5;
				//Chigh = 500.4;
				//Ilow = 401;
				//Ihigh = 500;
				retVal = 400 + Interpolate(350.4, 500.4, pmVal) * 100;
			}
			else if (pmVal >= 250.4)
			{
				//Clow = 250.5;
				//Chigh = 350.4;
				//Ilow = 301;
				//Ihigh = 400;
				retVal = 300 + Interpolate(250.4, 350.4, pmVal) * 100;
			}
			else if (pmVal >= 150.4)
			{
				//Clow = 150.5;
				//Chigh = 250.4;
				//Ilow = 201;
				//Ihigh = 300;
				retVal = 200 + Interpolate(150.4, 250.4, pmVal) * 100;
			}
			else if (pmVal >= 55.4)
			{
				//Clow = 55.5;
				//Chigh = 150.4;
				//Ilow = 151;
				//Ihigh = 200;
				retVal = 150 + Interpolate(55.4, 150.4, pmVal) * 50;
			}
			else if (pmVal >= 35.4)
			{
				//Clow = 35.5;
				//Chigh = 55.4;
				//Ilow = 101;
				//Ihigh = 150;
				retVal = 100 + Interpolate(35.4, 55.4, pmVal) * 50;
			}
			else if (pmVal >= 12)
			{
				//Clow = 12.1;
				//Chigh = 35.4;
				//Ilow = 51;
				//Ihigh = 100;
				retVal = 50 + Interpolate(12, 35.4, pmVal) * 50;
			}
			else
			{
				//Clow = 0;
				//Chigh = 12;
				//Ilow = 0;
				//Ihigh = 50;
				retVal = Interpolate(0, 12, pmVal) * 50;
			}
			//return (Ihigh - Ilow) / (Chigh - Clow) * (pmVal - Clow) + Ilow;
			return (int)Math.Round(retVal);
		}

		/*
		 * US AQI - United States Environmental Protection Agency (EPA)
		 * https://www.airnow.gov/sites/default/files/2018-05/aqi-technical-assistance-document-may2016.pdf
		 */

		public static int US_EPApm10(double pmVal)
		{
			double retVal;

			if (pmVal >= 604)
				return 500;

			if (pmVal >= 504)
			{
				//Clow = 505;
				//Chigh = 604;
				//Ilow = 401;
				//Ihigh = 500;
				retVal = 400 + Interpolate(504, 604, pmVal) * 100;
			}
			else if (pmVal >= 424)
			{
				//Clow = 425;
				//Chigh = 504;
				//Ilow = 301;
				//Ihigh = 400;
				retVal = 300 + Interpolate(424, 504, pmVal) * 100;
			}
			else if (pmVal >= 354)
			{
				//Clow = 355;
				//Chigh = 424;
				//Ilow = 201;
				//Ihigh = 300;
				retVal = 200 + Interpolate(354, 424, pmVal) * 100;
			}
			else if (pmVal >= 254)
			{
				//Clow = 255;
				//Chigh = 354;
				//Ilow = 151;
				//Ihigh = 200;
				retVal = 150 + Interpolate(254, 354, pmVal) * 50;
			}
			else if (pmVal >= 154)
			{
				//Clow = 155;
				//Chigh = 254;
				//Ilow = 101;
				//Ihigh = 150;
				retVal = 100 + Interpolate(154, 254, pmVal) * 50;
			}
			else if (pmVal >= 54)
			{
				//Clow = 55;
				//Chigh = 154;
				//Ilow = 51;
				//Ihigh = 100;
				retVal = 50 + Interpolate(54, 154, pmVal) * 50;
			}
			else
			{
				//Clow = 0;
				//Chigh = 54;
				//Ilow = 0;
				//Ihigh = 50;
				retVal = Interpolate(0, 54, pmVal) * 50;
			}
			//return (Ihigh - Ilow) / (Chigh - Clow) * (pmVal - Clow) + Ilow;
			return (int)Math.Round(retVal);
		}


		/*
		 * UK Air Quality Index - Committee on the Medical Effects of Air Pollutants (COMEAP)
		 * https://assets.publishing.service.gov.uk/government/uploads/system/uploads/attachment_data/file/304633/COMEAP_review_of_the_uk_air_quality_index.pdf
		 * Only integer values are defined, but we will interpolate between them
		 */
		public static double UK_COMEAPpm2p5(double pmVal)
		{
			if (pmVal >= 71)
				return 10;
			else if (pmVal >= 65)
				return 9 + Interpolate(65, 71, pmVal);
			else if (pmVal >= 59)
				return 8 + Interpolate(59, 65, pmVal);
			else if (pmVal >= 54)
				return 7 + Interpolate(54, 59, pmVal);
			else if (pmVal >= 48)
				return 6 + Interpolate(48, 54, pmVal);
			else if (pmVal >= 42)
				return 5 + Interpolate(42, 48, pmVal);
			else if (pmVal >= 36)
				return 4 + Interpolate(36, 42, pmVal);
			else if (pmVal >= 24)
				return 3 + Interpolate(24, 36, pmVal);
			else if (pmVal >= 12)
				return 2 + Interpolate(12, 24, pmVal);
			else
				return 1 + Interpolate(0, 12, pmVal);
		}

		/*
		 * UK Air Quality Index - Committee on the Medical Effects of Air Pollutants (COMEAP)
		 * https://assets.publishing.service.gov.uk/government/uploads/system/uploads/attachment_data/file/304633/COMEAP_review_of_the_uk_air_quality_index.pdf
		 * Only integer values are defined, but we will interpolate between them
		 */
		public static double UK_COMEAPpm10(double pmVal)
		{
			if (pmVal >= 101)
				return 10;
			else if (pmVal >= 92)
				return 9 + Interpolate(92, 101, pmVal);
			else if (pmVal >= 84)
				return 8 + Interpolate(84, 92, pmVal);
			else if (pmVal >= 76)
				return 7 + Interpolate(76, 84, pmVal);
			else if (pmVal >= 67)
				return 6 + Interpolate(67, 76, pmVal);
			else if (pmVal >= 59)
				return 5 + Interpolate(59, 67, pmVal);
			else if (pmVal >= 51)
				return 4 + Interpolate(51, 59, pmVal);
			else if (pmVal >= 34)
				return 3 + Interpolate(34, 51, pmVal);
			else if (pmVal >= 17)
				return 2 + Interpolate(17, 34, pmVal);
			else
				return 1 + Interpolate(0, 17, pmVal);
		}


		/*
		 * EU AQI - PM2.5 (1 hr avg) - Very Low = 1, Very High = 5
		 * http://www.airqualitynow.eu/about_indices_definition.php
		 * Only integer values are defined, but we will interpolate between them
		 */
		public static double EU_AQIpm2p5h1(double pmVal)
		{
			if (pmVal > 110)		// Very High
				return 5;
			else if (pmVal >= 55)	// High
				return 4 + Interpolate(55, 110, pmVal);
			else if (pmVal >= 30)	// Medium
				return 3 + Interpolate(30, 55, pmVal);
			else if (pmVal >= 15)	// Low
				return 2 + Interpolate(15, 30, pmVal);
			else					// Very Low
				return 1 + Interpolate(0, 15, pmVal);
		}

		/*
		* EU AQI - PM2.5 (24 hr avg) - Very Low = 1, Very High = 5
		* http://www.airqualitynow.eu/about_indices_definition.php
		*/
		public static double EU_AQI2p5h24(double pmVal)
		{
			if (pmVal > 60)			// Very High
				return 5;
			else if (pmVal >= 30)	// High
				return 4 + Interpolate(30, 60, pmVal);
			else if (pmVal >= 20)	// Medium
				return 3 + Interpolate(20, 30, pmVal);
			else if (pmVal >= 10)	// Low
				return 2 + Interpolate(10, 20, pmVal);
			else					// Very Low
				return 1 + Interpolate(0, 10, pmVal);
		}

		/*
		 * EU AQI - PM2.5 (1 hr avg) - Very Low = 1, Very High = 5
		 * http://www.airqualitynow.eu/about_indices_definition.php
		 */
		public static double EU_AQI10h1(double pmVal)
		{
			if (pmVal > 180)		// Very High
				return 5;
			else if (pmVal >= 90)	// High
				return 4 + Interpolate(90, 180, pmVal);
			else if (pmVal >= 50)	// Medium
				return 3 + Interpolate(50, 90, pmVal);
			else if (pmVal >= 25)	// Low
				return 2 + Interpolate(25, 50, pmVal);
			else					// Very Low
				return 1 + Interpolate(0, 25, pmVal);
		}

		/*
		* EU AQI - PM2.5 (24 hr avg) - Very Low = 1, Very High = 5
		* http://www.airqualitynow.eu/about_indices_definition.php
		*/
		public static double EU_AQI10h24(double pmVal)
		{
			if (pmVal > 100)		// Very High
				return 5;
			else if (pmVal >= 50)	// High
				return 4 + Interpolate(50, 100, pmVal);
			else if (pmVal >= 30)	// Medium
				return 3 + Interpolate(30, 50, pmVal);
			else if (pmVal >= 15)	// Low
				return 2 + Interpolate(15, 30, pmVal);
			else					// Very Low
				return 1 + Interpolate(0, 15, pmVal);
		}


		/*
		 * Canada AQHI - only valid for PM2.5 and 3 hour data
		 * https://en.wikipedia.org/wiki/Air_Quality_Health_Index_(Canada)
		 */
		public static int CA_AQHI(double pmVal)
		{
			var aqi = (int)(1000 / 10.4 * (Math.Exp(0.000487 * pmVal) - 1));
			return aqi < 1 ? 1 : aqi;
		}


		/*
		 * EU Common Air Quality Index - CAQI - 0-100 scale
		 * https://www.airqualitynow.eu/download/CITEAIR-Comparing_Urban_Air_Quality_across_Borders.pdf
		 * PM10
		 * Use 101 to indicate >100
		 * Uses the Urban background scale
		 */
		public static double EU_CAQI10h1(double pmVal)
		{
			if (pmVal > 180)
				return 101;
			else if (pmVal >= 90) // AQI 75-100
				return 75 + Interpolate(90, 180, pmVal) * 25;
			else if (pmVal >= 50) // AQI 50-75
				return 50 + Interpolate(50, 90, pmVal) * 25;
			else if (pmVal >= 26) // AQI 25-50
				return 25 + Interpolate(26, 50, pmVal) * 25;
			else                  // AQI 1-25
				return 1 + Interpolate(0, 26, pmVal) * 24;
		}
		/*
		 * EU Common Air Quality Index - CAQI - 0-100 scale
		 * https://www.airqualitynow.eu/download/CITEAIR-Comparing_Urban_Air_Quality_across_Borders.pdf
		 * PM10
		 * Use 101 to indicate >100
		 * Uses the Urban background scale
		 */
		public static double EU_CAQI10h24(double pmVal)
		{
			if (pmVal > 100)
				return 101;
			else if (pmVal >= 50) // AQI 75-100
				return 75 + Interpolate(50, 100, pmVal) * 25;
			else if (pmVal >= 30) // AQI 50-75
				return 50 + Interpolate(30, 50, pmVal) * 25;
			else if (pmVal >= 15) // AQI 25-50
				return 25 + Interpolate(15, 30, pmVal) * 25;
			else                  // AQI 1-25
				return 1 + Interpolate(0, 15, pmVal) * 24;
		}
		/*
		 * EU Common Air Quality Index - CAQI - 0-100 scale
		 * https://www.airqualitynow.eu/download/CITEAIR-Comparing_Urban_Air_Quality_across_Borders.pdf
		 * PM2.5
		 * Use 101 to indicate >100
		 * Uses the Urban background scale
		 */
		public static double EU_CAQI2p5h1(double pmVal)
		{
			if (pmVal > 110)
				return 101;
			else if (pmVal >= 55) // AQI 75-100
				return 75 + Interpolate(55, 110, pmVal) * 25;
			else if (pmVal >= 30) // AQI 50-75
				return 50 + Interpolate(30, 55, pmVal) * 25;
			else if (pmVal >= 15) // AQI 25-50
				return 25 + Interpolate(15, 30, pmVal) * 25;
			else                  // AQI 1-25
				return 1 + Interpolate(0, 15, pmVal) * 24;
		}
		/*
		 * EU Common Air Quality Index - CAQI - 0-100 scale
		 * https://www.airqualitynow.eu/download/CITEAIR-Comparing_Urban_Air_Quality_across_Borders.pdf
		 * PM2.5
		 * Use 101 to indicate >100
		 * Uses the Urban background scale
		 */
		public static double EU_CAQI2p5h24(double pmVal)
		{
			if (pmVal > 60)
				return 101;
			else if (pmVal >= 30) // AQI 75-100
				return 75 + Interpolate(30, 60, pmVal) * 25;
			else if (pmVal >= 20) // AQI 50-75
				return 50 + Interpolate(20, 30, pmVal) * 25;
			else if (pmVal >= 10) // AQI 25-50
				return 25 + Interpolate(10, 20, pmVal) * 25;
			else                  // AQI 1-25
				return 1 + Interpolate(0, 10, pmVal) * 24;
		}


		/*
		 * Australia National Environment Pollution Measure - NEPM
		 * https://www.environment.nsw.gov.au/topics/air/understanding-air-quality-data/air-quality-categories/history-of-air-quality-reporting/about-the-air-quality-index
		 * PM2.5 - standard is 24hr avg
		 * AQI = pm / 25 * 100
		 */
		public static double AU_NEpm2p5(double pmVal)
		{
			return pmVal * 4;
		}
		/*
		 * Australia National Environment Pollution Measure - NEPM
		 * https://www.environment.nsw.gov.au/topics/air/understanding-air-quality-data/air-quality-categories/history-of-air-quality-reporting/about-the-air-quality-index
		 * PM10 - standard is 24hr avg
		 * AQI = pm / 50 * 100
		 */
		public static double AU_NEpm10(double pmVal)
		{
			return pmVal * 2;
		}


		/*
		 * Netherlands AQI (turn it up to 11!) - 1-11
		 * https://www.luchtmeetnet.nl/informatie/luchtkwaliteit/luchtkwaliteitsindex-(lki)
		 */
		public static double NL_LKIpm2p5(double pmVal)
		{
			if (pmVal > 140)
				return 11;
			else if (pmVal >= 100) // AQI 10
				return 10 + Interpolate(100, 140, pmVal);
			else if (pmVal >= 90) // AQI 9
				return 9 + Interpolate(90, 100, pmVal);
			else if (pmVal >= 70) // AQI 8
				return 8 + Interpolate(70, 90, pmVal);
			else if (pmVal >= 50) // AQI 7
				return 7 + Interpolate(50, 70, pmVal);
			else if (pmVal >= 40) // AQI 6
				return 6 + Interpolate(40, 50, pmVal);
			else if (pmVal >= 30) // AQI 5
				return 5 + Interpolate(30, 40, pmVal);
			else if (pmVal >= 20) // AQI 4
				return 4 + Interpolate(20, 30, pmVal);
			else if (pmVal >= 15) // AQI 3
				return 3 + Interpolate(15, 20, pmVal);
			else if (pmVal >= 10) // AQI 2
				return 2 + Interpolate(10, 15, pmVal);
			else // AQI 1
				return 1 + Interpolate(0, 10, pmVal);
		}

		/*
		 * Netherlands AQI (turn it up to 11!) - 1-11
		 * https://www.luchtmeetnet.nl/informatie/luchtkwaliteit/luchtkwaliteitsindex-(lki)
		 */
		public static double NL_LKIpm10(double pmVal)
		{
			if (pmVal > 200)
				return 11;
			else if (pmVal >= 150) // AQI 10
				return 10 + Interpolate(150, 200, pmVal);
			else if (pmVal >= 125) // AQI 9
				return 9 + Interpolate(125, 150, pmVal);
			else if (pmVal >= 100) // AQI 8
				return 8 + Interpolate(100, 125, pmVal);
			else if (pmVal >= 75) // AQI 7
				return 7 + Interpolate(75, 100, pmVal);
			else if (pmVal >= 60) // AQI 6
				return 6 + Interpolate(60, 75, pmVal);
			else if (pmVal >= 45) // AQI 5
				return 5 + Interpolate(45, 60, pmVal);
			else if (pmVal >= 30) // AQI 4
				return 4 + Interpolate(30, 45, pmVal);
			else if (pmVal >= 20) // AQI 3
				return 3 + Interpolate(20, 30, pmVal);
			else if (pmVal >= 10) // AQI 2
				return 2 + Interpolate(10, 20, pmVal);
			else // AQI 1
				return 1 + Interpolate(0, 10, pmVal);
		}


		/*
		 * Belgian AQI - 1-10
		 * https://www.irceline.be/en/air-quality/measurements/belaqi-air-quality-index/information?set_language=en
		 */
		public static double BE_BelAQIpm2p5(double pmVal)
		{
			if (pmVal > 70)
				return 10;
			else if (pmVal >= 60) // AQI 9
				return 9 + Interpolate(60, 70, pmVal);
			else if (pmVal >= 50) // AQI 8
				return 8 + Interpolate(50, 60, pmVal);
			else if (pmVal >= 40) // AQI 7
				return 7 + Interpolate(40, 50, pmVal);
			else if (pmVal >= 35) // AQI 6
				return 6 + Interpolate(35, 40, pmVal);
			else if (pmVal >= 25) // AQI 5
				return 5 + Interpolate(25, 35, pmVal);
			else if (pmVal >= 15) // AQI 4
				return 4 + Interpolate(15, 25, pmVal);
			else if (pmVal >= 10) // AQI 3
				return 3 + Interpolate(10, 15, pmVal);
			else if (pmVal >= 5) // AQI 2
				return 2 + Interpolate(5, 10, pmVal);
			else // AQI 1
				return 1 + Interpolate(0, 5, pmVal);
		}

		/*
		 * Belgian AQI - 1-10
		 * https://www.irceline.be/en/air-quality/measurements/belaqi-air-quality-index/information?set_language=en
		 */
		public static double BE_BelAQIpm10(double pmVal)
		{
			if (pmVal > 100)
				return 10;
			else if (pmVal >= 80) // AQI 9
				return 9 + Interpolate(80, 100, pmVal);
			else if (pmVal >= 70) // AQI 8
				return 8 + Interpolate(70, 80, pmVal);
			else if (pmVal >= 60) // AQI 7
				return 7 + Interpolate(60, 70, pmVal);
			else if (pmVal >= 50) // AQI 6
				return 6 + Interpolate(50, 60, pmVal);
			else if (pmVal >= 40) // AQI 5
				return 5 + Interpolate(40, 50, pmVal);
			else if (pmVal >= 30) // AQI 4
				return 4 + Interpolate(30, 40, pmVal);
			else if (pmVal >= 20) // AQI 3
				return 3 + Interpolate(20, 30, pmVal);
			else if (pmVal >= 10) // AQI 2
				return 2 + Interpolate(10, 20, pmVal);
			else // AQI 1
				return 1 + Interpolate(0, 10, pmVal);
		}

		// Returns the fraction of val between min and max
		private static double Interpolate(double min, double max, double val)
		{
			if (val < min) return 0;
			if (val > max) return 1;
			return (val - min) / (max - min);
		}
	}
}
