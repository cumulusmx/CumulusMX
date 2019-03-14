using System;
using System.Text;
using CumulusMX.Common;
using CumulusMX.Extensions.Station;
using UnitsNet;
using UnitsNet.Units;

namespace DavisStation
{
    // The LoopData class extracts and stores the weather data from the array of bytes returned from the Vantage weather station
    // The array is generated from the return of the LOOP command.
    //
    // Contents of the character array (LOOP packet from Vantage):
    //
    //    Field                           Offset  Size    Explanation 
    //    "L"                             0       1 
    //    "O"                             1       1 
    //    "O"                             2       1       Spells out "LOO" for Rev B packets and "LOOP" for Rev A packets. Identifies a LOOP packet 
    //    "P" (Rev A), Bar Trend (Rev B)  3       1       Signed byte that indicates the current 3-hour barometer trend. It is one of these values: 
    //                                                    -60 = Falling Rapidly  = 196 (as an unsigned byte) 
    //                                                    -20 = Falling Slowly   = 236 (as an unsigned byte)   
    //                                                    0 = Steady  
    //                                                    20 = Rising Slowly  
    //                                                    60 = Rising Rapidly  
    //                                                    80 = ASCII "P" = Rev A firmware, no trend info is available. 
    //                                                    Any other value means that the Vantage does not have the 3 hours of bar data needed 
    //                                                        to determine the bar trend. 
    //    Packet Type                     4       1       Has the value zero. In the future we may define new LOOP packet formats and assign a different 
    //                                                        value to this field. 
    //    Next Record                     5       2       Location in the archive memory where the next data packet will be written. This can be 
    //                                                        monitored to detect when a new record is created. 
    //    Pressure                        7       2       Current Pressure. Units are (in Hg / 1000). The barometric value should be between 20 inches 
    //                                                        and 32.5 inches in Vantage Pro and between 20 inches and 32.5 inches in both Vantatge Pro 
    //                                                        Vantage Pro2.  Values outside these ranges will not be logged. 
    //    Inside Temperature              9       2       The value is sent as 10th of a degree in F.  For example, 795 is returned for 79.5°F. 
    //    Inside Humidity                 11      1       This is the relative humidity in %, such as 50 is returned for 50%. 
    //    Outside Temperature             12      2       The value is sent as 10th of a degree in F.  For example, 795 is returned for 79.5°F. 
    //    Wind Speed                      14      1       It is a byte unsigned value in mph.  If the wind speed is dashed because it lost synchronization 
    //                                                        with the radio or due to some other reason, the wind speed is forced to be 0. 
    //    10 Min Avg Wind Speed           15      1       It is a byte unsigned value in mph. 
    //    Wind Direction                  16      2       It is a two byte unsigned value from 0 to 360 degrees.  
    //                                                        (0° is North, 90° is East, 180° is South and 270° is West.) 
    //    Extra Temperatures              18      7       This field supports seven extra temperature stations. Each byte is one extra temperature value 
    //                                                        in whole degrees F with an offset of 90 degrees.  For example, a value of 0 = -90°F ; 
    //                                                        a value of 100 = 10°F ; and a value of 169 = 79°F.
    //    Soil Temperatures               25      4       This field supports four soil temperature sensors, in the same format as the Extra Temperature 
    //                                                        field above 
    //    Leaf Temperatures               29      4       This field supports four leaf temperature sensors, in the same format as the Extra Temperature 
    //                                                        field above 
    //    Outside Humidity                33      1       This is the relative humitiy in %.  
    //    Extra Humidities                34      7       Relative humidity in % for extra seven humidity stations.  
    //    Rain Rate                       41      2       This value is sent as 100th of a inch per hour.  For example, 256 represent 2.56 inches/hour. 
    //    UV                              43      1       The unit is in UV index. 
    //    Solar Radiation                 44      2       The unit is in watt/meter2. 
    //    Storm Rain                      46      2       The storm is stored as 100th of an inch. 
    //    Start Date of current Storm     48      2       Bit 15 to bit 12 is the month, bit 11 to bit 7 is the day and bit 6 to bit 0 is the year offseted 
    //                                                        by 2000. 
    //    Day Rain                        50      2       This value is sent as the 100th of an inch. 
    //    Month Rain                      52      2       This value is sent as the 100th of an inch. 
    //    Year Rain                       54      2       This value is sent as the 100th of an inch. 
    //    Day ET                          56      2       This value is sent as the 100th of an inch. 
    //    Month ET                        58      2       This value is sent as the 100th of an inch. 
    //    Year ET                         60      2       This value is sent as the 100th of an inch. 
    //    Soil Moistures                  62      4       The unit is in centibar.  It supports four soil sensors. 
    //    Leaf Wetnesses                  66      4       This is a scale number from 0 to 15 with 0 meaning very dry and 15 meaning very wet.  It supports 
    //                                                        four leaf sensors. 
    //    Inside Alarms                   70      1       Currently active inside alarms. See the table below 
    //    Rain Alarms                     71      1       Currently active rain alarms. See the table below 
    //    Outside Alarms                  72      2       Currently active outside alarms. See the table below 
    //    Extra Temp/Hum Alarms           74      8       Currently active extra temp/hum alarms. See the table below 
    //    Soil & Leaf Alarms              82      4       Currently active soil/leaf alarms. See the table below 
    //    Transmitter Battery Status      86      1       
    //    Console Battery Voltage         87      2       Voltage = ((Data * 300)/512)/100.0 
    //    Forecast Icons                  89      1       
    //    Forecast Rule number            90      1  
    //    Time of Sunrise                 91      2       The time is stored as hour * 100 + min. 
    //    Time of Sunset                  93      2       The time is stored as hour * 100 + min. 
    //    "\n" <LF> = 0x0A                95      1  
    //    "\r" <CR> = 0x0D                96      1   
    //    CRC                             97      2  
    //    Total Length                    99  
    public class LoopData : RawWeatherData
    {
        public int PressureTrend { get; private set; }

