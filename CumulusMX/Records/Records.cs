using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{

	internal class Records
	{
		// Public record values

		// holds all time highs and lows
		public static Records AllTime = new();

		// holds monthly all time highs and lows
		private static Records[] monthlyRecs = new Records[13];
		public static Records[] MonthlyRecs
		{
			get
			{
				monthlyRecs ??= new Records[13];

				return monthlyRecs;
			}
		}

		// this month highs and lows
		public static Records ThisMonth = new();

		public static Records ThisYear = new();




		// Add an indexer so we can reference properties with a string
		public Record this[string propertyName]
		{
			get
			{
				// probably faster without reflection
				var info = PropertyInfoByName(propertyName);
				return (Record) info.GetValue(this, null);
			}
			set
			{
				var info = PropertyInfoByName(propertyName);
				info.SetValue(this, value, null);
			}
		}

		private System.Reflection.PropertyInfo PropertyInfoByName(string name)
		{
			var type = this.GetType();
			var info = type.GetProperty(name);
			if (info == null)
			{
				throw new Exception(String.Format("A property called {0} can't be accessed for type {1}", name, type.FullName));
			}
			return info;
		}

		public Record HighTemp { get; set; } = new Record(0);
		public Record LowTemp { get; set; } = new Record(1);
		public Record HighGust { get; set; } = new Record(2);
		public Record HighWind { get; set; } = new Record(3);
		public Record LowChill { get; set; } = new Record(4);
		public Record HighRainRate { get; set; } = new Record(5);
		public Record DailyRain { get; set; } = new Record(6);
		public Record HourlyRain { get; set; } = new Record(7);
		public Record LowPress { get; set; } = new Record(8);
		public Record HighPress { get; set; } = new Record(9);
		public Record MonthlyRain { get; set; } = new Record(10);
		public Record HighMinTemp { get; set; } = new Record(11);
		public Record LowMaxTemp { get; set; } = new Record(12);
		public Record HighHumidity { get; set; } = new Record(13);
		public Record LowHumidity { get; set; } = new Record(14);
		public Record HighAppTemp { get; set; } = new Record(15);
		public Record LowAppTemp { get; set; } = new Record(16);
		public Record HighHeatIndex { get; set; } = new Record(17);
		public Record HighDewPoint { get; set; } = new Record(18);
		public Record LowDewPoint { get; set; } = new Record(19);
		public Record HighWindRun { get; set; } = new Record(20);
		public Record LongestDryPeriod { get; set; } = new Record(21);
		public Record LongestWetPeriod { get; set; } = new Record(22);
		public Record HighDailyTempRange { get; set; } = new Record(23);
		public Record LowDailyTempRange { get; set; } = new Record(24);
		public Record HighFeelsLike { get; set; } = new Record(25);
		public Record LowFeelsLike { get; set; } = new Record(26);
		public Record HighHumidex { get; set; } = new Record(27);
		public Record HighRain24Hours { get; set; } = new Record(28);
		public Record HighBgt { get; set; } = new Record(29);
		public Record HighWbgt { get; set; } = new Record(30);
	}

	public class Record(int index)
	{
		private static readonly string[] alltimedescs =
		[
			"High temperature", "Low temperature", "High gust", "High wind speed", "Low wind chill", "High rain rate", "High daily rain",
			"High hourly rain", "Low pressure", "High pressure", "Highest monthly rainfall", "Highest minimum temp", "Lowest maximum temp",
			"High humidity", "Low humidity", "High apparent temp", "Low apparent temp", "High heat index", "High dew point", "Low dew point",
			"High daily windrun", "Longest dry period", "Longest wet period", "High daily temp range", "Low daily temp range",
			"High feels like", "Low feels like", "High Humidex", "High 24 hour rain", "High BGT", "High WBGT"
		];
		private readonly int idx = index;

		public double Val { get; set; }
		public DateTime Ts { get; set; }
		public string Desc
		{
			get
			{
				return alltimedescs[idx];
			}
		}

		public string GetValString(string format = "")
		{
			if (Val < Cumulus.DefaultHiVal + 1 || Val > Cumulus.DefaultLoVal - 1)
				return "-";
			else
				return Val.ToString(format);
		}

		public string GetTsString(string format = "")
		{
			if (Ts == DateTime.MinValue)
				return "-";
			else
				return Ts.ToString(format);
		}

	}
}
