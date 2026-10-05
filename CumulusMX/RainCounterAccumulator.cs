using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	public sealed class RainCounterAccumulator(double maxDelta)
	{
		public double RainCounter { get; set; }
		public double? LastTotalRainfall { get; private set; }

		// Tune this for your station resolution (e.g., 0.2 mm per tip)
		public double MaxPlausibleDelta { get; set; } = maxDelta;

		public void ProcessReading(double totalRainfall)
		{
			if (LastTotalRainfall is not null)
			{
				double delta = totalRainfall - LastTotalRainfall.Value;

				bool plausible = delta >= 0 && delta <= MaxPlausibleDelta;

				if (plausible)
				{
					RainCounter += delta;
				}
				else
				{
					var format = Program.cumulus.RainFormat;
					Program.cumulus.LogWarningMessage($"Rain Counter has jumped by {delta.ToString(format)}. Previous = {LastTotalRainfall.Value.ToString(format)}, New = {totalRainfall.ToString(format)}");
				}
			}

			LastTotalRainfall = totalRainfall;
		}
	}
}
