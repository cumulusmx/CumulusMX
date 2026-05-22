using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public static void DoExtraTemp(double? temp, int channel)
		{
			if (channel > 0 && channel < MetData.ExtraTemp.Length)
			{
				MetData.ExtraTemp[channel] = temp;
			}
		}

		public static void DoExtraHum(int? hum, int channel)
		{
			if (channel > 0 && channel < MetData.ExtraHum.Length)
			{
				MetData.ExtraHum[channel] = hum;
			}
		}

		public static void DoExtraDP(double? dp, int channel)
		{
			if (channel > 0 && channel < MetData.ExtraDewPoint.Length)
			{
				MetData.ExtraDewPoint[channel] = dp;
			}
		}

		public static void DoUserTemp(double? temp, int channel)
		{
			if (channel > 0 && channel < MetData.UserTemp.Length)
			{
				MetData.UserTemp[channel] = temp;
			}
		}

		public static void DoSoilMoisture(double? value, int index)
		{
			if (index > 0 && index < MetData.SoilMoisture.Length)
				MetData.SoilMoisture[index] = (int?) value;
		}

		public static void DoSoilTemp(double? value, int index)
		{
			if (index > 0 && index < MetData.SoilTemp.Length)
				MetData.SoilTemp[index] = value;
		}

		public static void DoSoilEc(int? value, int index)
		{
			if (index > 0 && index < MetData.SoilEc.Length)
				MetData.SoilEc[index] = value;
		}

		public void DoLeafWetness(double? value, int index)
		{
			if (index > 0 && index < MetData.LeafWetness.Length)
				MetData.LeafWetness[index] = value;

			if (cumulus.StationOptions.LeafWetnessIsRainingIdx == index)
			{
				MetData.IsRaining = (value ?? 0) >= cumulus.StationOptions.LeafWetnessIsRainingThrsh;
				cumulus.IsRainingAlarm.Triggered = MetData.IsRaining;
			}
		}

		public static void DoLeakSensor(int? value, int index)
		{
			switch (index)
			{
				case 1:
					MetData.LeakSensor1 = value;
					break;
				case 2:
					MetData.LeakSensor2 = value;
					break;
				case 3:
					MetData.LeakSensor3 = value;
					break;
				case 4:
					MetData.LeakSensor4 = value;
					break;
			}
		}


	}
}
