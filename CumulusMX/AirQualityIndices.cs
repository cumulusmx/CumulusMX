using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Web.ModelBinding;

namespace CumulusMX
{
	internal class AirQualityIndices
	{
		/*
		 * US AQI - United States Environmental Protection Agency (EPA)
		 * https://www.airnow.gov/sites/default/files/2018-05/aqi-technical-assistance-document-may2016.pdf
		 */

		public static double US_EPApm2p5(double pmVal)
		{
			//double Clow, Chigh, Ilow, Ihigh;

			if (pmVal > 500.4)
				return 500;

			if (pmVal >= 350.5)
			{
				//Clow = 350.5;
				//Chigh = 500.4;
				//Ilow = 401;
				//Ihigh = 500;
				return 401 + interpolate(350.5, 500.4, pmVal) * 99;
			}
			else if (pmVal >= 250.5)
			{
				//Clow = 250.5;
				//Chigh = 350.4;
				//Ilow = 301;
				//Ihigh = 400;
				return 301 + interpolate(250.5, 350.4, pmVal) * 99;
			}
			else if (pmVal >= 150.5)
			{
				//Clow = 150.5;
				//Chigh = 250.4;
				//Ilow = 201;
				//Ihigh = 300;
				return 201 + interpolate(150.5, 250.4, pmVal) * 99;
			}
			else if (pmVal >= 55.5)
			{
				//Clow = 55.5;
				//Chigh = 150.4;
				//Ilow = 151;
				//Ihigh = 200;
				return 151 + interpolate(55.5, 150.4, pmVal) * 49;
			}
			else if (pmVal >= 35.5)
			{
				//Clow = 35.5;
				//Chigh = 55.4;
				//Ilow = 101;
				//Ihigh = 150;
				return 101 + interpolate(35.5, 55.4, pmVal) * 49;
			}
			else if (pmVal >= 12.1)
			{
				//Clow = 12.1;
				//Chigh = 35.4;
				//Ilow = 51;
				//Ihigh = 100;
				return 51 + interpolate(12.1, 35.4, pmVal) * 49;
			}
			else
			{
				//Clow = 0;
				//Chigh = 12;
				//Ilow = 0;
				//Ihigh = 50;
				return interpolate(0, 12, pmVal) * 50;
			}
			//return (Ihigh - Ilow) / (Chigh - Clow) * (pmVal - Clow) + Ilow;
		}

		/*
		 * US AQI - United States Environmental Protection Agency (EPA)
		 * https://www.airnow.gov/sites/default/files/2018-05/aqi-technical-assistance-document-may2016.pdf
		 */