        public int CurrentWindSpeed { get; private set; }

        public int AvgWindSpeed { get; private set; }

        public int InsideHumidity { get; private set; }

        public int OutsideHumidity { get; private set; }

        public double Pressure { get; private set; }

        public double InsideTemperature { get; private set; }

        public double OutsideTemperature { get; private set; }

        public double DailyRain { get; private set; }

        public int WindDirection { get; private set; }

        public DateTime SunRise { get; private set; }

        public DateTime SunSet { get; private set; }

        public double MonthRain { get; private set; }

        public double YearRain { get; private set; }

        public double RainRate { get; private set; }

        public double StormRain { get; private set; }

        public DateTime StormRainStart { get; private set; }

        public int?[] SoilMoisture { get; private set; } = new int?[5];

        public int?[] ExtraTemp { get; private set; } = new int?[8];

        public int?[] ExtraHum { get; private set; } = new int?[8];

        public int?[] SoilTemp { get; private set; } = new int?[5];
        
        public double AnnualET { get; private set; }

        public double UVIndex { get; private set; }

        public int?[] LeafWetness { get; private set; } = new int?[5];

        public int?[] LeafTemp { get; private set; } = new int?[5];

        public int ForecastRule { get; private set; }

        public int HiSolarRad { get; private set; }

        public int SolarRad { get; private set; }

        public byte TXbattStatus { get; private set; }

        public double ConBatVoltage { get; private set; }


