using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void DoSolarRad(int? solar, DateTime timestamp)
		{
			if (!solar.HasValue)
			{
				MetData.SolarRad = solar;
				return;
			}


			try
			{
				MetData.SolarRad = (int) Math.Round(cumulus.Calib.Solar.Calibrate(solar.Value));
			}
			catch
			{
				MetData.SolarRad = null;
			}

			if (MetData.SolarRad.HasValue)
			{
				if (MetData.SolarRad < 0)
				{
					MetData.SolarRad = 0;
				}
				else
				{
					if (MetData.SolarRad > DailyHighLow.Today.HighSolar)
					{
						DailyHighLow.Today.HighSolar = MetData.SolarRad.Value;
						DailyHighLow.Today.HighSolarTime = timestamp;
					}

					if (!cumulus.SolarOptions.UseBlakeLarsen)
					{
						MetData.IsSunny = MetData.SolarRad > MetData.CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100 && MetData.SolarRad >= cumulus.SolarOptions.SolarMinimum;
					}
				}
			}
			HaveReadData = true;
		}

		public void DoUV(double? uv, DateTime timestamp)
		{
			if (!uv.HasValue)
			{
				MetData.UV = null;
				return;
			}

			MetData.UV = cumulus.Calib.UV.Calibrate(uv.Value);
			if (MetData.UV < 0)
				MetData.UV = 0;
			if (MetData.UV > 16)
				MetData.UV = 16;

			if (MetData.UV > DailyHighLow.Today.HighUv)
			{
				DailyHighLow.Today.HighUv = MetData.UV.Value;
				DailyHighLow.Today.HighUvTime = timestamp;
			}

			HaveReadData = true;
		}

		protected void DoSunHours(double hrs)
		{
			if (MetData.SunHourCounter == hrs) return;

			if (MetData.StartOfDaySunHourCounter < -9998)
			{
				cumulus.LogWarningMessage("No start of day sun counter. Start counting from now");
				MetData.StartOfDaySunHourCounter = hrs;
			}

			// Has the counter reset to a value less than we were expecting. Or has it changed by some infeasibly large value?
			if (hrs < MetData.SunHourCounter || Math.Abs(hrs - MetData.SunHourCounter) > 20)
			{
				// counter reset
				cumulus.LogMessage("Sun hour counter reset. Old value = " + MetData.SunHourCounter + ", New value = " + hrs);
				MetData.StartOfDaySunHourCounter = hrs - MetData.SunshineHours;
			}
			MetData.SunHourCounter = hrs;
			MetData.SunshineHours = hrs - MetData.StartOfDaySunHourCounter;
		}

		private void ReadBlakeLarsenData()
		{
			var blFile = Path.Combine(Directory.GetCurrentDirectory(), "SRsunshine.dat");

			if (File.Exists(blFile))
			{
				try
				{
					using var sr = new StreamReader(blFile);
					var line = sr.ReadLine();
					MetData.SunshineHours = double.Parse(line, CultureInfo.InvariantCulture.NumberFormat);
					sr.ReadLine();
					sr.ReadLine();
					line = sr.ReadLine();
					MetData.IsSunny = line == "True";
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Error reading SRsunshine.dat: " + ex.Message);
				}
			}
		}

	}
}