		public static double US_EPApm10(double pmVal)
		{
			//int Clow, Chigh, Ilow, Ihigh;

			if (pmVal > 604)
				return 500;

			if (pmVal >= 505)
			{
				//Clow = 505;
				//Chigh = 604;
				//Ilow = 401;
				//Ihigh = 500;
				return 401 + interpolate(505, 604, pmVal) * 99;
			}
			else if (pmVal >= 425)
			{
				//Clow = 425;
				//Chigh = 504;
				//Ilow = 301;
				//Ihigh = 400;
				return 301 + interpolate(425, 504, pmVal) * 99;
			}
			else if (pmVal >= 355)
			{
				//Clow = 355;
				//Chigh = 424;
				//Ilow = 201;
				//Ihigh = 300;
				return 201 + interpolate(355, 424, pmVal) * 99;
			}
			else if (pmVal >= 255)
			{
				//Clow = 255;
				//Chigh = 354;
				//Ilow = 151;
				//Ihigh = 200;
				return 151 + interpolate(255, 3544, pmVal) * 49;
			}
			else if (pmVal >= 155)
			{
				//Clow = 155;
				//Chigh = 254;
				//Ilow = 101;
				//Ihigh = 150;
				return 101 + interpolate(155, 254, pmVal) * 49;
			}
			else if (pmVal >= 55)
			{
				//Clow = 55;
				//Chigh = 154;
				//Ilow = 51;
				//Ihigh = 100;
				return 51 + interpolate(55, 154, pmVal) * 99;
			}
			else
			{
				//Clow = 0;
				//Chigh = 54;
				//Ilow = 0;
				//Ihigh = 50;
				return interpolate(0, 54, pmVal) * 50;
			}
			//return (Ihigh - Ilow) / (Chigh - Clow) * (pmVal - Clow) + Ilow;
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
				return 9 + interpolate(65, 71, pmVal);
			else if (pmVal >= 59)
				return 8 + interpolate(59, 65, pmVal);
			else if (pmVal >= 54)
				return 7 + interpolate(54, 59, pmVal);
			else if (pmVal >= 47)
				return 6 + interpolate(47, 54, pmVal);
			else if (pmVal >= 42)
				return 5 + interpolate(42, 47, pmVal);
			else if (pmVal >= 36)
				return 4 + interpolate(36, 42, pmVal);
			else if (pmVal >= 24)
				return 3 + interpolate(24, 36, pmVal);
			else if (pmVal >= 12)
				return 2 + interpolate(12, 24, pmVal);
			else
				return 1 + interpolate(0, 12, pmVal);
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
				return 9 + interpolate(92, 101, pmVal);
			else if (pmVal >= 84)
				return 8 + interpolate(84, 92, pmVal);
			else if (pmVal >= 76)
				return 7 + interpolate(76, 84, pmVal);
			else if (pmVal >= 67)
				return 6 + interpolate(67, 76, pmVal);
			else if (pmVal >= 59)
				return 5 + interpolate(59, 67, pmVal);
			else if (pmVal >= 51)
				return 4 + interpolate(51, 59, pmVal);
			else if (pmVal >= 34)
				return 3 + interpolate(34, 51, pmVal);
			else if (pmVal >= 17)
				return 2 + interpolate(17, 34, pmVal);
			else
				return 1 + interpolate(0, 17, pmVal);
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
				return 4 + interpolate(55, 110, pmVal);
			else if (pmVal >= 30)	// Medium
				return 3 + interpolate(30, 55, pmVal);
			else if (pmVal >= 15)	// Low
				return 2 + interpolate(15, 30, pmVal);
			else					// Very Low
				return 1 + interpolate(0, 15, pmVal);
		}

		/*
		* EU AQI - PM2.5 (24 hr avg) - Very Low = 1, Very High = 5
		* http://www.airqualitynow.eu/about_indices_definition.php
		*/
		public static double euAqi2p5h24(double pmVal)
		{
			if (pmVal > 60)			// Very High
				return 5;
			else if (pmVal >= 30)	// High
				return 4 + interpolate(30, 60, pmVal);
			else if (pmVal >= 20)	// Medium
				return 3 + interpolate(20, 30, pmVal);
			else if (pmVal >= 10)	// Low
				return 2 + interpolate(10, 20, pmVal);
			else					// Very Low
				return 1 + interpolate(0, 10, pmVal);
		}

		/*
		 * EU AQI - PM2.5 (1 hr avg) - Very Low = 1, Very High = 5
		 * http://www.airqualitynow.eu/about_indices_definition.php
		 */
		public static double euAqi10h1(double pmVal)
		{
			if (pmVal > 180)		// Very High
				return 5;
			else if (pmVal >= 90)	// High
				return 4 + interpolate(90, 180, pmVal);
			else if (pmVal >= 50)	// Medium
				return 3 + interpolate(50, 90, pmVal);
			else if (pmVal >= 25)	// Low
				return 2 + interpolate(25, 50, pmVal);
			else					// Very Low
				return 1 + interpolate(0, 25, pmVal);
		}

		/*
		* EU AQI - PM2.5 (24 hr avg) - Very Low = 1, Very High = 5
		* http://www.airqualitynow.eu/about_indices_definition.php
		*/
		public static double euAqi10h24(double pmVal)
		{
			if (pmVal > 100)		// Very High
				return 5;
			else if (pmVal >= 50)	// High
				return 4 + interpolate(50, 100, pmVal);
			else if (pmVal >= 30)	// Medium
				return 3 + interpolate(30, 50, pmVal);
			else if (pmVal >= 15)	// Low
				return 2 + interpolate(15, 30, pmVal);
			else					// Very Low
				return 1 + interpolate(0, 15, pmVal);
		}


		/*
		 * Canada AQHI - only valid for PM2.5 and 3 hour data
		 * https://en.wikipedia.org/wiki/Air_Quality_Health_Index_(Canada)
		 */
		public static int canadaAqhi(double pmVal)
		{
			return (int)((1000 / 10.4) + Math.Exp(0.000487 * pmVal) - 1);
		}


