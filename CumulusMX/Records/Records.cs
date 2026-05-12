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

		public Record HighTemp { get; set; } = new Record("HiTemp");
		public Record LowTemp { get; set; } = new Record("LoTemp");
		public Record HighGust { get; set; } = new Record("HiGust");
		public Record HighWind { get; set; } = new Record("HiWindSpeed");
		public Record LowChill { get; set; } = new Record("LoWindChill");
		public Record HighRainRate { get; set; } = new Record("HiRainRate");
		public Record DailyRain { get; set; } = new Record("HiDailyRain");
		public Record HourlyRain { get; set; } = new Record("HiHourlyRain");
		public Record LowPress { get; set; } = new Record("LoPress");
		public Record HighPress { get; set; } = new Record("HiPress");
		public Record MonthlyRain { get; set; } = new Record("HiMonthRain");
		public Record HighMinTemp { get; set; } = new Record("HiMinTemp");
		public Record LowMaxTemp { get; set; } = new Record("LoMaxTemp");
		public Record HighHumidity { get; set; } = new Record("HiHum");
		public Record LowHumidity { get; set; } = new Record("LoHum");
		public Record HighAppTemp { get; set; } = new Record("HiAppTemp");
		public Record LowAppTemp { get; set; } = new Record("LoAppTemp");
		public Record HighHeatIndex { get; set; } = new Record("HiHeatInd");
		public Record HighDewPoint { get; set; } = new Record("HiDewPnt");
		public Record LowDewPoint { get; set; } = new Record("LoDewPnt");
		public Record HighWindRun { get; set; } = new Record("HiWindDailyRun");
		public Record LongestDryPeriod { get; set; } = new Record("LongDryPeriod");
		public Record LongestWetPeriod { get; set; } = new Record("LongWetPeriod");
		public Record HighDailyTempRange { get; set; } = new Record("HiTempRange");
		public Record LowDailyTempRange { get; set; } = new Record("LoTempRange");
		public Record HighFeelsLike { get; set; } = new Record("HiFeelsLike");
		public Record LowFeelsLike { get; set; } = new Record("LoFeelsLike");
		public Record HighHumidex { get; set; } = new Record("HiHumidex");
		public Record HighRain24Hours { get; set; } = new Record("Hi24hRain");
		public Record HighBgt { get; set; } = new Record("HiBGT");
		public Record HighWbgt { get; set; } = new Record("HiWBGT");
	}

	public class Record(string keyStr)
	{
		private readonly string key = keyStr;

		// store {shortname, fulltext]} for each entry
		public static Dictionary<string, string> Captions { get; set; }

		public double Val { get; set; }
		public DateTime Ts { get; set; }
		public string Desc
		{
			get
			{
				return Captions[key];
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
