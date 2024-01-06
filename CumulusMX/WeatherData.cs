using System;
using System.ComponentModel;

namespace CumulusMX
{
	internal class WeatherData
	{
		public DateTime DT { get; set; }

		public double WindSpeed
		{
			get => windspeed;
			set
			{
				windspeed = value;
				var handler = PropertyChanged;
				handler?.Invoke(this, new PropertyChangedEventArgs(nameof(WindSpeed)));
			}
		}

		private double windspeed;

		public double WindAverage
		{
			get => windaverage;
			set
			{
				windaverage = value;
				var handler = PropertyChanged;
				handler?.Invoke(this, new PropertyChangedEventArgs(nameof(WindAverage)));
			}
		}

		private double windaverage;

		public double OutdoorTemp
		{
			get => outdoortemp;
			set
			{
				outdoortemp = value;
				var handler = PropertyChanged;
				handler?.Invoke(this, new PropertyChangedEventArgs(nameof(OutdoorTemp)));
			}
		}

		private double outdoortemp;

		public double Pressure
		{
			get => pressure;
			set
			{
				pressure = value;
				var handler = PropertyChanged;
				handler?.Invoke(this, new PropertyChangedEventArgs(nameof(Pressure)));
			}
		}

		private double pressure;

		public double Raintotal
		{
			get => raintotal;
			set
			{
				raintotal = value;
				var handler = PropertyChanged;
				handler?.Invoke(this, new PropertyChangedEventArgs(nameof(Raintotal)));
			}
		}

		private double raintotal;

		public event PropertyChangedEventHandler PropertyChanged;
	}
}
