using System;
using System.Text;

namespace CumulusMX
{
	// The VPLoopData class extracts and stores the weather data from the array of bytes returned from the Vantage weather station
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
	//    Packet Type                     4       1       Has the value zero. LOOP2 packets are set to 1.
	//    Next Record                     5       2       Location in the archive memory where the next data packet will be written. This can be
	//                                                        monitored to detect when a new record is created.
	//    Pressure                        7       2       Current Pressure. Units are (in Hg / 1000). The barometric value should be between 20 inches
	//                                                        and 32.5 inches in Vantage Pro and between 20 inches and 32.5 inches in both Vantage Pro
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
	//                                                        in whole degrees F with an offset of 90 degrees.  For example, a value of 0 = -90°F
	//                                                        a value of 100 = 10°F ; and a value of 169 = 79°F.
	//    Soil Temperatures               25      4       This field supports four soil temperature sensors, in the same format as the Extra Temperature
	//                                                        field above
	//    Leaf Temperatures               29      4       This field supports four leaf temperature sensors, in the same format as the Extra Temperature
	//                                                        field above
	//    Outside Humidity                33      1       This is the relative humidity in %.
	//    Extra Humidities                34      7       Relative humidity in % for extra seven humidity stations.
	//    Rain Rate                       41      2       This value is sent as 100th of a inch per hour.  For example, 256 represent 2.56 inches/hour.
	//    UV                              43      1       The unit is in UV index.
	//    Solar Radiation                 44      2       The unit is in watt/meter2.
	//    Storm Rain                      46      2       The storm is stored as 100th of an inch.
	//    Start Date of current Storm     48      2       Bit 15 to bit 12 is the month, bit 11 to bit 7 is the day and bit 6 to bit 0 is the year offset
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
	public class VPLoopData
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

		public int[] SoilMoisture { get; private set; }

		public int[] ExtraTemp { get; private set; }

		public int[] ExtraHum { get; private set; }

		public int[] SoilTemp { get; private set; }

		public double AnnualET { get; private set; }

		public double UVIndex { get; private set; }

		public int[] LeafWetness { get; private set; }

		public int ForecastRule { get; private set; }

		public int SolarRad { get; private set; }

		public byte TXbattStatus { get; private set; }

		public double ConBatVoltage { get; private set; }

		// Load - disassembles the byte array passed in and loads it into local data that the accessors can use.
		// Actual data is in the format to the right of the assignments - I convert it to make it easier to use
		// When bytes have to be assembled into 2-byte, 16-bit numbers, I convert two bytes from the array into
		// an Int16 (16-bit integer).  When a single byte is all that's needed, I just convert it to an Int32.
		// In the end, all integers are cast to Int32 for return.
		public void Load(byte[] byteArray)
		{
			PressureTrend = Convert.ToInt32((sbyte) byteArray[3]); // Sbyte - signed byte
			Pressure = (double) (BitConverter.ToInt16(byteArray, 7)) / 1000; // Uint16
			InsideTemperature = (double) (BitConverter.ToInt16(byteArray, 9)) / 10; // Uint16
			InsideHumidity = Convert.ToInt32(byteArray[11]); // Byte - unsigned byte
			OutsideTemperature = (double) (BitConverter.ToInt16(byteArray, 12)) / 10; // Uint16
			OutsideHumidity = Convert.ToInt32(byteArray[33]); // Byte - unsigned byte
			WindDirection = BitConverter.ToInt16(byteArray, 16); // Uint16
			CurrentWindSpeed = Convert.ToInt32(byteArray[14]); // Byte - unsigned byte
			AvgWindSpeed = Convert.ToInt32(byteArray[15]); // Byte - unsigned byte
			DailyRain = BitConverter.ToInt16(byteArray, 50); // Uint16
			MonthRain = BitConverter.ToInt16(byteArray, 52); // Uint16
			YearRain = BitConverter.ToInt16(byteArray, 54); // Uint16
			RainRate = BitConverter.ToInt16(byteArray, 41); // Uint16
			StormRain = BitConverter.ToInt16(byteArray, 46);
			ForecastRule = Convert.ToInt32(byteArray[90]);
			//LeafTemp1 = Convert.ToInt32(byteArray[29]) // No such thing!
			//LeafTemp2 = Convert.ToInt32(byteArray[30])
			//LeafTemp3 = Convert.ToInt32(byteArray[31])
			//LeafTemp4 = Convert.ToInt32(byteArray[32])
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
			UVIndex = (double) (Convert.ToInt32(byteArray[43])) / 10;
			SolarRad = BitConverter.ToInt16(byteArray, 44);
			var dayET = (double) (BitConverter.ToInt16(byteArray, 56)) / 1000;
			var yearET = (double) (BitConverter.ToInt16(byteArray, 60)) / 100;
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
				if (srMonth < 13 && srDay < 32)  // Exception suppression!
				{
					StormRainStart = new DateTime(srYear, srMonth, srDay, 0, 0, 0, DateTimeKind.Local);
				}
				else
				{
					StormRainStart = DateTime.MinValue;
				}
			}
			catch (Exception)
			{
				StormRainStart = DateTime.MinValue;
			}
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

