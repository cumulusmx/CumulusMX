using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Common;
using CumulusMX.Extensions.Station;
using UnitsNet;

namespace DavisStation
{
    // The VPLoop2Data class extracts and stores the weather data from the array of bytes returned from the Vantage weather station
    // The array is generated from the return of the LPS command.
    //
    // Contents of the character array (LOOP2 packet from Vantage):
    //
    //    Field                           Offset  Size    Explanation
    //    "L"                             0       1
    //    "O"                             1       1
    //    "O"                             2       1       Spells out "LOO"  Identifies a LOOP packet
    //    "Bar Trend                      3       1       Signed byte that indicates the current 3-hour barometer trend. It is one of these values:
    //                                                    -60 = Falling Rapidly  = 196 (as an unsigned byte)
    //                                                    -20 = Falling Slowly   = 236 (as an unsigned byte)
    //                                                    0 = Steady
    //                                                    20 = Rising Slowly
    //                                                    60 = Rising Rapidly
    //                                                    80 = ASCII "P" = Rev A firmware, no trend info is available.
    //                                                    Any other value means that the Vantage does not have the 3 hours of bar data needed
    //                                                        to determine the bar trend.
    //    Packet Type                     4       1       Has the value 1, indicating a LOOP2 packet
    //    Unused                          5       2       Unused, contains 0x7FFF
    //    Pressure                        7       2       Current Pressure. Units are (in Hg / 1000). The barometric value should be between 20 inches
    //                                                        and 32.5 inches in Vantage Pro and between 20 inches and 32.5 inches in both Vantatge Pro
    //                                                        Vantage Pro2.  Values outside these ranges will not be logged.
    //    Inside Temperature              9       2       The value is sent as 10th of a degree in F.  For example, 795 is returned for 79.5°F.
    //    Inside Humidity                 11      1       This is the relative humidity in %, such as 50 is returned for 50%.
    //    Outside Temperature             12      2       The value is sent as 10th of a degree in F.  For example, 795 is returned for 79.5°F.
    //    Wind Speed                      14      1       It is a byte unsigned value in mph.  If the wind speed is dashed because it lost synchronization
    //                                                        with the radio or due to some other reason, the wind speed is forced to be 0.
    //    Unused                          15      1       Unused, contains 0xFF
    //    Wind Direction                  16      2       It is a two byte unsigned value from 0 to 360 degrees.
    //                                                        (0° is North, 90° is East, 180° is South and 270° is West.)
    //    10-Min Avg Wind Speed           18      2       It is a two-byte unsigned value.
    //    2-Min Avg Wind Speed            20      2       It is a two-byte unsigned value.
    //    10-Min Wind Gust                22      2       It is a two-byte unsigned value.
    //    Wind Direction for the          24      2       It is a two-byte unsigned value from 1 to 360 degrees.
    //    10-Min Wind Gust                                    (0° is no wind data, 90° is East, 180° is South, 270° is West and 360° is north)
    //    Unused                          26      2       Unused field, filled with 0x7FFF
    //    Unused                          28      2       Unused field, filled with 0x7FFF
    //    Dew Point                       30      2       The value is a signed two byte value in whole degrees F. 255 = dashed data
    //    Unused                          32      1       Unused field, filled with 0xFF
    //    Outside Humidity                33      1       This is the relative humidity in %, such as 50 is returned for 50%.
    //    Unused                          34      1       Unused field, filled with 0xFF
    //    Heat Index                      35      2       The value is a signed two byte value in whole degrees F. 255 = dashed data
    //    Wind Chill                      37      2       The value is a signed two byte value in whole degrees F. 255 = dashed data
    //    THSW Index                      39      2       The value is a signed two byte value in whole degrees F. 255 = dashed data
    //    Rain Rate                       41      2       In rain clicks per hour.
    //    UV                              43      1       Unit is in UV Index
    //    Solar Radiation                 44      2       The unit is in watt/meter2.
    //    Storm Rain                      46      2       The storm is stored as number of rain clicks. (0.2mm or 0.01in)
    //    Start Date of current Storm     48      2       Bit 15 to bit 12 is the month, bit 11 to bit 7 is the day and bit 6 to bit 0 is the year offseted by 2000.
    //    Daily Rain                      50      2       This value is sent as number of rain clicks. (0.2mm or 0.01in)
    //    Last 15-min Rain                52      2       This value is sent as number of rain clicks. (0.2mm or 0.01in)
    //    Last Hour Rain                  54      2       This value is sent as number of rain clicks. (0.2mm or 0.01in)
    //    Daily ET                        56      2       This value is sent as the 1000th of an inch.
    //    Last 24-Hour Rain               58      2       This value is sent as number of rain clicks. (0.2mm or 0.01in)
    //    Barometric Reduction Method     60      1       Bar reduction method: 0 - user offset 1- Altimeter Setting 2- NOAA Bar Reduction. For VP2, this will always be 2.
    //    User-entered Barometric Offset  61      2       Barometer calibration number in 1000th of an inch
    //    Barometric calibration number   63      2       Calibration offset in 1000th of an inch
    //    Barometric Sensor Raw Reading   65      2       In 1000th of an inch
    //    Absolute Barometric Pressure    67      2       In 1000th of an inch, equals to the raw sensor reading plus user entered offset
    //    Altimeter Setting               69      2       In 1000th of an inch
    //    Unused                          71      1       Unused field, filled with 0xFF
    //    Unused                          72      1       Undefined
    //    Next 10-min Wind Speed Graph
    //      Pointer                       73      1       Points to the next 10-minute wind speed graph point. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 24 on Vantage Vue console)
    //    Next 15-min Wind Speed Graph
    //      Pointer                       74      1       Points to the next 15-minute wind speed graph point. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 24 on Vantage Vue console)
    //    Next Hourly Wind Speed Graph
    //      Pointer                       75      1       Points to the next hour wind speed graph point. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 24 on Vantage Vue console)
    //    Next Daily Wind Speed Graph
    //      Pointer                       76      1       Points to the next daily wind speed graph point. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 24 on Vantage Vue console)
    //    Next Minute Rain Graph Pointer  77      1       Points to the next minute rain graph point. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 24 on Vantage Vue console)
    //    Next Rain Storm Graph Pointer   78      1       Points to the next rain storm graph point. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 254on Vantage Vue console)
    //    Index to the Minute within
    //      an Hour                       79      1       It keeps track of the minute within an hour for the rain calculation. (range from 0 to 59)
    //    Next Monthly Rain               80      1       Points to the next monthly rain graph point. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 24 on Vantage Vue console)
    //    Next Yearly Rain                81      1       Points to the next yearly rain graph point. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 24 on Vantage Vue console)
    //    Next Seasonal Rain              82      1       Points to the next seasonal rain graph point. Yearly rain always resets at the beginning of the calendar,
    //                                                      but seasonal rain resets when rain season begins. For current graph point,
    //                                                      just subtract 1 (range from 0 to 23 on VP/VP2 console and 0 to 24 on Vantage Vue console)
    //    Unused                          83      2       Unused field, filled with 0x7FFF
    //    Unused                          85      2       Unused field, filled with 0x7FFF
    //    Unused                          87      2       Unused field, filled with 0x7FFF
    //    Unused                          89      2       Unused field, filled with 0x7FFF
    //    Unused                          91      2       Unused field, filled with 0x7FFF
    //    Unused                          93      2       Unused field, filled with 0x7FFF
    //    "\n" <LF> = 0x0A                95      1
    //    "\r" <CR> = 0x0D                96      1
    //    CRC                             97      2
    //    Total Length                    99
    public class Loop2Data : RawWeatherData
    {
        private readonly Length _altitude;
        public int WindGust10Min { get; private set; }