		/*
		 * EU Common Air Quality Index - CAQI - 0-100 scale
		 * https://www.airqualitynow.eu/download/CITEAIR-Comparing_Urban_Air_Quality_across_Borders.pdf
		 * PM10
		 * Use 101 to indicate >100
		 */
		public static double euCaqi10h1(double pmVal)
		{
			if (pmVal > 180)
				return 101;
			else if (pmVal >= 90) // AQI 75
				return 75 + interpolate(90, 180, pmVal) * 25;
			else if (pmVal >= 50) // AQI 50
				return 50 + interpolate(50, 75, pmVal) * 25;
			else if (pmVal >= 25) // AQI 25
				return 25 + interpolate(25, 75, pmVal) * 25;
			else
				return 1 + interpolate(0, 25, pmVal) * 24;
		}
		/*
		 * EU Common Air Quality Index - CAQI - 0-100 scale
		 * https://www.airqualitynow.eu/download/CITEAIR-Comparing_Urban_Air_Quality_across_Borders.pdf
		 * PM10
		 * Use 101 to indicate >100
		 */
		public static double euCaqi10h24(double pmVal)
		{
			if (pmVal > 100)
				return 101;
			else if (pmVal >= 50) // AQI 75
				return 75 + interpolate(50, 100, pmVal) * 25;
			else if (pmVal >= 30) // AQI 50
				return 50 + interpolate(30, 50, pmVal) * 25;
			else if (pmVal >= 15) // AQI 25
				return 25 + interpolate(15, 30, pmVal) * 25;
			else
				return 1 + interpolate(0, 15, pmVal) * 24;
		}
		/*
		 * EU Common Air Quality Index - CAQI - 0-100 scale
		 * https://www.airqualitynow.eu/download/CITEAIR-Comparing_Urban_Air_Quality_across_Borders.pdf
		 * PM2.5
		 * Use 101 to indicate >100
		 */
		public static double euCaqi2p5h1(double pmVal)
		{
			if (pmVal > 110)
				return 101;
			else if (pmVal >= 55) // AQI 75
				return 75 + interpolate(55, 110, pmVal) * 25;
			else if (pmVal >= 30) // AQI 50
				return 50 + interpolate(30, 55, pmVal) * 25;
			else if (pmVal >= 15) // AQI 25
				return 15 + interpolate(15, 30, pmVal) * 25;
			else
				return 1 + interpolate(0, 15, pmVal) * 24;
		}
		/*
		 * EU Common Air Quality Index - CAQI - 0-100 scale
		 * https://www.airqualitynow.eu/download/CITEAIR-Comparing_Urban_Air_Quality_across_Borders.pdf
		 * PM2.5
		 * Use 101 to indicate >100
		 */
		public static double euCaqi2p5h24(double pmVal)
		{
			if (pmVal > 60)
				return 101;
			else if (pmVal >= 30) // AQI 75
				return 75 + interpolate(30, 60, pmVal) * 25;
			else if (pmVal >= 20) // AQI 50
				return 50 + interpolate(20, 30, pmVal) * 25;
			else if (pmVal >= 10) // AQI 25
				return 25 + interpolate(10, 20, pmVal) * 25;
			else
				return 1 + interpolate(0, 10, pmVal) * 24;
		}


		/*
		 * Australia National Environment Pollution Measure - NEPM
		 * https://www.environment.nsw.gov.au/topics/air/understanding-air-quality-data/air-quality-index
		 * PM2.5 - standard is 24hr avg
		 */
		public static double australiaNepm2p5(double pmVal)
		{
			if (pmVal > 25) return 101;
			return pmVal * 4;
		}
		/*
		 * Australia National Environment Pollution Measure - NEPM
		 * https://www.environment.nsw.gov.au/topics/air/understanding-air-quality-data/air-quality-index
		 * PM10 - standard is 24hr avg
		 */
		public static double australiaNepm10(double pmVal)
		{
			if (pmVal > 50) return 101;
			return pmVal * 2;
		}

		// Returns the fraction of val between min and max
		static double interpolate(double min, double max, double val)
		{
			if (val < min) return 0;
			if (val > max) return 1;
			return (val - min) / (max - min);
		}
	}
}