        // Load - disassembles the byte array passed in and loads it into local data that the accessors can use.
        // Actual data is in the format to the right of the assignments - I convert it to make it easier to use
        // When bytes have to be assembled into 2-byte, 16-bit numbers, I convert two bytes from the array into 
        // an Int16 (16-bit integer).  When a single byte is all that's needed, I just convert it to an Int32.
        // In the end, all integers are cast to Int32 for return.
        public void Load(Byte[] byteArray)
        {
            PressureTrend = Convert.ToInt32((sbyte)byteArray[3]); // Sbyte - signed byte
            Pressure = (double)(BitConverter.ToInt16(byteArray, 7)) / 1000; // Uint16
            InsideTemperature = (double)(BitConverter.ToInt16(byteArray, 9)) / 10; // Uint16
            InsideHumidity = Convert.ToInt32(byteArray[11]); // Byte - unsigned byte
            OutsideTemperature = (double)(BitConverter.ToInt16(byteArray, 12)) / 10; // Uint16
            OutsideHumidity = Convert.ToInt32(byteArray[33]); // Byte - unsigned byte
            WindDirection = BitConverter.ToInt16(byteArray, 16); // Uint16
            CurrentWindSpeed = Convert.ToInt32(byteArray[14]); // Byte - unsigned byte
            AvgWindSpeed = Convert.ToInt32(byteArray[15]); // Byte - unsigned byte
            DailyRain = (double)(BitConverter.ToInt16(byteArray, 50)); // Uint16
            MonthRain = (double)(BitConverter.ToInt16(byteArray, 52)); // Uint16
            YearRain = (double)(BitConverter.ToInt16(byteArray, 54)); // Uint16
            RainRate = (double)(BitConverter.ToInt16(byteArray, 41)); // Uint16
            StormRain = (double)(BitConverter.ToInt16(byteArray, 46));
            ForecastRule = Convert.ToInt32(byteArray[90]);
            LeafTemp[1] = Convert.ToInt32(byteArray[29]);
            LeafTemp[2] = Convert.ToInt32(byteArray[30]);
            LeafTemp[3] = Convert.ToInt32(byteArray[31]);
            LeafTemp[4] = Convert.ToInt32(byteArray[32]);
            LeafWetness[1] = Convert.ToInt32(byteArray[66]);
            LeafWetness[2] = Convert.ToInt32(byteArray[67]);
            LeafWetness[3] = Convert.ToInt32(byteArray[68]);
            LeafWetness[4] = Convert.ToInt32(byteArray[69]);
            SoilTemp[1] = Convert.ToInt32(byteArray[25]);
            SoilTemp[2] = Convert.ToInt32(byteArray[26]);
            SoilTemp[3] = Convert.ToInt32(byteArray[27]);
            SoilTemp[4] = Convert.ToInt32(byteArray[28]);
            ExtraHum[1] = Convert.ToInt32(byteArray[34]);
            ExtraHum[2] = Convert.ToInt32(byteArray[35]);
            ExtraHum[3] = Convert.ToInt32(byteArray[36]);
            ExtraHum[4] = Convert.ToInt32(byteArray[37]);
            ExtraHum[5] = Convert.ToInt32(byteArray[38]);
            ExtraHum[6] = Convert.ToInt32(byteArray[39]);
            ExtraHum[7] = Convert.ToInt32(byteArray[40]);
            ExtraTemp[1] = Convert.ToInt32(byteArray[18]);
            ExtraTemp[2] = Convert.ToInt32(byteArray[19]);
            ExtraTemp[3] = Convert.ToInt32(byteArray[20]);
            ExtraTemp[4] = Convert.ToInt32(byteArray[21]);
            ExtraTemp[5] = Convert.ToInt32(byteArray[22]);
            ExtraTemp[6] = Convert.ToInt32(byteArray[23]);
            ExtraTemp[7] = Convert.ToInt32(byteArray[24]);
            SoilMoisture[1] = Convert.ToInt32(byteArray[62]);
            SoilMoisture[2] = Convert.ToInt32(byteArray[63]);
            SoilMoisture[3] = Convert.ToInt32(byteArray[64]);
            SoilMoisture[4] = Convert.ToInt32(byteArray[65]);
            UVIndex = (double)(Convert.ToInt32(byteArray[43])) / 10;
            SolarRad = BitConverter.ToInt16(byteArray, 44);
            var dayET = (double)(BitConverter.ToInt16(byteArray, 56)) / 1000;
            var yearET = (double)(BitConverter.ToInt16(byteArray, 60)) / 100;
            // It appears that the annual ET in the loop data does not include today
            AnnualET = yearET + dayET;
            TXbattStatus = byteArray[86];
            ConBatVoltage = ((BitConverter.ToInt16(byteArray, 87) * 300) / 512.0) / 100.0;

            try
            {
                var stormRainStart = BitConverter.ToInt16(byteArray, 48);
                var srMonth = (stormRainStart & 0xF000) >> 12;
                var srDay = (stormRainStart & 0x0F80) >> 7;
                var srYear = (stormRainStart & 0x007F) + 2000;
                StormRainStart = new DateTime(srYear, srMonth, srDay);
            }
            catch (Exception)
            {
                StormRainStart = DateTime.MinValue;
            }

            // get the current date and time
            // DateTime currTime = DateTime.Now;

            //int timevalue = BitConverter.ToInt16(byteArray, 91);
            //int minutes;
            //int hours = Math.DivRem(timevalue, 100, out minutes);

            // Create a new Datetime instance - use current year, month and day
            //SunRise = new DateTime(currTime.Year, currTime.Month, currTime.Day, hours, minutes, 0);

            //timevalue = BitConverter.ToInt16(byteArray, 93);
            //hours = Math.DivRem(timevalue, 100, out minutes);
            //SunSet = new DateTime(currTime.Year, currTime.Month, currTime.Day, hours, minutes, 0);

        }

