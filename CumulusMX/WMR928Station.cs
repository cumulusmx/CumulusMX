using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace CumulusMX
{
	internal class WMR928Station : WeatherStation
	{
		private const int WMR928WindData = 0;
		private const int WMR928RainData = 1;
		private const int WMR928ExtraOutdoorData = 2;
		private const int WMR928OutdoorData = 3;
		private const int WMR928ExtraTempOnlyData = 4;
		private const int WMR928IndoorData = 6;
		private const int WMR928IndoorData2 = 5;
		private const int WMR928MinuteData = 14;
		private const int WMR928ClockData = 15;

		private int currentPacketLength;
		private int currentPacketType;

		private bool stop;

		private readonly int[] WMR928PacketLength = {11, 16, 9, 9, 7, 13, 14, 0, 0, 0, 0, 0, 0, 0, 5, 9, 255};

		public WMR928Station(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Manufacturer = cumulus.OREGON;
			// station supplies rain rate
			calculaterainrate = false;

			cumulus.LogMessage("Station type = WMR928");

			// start reading the data

			startReadingHistoryData();
		}

		public override void startReadingHistoryData()
		{
			cumulus.LogMessage("Opening COM port " + cumulus.ComportName);

			try
			{
				comport = new SerialPort(cumulus.ComportName, 9600, Parity.None, 8, StopBits.One) {Handshake = Handshake.None, RtsEnable = true, DtrEnable = true};
				comport.Open();

				cumulus.CurrentActivity = "Normal running";

				LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);
				DoDayResetIfNeeded();
				DoTrendValues(DateTime.Now);
				timerStartNeeded = true;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"Error opening com port [{cumulus.ComportName}]: {ex.Message}");
				cumulus.LogConsoleMessage($"Error opening com port [{cumulus.ComportName}]: {ex.Message}");
			}
		}

		public override void Stop()
		{
			stop = true;
			StopMinuteTimer();
		}

		public override void Start()
		{
			cumulus.LogMessage("Start normal reading loop");

			try
			{
				while (!stop)
				{
					Thread.Sleep(1000);
					if (comport.BytesToRead > 0)
					{
						// wait a little to let more data in
						Thread.Sleep(200);
						// Obtain the number of bytes waiting in the port's buffer
						int bytes = comport.BytesToRead;

						if (stop) break;

						string datastr = "Data: ";

						cumulus.LogDebugMessage("Data received, number of bytes = " + bytes);

						// Create a byte array buffer to hold the incoming data
						//byte[] buffer = new byte[bytes];

						for (int i = 0; i < bytes; i++)
						{
							// Read a byte from the port
							int nextByte = comport.ReadByte();

							datastr = datastr + " " + nextByte.ToString("X2");

							switch (currentPacketLength)
							{
								case 0: // We're looking for the start of a packet
									if (nextByte == 255)
										// Possible start of packet
										currentPacketLength = 1;
									break;
								case 1: // We're looking for the second start-of-packet character
									if (nextByte == 255)
										// Possible continuation
										currentPacketLength = 2;
									else
										// Incorrect sequence, start again
										currentPacketLength = 0;
									break;
								case 2: // We're looking for the packet type
									if (nextByte < 16 && WMR928PacketLength[currentPacketType] > 0)
									{
										// Success
										buffer.Add(255);
										buffer.Add(255);
										buffer.Add(nextByte);
										currentPacketLength = 3;
										currentPacketType = nextByte;
										cumulus.LogDebugMessage("Found packet type: " + currentPacketType);
									}
									else
									{
										// Incorrect sequence
										if (nextByte == 255)
											// Might be second start-of-packet character
											currentPacketLength = 2;
										else
											// Start again
											currentPacketLength = 0;
									}
									break;
								default: // We've had the packet header, continue collecting the packet
									buffer.Add(nextByte);
									currentPacketLength++;

									if (currentPacketLength == WMR928PacketLength[currentPacketType])
									{
										// We've collected a complete packet, process it
										/* if
										debug then
										begin
										transbuff :=
										DisplayHex(@buff[1], length(buff));
										DebugForm.Memo1.Lines.Add(transbuff);
								end; */

										Parse(buffer);
										// Get ready for the next packet
										buffer.Clear();
										currentPacketLength = 0;
										currentPacketType = 16;
									}
									break;
							} // end of switch for current packet length
						} // end of for loop for available chars

						cumulus.LogDataMessage(datastr);

						CheckBatteryStatus();
					}
				}
			}
			// Catch the ThreadAbortException
			catch (ThreadAbortException)
			{
			}
			finally
			{
				cumulus.LogMessage("Closing serial port");
				comport.Close();
			}
		}

		private bool WMR928valid(List<int> s, out int csum) // Validates a WMR928 packet
		{
			bool result;

			csum = -1;

			if (s.Count < 5)
				result = false;
			else if (s[0] != 255)
				result = false;
			else if (s[1] != 255)
				result = false;
			else
			{
				csum = checksum(s);

				if (csum != s[s.Count - 1])
					result = false;
				else
					result = true;
			}

			return result;
		}

		private void Parse(List<int> buff)
		{
			string msg = "Packet received: ";

			for (int i = 0; i < buff.Count; i++)
			{
				msg += buff[i].ToString("X2");
			}

			if (WMR928valid(buff, out _))
			{
				DateTime now = DateTime.Now;
				switch (buff[2])
				{
					case WMR928WindData:
						cumulus.LogDebugMessage("Wind " + msg);
						WMR928Wind(buff);
						break;
					case WMR928RainData:
						cumulus.LogDebugMessage("Rain " + msg);
						WMR928Rain(buff);
						break;
					case WMR928ExtraOutdoorData:
						cumulus.LogDebugMessage("Extra Outdoor " + msg);
						WMR928ExtraOutdoor(buff);
						break;
					case WMR928OutdoorData:
						cumulus.LogDebugMessage("Outdoor " + msg);
						WMR928Outdoor(buff);
						break;
					case WMR928ExtraTempOnlyData:
						cumulus.LogDebugMessage("Extra Temp " + msg);
						WMR928ExtraTempOnly(buff);
						break;
					case WMR928IndoorData:
						cumulus.LogDebugMessage("Indoor " + msg);
						WMR928Indoor(buff);
						break;
					case WMR928IndoorData2:
						cumulus.LogDebugMessage("Indoor2 " + msg);
						WMR928Indoor2(buff);
						break;
					case WMR928MinuteData:
						cumulus.LogDebugMessage("Minute " + msg);
						WMR928Minute(buff);
						break;
					case WMR928ClockData:
						cumulus.LogDebugMessage("Clock " + msg);
						WMR928Clock(buff);
						break;
					default:
						Trace.Write("Unrecognised packet:");
						for (int i = 0; i < buff.Count; i++)
						{
							Trace.Write(" " + buff[i].ToString("X2"));
						}
						cumulus.LogMessage(" ");
						return;
				}

				UpdateStatusPanel(now);
				UpdateMQTT();
			}
			else
			{
				cumulus.LogMessage("Invalid packet:");
				for (int i = 0; i < buff.Count; i++)
				{
					Trace.Write(" " + buff[i].ToString("X2"));
				}
				cumulus.LogMessage(" ");
			}
		}

		private void WMR928ExtraTempOnly(List<int> buff)
		{
			// Extra sensor
			// FF FF 04 bc T2T3 TST1 C1C2
			//  0  1  2  3    4    5    6
			// Battery status b
			// Channel Number c
			// Temperature T1T2T3 (TS gives sign)
			// Checksum C1C2

			int channel = buff[3] & 0xF;
			if (channel == 4)
				channel = 3;

			if ((channel > 3) || (channel < 1))
			{
				cumulus.LogMessage("WMR928 channel error, ch=" + channel);
				channel = 1;
			}

			WMR928ChannelPresent[channel] = true;
			WMR928ExtraTempValueOnly[channel] = true;
			ExtraSensorsDetected = true;

			double temp = ExtractTemp(buff[4], buff[5]);

			WMR928ExtraTempValues[channel] = ConvertTempCToUser(temp);
			DoExtraTemp(WMR928ExtraTempValues[channel], channel);

			if (cumulus.WMR928TempChannel == channel)
			{
				// use this sensor as main temp sensor
				TempBattStatus = buff[3]/16;

				DoOutdoorTemp(WMR928ExtraTempValues[channel], DateTime.Now);

				DoApparentTemp(DateTime.Now);
				DoFeelsLike(DateTime.Now);
				DoHumidex(DateTime.Now);
				DoCloudBaseHeatIndex(DateTime.Now);
			}
		}

		private void WMR928Clock(List<int> buff)
		{
		}

		private void WMR928Minute(List<int> buff)
		{
		}

		private void CheckBatteryStatus()
		{
			if (IndoorBattStatus == 4 || WindBattStatus == 4 || RainBattStatus == 4 || TempBattStatus == 4)
			{
				cumulus.BatteryLowAlarm.Triggered = true;
			}
			else if (cumulus.BatteryLowAlarm.Triggered)
			{
				cumulus.BatteryLowAlarm.Triggered = false;
			}
		}

		private void WMR928ExtraOutdoor(List<int> buff)
		{
			// Extra sensor
			// FF FF 02 bc T2T3 TST1 H1H2 D1D2 C1C2
			//  0  1  2  3    4    5    6    7    8
			// Battery status b
			// Channel Number c
			// Temperature T1T2T3 (TS gives sign)
			// Relative Humidity H1H2
			// Dewpoint D1D2
			// Checksum C1C2

			int channel = buff[3] & 0xF;
			if (channel == 4)
				channel = 3;

			if ((channel > 3) || (channel < 1))
			{
				cumulus.LogMessage("WMR928 channel error, ch=" + channel);
				channel = 1;
			}

			WMR928ChannelPresent[channel] = true;
			ExtraSensorsDetected = true;

			int hum = BCDchartoint(buff[6]);
			WMR928ExtraHumValues[channel] = hum;
			DoExtraHum(hum, channel);

			double temp = ExtractTemp(buff[4], buff[5]);

			WMR928ExtraTempValues[channel] = ConvertTempCToUser(temp);
			DoExtraTemp(WMR928ExtraTempValues[channel], channel);

			WMR928ExtraDPValues[channel] = ConvertTempCToUser(BCDchartoint(buff[7]));
			ExtraDewPoint[channel] = ConvertTempCToUser(BCDchartoint(buff[7]));

			if (cumulus.WMR928TempChannel == channel)
			{
				// use this sensor as main temp and hum sensor
				TempBattStatus = buff[3]/16;

				// Extract humidity
				DoOutdoorHumidity(BCDchartoint(buff[6]), DateTime.Now);

				DoOutdoorTemp(ConvertTempCToUser(temp), DateTime.Now);

				DoOutdoorDewpoint(ConvertTempCToUser(BCDchartoint(buff[7])), DateTime.Now);
			}
		}

		private void WMR928Wind(List<int> buff)
		{
			// Wind speed and direction
			// FF FF 00 b0 B2B3 S3B1 S1S2 A2A3 WSA1 W1W2 C1C2
			// 0  1  2  3  4    5    6    7    8    9    10
			// Battery status b
			// Wind Bearing B1B2B3
			// Wind Gust   S1S2S3
			// Wind Speed Average A1A2A3
			// Wind Chill W1W2 (WS gives sign)
			// Checksum C1C2

			WindBattStatus = buff[3]/16;

			double current = (double) (BCDchartoint(buff[5])/10 + (BCDchartoint(buff[6])*10))/10;
			double average = (double) (BCDchartoint(buff[7]) + ((BCDchartoint(buff[8])%10)*100))/10;
			int bearing = BCDchartoint(buff[4]) + ((BCDchartoint(buff[5])%10)*100);

			DoWind(ConvertWindMSToUser(current), bearing, ConvertWindMSToUser(average), DateTime.Now);

			// Extract wind chill
			double wc = BCDchartoint(buff[9]);

			if (buff[9]/16 == 8) wc = -wc;

			DoWindChill(ConvertTempCToUser(wc), DateTime.Now);
		}

		private void WMR928Rain(List<int> buff)
		{
			// Rain gauge
			// FF FF 01 b0 R1R2 ?? T3T4 T1T2 Y3Y4 Y1Y2 M1M2 H1H2 D1D2 M1M2 Y1Y2 C1C2
			// 0  1  2  3  4    5  6    7    8    9    10   11   12   13   14   15
			// Battery status b
			// Rainfall Rate R1R2
			// Rainfall Total T1T2T3T4
			// Rainfall Yesterday Y1Y2Y3Y4
			// Rainfall Totalling Start (Minute M1M2,
			//                           Hour H1H2,
			//                           Day D1D2,
			//                           Month M1M2,
			//                           Year Y1Y2)
			// Checksum C1C2
			// the unknown byte has values such as 0x80 or 0x90

			RainBattStatus = buff[3]/16;
			// MainForm.Rainbatt.Position := (15-rain_batt_status)*100 DIV 15;

			double raincounter = ConvertRainMMToUser(BCDchartoint(buff[6]) + (BCDchartoint(buff[7])*100));
			double rainrate = ConvertRainMMToUser(BCDchartoint(buff[4]) + ((BCDchartoint(buff[5])%10)*100));

			DoRain(raincounter, rainrate, DateTime.Now);
		}

		private void WMR928Outdoor(List<int> buff)
		{
			// Outdoor temperature sensor
			// FF FF 03 bc T2T3 TST1 H1H2 D1D2 C1C2
			// 0  1  2  3  4    5    6    7    8
			// Battery status b
			// Channel Number c
			// Temperature T1T2T3 (TS gives sign)
			// Relative Humidity H1H2
			// Dewpoint D1D2
			// Checksum C1C2

			if (cumulus.WMR928TempChannel == 0)
			{
				TempBattStatus = buff[3]/16;

				// Extract humidity
				int hum = BCDchartoint(buff[6]);
				DoOutdoorHumidity(hum, DateTime.Now);

				// Extract temperature
				double temp = ExtractTemp(buff[4], buff[5]);

				DoOutdoorTemp(ConvertTempCToUser(temp), DateTime.Now);

				// Extract dewpoint
				DoOutdoorDewpoint(ConvertTempCToUser(BCDchartoint(buff[7])), DateTime.Now);

				DoApparentTemp(DateTime.Now);
				DoFeelsLike(DateTime.Now);
				DoHumidex(DateTime.Now);
				DoCloudBaseHeatIndex(DateTime.Now);
			}
		}

		private void WMR928Indoor(List<int> buff)
		{
			// Indoor Baro-Thermo-Hygrometer
			// FF FF 06 b0 T3T4 T1T2 H1H2 D1D2 P1P2 F1F2 ?? S3S4 S1S2 C1C2
			// 0   1  2  3 4    5    6    7    8    9    10 11   12   13
			// Battery status b
			// Temperature T1T2T3T4
			// Relative Humidity H1H2
			// Dewpoint D1D2
			// Ambient Pressure P1P2
			// Forecast F1F2 (F2 unknown with values like 1, 5, 9)
			// Sea-Level Offset S1S2S3S4
			// Checksum C1C2
			// the unknown byte seems to be fixed at 00

			IndoorBattStatus = buff[3]/16;

			// Extract temp (tenths of deg C) in BCD; bytes 4 (LSB) and 5 (MSB)
			double temp10 = BCDchartoint(buff[4]) + (BCDchartoint(buff[5])*100);

			DoIndoorTemp(temp10/10);

			// humidity in BCD; byte 6
			DoIndoorHumidity(BCDchartoint(buff[6]));

			// local pressure (not BCD); byte 8, with 856mb offset
			double loc = buff[8] + 856;
			StationPressure = ConvertPressMBToUser(loc);
			double num = BCDchartoint((buff[10])/10) + BCDchartoint(buff[11])*10 + (BCDchartoint(buff[12])*1000);
			double slcorr = num/10.0 - 600;

			DoPressure(ConvertPressMBToUser(loc + slcorr), DateTime.Now);

			UpdatePressureTrendString();

			string forecast = String.Empty;

			// forecast - top 4 bits of byte 9
			int fcnum = buff[9]/16;
			switch (fcnum)
			{
				case 2:
					forecast = "Cloudy";
					break;
				case 3:
					forecast = "Rain";
					break;
				case 6:
					forecast = "Partly Cloudy";
					break;
				case 12:
					forecast = "Clear";
					break;
			}

			DoForecast(forecast, false);
		}

		private void WMR928Indoor2(List<int> buff)
		{
			// Indoor Baro-Thermo-Hygrometer  (alternative for some types of station)
			// FF FF 05 b0 T3T4 T1T2 H1H2 D1D2 P1P2 F1F2 S3S4 S1S2 C1C2
			// 0  1  2  3  4    5    6    7    8    9    10   11   12
			// Battery status b
			// Temperature T2T3T4, T1 is sign
			// Relative Humidity H1H2
			// Dewpoint D1D2
			// Ambient Pressure P1P2
			// Forecast F1F2 (F2 unknown with values like 1, 5, 9)
			// Sea-Level Offset S1S2S3.S4
			// Checksum C1C2
			// the unknown byte seems to be fixed at 00

			IndoorBattStatus = buff[3]/16;
			//MainForm.Indoorbatt.Position := (15-indoor_batt_status)*100 DIV 15;

			// Extract temp (tenths of deg C) in BCD;
			double temp = ExtractTemp(buff[4], buff[5]);

			DoIndoorTemp(temp);

			// humidity in BCD; byte 6
			DoIndoorHumidity(BCDchartoint(buff[6]));

			// local pressure (not BCD); byte 8, with 795mb offset
			double loc = buff[8] + 795;
			StationPressure = ConvertPressMBToUser(loc);
			// SL pressure correction; bytes 10 (LSB) and 11 (MSB)
			double num = BCDchartoint(buff[10]/10) + (BCDchartoint(buff[11])*10) + buff[8];
			DoPressure(num, DateTime.Now);

			UpdatePressureTrendString();
			// forecast - bottom 4 bits of byte 9
			string forecast = String.Empty;

			int fcnum = buff[9] & 16;
			switch (fcnum)
			{
				case 2:
					forecast = "Cloudy";
					break;
				case 3:
					forecast = "Rain";
					break;
				case 6:
					forecast = "Partly Cloudy";
					break;
				case 12:
					forecast = "Clear";
					break;
			}

			DoForecast(forecast, false);
		}

		private double ExtractTemp(int byteOne, int byteTwo)
		{
			double temp10 = BCDchartoint(byteOne) + ((BCDchartoint(byteTwo) % 10) * 100);
			if (byteTwo / 16 == 8) temp10 = -temp10;
			return temp10 / 10;
		}

	}
}
