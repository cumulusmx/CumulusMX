using System;
using System.IO;
using System.Timers;

namespace CumulusMX
{
	/*
	 * EasyWeather.dat file format (* indicates fields used by Cumulus)
	 * 1    Record number       int
	 * 2    Transfer Date       yyyy-mmm-dd hh:mm:ss
	 * 3*   Reading Date        yyyy-mmm-dd hh:mm:ss
	 * 4    Reading interval    int     (minutes since previous reading)
	 * 5*   Indoor humidity     int
	 * 6*   Indoor temp         float   Celsius
	 * 7*   Outdoor humidity    int
	 * 8*   Outdoor temp        float   Celsius
	 * 9*   Dew point           float   Celsius
	 * 10*  Wind chill          float   Celsius
	 * 11*  Absolute press      float   mB/hPa
	 * 12*  Relative press      float   mB/hPa
	 * 13*  Wind average        float   m/s
	 * 14   Wind average        int     Beaufort
	 * 15*  Wind gust           float   m/s
	 * 16   Wind gust           int     Beaufort
	 * 17   Wind direction      int     0 - 15. 0 = North, 1 = NNE etc
	 * 18*  Wind direction      str     Oddly ENE appears as NEE, and ESE appears as SEE
	 * 19   Rain ticks          int
	 * 20   Rain total          float   mm
	 * 21   Rain since last     float   mm
	 * 22*  Rain in last hour   float   mm
	 * 23   Rain in last 24h    float   mm
	 * 24   Rain in last 7d     float   mm
	 * 25   Rain in last 30d    float   mm
	 * 26*  Rain total          float   mm
	 * 27*  Light reading       int     Lux
	 * 28*  UV Index            int
	 * 29   Status bit #0       int     0|1
	 * 30   Status bit #1       int     0|1
	 * 31   Status bit #2       int     0|1
	 * 32   Status bit #3       int     0|1
	 * 33   Status bit #4       int     0|1
	 * 34   Status bit #5       int     0|1
	 * 35   Status bit #6       int     0|1 - Outdoor readings invalid = 1
	 * 36   Status bit #7       int     0|1
	 * 37   Data address        6 digit hex
	 * 38   Raw data            16x 2-digit hex
	*/
	internal class EasyWeather : WeatherStation
	{
		private readonly Timer tmrDataRead;

		private const int EW_READING_DATE = 3;
		private const int EW_READING_TIME = 4;
		private const int EW_INDOOR_HUM = 6;
		private const int EW_INDOOR_TEMP = 7;
		private const int EW_OUTDOOR_HUM = 8;
		private const int EW_OUTDOOR_TEMP = 9;
		private const int EW_DEW_POINT = 10;
		private const int EW_WIND_CHILL = 11;
		private const int EW_ABS_PRESSURE = 12;
		private const int EW_REL_PRESSURE = 13;
		private const int EW_AVERAGE_WIND = 14;
		private const int EW_WIND_GUST = 16;
		private const int EW_WIND_BEARING_CP = 19;
		private const int EW_RAIN_LAST_HOUR = 23;
		private const int EW_RAIN_LAST_YEAR = 27;
		private const int EW_LIGHT = 28;
		private const int EW_UV = 29;

		private string lastTime = "";
		private string lastDate = "";

		public EasyWeather(Cumulus cumulus) : base(cumulus)
		{
			tmrDataRead = new Timer();


		}

		public override void Start()
		{
			tmrDataRead.Elapsed += EWGetData;
			tmrDataRead.Interval = cumulus.EwOptions.Interval*60*1000;
			tmrDataRead.Enabled = true;

			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);

			// Easyweather file does not provide pressure trend strings
			cumulus.StationOptions.UseCumulusPresstrendstr = true;


			if (File.Exists(cumulus.EwOptions.Filename))
			{
				EWGetData(null, null);
				cumulus.StartTimersAndSensors();
			}
		}

		public override void Stop()
		{
			tmrDataRead.Stop();
			StopMinuteTimer();
		}

