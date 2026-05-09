using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal class DailyHighLow
	{
		public struct DailyHighLows
		{
			public double HighGust;
			public int HighGustBearing;
			public DateTime HighGustTime;
			public double HighWind;
			public DateTime HighWindTime;
			public double HighTemp;
			public DateTime HighTempTime;
			public double LowTemp;
			public DateTime LowTempTime;
			public double TempRange;
			public double HighAppTemp;
			public DateTime HighAppTempTime;
			public double LowAppTemp;
			public DateTime LowAppTempTime;
			public double HighFeelsLike;
			public DateTime HighFeelsLikeTime;
			public double LowFeelsLike;
			public DateTime LowFeelsLikeTime;
			public double HighHumidex;
			public DateTime HighHumidexTime;
			public double HighPress;
			public DateTime HighPressTime;
			public double LowPress;
			public DateTime LowPressTime;
			public double HighRainRate;
			public DateTime HighRainRateTime;
			public double HighHourlyRain;
			public DateTime HighHourlyRainTime;
			public int HighHumidity;
			public DateTime HighHumidityTime;
			public int LowHumidity;
			public DateTime LowHumidityTime;
			public double HighHeatIndex;
			public DateTime HighHeatIndexTime;
			public double HighRain24h;
			public DateTime HighRain24hTime;
			public double LowWindChill;
			public DateTime LowWindChillTime;
			public double HighDewPoint;
			public DateTime HighDewPointTime;
			public double LowDewPoint;
			public DateTime LowDewPointTime;
			public int HighSolar;
			public DateTime HighSolarTime;
			public double HighUv;
			public DateTime HighUvTime;
			public double HighBgt;
			public DateTime HighBgtTime;
			public double HighWbgt;
			public DateTime HighWbgtTime;
		};

		// today highs and lows
		public static DailyHighLows Today = new()
		{
			HighTemp = -500,
			HighAppTemp = -500,
			HighFeelsLike = -500,
			HighHumidex = -500,
			HighHeatIndex = -500,
			HighDewPoint = -500,
			HighRain24h = -500,
			LowTemp = 999,
			LowAppTemp = 999,
			LowFeelsLike = 999,
			LowWindChill = 999,
			LowDewPoint = 999,
			LowPress = 9999,
			LowHumidity = 100,
			HighBgt = Cumulus.DefaultHiVal,
			HighWbgt = Cumulus.DefaultHiVal
		};

		// yesterdays highs and lows
		public static DailyHighLows Yest = new()
		{
			HighTemp = -500,
			HighAppTemp = -500,
			HighFeelsLike = -500,
			HighHumidex = -500,
			HighHeatIndex = -500,
			HighDewPoint = -500,
			HighRain24h = -500,
			LowTemp = 999,
			LowAppTemp = 999,
			LowFeelsLike = 999,
			LowWindChill = 999,
			LowDewPoint = 999,
			LowPress = 9999,
			LowHumidity = 100,
			HighBgt = Cumulus.DefaultHiVal,
			HighWbgt = Cumulus.DefaultHiVal
		};

		// todays midnight highs and lows
		public static DailyHighLows TodayMidnight = new()
		{
			HighTemp = -500,
			LowTemp = 999
		};

		// yesterdays midnight highs and lows
		public static DailyHighLows YestMidnight = new()
		{
			HighTemp = -500,
			LowTemp = 999
		};

		// todays 9am highs and lows
		public static DailyHighLows Today9am = new()
		{
			HighTemp = -500,
			LowTemp = 999
		};

		// yesterdays 9am highs and lows
		public static DailyHighLows Yest9am = new()
		{
			HighTemp = -500,
			LowTemp = 999
		};
	}
}
