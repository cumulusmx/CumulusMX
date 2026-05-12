using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void CreateWxnowFile()
		{
			// Jun 01 2003 08:07
			// 272/000g006t069r010p030P020h61b10150CommentString

			// 272 - wind direction - 272 degrees
			// 010 - wind speed - 10 mph

			// g015 - wind gust - 15 mph
			// t069 - temperature - 69 degrees F
			// r010 - rain in last hour in hundredths of an inch - 0.1 inches
			// p030 - rain in last 24 hours in hundredths of an inch - 0.3 inches
			// P020 - rain since midnight in hundredths of an inch - 0.2 inches
			// h61 - humidity 61% (00 = 100%)
			// b10153 - barometric pressure in tenths of a millibar - 1015.3 millibars
			// CommentString - free format information text

			var filename = Path.Combine(Directory.GetCurrentDirectory(), Cumulus.WxnowFile);

			var data = CreateWxnowFileString();
			using var file = new StreamWriter(filename, false);
			file.WriteLine(data);
			file.Close();
		}

		public string CreateWxnowFileString()
		{
			// Jun 01 2003 08:07
			// 272/000g006t069r010p030P020h61b10150CommentString

			// 272 - wind direction - 272 degrees
			// 010 - wind speed - 10 mph

			// g015 - wind gust - 15 mph
			// t069 - temperature - 69 degrees F
			// r010 - rain in last hour in hundredths of an inch - 0.1 inches
			// p030 - rain in last 24 hours in hundredths of an inch - 0.3 inches
			// P020 - rain since midnight in hundredths of an inch - 0.2 inches
			// h61 - humidity 61% (00 = 100%)
			// b10153 - barometric pressure in tenths of a millibar - 1015.3 millibars
			// CommentString - free format information text

			var timestamp = cumulus.APRS.UseUtcInWxNowFile ? DateTime.UtcNow.ToUniversalTime().ToString(@"MMM dd yyyy HH\:mm") : DateTime.Now.ToString(@"MMM dd yyyy HH\:mm");

			var mphwind = Convert.ToInt32(ConvertUnits.UserWindToMPH(MetData.WindAverage));
			var mphgust = Convert.ToInt32(ConvertUnits.UserWindToMPH(MetData.RecentMaxGust));
			var ftempstr = APRStemp(MetData.Temperature);
			var in100rainlasthour = Convert.ToInt32(ConvertUnits.UserRainToIN(MetData.RainLastHour) * 100);
			var in100rainlast24hours = Convert.ToInt32(ConvertUnits.UserRainToIN(RainLast24Hour) * 100);
			int in100raintoday;
			// use today's rain for safety
			// 0900 day, use midnight calculation
			in100raintoday = Convert.ToInt32(ConvertUnits.UserRainToIN(cumulus.RolloverHour == 0 ? MetData.RainToday : MetData.RainSinceMidnight) * 100);
			var mb10press = Convert.ToInt32(ConvertUnits.UserPressToMB(MetData.AltimeterPressure) * 10);
			// For 100% humidity, send zero. For zero humidity, send 1
			int hum;
			if (MetData.Humidity == 0)
				hum = 1;
			else if (MetData.Humidity == 100)
				hum = 0;
			else
				hum = MetData.Humidity;

			var data = string.Format("{0}\n{1:000}/{2:000}g{3:000}t{4}r{5:000}p{6:000}P{7:000}h{8:00}b{9:00000}", timestamp, MetData.WindAvgBearing, mphwind, mphgust, ftempstr, in100rainlasthour,
				in100rainlast24hours, in100raintoday, hum, mb10press);

			if (cumulus.APRS.SendSolar && MetData.SolarRad.HasValue)
			{
				data += APRSsolarradStr(MetData.SolarRad.Value);
			}

			if (!string.IsNullOrWhiteSpace(cumulus.WxnowComment))
			{
				var tokenParser = new TokenParser(cumulus.TokenParserOnToken) { InputText = cumulus.WxnowComment };

				// process the webtags in the content string
				data += tokenParser.ToStringFromString();
			}

			return data;
		}

		private static string APRSsolarradStr(double solarRad)
		{
			if (solarRad < 1000)
			{
				return 'L' + Convert.ToInt32(solarRad).ToString("D3");
			}
			else
			{
				return 'l' + Convert.ToInt32(solarRad - 1000).ToString("D3");
			}
		}

		private string APRStemp(double temp)
		{
			// input is in TempUnit units, convert to F for APRS
			// and return three digits
			int num;

			if (cumulus.Units.Temp == 0)
			{
				num = Convert.ToInt32(temp * 1.8 + 32);
			}

			else
			{
				num = Convert.ToInt32(temp);
			}

			if (num < 0)
			{
				num = -num;
				return '-' + num.ToString("00");
			}
			else
			{
				return num.ToString("000");
			}
		}
	}
}