		private void EWGetData(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			if (File.Exists(cumulus.EwOptions.Filename))
			{
				try
				{
					string line;
					using (FileStream fs = new FileStream(cumulus.EwOptions.Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					using (var sr = new StreamReader(fs))
					{
						do
						{
							line = sr.ReadLine();
						} while (!sr.EndOfStream);
						sr.Close();
						fs.Close();
					}
					cumulus.LogDataMessage("Data: " + line);

					// split string on commas and spaces
					char[] charSeparators = { ',', ' ' };

					var st = line.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);

					string datestr = st[EW_READING_DATE];
					string timestr = st[EW_READING_TIME];

					DateTime now = DateTime.Now;

					if ((datestr != lastDate) || (timestr != lastTime))
					{
						lastDate = datestr;
						lastTime = timestr;
					}

					DoWind(ConvertWindMSToUser(GetConvertedValue(st[EW_WIND_GUST])), CPtoBearing(st[EW_WIND_BEARING_CP]), ConvertWindMSToUser(GetConvertedValue(st[EW_AVERAGE_WIND])), now);

					DoWindChill(ConvertTempCToUser(GetConvertedValue(st[EW_WIND_CHILL])), now);

					DoIndoorHumidity(Convert.ToInt32(st[EW_INDOOR_HUM]));

					DoOutdoorHumidity(Convert.ToInt32(st[EW_OUTDOOR_HUM]), now);

					DoOutdoorDewpoint(ConvertTempCToUser(GetConvertedValue(st[EW_DEW_POINT])), now);

					DoPressure(ConvertPressMBToUser(GetConvertedValue(st[EW_REL_PRESSURE])), now);
					UpdatePressureTrendString();

					StationPressure = ConvertPressMBToUser(GetConvertedValue(st[EW_ABS_PRESSURE]));

					DoIndoorTemp(ConvertTempCToUser(GetConvertedValue(st[EW_INDOOR_TEMP])));

					DoOutdoorTemp(ConvertTempCToUser(GetConvertedValue(st[EW_OUTDOOR_TEMP])), now);

					DoRain(ConvertRainMMToUser(GetConvertedValue(st[EW_RAIN_LAST_YEAR])), // use year as total
							ConvertRainMMToUser(GetConvertedValue(st[EW_RAIN_LAST_HOUR])), // use last hour as current rate
							now);

					DoApparentTemp(now);
					DoFeelsLike(now);
					DoHumidex(now);
					DoCloudBaseHeatIndex(now);

					DoForecast(string.Empty, false);

					if (cumulus.StationOptions.LogExtraSensors)
					{
						var lightReading = GetConvertedValue(st[EW_LIGHT]);

						if ((lightReading >= 0) && (lightReading <= 300000))
						{
							DoSolarRad((int)(lightReading * cumulus.SolarOptions.LuxToWM2), now);
							LightValue = lightReading;
						}

						var uVreading = GetConvertedValue(st[EW_UV]);

						if (uVreading == 255)
						{
							// ignore
						}
						else if (uVreading < 0)
						{
							DoUV(0, now);
						}
						else if (uVreading > 16)
						{
							DoUV(16, now);
						}
						else
						{
							DoUV(uVreading, now);
						}
					}

					if (cumulus.StationOptions.CalculatedET && now.Minute == 0)
					{
						// Start of a new hour, and we want to calculate ET in Cumulus
						CalculateEvaoptranspiration(now);
					}

					UpdateStatusPanel(now);
					UpdateMQTT();
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Error while processing easyweather file: " + ex.Message);
				}
			} else
			{
				cumulus.LogDebugMessage("Easyweather file not found");
			}
		}

		private static int CPtoBearing(string cp)
		{
			switch (cp)
			{
				case "N":
					return 360;
				case "NNE":
					return 22;
				case "NE":
					return 45;
				case "NEE":
					return 67;
				case "ENE":
					return 67;
				case "E":
					return 90;
				case "SEE":
					return 112;
				case "EES":
					return 112;
				case "ESE":
					return 112;
				case "SE":
					return 135;
				case "SSE":
					return 157;
				case "S":
					return 180;
				case "SSW":
					return 202;
				case "SW":
					return 225;
				case "SWW":
					return 247;
				case "WSW":
					return 247;
				case "W":
					return 270;
				case "NWW":
					return 292;
				case "WNW":
					return 292;
				case "NW":
					return 315;
				case "NNW":
					return 337;
				default:
					return 0;
			}
		}

		private string ConvertPeriodToSystemDecimal(string aStr)
		{
			return aStr.Replace(".", cumulus.DecimalSeparator);
		}

		private double GetConvertedValue(string aStr)
		{
			return Convert.ToDouble(ConvertPeriodToSystemDecimal(aStr));
		}
	}
}
