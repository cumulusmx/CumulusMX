using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnitsNet;

namespace CumulusMX.Common.ExtensionMethods
{
    public static class SpeedExtensions
    {
        private struct BeaufortEntry
        {
            internal int Number;
            internal string Description;
        }

        private static Dictionary<double, BeaufortEntry> BeaufortLevels = new Dictionary<double, BeaufortEntry>()
        {
            {0.3, new BeaufortEntry() {Number = 0, Description = "Calm"}},
            {1.6, new BeaufortEntry() {Number = 1, Description = "Light Air"}},
            {3.4, new BeaufortEntry() {Number = 2, Description = "Light Breeze"}},
            {5.5, new BeaufortEntry() {Number = 3, Description = "Gentle Breeze"}},
            {8.0, new BeaufortEntry() {Number = 4, Description = "Moderate Breeze"}},
            {10.8, new BeaufortEntry() {Number = 5, Description = "Fresh Breeze"}},
            {13.9, new BeaufortEntry() {Number = 6, Description = "Strong Breeze"}},
            {17.2, new BeaufortEntry() {Number = 7, Description = "Near Gale"}},
            {20.8, new BeaufortEntry() {Number = 8, Description = "Gale"}},
            {24.5, new BeaufortEntry() {Number = 9, Description = "Strong Gale"}},
            {28.5, new BeaufortEntry() {Number = 10, Description = "Storm"}},
            {32.7, new BeaufortEntry() {Number = 11, Description = "Violent Storm"}},
            {double.MaxValue, new BeaufortEntry() {Number = 12, Description = "Hurricane"}},
        };

        public static int Beaufort(this Speed value)
        {
            return BeaufortLevels.Last(x => x.Key < value.MetersPerSecond).Value.Number;
        }

        public static string BeaufortDescription(this Speed value)
        {
            return BeaufortLevels.Last(x => x.Key < value.MetersPerSecond).Value.Description;
        }
    }
}
