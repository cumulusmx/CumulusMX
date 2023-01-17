using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class GraphOptions
	{
		public GraphOptionsVisible Visible { get; set; }
		public GraphOptionsColour Colour { get; set; }

		public GraphOptions()
		{
			Visible = new GraphOptionsVisible();
			Visible.CO2Sensor = new GraphOptionsCo2Sensor();
			Colour = new GraphOptionsColour();
		}

		public class GraphOptionsVisible
		{
			public bool Temp { get; set; }
			public bool InTemp { get; set; }
			public bool HeatIndex { get; set; }
			public bool DewPoint { get; set; }
			public bool WindChill { get; set; }
			public bool AppTemp { get; set; }
			public bool FeelsLike { get; set; }
			public bool Humidex { get; set; }
			public bool InHum { get; set; }
			public bool OutHum { get; set; }
			public bool UV { get; set; }
			public bool Solar { get; set; }
			public bool Sunshine { get; set; }
			public bool GrowingDegreeDays1 { get; set; }
			public bool GrowingDegreeDays2 { get; set; }
			public bool TempSum0 { get; set; }
			public bool TempSum1 { get; set; }
			public bool TempSum2 { get; set; }
			public bool[] ExtraTemp = new bool[10];
			public bool[] ExtraHum = new bool[10];
			public bool[] ExtraDewPoint = new bool[10];
			public bool[] SoilTemp = new bool[16];
			public bool[] SoilMoist = new bool[16];
			public bool[] UserTemp = new bool[8];
			// daily values
			public bool MaxTemp { get; set; }
			public bool AvgTemp { get; set; }
			public bool MinTemp { get; set; }
			public GraphOptionsCo2Sensor CO2Sensor { get; set; }
		}

		public class GraphOptionsCo2Sensor
		{
			public bool CO2 { get; set; }
			public bool CO2Avg { get; set; }
			public bool Pm25 { get; set; }
			public bool Pm25Avg { get; set; }
			public bool Pm10 { get; set; }
			public bool Pm10Avg { get; set; }
			public bool Temp { get; set; }
			public bool Hum { get; set; }
		}

		public class GraphOptionsColour
		{
			public string Temp { get; set; }
			public string InTemp { get; set; }
			public string HeatIndex { get; set; }
			public string DewPoint { get; set; }
			public string WindChill { get; set; }
			public string AppTemp { get; set; }
			public string FeelsLike { get; set; }
			public string Humidex { get; set; }
			public string InHum { get; set; }
			public string OutHum { get; set; }
			public string Press { get; set; }
			public string WindGust { get; set; }
			public string WindAvg { get; set; }
			public string WindBearing { get; set; }
			public string WindBearingAvg { get; set; }
			public string UV { get; set; }
			public string Rainfall { get; set; }
			public string RainRate { get; set; }
			public string Solar { get; set; }
			public string SolarTheoretical { get; set; }
			public string Sunshine { get; set; }
			public string Pm2p5 { get; set; }
			public string Pm10 { get; set; }
			public string[] ExtraTemp = new string[10];
			public string[] ExtraHum = new string[10];
			public string[] ExtraDewPoint = new string[10];
			public string[] SoilTemp = new string[16];
			public string[] SoilMoist = new string[16];
			public string[] UserTemp = new string[8];
			// daily values
			public string MaxTemp { get; set; }
			public string MinTemp { get; set; }
			public string AvgTemp { get; set; }
			public string MaxApp { get; set; }
			public string MinApp { get; set; }
			public string MaxDew { get; set; }
			public string MinDew { get; set; }
			public string MinWindChill { get; set; }
			public string MaxFeels { get; set; }
			public string MinFeels { get; set; }
			public string MaxPress { get; set; }
			public string MinPress { get; set; }
			public string MaxHumidex { get; set; }
			public string MaxHeatIndex { get; set; }
			public string MaxWindGust { get; set; }
			public string MaxWindSpeed { get; set; }
			public string WindRun { get; set; }
			public string MaxOutHum { get; set; }
			public string MinOutHum { get; set; }

			public GraphOptionsCo2SensorColour CO2Sensor { get; set; }

			public GraphOptionsColour()
			{
				CO2Sensor = new GraphOptionsCo2SensorColour();
			}
		}

		public class GraphOptionsCo2SensorColour
		{
			public string CO2 { get; set; }
			public string CO2Avg { get; set; }
			public string Pm25 { get; set; }
			public string Pm25Avg { get; set; }
			public string Pm10 { get; set; }
			public string Pm10Avg { get; set; }
			public string Temp { get; set; }
			public string Hum { get; set; }
		}
	}
}