		private string WindDirectionText()
		{
			string windDirString;

			// The wind direction is given in degrees - 0-359 - convert to string representing the direction
			if (WindDirection >= 337 && WindDirection < 360)
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
			else if (WindDirection >= 0)
				windDirString = "N";
			else
				windDirString = "??";

			return windDirString;
		}

		private string PressureTrendText()
		{
			// The barometric trend is in signed integer values.  Convert these to something meaningful.
			return PressureTrend switch
			{
				(-60) => "Falling Rapidly",
				(-20) => "Falling Slowly",
				(0) => "Steady",
				(20) => "Rising Slowly",
				(60) => "Rising Rapidly",
				_ => "??",
			};
		}

		public VPLoopData()
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
			SunRise = DateTime.Now;
			SunSet = DateTime.Now;
			MonthRain = 0.0F;
			YearRain = 0.0F;
			RainRate = 0.0F;
			ExtraTemp = new int[8];
			ExtraHum = new int[8];
			SoilMoisture = new int[5];
			SoilTemp = new int[5];
			LeafWetness = new int[5];
		}
	}

	// The WeatherCalibrationData class extracts and stores the weather data from the array of bytes returned from the Vantage weather station
	// The array is generated from the return of the CALED command.
	//
	//    Field                           Offset  Size    Explanation
	//    Inside Temperature              0       2
	//    Outside Temperature             2       2
	//    Extra Temperature               4       14      (7 * 2)
	//    Soil Temperatures               18      8       (4 * 2)
	//    Leaf Temperatures               26      8       (4 * 2)
	//    Inside Humidity                 34      1
	//    Outside Humidity                35      1
	//    Extra Humidities                36      7
	//    Total Length                    43
	public class WeatherCalibrationData
	{
		// TODO: Not yet implemented
	}

	public class VPArchiveData
	{
		//  Format of the archive records read from the VP data logger
		//  Field                       Offset  Size    Dash Value  Explanation
		//  Date Stamp                  0       2       N/A         These 16 bits hold the date that the archive was
		//                                                          written in the following format:
		//                                                              Year (7 bits) | Month (4 bits) | Day (5 bits) or:
		//                                                              day + month*32 + (year-2000)*512)
		//  Time Stamp                  2       2       N/A         Time on the Vantage that the archive record was written:
		//                                                              (Hour * 100) + minute.
		//  Outside Temperature         4       2       32767       Either the Average Outside Temperature, or the
		//                                                          Final Outside Temperature over the archive period.
		//                                                          Units are (°F / 10)
		//  High Out Temperature        6       2       -32768      Highest Outside Temp over the archive period.
		//  Low Out Temperature         8       2       32767       Lowest Outside Temp over the archive period.
		//  Rainfall                    10      2       0           Number of rain clicks over the archive period
		//  High Rain Rate              12      2       0           Highest rain rate over the archive period, or the rate
		//                                                          shown on the console at the end of the period if there
		//                                                          was no rain. Units are (rain clicks / hour)
		//  Barometer                   14      2       0           Barometer reading at the end of the archive period.
		//                                                          Units are (in Hg / 1000).
		//  Solar Radiation             16      2       32767       Average Solar Rad over the archive period.
		//                                                          Units are (Watts / m2)
		//  Number of Wind Samples      18      2       0           Number of packets containing wind speed data
		//                                                          received from the ISS or wireless anemometer.
		//  Inside Temperature          20      2       32767       Either the Average Inside Temperature, or the Final
		//                                                          Inside Temperature over the archive period.
		//                                                          Units are (°F / 10)
		//  Inside Humidity             22      1       255         Inside Humidity at the end of the archive period
		//  Outside Humidity            23      1       255         Outside Humidity at the end of the archive period
		//  Average Wind Speed          24      1       255         Average Wind Speed over the archive interval.
		//                                                          Units are (MPH)
		//  High Wind Speed             25      1       0           Highest Wind Speed over the archive interval.
		//                                                          Units are (MPH)
		//  Direction of Hi Wind Speed  26      1       32767       Direction code of the High Wind speed.
		//                                                          0=N, 1=NNE, 2=NE, … 14=NW, 15=NNW, 255=Dashed
		//  Prevailing Wind Direction   27      1       32767       Prevailing or Dominant Wind Direction code.
		//                                                          0=N, 1=NNE, 2=NE, … 14=NW, 15=NNW, 255=Dashed
		//  Average UV Index            28      1       255         Average UV Index. Units are (UV Index / 10)
		//  ET                          29      1       0           ET accumulated over the last hour. Only records "on
		//                                                          the hour" will have a non-zero value.
		//                                                          Units are (in / 1000)
		//  High Solar Radiation        30      2       0           Highest Solar Rad value over the archive period.
		//                                                          Units are (Watts / m2)
		//  High UV Index               32      1       0           Highest UV Index value over the archive period.
		//                                                          Units are (Watts / m2)
		//  Forecast Rule               33      1       193         Weather forecast rule at the end of the archive period.
		//  Leaf Temperature            34      2       255         2 Leaf Temperature values. Units are (°F + 90)
		//  Leaf Wetnesses              36      2       255         2 Leaf Wetness values. Range is 0 – 15
		//  Soil Temperatures           38      4       255         4 Soil Temperatures. Units are (°F + 90)
		//  Download Record Type        42      1       N/A         0xFF = Rev A, 0x00 = Rev B archive record
		//  Extra Humidities            43      2       255         2 Extra Humidity values
		//  Extra Temperatures          45      3       32767       3 Extra Temperature values. Units are (°F + 90)
		//  Soil Moistures              48      4       255         4 Soil Moisture values. Units are (cb)

		public int SoilMoisture1 { get; private set; }

		public int SoilMoisture2 { get; private set; }

		public int SoilMoisture3 { get; private set; }

		public int SoilMoisture4 { get; private set; }

		public int ExtraTemp1 { get; private set; }

		public int ExtraTemp2 { get; private set; }

		public int ExtraTemp3 { get; private set; }

		public int ExtraHum1 { get; private set; }

		public int ExtraHum2 { get; private set; }

		public int SoilTemp1 { get; private set; }

		public int SoilTemp2 { get; private set; }

		public int SoilTemp3 { get; private set; }

		public int SoilTemp4 { get; private set; }

		public double ET { get; private set; }

		public double HiUVIndex { get; private set; }

		public double AvgUVIndex { get; private set; }

		public int LeafWetness2 { get; private set; }

		public int LeafWetness1 { get; private set; }


		public int ForecastRule { get; private set; }

		public int HiSolarRad { get; private set; }

		public int SolarRad { get; private set; }

		public DateTime Timestamp { get; private set; }

		public double LoOutsideTemp { get; private set; }

		public double HiOutsideTemp { get; private set; }

		public double HiRainRate { get; private set; }

		public double Rainfall { get; private set; }

		public double OutsideTemperature { get; private set; }

		public double InsideTemperature { get; private set; }

		public double Pressure { get; private set; }

		public int WindDirection { get; private set; }

		public int HiWindDirection { get; private set; }

		public int OutsideHumidity { get; private set; }

		public int InsideHumidity { get; private set; }

		public int HiWindSpeed { get; private set; }

		public int AvgWindSpeed { get; private set; }

		public void Load(byte[] byteArray, out DateTime lastArchiveDate)
		{
			Pressure = BitConverter.ToInt16(byteArray, 14) / 1000.0; // Uint16
			InsideTemperature = BitConverter.ToInt16(byteArray, 20) / 10.0; // Uint16
			InsideHumidity = Convert.ToInt32(byteArray[22]); // Byte - unsigned byte
			OutsideTemperature = BitConverter.ToInt16(byteArray, 4) / 10.0; // Uint16
			HiOutsideTemp = BitConverter.ToInt16(byteArray, 6) / 10.0; // Uint16
			LoOutsideTemp = BitConverter.ToInt16(byteArray, 8) / 10.0; // Uint16
			OutsideHumidity = Convert.ToInt32(byteArray[23]); // Byte - unsigned byte
			HiWindDirection = Convert.ToInt32(byteArray[26]); // Uint16
			WindDirection = Convert.ToInt32(byteArray[27]); // Uint16
			SolarRad = BitConverter.ToInt16(byteArray, 16); // Uint16
			HiSolarRad = BitConverter.ToInt16(byteArray, 30); // Uint16
			AvgWindSpeed = Convert.ToInt32(byteArray[24]); // Byte - unsigned byte
			HiWindSpeed = Convert.ToInt32(byteArray[25]); // Byte - unsigned byte
			Rainfall = BitConverter.ToInt16(byteArray, 10); // Uint16
			HiRainRate = BitConverter.ToInt16(byteArray, 12); // Uint16
			ForecastRule = Convert.ToInt32(byteArray[33]);
			//LeafTemp1 = Convert.ToInt32(byteArray[34]) // Does not exist
			//LeafTemp2 = Convert.ToInt32(byteArray[35])
			LeafWetness1 = Convert.ToInt32(byteArray[36]);
			LeafWetness2 = Convert.ToInt32(byteArray[37]);
			SoilTemp1 = Convert.ToInt32(byteArray[38]);
			SoilTemp2 = Convert.ToInt32(byteArray[39]);
			SoilTemp3 = Convert.ToInt32(byteArray[40]);
			SoilTemp4 = Convert.ToInt32(byteArray[41]);
			ExtraHum1 = Convert.ToInt32(byteArray[43]);
			ExtraHum2 = Convert.ToInt32(byteArray[44]);
			ExtraTemp1 = Convert.ToInt32(byteArray[45]);
			ExtraTemp2 = Convert.ToInt32(byteArray[46]);
			ExtraTemp3 = Convert.ToInt32(byteArray[47]);
			SoilMoisture1 = Convert.ToInt32(byteArray[48]);
			SoilMoisture2 = Convert.ToInt32(byteArray[49]);
			SoilMoisture3 = Convert.ToInt32(byteArray[50]);
			SoilMoisture4 = Convert.ToInt32(byteArray[51]);
			AvgUVIndex = Convert.ToInt32(byteArray[28]) / 10.0;
			ET = (double) (Convert.ToInt32(byteArray[29])) / 1000;
			HiUVIndex = Convert.ToInt32(byteArray[32]) / 10.0;

			// Get timestamp
			int datevalue = BitConverter.ToInt16(byteArray, 0);
			int day;
			int year = Math.DivRem(datevalue, 512, out datevalue) + 2000;
			int month = Math.DivRem(datevalue, 32, out day);

			int timevalue = BitConverter.ToInt16(byteArray, 2);
			int minutes;
			int hours = Math.DivRem(timevalue, 100, out minutes);

			try
			{
				Timestamp = new DateTime(year, month, day, hours, minutes, 0, DateTimeKind.Local);
			}
			catch (Exception)
			{
				Timestamp = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Local);
			}
			lastArchiveDate = Timestamp;
		}

		public VPArchiveData()
		{
			HiUVIndex = 0;
			ET = 0.0F;
			LeafWetness1 = 0;
			LeafWetness2 = 0;
			SoilTemp1 = 0;
			SoilTemp2 = 0;
			SoilTemp3 = 0;
			SoilTemp4 = 0;
			SoilMoisture1 = 0;
			SoilMoisture2 = 0;
			SoilMoisture3 = 0;
			SoilMoisture4 = 0;
			ExtraTemp1 = 0;
			ExtraTemp2 = 0;
			ExtraTemp3 = 0;
			ExtraHum1 = 0;
			ExtraHum2 = 0;
			ForecastRule = 0;
			HiSolarRad = 0;
			SolarRad = 0;
			Timestamp = DateTime.Now;
			LoOutsideTemp = 0.0F;
			HiOutsideTemp = 0.0F;
			HiRainRate = 0.0F;
			OutsideTemperature = 0.0F;
			InsideTemperature = 0.0F;
			Pressure = 0.0F;
			WindDirection = 0;
			HiWindDirection = 0;
			OutsideHumidity = 0;
			InsideHumidity = 0;
			HiWindSpeed = 0;
			AvgWindSpeed = 0;
			AvgUVIndex = 0.0F;
			Rainfall = 0.0F;
		}
	}

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
	//                                                        and 32.5 inches in Vantage Pro and between 20 inches and 32.5 inches in both Vantage Pro
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
	//    Start Date of current Storm     48      2       Bit 15 to bit 12 is the month, bit 11 to bit 7 is the day and bit 6 to bit 0 is the year offset by 2000.
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
	public class VPLoop2Data
	{
		//public int BarTrend { get; private set; }
		//public int IndoorHum { get; private set; }
		//public double Temperature { get; private set; }
		public int CurrentWindSpeed { get; private set; }
		public int WindDirection { get; private set; }
		//public double WindAverage { get; private set; }
		//public double WindAverage2Min { get; private set; }
		public int WindGust10Min { get; private set; }
		public int WindGustDir { get; private set; }
		//public int Humidity { get; private set; }
		//public int HeatIndex { get; private set; }
		//public int WindChill { get; private set; }
		public int THSWindex { get; private set; }
		//public int RainRate { get; private set; }
		//public int UV { get; private set; }
		//public int Solar { get; private set; }
		//public int StormRain { get; private set; }
		//public int DailyRain { get; private set; }
		//public int Last15mRain { get; private set; }
		//public int LastHourRain { get; private set; }
		//public double DailyET { get; private set; }
		//public int Last24hRain { get; private set; }
		public double AbsolutePressure { get; private set; }

		// Load - disassembles the byte array passed in and loads it into local data that the accessors can use.
		// Actual data is in the format to the right of the assignments - I convert it to make it easier to use
		// When bytes have to be assembled into 2-byte, 16-bit numbers, I convert two bytes from the array into
		// an Int16 (16-bit integer).  When a single byte is all that's needed, I just convert it to an Int32.
		// In the end, all integers are cast to Int32 for return.
		public void Load(byte[] byteArray)
		{
			//BarTrend = Convert.ToInt32(byteArray[3]);
			//IndoorHum = Convert.ToInt32(byteArray[11]);
			//Temperature = (double)BitConverter.ToInt16(byteArray, 12) / 10;
			CurrentWindSpeed = Convert.ToInt32(byteArray[14]); // Byte - unsigned byte
			WindDirection = BitConverter.ToInt16(byteArray, 16); // Uint16
																 //WindAverage = (double)BitConverter.ToInt16(byteArray, 18) / 10;
																 //WindAverage2Min = (double)BitConverter.ToInt16(byteArray, 20) / 10;
			WindGust10Min = Convert.ToInt32(byteArray[22]);
			WindGustDir = BitConverter.ToInt16(byteArray, 24); // Uint16
															   //Humidity = Convert.ToInt32(byteArray[33]);
															   //HeatIndex = BitConverter.ToInt16(byteArray, 35);
															   //WindChill = BitConverter.ToInt16(byteArray, 37);
			THSWindex = BitConverter.ToInt16(byteArray, 39);
			//RainRate = BitConverter.ToInt16(byteArray, 41);  // clicks per hour
			//UV = Convert.ToInt32(byteArray[43]);
			//Solar = BitConverter.ToInt16(byteArray, 44);
			//StormRain = BitConverter.ToInt16(byteArray, 46);
			//DailyRain = BitConverter.ToInt16(byteArray, 50);
			//Last15mRain = BitConverter.ToInt16(byteArray, 52);
			//LastHourRain = BitConverter.ToInt16(byteArray, 54);
			//DailyET = (double)BitConverter.ToInt16(byteArray, 56) / 1000;
			//Last24hRain = BitConverter.ToInt16(byteArray, 58);
			AbsolutePressure = (double) (BitConverter.ToInt16(byteArray, 67)) / 1000; // Uint16
		}
	}
}