        // This procedure displays the data that we've captured from the Vantage and processed already.
        public string DebugString()
        {
            StringBuilder outputString = new StringBuilder();

            // Format the string for output
            outputString.Append("Pressure: " + Pressure.ToString("f2") + "in. " + PressureTrendText() + Environment.NewLine);
            outputString.Append("Inside Temp: " + InsideTemperature.ToString() + Environment.NewLine);
            outputString.Append("Inside Humidity: " + InsideHumidity.ToString() + "%" + Environment.NewLine);
            outputString.Append("Outside Temp: " + OutsideTemperature.ToString() + Environment.NewLine);
            outputString.Append("Outside Humidity: " + OutsideHumidity.ToString() + "%" + Environment.NewLine);
            outputString.Append("Wind Direction: " + WindDirectionText() + " @ " + WindDirection.ToString() + " degrees" + Environment.NewLine);
            outputString.Append("Current Wind Speed: " + CurrentWindSpeed.ToString() + "MPH" + Environment.NewLine);
            outputString.Append("10 Minute Average Wind Speed: " + AvgWindSpeed.ToString() + "MPH" + Environment.NewLine);
            outputString.Append("Daily Rain: " + DailyRain.ToString() + "in" + Environment.NewLine);
            outputString.Append("Sunrise: " + SunRise.ToString("t") + Environment.NewLine);
            outputString.Append("Sunset: " + SunSet.ToString("t") + Environment.NewLine);

            return (outputString.ToString());
        }

        public string WindDirectionText()
        {
            string windDirString;

            // The wind direction is given in degrees - 0-359 - convert to string representing the direction
            if (WindDirection >= 337 || WindDirection <= 23)
                windDirString = "N";
            else if (WindDirection > 292)
                windDirString = "NW";
            else if (WindDirection >= 247)
                windDirString = "W";
            else if (WindDirection > 203)
                windDirString = "SW";
            else if (WindDirection >= 157)
                windDirString = "S";
            else if (WindDirection > 113)
                windDirString = "SE";
            else if (WindDirection >= 67)
                windDirString = "E";
            else if (WindDirection > 23)
                windDirString = "NE";
            else
                windDirString = "??";

            return windDirString;
        }

        public string PressureTrendText()
        {
            // The barometric trend is in signed integer values.  Convert these to something meaningful.
            switch (PressureTrend)
            {
                case (-60):
                    return "Falling Rapidly";
                case (-20):
                    return "Falling Slowly";
                case (0):
                    return "Steady";
                case (20):
                    return "Rising Slowly";
                case (60):
                    return "Rising Rapidly";
                default:
                    return "??";
            }
        }

        public LoopData()
        {
            PressureTrend = 0;
            CurrentWindSpeed = 0;
            AvgWindSpeed = 0;
            InsideHumidity = 0;
            OutsideHumidity = 0;
            InsideTemperature = 0.0F;
            OutsideTemperature = 0.0F;
            DailyRain = 0.0F;
            WindDirection = 0;
            SunRise = DateTime.MinValue;
            SunSet = DateTime.MinValue;
            MonthRain = 0.0F;
            YearRain = 0.0F;
            RainRate = 0.0F;
        }

