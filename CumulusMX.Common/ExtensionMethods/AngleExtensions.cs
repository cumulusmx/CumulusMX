using System;
using System.Collections.Generic;
using System.Text;
using UnitsNet;

namespace CumulusMX.Common.ExtensionMethods
{
    public static class AngleExtensions
    {

        public static string[] compassPointNames = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        public static string CompassPoint(this Angle value)
        {
            return compassPointNames[(int) (((value.Degrees * 100) + 1125) % 36000) / 2250];

        }
    }
}
