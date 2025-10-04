using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CumulusMX
{
	internal class ExtraLogFileRec
	{
		public string DateTimeStr { get; set; }

		public long UnixTimestamp;
		public DateTime DateTime
		{
			get
			{
				return UnixTimestamp.FromUnixTime();
			}
			set
			{
				UnixTimestamp = value.ToUnixTime();
				DateTimeStr = value.ToString("dd/MM/yy HH:mm", CultureInfo.InvariantCulture);
			}
		}
		public double?[] ExtraTemp { get; set; } = new double?[17];
		public int?[] ExtraHum { get; set; } = new int?[17];
		public double?[] ExtraDewpoint { get; set; } = new double?[17];
		public double?[] SoilTemp { get; set; } = new double?[17];
		public int?[] SoilMoist { get; set; } = new int?[17];
		public double?[] LeafWetness { get; set; } = new double?[9];
		public double?[] AirQuality { get; set; } = new double?[5];
		public double?[] AirQualityAvg { get; set; } = new double?[5];
		public double?[] AirQuality10 { get; set; } = new double?[5];
		public double?[] AirQuality10Avg { get; set; } = new double?[5];
		public double?[] UserTemp { get; set; } = new double?[9];
		public double? CO2 { get; set; }
		public double? CO2_24h { get; set; }
		public double? CO2_pm2p5 { get; set; }
		public double? CO2_pm2p5_24h { get; set; }
		public double? CO2_pm10 { get; set; }
		public double? CO2_pm10_24h { get; set; }
		public double? CO2_temperature { get; set; }
		public int? CO2_humidity { get; set; }
		public double?[] LaserDist { get; set; } = new double?[5];
		public double?[] LaserDepth { get; set; } = new double?[5];
		public double? Snow24h { get; set; }

		public ExtraLogFileRec()
		{
		}

		public ExtraLogFileRec(string csv)
		{
			ParseCsvRec(csv);
		}


		// errors are caught by the caller
		public void ParseCsvRec(string data)
		{
			// 0  Date/Time in the form dd/mm/yy hh:mm
			// 1  Current Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16
			// 119-122 AQ PM10
			// 123-126 AQ PM10 Avg


			var inv = CultureInfo.InvariantCulture;
			var st = new List<string>(data.Split(','));
			double resultDbl;
			int resultInt;

			try
			{
				DateTimeStr = st[0];
				UnixTimestamp = Convert.ToInt64(st[1]);

				if (st.Count >= 32)
				{
					for (int i = 1; i <= 10; i++)
					{
						//2-11
						if (double.TryParse(st[1 + i], inv, out resultDbl))
							ExtraTemp[i] = resultDbl;

						//12-21
						if (int.TryParse(st[11 + i], out resultInt))
							ExtraHum[i] = resultInt;

						//22-31
						if (double.TryParse(st[22 + i], inv, out resultDbl))
							ExtraDewpoint[i] = resultDbl;
					}
				}

				if (st.Count >= 36)
				{
					for (int i = 1; i <= 4; i++)
					{
						//32-35
						if (double.TryParse(st[32 + i], inv, out resultDbl))
							SoilTemp[i] = resultDbl;
					}
				}

				if (st.Count >= 40)
				{
					for (int i = 1; i <= 4; i++)
					{
						//36-39
						if (int.TryParse(st[36 + i], out resultInt))
							SoilMoist[i] = resultInt;
					}
				}

				if (st.Count > 44)
				{
					//42-43
					if (double.TryParse(st[42], inv, out resultDbl))
						LeafWetness[1] = resultDbl;

					if (double.TryParse(st[43], inv, out resultDbl))
						LeafWetness[2] = resultDbl;
				}

				if (st.Count > 56)
				{
					//44-55
					for (int i = 5; i <= 16; i++)
					{
						if (double.TryParse(st[44 + i - 4], inv, out resultDbl))
							SoilTemp[i] = resultDbl;
					}
				}
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, $"LogFileParseCsvRec: Error");
				throw;
			}
		}

		public string ToCsv()
		{
			var inv = CultureInfo.InvariantCulture;
			return string.Join(",",
				DateTimeStr,
				UnixTimestamp.ToString(inv)
				);
		}

		public static string CurrentToCsv(DateTime timestamp, Cumulus cumulus, WeatherStation station)
		{ 
			var inv = CultureInfo.InvariantCulture;
			var line = string.Join(",",
				timestamp.ToString("dd/MM/yy HH:mm", inv),
				timestamp.ToUnixTime());

			var sb = new StringBuilder(line, 512);
			var sep = ",";

			for (int i = 1; i <= 10; i++)
			{
				sb.Append(sep + (station.ExtraTemp[i].HasValue ? station.ExtraTemp[i].Value.ToString(cumulus.TempFormat, inv) : string.Empty));       //2-11
			}
			for (int i = 1; i <= 10; i++)
			{
				sb.Append(sep + (station.ExtraHum[i].HasValue ? station.ExtraHum[i].Value.ToString(cumulus.HumFormat, inv) : string.Empty));        //12-21
			}
			for (int i = 1; i <= 10; i++)
			{
				sb.Append(sep + (station.ExtraDewPoint[i].HasValue ? station.ExtraDewPoint[i].Value.ToString(cumulus.TempFormat, inv) : string.Empty));  //22-31
			}
			for (int i = 1; i <= 4; i++)
			{
				sb.Append(sep + (station.SoilTemp[i].HasValue ? station.SoilTemp[i].Value.ToString(cumulus.TempFormat, inv) : string.Empty));     //32-35
			}

			for (int i = 1; i <= 4; i++)
			{
				sb.Append(sep + (station.SoilMoisture[i].HasValue ? station.SoilMoisture[i].ToString() : string.Empty));                      //36-39
			}

			sb.Append(sep + sep);     //40-41 - was leaf temp 1/2

			sb.Append(sep + (station.LeafWetness[1].HasValue ? station.LeafWetness[1].Value.ToString(cumulus.LeafWetFormat, inv) : string.Empty));    //42
			sb.Append(sep + (station.LeafWetness[2].HasValue ? station.LeafWetness[2].Value.ToString(cumulus.LeafWetFormat, inv) : string.Empty));    //43

			for (int i = 5; i <= 16; i++)
			{
				sb.Append(sep + (station.SoilTemp[i].HasValue ? station.SoilTemp[i].Value.ToString(cumulus.TempFormat, inv) : string.Empty));     //44-55
			}

			for (int i = 5; i <= 16; i++)
			{
				sb.Append(sep + station.SoilMoisture[i]);      //56-67
			}

			for (int i = 1; i <= 4; i++)
			{
				sb.Append(sep + (station.AirQuality[i].HasValue ? station.AirQuality[i].Value.ToString("F1", inv) : string.Empty));     //68-71
			}

			for (int i = 1; i <= 4; i++)
			{
				sb.Append(sep + (station.AirQualityAvg[i].HasValue ? station.AirQualityAvg[i].Value.ToString("F1", inv) : string.Empty)); //72-75
			}

			for (int i = 1; i < 9; i++)
			{
				sb.Append(sep + (station.UserTemp[i].HasValue ? station.UserTemp[i].Value.ToString(cumulus.TempFormat, inv) : string.Empty));   //76-83
			}

			sb.Append(sep + (station.CO2.HasValue ? station.CO2.ToString() : string.Empty));                                                //84
			sb.Append(sep + (station.CO2_24h.HasValue ? station.CO2_24h.ToString() : string.Empty));                                        //85
			sb.Append(sep + (station.CO2_pm2p5.HasValue ? station.CO2_pm2p5.Value.ToString("F1", inv) : string.Empty));                     //86
			sb.Append(sep + (station.CO2_pm2p5_24h.HasValue ? station.CO2_pm2p5_24h.Value.ToString("F1", inv) : string.Empty));             //87
			sb.Append(sep + (station.CO2_pm10.HasValue ? station.CO2_pm10.Value.ToString("F1", inv) : string.Empty));                       //88
			sb.Append(sep + (station.CO2_pm10_24h.HasValue ? station.CO2_pm10_24h.Value.ToString("F1", inv) : string.Empty));               //89
			sb.Append(sep + (station.CO2_temperature.HasValue ? station.CO2_temperature.Value.ToString(cumulus.TempFormat, inv) : string.Empty));   //90
			sb.Append(sep + (station.CO2_humidity.HasValue ? station.CO2_humidity.Value.ToString("F0") : string.Empty));                    //91

			for (int i = 1; i < station.LaserDist.Length; i++)
			{
				sb.Append(sep + (station.LaserDist[i].HasValue ? station.LaserDist[i].Value.ToString(cumulus.LaserFormat, inv) : string.Empty)); //92-95
			}
			for (int i = 1; i < station.LaserDepth.Length; i++)
			{
				sb.Append(sep + (station.LaserDepth[i].HasValue ? station.LaserDepth[i].Value.ToString(cumulus.LaserFormat, inv) : string.Empty)); //96-99
			}

			sb.Append(sep + (station.Snow24h[cumulus.SnowAutomated].HasValue ? station.Snow24h[cumulus.SnowAutomated].Value.ToString(cumulus.SnowFormat, inv) : string.Empty)); //100

			for (int i = 11; i <= 16; i++)
			{
				sb.Append(sep + (station.ExtraTemp[i].HasValue ? station.ExtraTemp[i].Value.ToString(cumulus.TempFormat, inv) : string.Empty));       //101-106
			}
			for (int i = 11; i <= 16; i++)
			{
				sb.Append(sep + (station.ExtraHum[i].HasValue ? station.ExtraHum[i].Value.ToString(cumulus.HumFormat, inv) : string.Empty));        //107-112
			}
			for (int i = 11; i <= 16; i++)
			{
				sb.Append(sep + (station.ExtraDewPoint[i].HasValue ? station.ExtraDewPoint[i].Value.ToString(cumulus.TempFormat, inv) : string.Empty));  //113-118
			}

			for (int i = 1; i <= 4; i++)
			{
				sb.Append(sep + (station.AirQuality10[i].HasValue ? station.AirQuality10[i].Value.ToString("F1", inv) : string.Empty));     //119-122
			}

			for (int i = 1; i <= 4; i++)
			{
				sb.Append(sep + (station.AirQuality10Avg[i].HasValue ? station.AirQuality10Avg[i].Value.ToString("F1", inv) : string.Empty)); //123-126
			}

			sb.Append(Environment.NewLine);

			return sb.ToString();
		}
	}
}
