using System;
using System.Collections.Generic;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace CumulusMX
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
	internal class StationData
	{
		#region Ecowitt
		public static Dictionary<string, byte> SensorReception { get; set; }
		public static Dictionary<string, int> SensorRssi { get; set; }
		#endregion Ecowitt

	}
}
