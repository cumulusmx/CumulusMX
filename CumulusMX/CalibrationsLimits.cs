namespace CumulusMX
{
	public class Calibrations
	{
		public Calibrations()
		{
			Temp = new Settings();
			InTemp = new Settings();
			Hum = new Settings();
			InHum = new Settings();
			Press = new Settings();
			Rain = new Settings();
			WindSpeed = new Settings();
			WindGust = new Settings();
			WindDir = new Settings();
			Solar = new Settings();
			UV = new Settings();
			WetBulb = new Settings();
		}
		public Settings Temp { get; set; }
		public Settings InTemp { get; set; }
		public Settings Hum { get; set; }
		public Settings InHum { get; set; }
		public Settings Press { get; set; }
		public Settings Rain { get; set; }
		public Settings WindSpeed { get; set; }
		public Settings WindGust { get; set; }
		public Settings WindDir { get; set; }
		public Settings Solar { get; set; }
		public Settings UV { get; set; }
		public Settings WetBulb { get; set; }
	}
	public class Settings
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
		public double InHumDiff { get; set; }= 999;
	}
}
