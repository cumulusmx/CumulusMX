using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		internal void SetSoilMoistUnits(int index, string val)
		{
			for (var i = 0; i < cumulus.SensorMaps.SoilMoist.Length; i++)
			{
				if (index == cumulus.SensorMaps.SoilMoist[i])
					cumulus.Units.SoilMoistureUnitText[i] = val;
			}

		}

		internal void SetAirQualUnits(int index, string val)
		{
			for (var i = 0; i < cumulus.SensorMaps.AirQual.Length; i++)
			{
				if (index == cumulus.SensorMaps.AirQual[i])
				{
					cumulus.Units.AirQualityUnitText[i] = val;
				}
			}
		}

		internal void SetLeafWetUnits(int index, string val)
		{
			for (var i = 0; i < cumulus.SensorMaps.LeafWet.Length; i++)
			{
				if (index == cumulus.SensorMaps.LeafWet[i])
				{
					cumulus.Units.LeafWetnessUnitText[i] = val;
				}
			}
		}
	}
}
