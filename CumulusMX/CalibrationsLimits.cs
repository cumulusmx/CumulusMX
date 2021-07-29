namespace CumulusMX
{
	public class Calibrations
	{
		public Calibrations()
		{
			Temp = new Settings();
			InTemp = new Settings();
			Hum = new Settings();
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
		public double Offset = 0;
		public double Mult = 1;
		public double Mult2 = 0;
	}

	public class Limits
	{
		public double TempHigh = 60;		// Celsius
		public double TempLow = -60;        // Celsius
		public double DewHigh = 40;         // Celsius
		public double PressHigh = 1090;		// hPa
		public double PressLow = 870;		// hPa
		public double WindHigh = 90;		// m/s
	}

	public class Spikes
	{
		public double MaxHourlyRain = 999;
		public double MaxRainRate = 999;
		public double WindDiff = 999;
		public double GustDiff = 999;
		public double HumidityDiff = 999;
		public double PressDiff = 999;
		public double TempDiff = 999;
	}
}
