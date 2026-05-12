using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public string BetelCast(double z_hpa, int z_month, string z_wind, int z_trend, bool z_north, double z_baro_top, double z_baro_bottom)
		{
			var z_range = z_baro_top - z_baro_bottom;
			var z_constant = z_range / 22.0F;

			var z_summer = z_month >= 4 && z_month <= 9; // true if "Summer"

			if (z_north)
			{
				// North hemisphere
				z_hpa += (z_wind) switch
				{
					var s when s == cumulus.Trans.compassp[0] => 6F / 100F * z_range,    // N
					var s when s == cumulus.Trans.compassp[1] => 5F / 100F * z_range,    // NNE
					var s when s == cumulus.Trans.compassp[2] => 5F / 100F * z_range,    // NE
					var s when s == cumulus.Trans.compassp[3] => 2F / 100F * z_range,    // ENE
					var s when s == cumulus.Trans.compassp[4] => -0.5F / 100F * z_range, // E
					var s when s == cumulus.Trans.compassp[5] => -2F / 100F * z_range,   // ESE
					var s when s == cumulus.Trans.compassp[6] => -5F / 100F * z_range,   // SE
					var s when s == cumulus.Trans.compassp[7] => -8.5F / 100F * z_range, // SSE
					var s when s == cumulus.Trans.compassp[8] => -12F / 100F * z_range,  // S
					var s when s == cumulus.Trans.compassp[9] => -10F / 100F * z_range,  // SSW
					var s when s == cumulus.Trans.compassp[10] => -6F / 100F * z_range,  // SW
					var s when s == cumulus.Trans.compassp[11] => -4.5F / 100F * z_range,// WSW
					var s when s == cumulus.Trans.compassp[12] => -3F / 100F * z_range,  // W
					var s when s == cumulus.Trans.compassp[13] => -0.5F / 100F * z_range,// WNW
					var s when s == cumulus.Trans.compassp[14] => 1.5F / 100F * z_range, // NW
					var s when s == cumulus.Trans.compassp[15] => 3F / 100F * z_range,   // NNW
					_ => 0F
				};

				if (z_summer)
				{
					// if Summer
					if (z_trend == 1)
					{
						// rising
						z_hpa += 7F / 100F * z_range;
					}
					else if (z_trend == 2)
					{
						//	falling
						z_hpa -= 7F / 100F * z_range;
					}
				}
			}
			else
			{
				// must be South hemisphere
				z_hpa += (z_wind) switch
				{
					var s when s == cumulus.Trans.compassp[0] => -12F / 100F * z_range,  // N
					var s when s == cumulus.Trans.compassp[1] => -10F / 100F * z_range,  // NNE
					var s when s == cumulus.Trans.compassp[2] => -6F / 100F * z_range,   // NE
					var s when s == cumulus.Trans.compassp[3] => -4.5F / 100F * z_range, // ENE
					var s when s == cumulus.Trans.compassp[4] => -3F / 100F * z_range,   // E
					var s when s == cumulus.Trans.compassp[5] => -0.5F / 100F * z_range, // ESE
					var s when s == cumulus.Trans.compassp[6] => 1.5F / 100F * z_range,  // SE
					var s when s == cumulus.Trans.compassp[7] => 3F / 100F * z_range,    // SSE
					var s when s == cumulus.Trans.compassp[8] => 6F / 100F * z_range,    // S
					var s when s == cumulus.Trans.compassp[9] => 5F / 100F * z_range,    // SSW
					var s when s == cumulus.Trans.compassp[10] => 5F / 100F * z_range,   // SW
					var s when s == cumulus.Trans.compassp[11] => 2F / 100F * z_range,   // WSW
					var s when s == cumulus.Trans.compassp[12] => 0.5F / 100F * z_range, // W
					var s when s == cumulus.Trans.compassp[13] => -2F / 100F * z_range,  // WNW
					var s when s == cumulus.Trans.compassp[14] => -5F / 100F * z_range,  // NW
					var s when s == cumulus.Trans.compassp[15] => -8.5F / 100F * z_range,// NW
					_ => 0F
				};
				if (!z_summer)
				{
					// if Winter
					if (z_trend == 1)
					{
						// rising
						z_hpa += 7F / 100F * z_range;
					}
					else if (z_trend == 2)
					{
						// falling
						z_hpa -= 7F / 100F * z_range;
					}
				}
			} // END North / South

			if (Math.Abs(z_hpa - z_baro_top) < 0.0001)
			{
				z_hpa = z_baro_top - 1;
			}

			var z_option = (int) Math.Floor((z_hpa - z_baro_bottom) / z_constant);

			var z_output = new StringBuilder(100);
			if (z_option < 0)
			{
				z_option = 0;
				z_output.Append($"{cumulus.Trans.Exceptional}, ");
			}
			if (z_option > 21)
			{
				z_option = 21;
				z_output.Append($"{cumulus.Trans.Exceptional}, ");
			}

			if (z_trend == 1)
			{
				// rising
				MetData.Forecastnumber = riseOptions[z_option] + 1;
				z_output.Append(cumulus.Trans.zForecast[riseOptions[z_option]]);
			}
			else if (z_trend == 2)
			{
				// falling
				MetData.Forecastnumber = fallOptions[z_option] + 1;
				z_output.Append(cumulus.Trans.zForecast[fallOptions[z_option]]);
			}
			else
			{
				// must be "steady"
				MetData.Forecastnumber = steadyOptions[z_option] + 1;
				z_output.Append(cumulus.Trans.zForecast[steadyOptions[z_option]]);
			}
			return z_output.ToString();
		}
	}
}