        public override WeatherDataModel GetDataModel()
        {
            var result = new WeatherDataModel();
            result.Pressure = new Pressure(ApplyCalibration("Pressure",Pressure)/1000,PressureUnit.InchOfMercury);
            result.IndoorHumidity = new Ratio(ApplyCalibration("Humidity",InsideHumidity),RatioUnit.Percent);
            result.IndoorTemperature = new Temperature(ApplyCalibration("Temperature", InsideTemperature)/10, TemperatureUnit.DegreeFahrenheit);
            result.OutdoorHumidity = new Ratio(ApplyCalibration("Humidity", OutsideHumidity), RatioUnit.Percent);
            result.OutdoorTemperature = new Temperature(ApplyCalibration("Temperature", OutsideTemperature) / 10, TemperatureUnit.DegreeFahrenheit);
            result.RainCounter = new Length(ApplyCalibration("RainCounter",DailyRain)/100,LengthUnit.Inch);
            result.RainRate = new Speed(ApplyCalibration("RainRate", RainRate) / 100, SpeedUnit.InchPerHour);
            result.SolarRadiation = new Irradiance(ApplyCalibration("Solar",SolarRad),IrradianceUnit.WattPerSquareMeter);
            result.UvIndex = (int)ApplyCalibration("UvIndex", UVIndex);
            result.WindBearing = new Angle(ApplyCalibration("WindBearing",WindDirection),AngleUnit.Degree);
            result.WindSpeed = new Speed(ApplyCalibration("WindSpeed",CurrentWindSpeed),SpeedUnit.MilePerHour);
            result.WindGust = new Speed(ApplyCalibration("WindSpeed", CurrentWindSpeed), SpeedUnit.MilePerHour);
            result.Timestamp = DateTime.Now;
            result.AltimeterPressure = result.Pressure;
            if (result.OutdoorHumidity.HasValue && result.OutdoorTemperature.HasValue)
                result.OutdoorDewpoint = MeteoLib.CalculateDewpoint(result.OutdoorTemperature.Value, result.OutdoorHumidity.Value);

            for (int i = 1; i <= 7; i++)
            {
                if (ExtraTemp[i].HasValue && ExtraTemp[i] != 255)
                    result.Extra[$"ExtraTemp{i}"] =
                        new Temperature(ApplyCalibration("ExtraTemp", ExtraTemp[i].Value - 90.0), TemperatureUnit.DegreeFahrenheit);
                if (ExtraHum[i].HasValue && ExtraHum[i] <= 100)
                    result.Extra[$"ExtraHum{i}"] = new Ratio(ApplyCalibration("ExtraHum", ExtraHum[i].Value), RatioUnit.Percent);

                if (result.Extra.ContainsKey($"ExtraHum{i}") && result.Extra.ContainsKey($"ExtraTemp{i}"))
                    result.Extra[$"ExtraDewpoint{i}"] = MeteoLib.CalculateDewpoint((Temperature)result.Extra[$"ExtraTemp{i}"], (Ratio)result.Extra[$"ExtraHum{i}"]);
            }

            for (int i = 1; i<=4;i++)
            {
                if (SoilTemp[i].HasValue && SoilTemp[i] != 255)
                    result.Extra[$"SoilTemp{i}"] =
                        new Temperature(ApplyCalibration("SoilTemp",SoilTemp[i].Value - 90.0), TemperatureUnit.DegreeFahrenheit);
                if (LeafTemp[i].HasValue && LeafTemp[i] != 255)
                    result.Extra[$"LeafTemp{i}"] = new Temperature(ApplyCalibration("LeafTemp", LeafTemp[i].Value - 90.0), TemperatureUnit.DegreeFahrenheit);
                if (SoilMoisture[i].HasValue && SoilMoisture[i] <= 250)
                    result.Extra[$"SoilMoisture{i}"] = new Pressure(ApplyCalibration("SoilMoisture", SoilMoisture[i].Value), PressureUnit.Centibar);
                if (LeafWetness[i].HasValue && LeafWetness[i] <= 16)
                    result.Extra[$"LeafWetness{i}"] = new Ratio(ApplyCalibration("LeafWetness", LeafWetness[i].Value), RatioUnit.Percent);
            }
            
            return result;
        }
    }
}
