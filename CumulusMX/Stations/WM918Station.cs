using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace CumulusMX.Stations
{
	class WM918Station : WeatherStation
	{
		private const int WM918HumidData = 0x8F;
		private const int WM918TempData = 0x9F;
		private const int WM918BaroData = 0xAF;
		private const int WM918RainData = 0xBF;
		private const int WM918WindData = 0xCF;

		private readonly int[] WM918PacketLength = [0, 0, 0, 0, 0, 0, 0, 0, 35, 34, 31, 14, 27, 0, 0, 0, 255];

		private int currentPacketLength;
		private int currentPacketType;
		private bool stop;

		public WM918Station(Cumulus cumulus)
			: base(cumulus)
		{
			cumulus.Manufacturer = Cumulus.StationManufacturer.OREGON;
			// station supplies rain rate
			calculaterainrate = false;

			cumulus.LogMessage("Station type = WM918");

			startReadingHistoryData();
		}

		// Not used - now uses polling in Start() because Mono doesn't support data received events
		/*
		public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			// Obtain the number of bytes waiting in the port's buffer
			if (!(sender is SerialPort port))
				return;

			int bytes = port.BytesToRead;

			int nextByte;

			// Create a byte array buffer to hold the incoming data
			//byte[] buffer = new byte[bytes];

			for (int i = 0; i < bytes; i++)
			{
				// Read a byte from the port
				nextByte = port.ReadByte();

				switch (currentPacketLength)
				{
					case 0: // We're looking for the start of a packet
						if (nextByte>=0x8F && nextByte<=0xCF)
						{
							// Possible start of packet
							currentPacketLength = 1;
							currentPacketType = nextByte>>4;
							buffer.Add(nextByte);
						}
						break;

					default: // We've had the packet header, continue collecting the packet
						buffer.Add(nextByte);
						currentPacketLength++;

						if (currentPacketLength == WM918PacketLength[currentPacketType])
						{
							// We've collected a complete packet, process it
							Parse(buffer);
							// Get ready for the next packet
							buffer.Clear();
							currentPacketLength = 0;
							currentPacketType = 16;
						}
						break;
				} // end of switch for current packet length
			} // end of for loop for available chars
		}
		*/

		public override void Start()
		{
			cumulus.LogMessage("Start normal reading loop");

			int nextByte;

			try
			{
				do
				{
					Thread.Sleep(1000);
					if (comport.BytesToRead > 0)
					{
						// wait a little to let more data in
						Thread.Sleep(200);
						// Obtain the number of bytes waiting in the port's buffer
						var bytes = comport.BytesToRead;

						var datastr = "Data: ";

						cumulus.LogDebugMessage("Data received, number of bytes = " + bytes);

						if (stop) break;

						// Create a byte array buffer to hold the incoming data
						//byte[] buffer = new byte[bytes];

						for (var i = 0; i < bytes; i++)
						{
							// Read a byte from the port
							nextByte = comport.ReadByte();

							switch (currentPacketLength)
							{
								case 0: // We're looking for the start of a packet
									if (nextByte >= 0x8F && nextByte <= 0xCF)
									{
										// Possible start of packet
										currentPacketLength = 1;
										currentPacketType = nextByte >> 4;
										buffer.Add(nextByte);
									}
									break;

								default: // We've had the packet header, continue collecting the packet
									buffer.Add(nextByte);
									currentPacketLength++;

									if (currentPacketLength == WM918PacketLength[currentPacketType])
									{
										// We've collected a complete packet, process it

										Parse(buffer);
										// Get ready for the next packet
										buffer.Clear();
										currentPacketLength = 0;
										currentPacketType = 16;
									}
									break;
							} // end of switch for current packet length
						} // end of for loop for available chars

						cumulus.LogDebugMessage(datastr);
					}
				} while (!stop);
			}
			// Catch the ThreadAbortException
			catch (ThreadAbortException)
			{
				// do nothing
			}
			finally
			{
				cumulus.LogMessage("Closing serial port");
				comport.Close();
			}
		}

		public override void startReadingHistoryData()
		{
			cumulus.LogMessage("Opening COM port " + cumulus.ComportName);

			comport = new SerialPort(cumulus.ComportName, 9600, Parity.None, 8, StopBits.One)
			{
				Handshake = Handshake.None,
				RtsEnable = true,
				DtrEnable = true
			};

			//comport.DataReceived += new SerialDataReceivedEventHandler(portDataReceived);

			try
			{
				comport.Open();
				cumulus.NormalRunning = true;

				LoadLastHoursFromDataLogs(DateTime.Now);

				DoDayResetIfNeeded();
				DoTrendValues(DateTime.Now);
				timerStartNeeded = true;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error starting station: " + ex.Message);
			}
		}


		public override void Stop()
		{
			stop = true;
			StopMinuteTimer();
		}

		/// <summary>
		/// Validates a WM918 packet
		/// </summary>
		/// <param name="s"></param>
		/// <param name="csum"></param>
		/// <returns></returns>
		private bool WM918valid(List<int> s, out int csum)
		{
			bool result;

			csum = -1;

			if (s.Count < 14)
			{
				cumulus.LogWarningMessage("WM918 packet too short. Length = " + s.Count);
				result = false;
			}
			else
			{
				csum = checksum(s);

				if (csum != s[^1])
				{
					cumulus.LogErrorMessage("Invalid checksum. Expected " + csum + ", got " + s[^1]);
					result = false;
				}
				else
					result = true;
			}
			return result;
		}

		/// <summary>
		/// Determines the type of packet received
		/// </summary>
		/// <param name="buff"></param>
		private void Parse(List<int> buff)
		{
			if (WM918valid(buff, out _))
			{
				var now = DateTime.Now;

				switch (buff[0])
				{
					case WM918HumidData:
						WM918Humid(buff);
						break;
					case WM918RainData:
						WM918Rain(buff);
						break;
					case WM918TempData:
						WM918Temp(buff);
						break;
					case WM918BaroData:
						WM918Baro(buff);
						break;
					case WM918WindData:
						WM918Wind(buff);
						break;
					default:
						cumulus.LogMessage("Unrecognised packet type: " + buff[0].ToString("X2"));
						for (var i = 0; i < buff.Count; i++)
						{
							cumulus.LogMessage(" " + buff[i].ToString("X2"));
						}
						cumulus.LogMessage(" ");
						return;
				}
				UpdateStatusPanel(now.ToUniversalTime());
				UpdateMQTT();
			}
			else
			{
				cumulus.LogMessage("Invalid packet:");
				for (var i = 0; i < buff.Count; i++)
				{
					cumulus.LogMessage(" " + buff[i].ToString("X2"));
				}
				cumulus.LogMessage(" ");
			}
		}

		private void WM918Wind(List<int> buff)
		{
			// CF S2S3 B3S1 B1B2 A2A3 xxA1     W1W2     WS      C1C2
			// 0  1    2    3    4    5    ... 16   ... 21  ... 26
			// Battery status b
			// Wind Bearing B1B2B3
			// Wind Gust   S1S2.S3
			// Wind Speed Average A1A2.A3
			// Wind Chill W1W2 (WS bit 1 gives sign)
			// Checksum C1C2

			var current = ConvertUnits.WindMSToUser((double) (BCDchartoint(buff[1]) + BCDchartoint(buff[2]) % 10 * 100) / 10);
			var average = ConvertUnits.WindMSToUser((double) (BCDchartoint(buff[4]) + BCDchartoint(buff[5]) % 10 * 100) / 10);
			var bearing = BCDchartoint(buff[2]) / 10 + BCDchartoint(buff[3]) * 10;

			DoWind(current, bearing, average, DateTime.Now);

			// Extract wind chill
			var wc = BCDchartoint(buff[16]);

			if ((buff[21] / 16 & 2) == 2) wc = -wc;

			if (wc > -70)
			{
				DoWindChill(ConvertUnits.TempCToUser(wc), DateTime.Now);
			}
		}

		private void WM918Baro(List<int> buff)
		{
			// Pressure/DewPoint etc
			// AF P3P4 P1P2 S4SD S2S3 xxS1 xxF1 I1I2     O1O2    C1C2
			// 0  1    2    3    4    5    6    7    ... 18   ...30
			// Indoor Dewpoint I1I2
			// Outdoor Dewpoint O1O2
			// Ambient Pressure P1P2P3P4
			// Forecast F1 1=Sunny, 2=Cloudy, 4=Partly, 8=Rain
			// Sea-Level pressure S1S2S3S4.SD
			// Checksum C1C2

			DoOutdoorDewpoint(ConvertUnits.TempCToUser(BCDchartoint(buff[18])), DateTime.Now);

			double locPress = BCDchartoint(buff[1]) + BCDchartoint(buff[2]) * 100;
			DoStationPressure(ConvertUnits.PressMBToUser(locPress));

			var pressure = ConvertUnits.PressMBToUser((double) (BCDchartoint(buff[3]) / 10) + BCDchartoint(buff[4]) * 10 +
				BCDchartoint(buff[5]) % 10 * 1000);

			DoPressure(pressure, DateTime.Now);

			var forecast = string.Empty;

			// Forecast
			var num = buff[6] & 0xF;
			switch (num)
			{
				case 1:
					forecast = "Sunny";
					break;
				case 2:
					forecast = "Cloudy";
					break;
				case 4:
					forecast = "Partly Cloudy";
					break;
				case 8:
					forecast = "Rain";
					break;
			}

			DoForecast(forecast, false);
		}

		private void WM918Temp(List<int> buff)
		{
			// 9F I2I3 xxI1    O2O3 xxO1
			// 0  1    2   ... 16   17
			// Indoor Temp I1I2I3 but top bit of I1 gives sign
			// Outdoor Temp O1O2O3 but top bit of O1 gives sign

			// Outdoor temp
			double temp10 = BCDchartoint(buff[16]) + (buff[17] & 0x7) * 100;

			if ((buff[17] & 0x08) == 8) temp10 = -temp10;

			if (temp10 > -500)
			{
				DoOutdoorTemp(ConvertUnits.TempCToUser(temp10 / 10), DateTime.Now);
			}

			// Indoor temp
			temp10 = BCDchartoint(buff[1]) + (buff[2] & 0x7) * 100;

			if ((buff[2] & 0x08) == 8) temp10 = -temp10;

			DoIndoorTemp(ConvertUnits.TempCToUser(temp10 / 10));

			DoApparentTemp(DateTime.Now);
			DoFeelsLike(DateTime.Now);
			DoHumidex(DateTime.Now);
			DoCloudBaseHeatIndex(DateTime.Now);
		}

		private void WM918Rain(List<int> buff)
		{
			// BF R1R2    Y3Y4 Y1Y2 T3T4 T1T2 M1M2 H1H2 D1D2 M1M2           C1C2
			// 0  1    2  3    4    5    6    7    8    9    10   11   12   13
			// Rainfall Rate R1R2
			// Rainfall Total T1T2T3T4
			// Rainfall Yesterday Y1Y2Y3Y4
			// Rainfall Totalling Start (Minute M1M2,
			//                           Hour H1H2,
			//                           Day D1D2,
			//                           Month M1M2)
			// Checksum C1C2

			var raincounter = ConvertUnits.RainMMToUser((double) BCDchartoint(buff[5]) + BCDchartoint(buff[6]) * 100);
			var rainrate = ConvertUnits.RainMMToUser((double) BCDchartoint(buff[1]) + BCDchartoint(buff[2]) % 10 * 100);

			DoRain(raincounter, rainrate, DateTime.Now);
		}

		private void WM918Humid(List<int> buff)
		{
			// 8F ...
			// Indoor humidity is BCD in byte 8
			// Outdoor humidity is BCD in byte 20

			DoIndoorHumidity(BCDchartoint(buff[8]));

			DoOutdoorHumidity(BCDchartoint(buff[20]), DateTime.Now);
		}
	}
}
