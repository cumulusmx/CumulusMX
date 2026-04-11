using System;

namespace CumulusMX.HistoricData
{
	internal class DataRecord
	{
		public DateTime Timestamp;
		// main station data
		public double? WindGust;
		public double? WindSpeed;
		public double? WindBearing;
		public double? Temperature;
		public double? DewPoint;
		public double? BGT;
		public double? WBGT;
		public int? Hum;
		public double? Pressure;
		public double? StationPressure;
		public double? RainRate;
		public double? RainCounter;
		public double? Solar;
		public double? UV;
		public double? InTemp;
		public double? InHum;
		// extra sensors
		public double?[] ExtraTemp = new double?[17];
		public int?[] ExtraHum = new int?[17];
		public double?[] ExtraDewPoint = new double?[17];
		public double?[] SoilTemp = new double?[17];
		public int?[] SoilMoist = new int?[17];
		public int?[] SoilEc = new int?[17];
		public double?[] LeafWet = new double?[9];
		public double?[] AirQual = new double?[5];
		public double?[] AirQualAvg = new double?[5];
		public double?[] UserTemp = new double?[9];
		public int? CO2;
		public int? CO2Avg;
		public double? CO2Pm2p5;
		public double? CO2Pm2p5Avg;
		public double? CO2Pm10;
		public double? CO2Pm10Avg;
		public double? CO2Temp;
		public int? CO2Hum;
		public double?[] LaserDist = new double?[5];
		public double?[] LaserDepth = new double?[5];
		public double?[] AqPm10 = new double?[5];
		public double?[] AqPm10Avg = new double?[5];
	}
}
