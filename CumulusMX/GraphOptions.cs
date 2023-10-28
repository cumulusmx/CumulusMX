using System;
using System.Linq;


namespace CumulusMX
{
	public class GraphOptions
	{
		public GraphOptionsVisible Visible { get; set; }
		public GraphOptionsColour Colour { get; set; }


		public GraphOptions()
		{
			Visible = new GraphOptionsVisible();
			Colour = new GraphOptionsColour();
		}

		public class GraphOptionsVisible
		{
			public GraphDataValue Temp { get; set; } = new GraphDataValue();
			public GraphDataValue InTemp { get; set; } = new GraphDataValue();
			public GraphDataValue HeatIndex { get; set; } = new GraphDataValue();
			public GraphDataValue DewPoint { get; set; } = new GraphDataValue();
			public GraphDataValue WindChill { get; set; } = new GraphDataValue();
			public GraphDataValue AppTemp { get; set; } = new GraphDataValue();
			public GraphDataValue FeelsLike { get; set; } = new GraphDataValue();
			public GraphDataValue Humidex { get; set; } = new GraphDataValue();
			public GraphDataValue InHum { get; set; } = new GraphDataValue();
			public GraphDataValue OutHum { get; set; } = new GraphDataValue();
			public GraphDataValue UV { get; set; } = new GraphDataValue();
			public GraphDataValue Solar { get; set; } = new GraphDataValue();
			public GraphDataValue Sunshine { get; set; } = new GraphDataValue();
			public GraphDataValue GrowingDegreeDays1 { get; set; } = new GraphDataValue();
			public GraphDataValue GrowingDegreeDays2 { get; set; } = new GraphDataValue();
			public GraphDataValue TempSum0 { get; set; } = new GraphDataValue();
			public GraphDataValue TempSum1 { get; set; } = new GraphDataValue();
			public GraphDataValue TempSum2 { get; set; } = new GraphDataValue();
			public GraphOptionsDataArray ExtraTemp { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray ExtraHum { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray ExtraDewPoint { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray SoilTemp { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray SoilMoist { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray UserTemp { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray LeafWetness { get; set; } = new GraphOptionsDataArray();
			// daily values
			public GraphDataValue MaxTemp { get; set; } = new GraphDataValue();
			public GraphDataValue AvgTemp { get; set; } = new GraphDataValue();
			public GraphDataValue MinTemp { get; set; } = new GraphDataValue();
			public GraphOptionsCo2Sensor CO2Sensor { get; set; } = new GraphOptionsCo2Sensor();
			public GraphOptionsAQSensor AqSensor { get; set; } = new GraphOptionsAQSensor();

			public GraphOptionsVisible()
			{
				ExtraTemp.Vals = new int[10];
				ExtraHum.Vals = new int[10];
				ExtraDewPoint.Vals = new int[10];
				SoilTemp.Vals = new int[16];
				SoilMoist.Vals = new int[16];
				UserTemp.Vals = new int[8];
				LeafWetness.Vals = new int[8];
			}
		}

		public class GraphDataValue
		{
			// Val: 0=Hidden, 1=Visible, 2=LocalOnly
			public int Val { get; set; }
			public bool IsVisible(bool local)
			{
				return Val == 1 || (Val == 2 && local);
			}
		}

		public class GraphOptionsCo2Sensor
		{
			public GraphDataValue CO2 { get; set; } = new GraphDataValue();
			public GraphDataValue CO2Avg { get; set; } = new GraphDataValue();
			public GraphDataValue Pm25 { get; set; } = new GraphDataValue();
			public GraphDataValue Pm25Avg { get; set; } = new GraphDataValue();
			public GraphDataValue Pm10 { get; set; } = new GraphDataValue();
			public GraphDataValue Pm10Avg { get; set; } = new GraphDataValue();
			public GraphDataValue Temp { get; set; } = new GraphDataValue();
			public GraphDataValue Hum { get; set; } = new GraphDataValue();

			public bool IsVisible(bool local)
			{
				return CO2.Val == 1 || CO2Avg.Val == 1 || Pm25.Val == 1 || Pm25Avg.Val == 1 || Pm10.Val == 1 || Pm10Avg.Val == 1 || Temp.Val == 1 || Hum.Val == 1 ||
					(local && (CO2.Val == 2 || CO2Avg.Val == 2 || Pm25.Val == 2 || Pm25Avg.Val == 2 || Pm10.Val == 2 || Pm10Avg.Val == 2 || Temp.Val == 2 || Hum.Val == 2));
			}
		}

		public class GraphOptionsAQSensor
		{
			public GraphOptionsDataArray Pm { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray PmAvg { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray Temp { get; set; } = new GraphOptionsDataArray();
			public GraphOptionsDataArray Hum { get; set; } = new GraphOptionsDataArray();

			public GraphOptionsAQSensor()
			{
				Pm.Vals = new int[4];
				PmAvg.Vals = new int[4];
			}

			public bool IsVisible(bool local)
			{
				return Pm.IsVisible(local) || PmAvg.IsVisible(local) || Temp.IsVisible(local) || Hum.IsVisible(local);
			}
		}


		public class GraphOptionsDataArray
		{
			public int[] Vals { get; set; }
			public bool ValVisible(int index, bool local)
			{
				return Vals[index] == 1 || (Vals[index] == 2 && local);
			}

			public bool IsVisible(bool local)
			{
				return Vals.Contains(1) || (Vals.Contains(2) && local);
			}
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
			public string[] LeafWetness = new string[2];
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
