using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions.Station
{
    public class WeatherDataModel
    {

        #region Current Data Properties


        /// <summary>
        /// Indoor temperature in Degrees Celsius
        /// </summary>
        public double? IndoorTemperature { get; set; }
        /// <summary>
        /// Outdoor temperatire in Degrees Celsius
        /// </summary>
        public double? OutdoorTemperature { get; set; }
        /// <summary>
        /// Indoor relative humidity in %
        /// </summary>
        public int? IndoorHumidity { get; set; }
        /// <summary>
        /// Outdoor relative humidity in %
        /// </summary>
        public int? OutdoorHumidity { get; set; }
        /// <summary>
        /// Latest wind gust in Metres/Second
        /// </summary>
        public double? WindGust { get; set; }
        /// <summary>
        /// Average wind speed in Metres/Second
        /// </summary>
        public double? WindSpeed { get; set; }
        /// <summary>
        /// Wind direction in degrees
        /// </summary>
        public int? WindBearing { get; set; }
        /// <summary>
        /// Sea-level pressure in MilliBars
        /// </summary>
        public double? AbsolutePressure { get; set; }
        public double? StationPressure { get; set; }
        /// <summary>
        /// Outdoor dew point in Degrees Celsius
        /// </summary>
        public double? OutdoorDewpoint { get; set; }
        /// <summary>
        /// Current rain rate
        /// </summary>
        public double? RainRate { get; set; }
        /// <summary>
        /// Current rain rate
        /// </summary>
        public double? RainCounter { get; set; }



        /// <summary>
        /// Solar Radiation in W/m2
        /// </summary>
        public double? SolarRad { get; set; }

        /// <summary>
        /// UV index
        /// </summary>
        public double? UV { get; set; }










        public string Forecast { get; set; }



        /// <summary>
        /// Wind chill
        /// </summary>
        public double? WindChill { get; set; }


        /// <summary>
        /// Apparent temperature
        /// </summary>
        public double? ApparentTemperature { get; set; }

        /// <summary>
        /// Heat index
        /// </summary>
        public double? HeatIndex { get; set; }

        /// <summary>
        /// Humidex
        /// </summary>
        public double? Humidex { get; set; }



        /// <summary>
        /// Peak wind gust in last 10 minutes
        /// </summary>
        public double? RecentMaxGust { get; set; }



        /// <summary>
        /// Wind direction as compass points
        /// </summary>
        public string BearingText { get; set; }

        /// <summary>
        /// Wind direction in degrees
        /// </summary>
        public int? AvgBearing { get; set; }

        /// <summary>
        /// Wind direction as compass points
        /// </summary>
        public string AvgBearingText { get; set; }




        public double? ET { get; set; }

        public double? LightValue { get; set; }

        public double? HeatingDegreeDays { get; set; }

        public double? CoolingDegreeDays { get; set; }

        public int? tempsamplestoday { get; set; }

        public double? TempTotalToday { get; set; }

        public double? ChillHours { get; set; }

        public double? midnightraincount { get; set; }

        public int? MidnightRainResetDay { get; set; }


        /// <summary>
        /// Wind run for today
        /// </summary>
        public double? WindRunToday { get; set; }

        /// <summary>
        /// Extra Temps
        /// </summary>
        public double?[] ExtraTemp { get; set; }

        /// <summary>
        /// Extra Humidity
        /// </summary>
        public double?[] ExtraHum { get; set; }

        /// <summary>
        /// Extra dewpoint
        /// </summary>
        public double?[] ExtraDewPoint { get; set; }

        /// <summary>
        /// Soil Temp 1 in C
        /// </summary>
        public double? SoilTemp1 { get; set; }

        /// <summary>
        /// Soil Temp 2 in C
        /// </summary>
        public double? SoilTemp2 { get; set; }

        /// <summary>
        /// Soil Temp 3 in C
        /// </summary>
        public double? SoilTemp3 { get; set; }

        /// <summary>
        /// Soil Temp 4 in C
        /// </summary>
        public double? SoilTemp4 { get; set; }


        public int? SoilMoisture1 { get; set; }

        public int? SoilMoisture2 { get; set; }

        public int? SoilMoisture3 { get; set; }

        public int? SoilMoisture4 { get; set; }

        public double? LeafTemp1 { get; set; }

        public double? LeafTemp2 { get; set; }

        public double? LeafTemp3 { get; set; }

        public double? LeafTemp4 { get; set; }

        public int? LeafWetness1 { get; set; }

        public int? LeafWetness2 { get; set; }

        public int? LeafWetness3 { get; set; }

        public int? LeafWetness4 { get; set; }

        public double? SunshineHours { get; set; }

        public double? SunshineToMidnight { get; set; }
        public double? SunHourCounter { get; set; }

        public double? StartOfDaySunHourCounter { get; set; }


        public double? CurrentSolarMax { get; set; }

        public double? RG11RainToday { get; set; }

        public double? RainSinceMidnight { get; set; }

        #endregion

    }
}
