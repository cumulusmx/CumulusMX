using System;
using System.Collections.Generic;
using System.Text;

namespace FineOffset
{
    internal class HistoryData
    {
        public DateTime timestamp;
        public int inHum;
        public double inTemp;
        public int interval;
        public int outHum;
        public double outTemp;
        public double pressure;
        public int rainCounter;
        public int windBearing;
        public double windGust;
        public double windSpeed;
        public int uvVal;
        public double solarVal;
        public bool SensorContactLost;
        public int followinginterval;
    }
}