        public int WindGustDir { get; private set; }

        public double AbsolutePressure { get; private set; }

        public int CurrentWindSpeed { get; private set; }

        public int WindDirection { get; private set; }

        public int THSWindex { get; private set; }

        public int WindAverage { get; private set; }

        public Loop2Data(Length altitude)
        {
            _altitude = altitude;
        }

        // Load - disassembles the byte array passed in and loads it into local data that the accessors can use.
        // Actual data is in the format to the right of the assignments - I convert it to make it easier to use
        // When bytes have to be assembled into 2-byte, 16-bit numbers, I convert two bytes from the array into
        // an Int16 (16-bit integer).  When a single byte is all that's needed, I just convert it to an Int32.
        // In the end, all integers are cast to Int32 for return.
        public void Load(Byte[] byteArray)
        {
            AbsolutePressure = (double)(BitConverter.ToInt16(byteArray, 67)) / 1000; // Uint16
            WindGust10Min = Convert.ToInt32(byteArray[22]);
            WindGustDir = BitConverter.ToInt16(byteArray, 24); // Uint16
            WindDirection = BitConverter.ToInt16(byteArray, 16); // Uint16
            CurrentWindSpeed = Convert.ToInt32(byteArray[14]); // Byte - unsigned byte
            THSWindex = BitConverter.ToInt16(byteArray, 39);
            WindAverage = Convert.ToInt32(byteArray[18]);
        }

        public void ApplyToDataModel(WeatherDataModel model)
        {
            if (model.Timestamp == default(DateTime))
                model.Timestamp = DateTime.Now;

            model.WindGust = Speed.FromMilesPerHour(ApplyCalibration("WindGust",WindGust10Min));
            model.WindSpeed = Speed.FromMilesPerHour(ApplyCalibration("WindSpeed", CurrentWindSpeed));
            model.Pressure = Pressure.FromInchesOfMercury(ApplyCalibration("Pressure", AbsolutePressure));
            model.AltimeterPressure = MeteoLib.AdjustPressureForAltitude(model.Pressure.Value, _altitude);
        }

        public override WeatherDataModel GetDataModel()
        {
            var result = new WeatherDataModel();
            ApplyToDataModel(result);
            return result;
        }
    }
}
