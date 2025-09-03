namespace CumulusMX
{
	public class Calibrations
	{
		public Calibrations()
		{
			Temp = new CalibSettings();
			InTemp = new CalibSettings();
			Hum = new CalibSettings();
			InHum = new CalibSettings();
			Press = new CalibSettings();
			PressStn = new CalibSettings();
			Rain = new CalibSettings();
			WindSpeed = new CalibSettings();
			WindGust = new CalibSettings();
			WindDir = new CalibSettings();
			Solar = new CalibSettings();
			UV = new CalibSettings();
			WetBulb = new CalibSettings();
		}
		public CalibSettings Temp { get; set; }
		public CalibSettings InTemp { get; set; }
		public CalibSettings Hum { get; set; }
		public CalibSettings InHum { get; set; }
		public CalibSettings Press { get; set; }
		public CalibSettings PressStn { get; set; }
		public CalibSettings Rain { get; set; }
		public CalibSettings WindSpeed { get; set; }
		public CalibSettings WindGust { get; set; }
		public CalibSettings WindDir { get; set; }
		public CalibSettings Solar { get; set; }
		public CalibSettings UV { get; set; }
		public CalibSettings WetBulb { get; set; }
	}
	public class CalibSettings
	{
		public double Offset { get; set; } = 0;
		public double Mult { get; set; } = 1;
		public double Mult2 { get; set; } = 0;

		public double Calibrate(double value)
		{
			return value * value * Mult2 + value * Mult + Offset;
		}
	}

	public class Limits
	{
		public double TempHigh { get; set; } = 60;        // Celsius
		public double TempLow { get; set; } = -60;        // Celsius
		public double DewHigh { get; set; } = 40;         // Celsius
		public double PressHigh { get; set; } = 1090;     // hPa
		public double PressLow { get; set; } = 870;       // hPa
		public double StationPressHigh { get; set; } = 0;     // hPa
		public double StationPressLow { get; set; } = 0;       // hPa
		public double WindHigh { get; set; } = 90;        // m/s
	}

	public class Spikes
	{
		public double MaxHourlyRain { get; set; } = 999;
		public double MaxRainRate { get; set; } = 999;
		public double WindDiff { get; set; } = 999;
		public double GustDiff { get; set; } = 999;
		public double HumidityDiff { get; set; } = 999;
		public double PressDiff { get; set; } = 999;
		public double TempDiff { get; set; } = 999;
		public double InTempDiff { get; set; } = 999;
		public double InHumDiff { get; set; } = 999;
		public decimal SnowDiff { get; set; } = 999;
	}
}
