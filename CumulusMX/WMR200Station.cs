using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Threading;
using System.Timers;
using HidSharp;
using Timer = System.Timers.Timer;

namespace CumulusMX
{
	internal sealed class WMR200Station : WeatherStation
	{
		private readonly DeviceList devicelist;
		private readonly HidDevice station;
		private readonly HidStream stream;
		private const int Vendorid = 0x0FDE;
		private const int Productid = 0xCA01;
		private const int HISTORY_AVAILABLE_PACKET_TYPE = 0xD1;
		private const int HISTORY_DATA_PACKET_TYPE = 0xD2;
		private const int WIND_PACKET_TYPE = 0xD3;
		private const int RAIN_PACKET_TYPE = 0xD4;
		private const int UV_PACKET_TYPE = 0xD5;
		private const int BARO_PACKET_TYPE = 0xD6;
		private const int TEMPHUM_PACKET_TYPE = 0xD7;
		private const int STATUS_PACKET_TYPE = 0xD9;
		private const int CLEAR_LOGGER_PACKET_TYPE = 0xDB;
		private const int STOP_COMMS_PACKET_TYPE = 0xDF;

		private readonly byte[] PacketBuffer;
		private int CurrentPacketLength;
		private const int PacketBufferBound = 255;
		//private bool ArchiveDataAvailable = false;
		//private bool ArchiveDataDownloaded = false;
		//private int ArchiveDataCount = 0;
		private bool FirstArchiveData = true;
		private bool midnightraindone;
		private double HourInc;
		private int rollHour;
		private bool rolloverdone;
		private DateTime PreviousHistoryTimeStamp;
		private bool GettingHistory;
		private int LivePacketCount;
		private readonly byte[] usbbuffer = new byte[9];

		private readonly Timer HearbeatTimer;

		public WMR200Station(Cumulus cumulus)
			: base(cumulus)
		{
			cumulus.Manufacturer = cumulus.OREGONUSB;
			devicelist = DeviceList.Local;
			station = devicelist.GetHidDeviceOrNull(Vendorid, Productid);

			if (station != null)
			{
				cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " WMR200 station found");
				Console.WriteLine("WMR200 station found");

				if (station.TryOpen(out stream))
				{
					cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " Stream opened");
					Console.WriteLine("Connected to station");
				}

				PacketBuffer = new byte[PacketBufferBound];

				WMR200ExtraTempValues = new double[11];
				WMR200ExtraHumValues = new double[11];
				WMR200ChannelPresent = new bool[11];
				WMR200ExtraDPValues = new double[11];

				HearbeatTimer = new Timer(30000);
				HearbeatTimer.Elapsed += HearbeatTimerProc;
				HearbeatTimer.Start();

				startReadingHistoryData();
			}
			else
			{
				cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " WMR200 station not found!");
				Console.WriteLine("WMR200 station not found!");
			}
		}

		private void HearbeatTimerProc(object sender, ElapsedEventArgs e)
		{
			if (cumulus.logging)
			{
				cumulus.LogMessage("Sending heartbeat");
			}

			SendHeartBeat();
		}

		public override void startReadingHistoryData()
		{
			cumulus.CurrentActivity = "Getting archive data";
			Console.WriteLine("Reading archive data");
			//lastArchiveTimeUTC = getLastArchiveTime();

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			bw = new BackgroundWorker();
			bw.DoWork += bw_DoWork;
			bw.RunWorkerCompleted += bw_RunWorkerCompleted;
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();
		}

		public override void Stop()
		{
		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
		}

		public override void getAndProcessHistoryData()
		{
			cumulus.LogMessage("Start reading history data");
			Console.WriteLine("Start reading history data...");
			//DateTime timestamp = DateTime.Now;
			//LastUpdateTime = DateTime.Now; // lastArchiveTimeUTC.ToLocalTime();
			cumulus.LogMessage("Last Update = " + cumulus.LastUpdateTime);

			// set up controls for end of day rollover
			if (cumulus.RolloverHour == 0)
			{
				rollHour = 0;
				HourInc = 0;
			}
			else if (cumulus.Use10amInSummer && (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now)))
			{
				// Locale is currently on Daylight time
				rollHour = cumulus.RolloverHour + 1;
				HourInc = -10;
			}
			else
			{
				// Locale is currently on Standard time or unknown
				rollHour = cumulus.RolloverHour;
				HourInc = -9;
			}

			int luhour = cumulus.LastUpdateTime.Hour;

			rolloverdone = luhour == rollHour;

			midnightraindone = luhour == 0;

			GettingHistory = true;
			LivePacketCount = 0;
			StartLoop();
			SendReset();
			SendHeartBeat();
		}

		/// <summary>
		///     Read and process data in a loop, sleeping between reads
		/// </summary>
		public override void Start()
		{
			cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " Start loop");
			int numBytes;
			int responseLength;
			int startByte;
			int offset;

			// Returns 9-byte usb packet, with report ID in first byte
			responseLength = 9;
			startByte = 1;
			offset = 0;

			try
			{
				while (true)
				{
					cumulus.LogDataMessage("Calling Read");

					try
					{
						numBytes = stream.Read(usbbuffer, offset, responseLength);
						cumulus.LogDataMessage("numBytes = " + numBytes);

						String Str = "";

						for (int I = startByte; I < responseLength; I++)
						{
							Str = Str + " " + usbbuffer[I].ToString("X2");
						}
						cumulus.LogDataMessage(DateTime.Now.ToLongTimeString() + Str);

						if ((CurrentPacketLength == 0) // expecting a new packet
							&& !((usbbuffer[2] >= HISTORY_AVAILABLE_PACKET_TYPE) && (usbbuffer[2] <= TEMPHUM_PACKET_TYPE))
							&& usbbuffer[2] != STATUS_PACKET_TYPE
							&& usbbuffer[2] != CLEAR_LOGGER_PACKET_TYPE
							&& usbbuffer[2] != STOP_COMMS_PACKET_TYPE)

						{
							cumulus.LogDebugMessage("Out of sync; discarding");

							//SendDA;
							// CodeSite.ExitMethod('HidControlDeviceData');
						}
						else
						{
							// Get length of valid data in this USB frame
							var datalength = usbbuffer[1];

							// Copy valid data to buffer
							for (int i = 1; i <= datalength; i++)
							{
								PacketBuffer[CurrentPacketLength] = usbbuffer[i + 1];
								CurrentPacketLength++;
							}

							if ((CurrentPacketLength > 1)
								// We must have at least received the pkt length to
								// determine the logic following in this "if" condition.
								&& (CurrentPacketLength >= PacketBuffer[1])
								// Collected length must be at least expected packet
								// length or greater.
								&&
								(((PacketBuffer[0] >= HISTORY_DATA_PACKET_TYPE) && (PacketBuffer[0] <= TEMPHUM_PACKET_TYPE)) ||
								 PacketBuffer[0] == STATUS_PACKET_TYPE))
							{
								// Process the packet
								ProcessWMR200Packet();
								RemovePacketFromBuffer();

								// Check for a history data available packet on the end of the current data
								if ((CurrentPacketLength == 1) && (PacketBuffer[0] == HISTORY_AVAILABLE_PACKET_TYPE))
								{
									//ArchiveDataAvailable = true;
									if (cumulus.logging)
									{
										cumulus.LogMessage("HISTORY_AVAILABLE_PACKET_TYPE");
										cumulus.LogMessage("Sending DA response");
									}
									SendDA();
								}

								ClearPacketBuffer();
							}
							else if (CurrentPacketLength == 1)
							{
								// process 1-byte packets
								if
									((PacketBuffer[0] == HISTORY_AVAILABLE_PACKET_TYPE) ||
									 (PacketBuffer[0] == CLEAR_LOGGER_PACKET_TYPE) ||
									 (PacketBuffer[0] == STOP_COMMS_PACKET_TYPE))
								{
									switch (PacketBuffer[0])
									{
										case HISTORY_AVAILABLE_PACKET_TYPE:
											if (cumulus.logging)
											{
												cumulus.LogMessage("HISTORY_AVAILABLE_PACKET_TYPE");
											}
											break;
										case CLEAR_LOGGER_PACKET_TYPE:
											if (cumulus.logging)
											{
												cumulus.LogMessage("CLEAR_LOGGER_PACKET_TYPE");
											}
											break;
										case STOP_COMMS_PACKET_TYPE:
											if (cumulus.logging)
											{
												cumulus.LogMessage("STOP_COMMS_PACKET_TYPE");
											}
											break;
									}

									if (PacketBuffer[0] == HISTORY_AVAILABLE_PACKET_TYPE)
									{
										//ArchiveDataAvailable = true;
										if (cumulus.logging)
										{
											cumulus.LogMessage("Sending DA response");
										}
										SendDA();
									}

									ClearPacketBuffer();
								}
								else
								{
									// cumulus.LogMessage('Unrecognised 1-byte packet: ' + Format('%.2x ', [PacketBuffer[0]]));
								}
							}
						}
					}
					catch
						(Exception ex)
					{
						// Might just be a timeout, which is normal, so debug log only
						cumulus.LogDebugMessage("Data read loop: " + ex.Message);
					}
				}
				//Thread.Sleep(100);
			}

			// Catch the ThreadAbortException
			catch (ThreadAbortException)
			{
			}
		}

		private void bw_DoWork(object sender, DoWorkEventArgs e)
		{
			getAndProcessHistoryData();
		}

		private void SendReset()
		{
			if (cumulus.DataLogging)
			{
				cumulus.LogMessage("Sending reset");
			}

			byte[] reset;

			if (cumulus.IsOSX)
			{
				reset = new byte[] { 0x20, 0x00, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00 };
			}
			else
			{
				reset = new byte[] {0x00, 0x20, 0x00, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00};
			}

			stream.Write(reset);
		}

		private void SendHeartBeat()
		{
			byte[] heartbeat;

			if (cumulus.IsOSX)
			{
				heartbeat = new byte[] { 0x01, 0xd0, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00 };
			}
			else
			{
				heartbeat = new byte[] { 0x00, 0x01, 0xd0, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00 };
			}

			stream.Write(heartbeat);
		}

		private void SendDA()
		{
			byte[] da;

			if (cumulus.IsOSX)
			{
				da = new byte[] { 0x01, 0xda, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00 };
			}
			else
			{
				da = new byte[] { 0x00, 0x01, 0xda, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00 };
			}

			stream.Write(da);
		}

		/**
		 * Check the CRC for the packet at the start of the packet buffer
		 **/

		private Boolean CRCOK()
		{
			var packetLen = PacketBuffer[1];

			if (packetLen < 3)
			{
				return true;
			}
			else
			{
				// packet CRC is in last two bytes, low byte then high byte
				var packetCRC = (PacketBuffer[packetLen - 1]*256) + PacketBuffer[packetLen - 2];

				var calculatedCRC = 0;

				// CRC is calulated by summing all but the last two bytes
				for (int i = 0; i < packetLen - 2; i++)
				{
					calculatedCRC += PacketBuffer[i];
				}

				return (packetCRC == calculatedCRC);
			}
		}

		private void ClearPacketBuffer()
		{
			for (int I = 0; I < PacketBufferBound; I++)
			{
				PacketBuffer[I] = 0;
			}
			CurrentPacketLength = 0;
		}

		private void RemovePacketFromBuffer()
		{
			// removes packet from start of buffer
			// there might be a partial packet behind it
			int actualpacketlength = PacketBuffer[1];
			int overflow = CurrentPacketLength - actualpacketlength;
			if (overflow == 0)
			{
				// only one packet in buffer, clear it
				ClearPacketBuffer();
			}
			else
			{
				// need to move surplus data to start of packet
				for (int I = 0; I < overflow; I++)
				{
					PacketBuffer[I] = PacketBuffer[actualpacketlength + I];
				}
				CurrentPacketLength = overflow;
				string Str = " ";
				for (int I = 0; I < CurrentPacketLength; I++)
				{
					Str += PacketBuffer[I].ToString("X2");
				}
				if (cumulus.logging)
				{
					cumulus.LogMessage(DateTime.Now.ToLongTimeString() + Str);
				}
			}
		}

		private void ProcessStatusPacket()
		{
			//
			// ----> Status----> CHSUM
			// 00 01 02 03 04 05 06 07 ...byte nr
			// ID LL aa bb cc dd cL-cH ...notation
			// ----- -- -- -- -- -----
			// d9 08 00 00 00 00 17 00 ...data
			// -----------------------
			//
			// HEADER ------
			// Byte 00: (ID) [D9] Packet Id
			// Byte 01: (LL) [08] Packet Length = 8 bytes
			//
			// STATUS ------
			// [8421 8421]
			// Byte 02:  (H) [1... ....] = [#80]
			// [.1.. ....] = [#40]
			// [..1. ....] = [#20]
			// [...1 ....] = [#10]
			// (L) [.... 1...] = [#08]
			// [.... .1..] = [#04]
			// [.... ..1.] = [#02] Sensor fault: Sensor 1 (temp/hum outdoor)
			// [.... ...1] = [#01] Sensor fault: Wind (---)
			//
			// Byte 03:  (H) [1... ....] = [#80]
			// [.1.. ....] = [#40]
			// [..1. ....] = [#20] Sensor fault: UV   (--)
			// [...1 ....] = [#10] Sensor fault: Rain (---)
			// (L) [.... 1...] = [#08]
			// [.... .1..] = [#04]
			// [.... ..1.] = [#02]
			// [.... ...1] = [#01]
			//
			// Byte 04:  (H) [1... ....] = [#80] RF Signal Weak: Console Clock (time not synchronized)
			// [.1.. ....] = [#40]
			// [..1. ....] = [#20]
			// [...1 ....] = [#10]
			// (L) [.... 1...] = [#08]
			// [.... .1..] = [#04]
			// [.... ..1.] = [#02] Battery Low: Sensor 1 (temp/hum outdoor)
			// [.... ...1] = [#01] Battery Low: Wind ... to be confirmed)
			//
			// Byte 05:  (H) [1... ....] = [#80]
			// [.1.. ....] = [#40]
			// [..1. ....] = [#20] Battery Low: UV ..... (below 2.4v out of 3.0v)
			// [...1 ....] = [#10] Battery Low: Rain ... to be confirmed)
			// (L) [.... 1...] = [#08]
			// [.... .1..] = [#04]
			// [.... ..1.] = [#02]
			// [.... ...1] = [#01]
			//
			// CHECKSUM ---- Add up the bytes 00 to 05 == ((256*cH) + cL)
			// Byte 06: (cL) Check-sum low  byte
			// Byte 07: (cH) Check-sum high byte
		}

		private void ProcessTempHumPacket()
		{
			//
			// ----> Time/Date----> TEMP/HUM/DEW-------> CHSUM
			// 00 01 02 03 04 05 06 07 08 09 10 11 12 13 14 15 ...byte nr
			// ID LL mm-hh dd-mm-yy tS TT-sT HU DD-sD HI cL-cH ...notation
			// ----- -------------- -- ----- -- ----- -- -----
			// d7 10 2f 0d 06 0c 0a 91 22 01 1b 14 80 52 8a 02 ...data
			// -----------------------------------------------
			//
			// DECODED:
			// Packet Id .......... [D7]
			// Packet Length ...... [10] 16 bytes
			// Time ............... [0d:2f] 13:47
			// Date ............... [06-0c-0a] 06-12-2010
			// Trend .............. [9] Temp falling / Humidity rising
			// Sensor Nr .......... [1] 1
			// Temperature ........ [0122] +21.1°C
			// Humidity ........... [1b] 27%
			// Dewpoint ........... [8014] -2.0°C
			// Heat Index ......... [52] 82°F (27.78°C)
			// Check-sum .......... [028a]
			// ----------------------------------------------------------------------------------------------
			//
			// HEADER ------
			// Byte 00: (ID) [D7] Packet Id
			// Byte 01: (LL) [10] Packet Length = 16 bytes
			//
			// DATE/TIME ---
			// Byte 02: (mm) Minute
			// Byte 03: (hh) Hour
			// Byte 04: (dd) Day
			// Byte 05: (mm) Month
			// Byte 06: (yy) Year
			//
			// TEMP/HUM ----
			// Byte 07:  (t) byte(07:H) .. Temp/Hum trend/icon.
			// [00..] = [#0] = 0: Temperature ...... steady
			// [01..] = [#4] = 1: Temperature ...... rising
			// [10..] = [#8] = 2: Temperature ...... falling
			//
			// [..00] = [#0] = 0: Humidity ......... steady
			// [..01] = [#1] = 1: Humidity ......... rising
			// [..10] = [#2] = 2: Humidity ......... falling
			//
			// tTemp = byte(07) >> 6 .......... or: tTemp =  byte(07) / 64
			// tHum  = byte(07) >> 4 & 0x03 ... or: tHum  = (byte(07) / 16) & 0x03
			//
			// (S) byte(07:L) .. Sensor Number
			// 0 = WMR200 Console temp/humidity (indoor)
			// 1 = Extra sensor #1 temp/humidity (outdoor)
			// . ... up to 10 sensors ...
			// 10 = Extra sensor #10 temp/humidity
			//
			// nSensor = byte(07) & 0x0F
			//
			// Byte 08: (TT) byte(08)   .. Temperature low byte  +
			// Byte 09:  (T) byte(09:L) .. Temperature high nibble
			// (s) byte(09:H) .. Temperature sign ...... #0 = positif / #8 = negative
			//
			// Temp_sign = ((byte(09) >> 8)==0x08)? -1 : 1;
			// TEMPERATURE in °C = Temp_sign * (((byte(09:L)*256) + byte(08)) / 10)
			// TEMPERATURE in °F = ((TEMPERATURE in °C) * 1.8) + 32
			//
			// Byte 10: (HU) Humidity %
			//
			// Byte 11: (DD) byte(11)   .. Dew Point low byte  +
			// Byte 12:  (D) byte(12:L) .. Dew Point high nibble
			// (s) byte(12:H) .. Dew Point sign ...... #0 = positif / #8 = negative
			//
			// Dewp_sign = ((byte(12) >> 4)==0x08)? -1 : 1;
			// DEWPOINT in °C = Dewp_sign * (((byte(12:L)*256) + byte(11)) / 10)
			// DEWPOINT in °F = ((DEWPOINT in °C) * 1.8) + 32
			//
			// Byte 13: (HI) Heat Index: (in Fahrenheit)
			// .... Reported when temp is > 80°F (26.7°C) otherwise value is [#00]
			//
			// HEAT INDEX in °C = ((byte(13)-32) / 1.8)
			// HEAT INDEX in °F =   byte(13)
			//
			// Stage 1:  80- 89°F (26.7°C to 32.0°C) ... Caution
			// Stage 2:  90-104°F (32.0°C to 40.0°C) ... Extreme Caution
			// Stage 3: 105-129°F (40.5°C to 53.8°C) ... Danger
			// Stage 4: 130-151°F (54.5°C to 66.1°C) ... Extreme Danger
			//
			// CHECKSUM ---- Add up the bytes 00 to 13 = ((256*cH) + cL)
			// Byte 14: (cL) Check-sum low  byte
			// Byte 15: (cH) Check-sum high byte
			//
			int sign;
			DateTime now = DateTime.Now;
			double num;

			// which sensor is this for? 0 = indoor, 1 = outdoor
			int sensor = PacketBuffer[7] & 0xF;
			if (sensor == cumulus.WMR200TempChannel)
			{
				// outdoor hum
				DoOutdoorHumidity(PacketBuffer[10],now);
				// outdoor temp
				if ((PacketBuffer[9] & 0x80) == 0x80)
				{
					sign = -1;
				}
				else
				{
					sign = 1;
				}

				num = sign*((PacketBuffer[9] & 0xF)*256 + PacketBuffer[8])/10.0;
				DoOutdoorTemp(ConvertTempCToUser(num),now);
				// outdoor dewpoint
				if ((PacketBuffer[12] & 0x80) == 0x80)
				{
					sign = -1;
				}
				else
				{
					sign = 1;
				}
				num = sign*((PacketBuffer[12] & 0xF)*256 + PacketBuffer[11])/10.0;
				DoOutdoorDewpoint(ConvertTempCToUser(num), now);

				DoApparentTemp(now);
			}
			else if (sensor == 0)
			{
				// indoor hum
				DoIndoorHumidity(PacketBuffer[10]);
				// indoor temp
				if ((PacketBuffer[9] & 0x80) == 0x80)
				{
					sign = -1;
				}
				else
				{
					sign = 1;
				}
				num = (sign*((PacketBuffer[9] & 0xF)*256 + PacketBuffer[8]))/10.0;
				DoIndoorTemp(ConvertTempCToUser(num));
			}
			if ((sensor > 1) && (sensor < 11))
			{
				WMR200ChannelPresent[sensor] = true;
				// outdoor hum
				WMR200ExtraHumValues[sensor] = PacketBuffer[10];
				DoExtraHum(WMR200ExtraHumValues[sensor],sensor);
				// outdoor temp
				if ((PacketBuffer[9] & 0x80) == 0x80)
				{
					sign = -1;
				}
				else
				{
					sign = 1;
				}

				WMR200ExtraTempValues[sensor] = ConvertTempCToUser((sign * ((PacketBuffer[9] & 0xF) * 256 + PacketBuffer[8])) / 10.0);
				DoExtraTemp(WMR200ExtraTempValues[sensor], sensor);
				// outdoor dewpoint
				if ((PacketBuffer[12] & 0x80) == 0x80)
				{
					sign = -1;
				}
				else
				{
					sign = 1;
				}

				WMR200ExtraDPValues[sensor] = ConvertTempCToUser((sign * ((PacketBuffer[12] & 0xF) * 256 + PacketBuffer[11])) / 10.0);
				DoExtraDP(WMR200ExtraDPValues[sensor],sensor);
				ExtraSensorsDetected = true;
			}
		}

		private void ProcessRainPacket()
		{
			//          ----> Time/Date----> Rain------------------> Rain Date----> CHSUM
			//00 01 02 03 04 05 06 07 08 09 10 11 12 13 14 15 16 17 18 19 20 21 ...byte nr
			//ID LL mm-hh dd-mm-yy rL-rH hL-hH dL-dH aL-aH mm-hh dd-mm-yy cL-cH ...notation
			//----- -------------- ----- ----- ----- ----- ----- -------- -----
			//d4 16 3b 15 08 0c 0a 0f 00 04 00 4b 00 7e 02 00 0c 01 01 07 4b 02 ...data
			//-----------------------------------------------------------------

			//DECODED:
			//Packet Nr .......... [D4]
			//Packet Length ...... [16] 22 bytes
			//Time ............... [15:3b] 21:59
			//Date ............... [07-0c-0a] 08-12-2010
			//Rain rate .......... [000f] 0.15 inch/hr
			//Rain last hour ..... [0004] 0.04 inch
			//Rain last 24hr ..... [004b] 0.75 inch
			//Rain accumulated ... [027e] 6.38 inch (162.052 mm)
			//Rain since time .... [0c:00] 12:00
			//Rain since date .... [01:01:07] 01-01-2007
			//Check-sum .......... [024b]
			//----------------------------------------------------------------------------------------------

			//HEADER ------
			//Byte 00: (ID) [D4] Packet Id
			//Byte 01: (LL) [16] Packet Length = 22 bytes

			//DATE/TIME ---
			//Byte 02: (mm) Minute
			//Byte 03: (hh) Hour
			//Byte 04: (dd) Day
			//Byte 05: (mm) Month
			//Byte 06: (yy) Year

			//D4 RAIN -----
			//Rainfall rate in inches (and not in millimeters)
			//Byte 07: (rH) Rainfall Rate high byte +
			//Byte 08: (rL) Rainfall Rate low  byte

			//RainRate in inch/hr = ((256*rH) + rL) / 100)
			//RainRate in mm/hr   = ((256*rH) + rL) / 100) * 25.4

			//Rain This Hour: activated when rain starts being measured
			//Byte 09: (hH) Rainfall This Hour high byte +
			//Byte 10: (hL) Rainfall This Hour low  byte

			//Rain_this_hour in inch = ((256*hH) + hL) / 100)
			//Rain_this_hour in mm   = ((256*hH) + hL) / 100) * 25.4

			//Rain Past 24 Hours: excluding current hour
			//Byte 11: (dL) Rainfall Past  24H high byte +
			//Byte 12: (dH) Rainfall Past  24H low  byte

			//Rain_last_24h in inch = ((256*dH) + dL) / 100)
			//Rain_last_24h in mm   = ((256*dH) + dL) / 100) * 25.4

			//Accumulated rain since 12h00 1st January 2007
			//Byte 13: (aL) Rainfall Accumulated high byte +
			//Byte 14: (aH) Rainfall Accumulated low  byte

			//Rain_accum in inch = ((256*aH) + aL) / 100)
			//Rain_accum in mm   = ((256*aH) + aL) / 100) * 25.4

			//Date/time since accumulated rainfall (always 12h00 01-01-2007)
			//Byte 15: (mm) Rainfall Accumulated Minute
			//Byte 16: (hh) Rainfall Accumulated Hour
			//Byte 17: (dd) Rainfall Accumulated Day
			//Byte 18: (mm) Rainfall Accumulated Month
			//Byte 19: (yy) Rainfall Accumulated Year

			//CHECKSUM ---- Add up the bytes 00 to 19 == ((256*cH) + cL)
			//Byte 20: (cL) Check-sum low  byte
			//Byte 21: (cH) Check-sum high byte

			double counter = (((PacketBuffer[14]*256) + PacketBuffer[13])/100.0);

			var rate = ((PacketBuffer[8]*256) + PacketBuffer[7])/100.0;

			// check for overflow  (9999 mm = approx 393 in) and set to 999 mm/hr
			if (rate > 393)
				rate = 39.33;

			DoRain(ConvertWMR200Rain(counter), ConvertWMR200Rain(rate), DateTime.Now);
		}

		/// <summary>
		/// Rain supplied in in, convert to units in use
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private double ConvertWMR200Rain(double value)
		{
			double num;
			if (cumulus.RainUnit == 0)
			{
				// mm
				num = value*25.4;
			}
			else
			{
				// inches
				num = value;
			}

			return num;
		}

		private void ProcessWindPacket()
		{
			//----> Time/Date----> WIND---------------> CHSUM
			//00 01 02 03 04 05 06 07 08 09 10 11 12 13 14 15 ...byte nr
			//ID LL mm-hh dd-mm-yy xD .. gg aG-Aa WC AL cL-cH ...notation
			//----- -------------- -- -- __ -_ -- -- -- -----
			//d3 10 0d 05 07 0c 0a 09 0c 12 b0 00 e6 00 c7 02 ...data
			//-----------------------------------------------

			//DECODED:
			//Packet Nr .......... [D3]
			//Length ............. [10] 16 bytes
			//Time ............... [0d:33] 05:13
			//Date ............... [07-0c-0a] 07-12-2010
			//Wind Direction ..... [9] SSW
			//Wind Speed Gust .... [012] 1.8 m/s
			//Wind Speed Avrg .... [00b] 1.1 m/s
			//Wind Chill ......... [e6]  23°F (-5.0°C)
			//Check-sum .......... [0711]
			//----------------------------------------------------------------------------------------------

			//HEADER ------
			//Byte 00: [D3] Packet Id
			//Byte 01: [10] Packet Length = 16 bytes

			//DATE/TIME ---
			//Byte 02: (mm) Minute
			//Byte 03: (hh) Hour
			//Byte 04: (dd) Day
			//Byte 05: (mm) Month
			//Byte 06: (yy) Year

			//D3 WIND -----
			//Byte 07:  (x) byte(07:H)
			//.. unknown / always [#0] ... maybe icon/alarm?

			//(D) byte(07:L)
			//Wind Direction = (D * 22.5) = degrees
			//0 = N   (between 348.76 and 011.25)
			//1 = NNE (between 011.26 and 033.75)
			//2 = NE  (between 033.76 and 056.25)
			//3 = ENE (between 056.26 and 078.75)
			//4 = E   (between 078.76 and 101.25)
			//5 = ESE (between 101.26 and 123.75)
			//6 = SE  (between 123.76 and 146.25)
			//7 = SSE (between 146.26 and 168.75)
			//8 = S   (between 168.76 and 191.25)
			//9 = SSW (between 191.26 and 213.75)
			//10 = SW  (between 213.76 and 236.25)
			//11 = WSW (between 236.26 and 258.75)
			//12 = W   (between 258.76 and 281.25)
			//13 = WNW (between 281.26 and 303.75)
			//14 = NW  (between 303.76 and 326.25)
			//15 = NNW (between 326.26 and 348.75)

			//Byte 08: (..) .. unknown / always [#0C] (0000x1100 = decimal 12)

			//Byte 09: (gg) byte(09)   = Wind Gust low  byte
			//Byte 10:  (G) byte(10:L) = Wind Gust high byte : low  nibble
			//(a) byte(10:H) = Wind Avrg low  byte : low  nibble
			//Byte 11:  (a) byte(11:L) = Wind Avrg low  byte : high nibble
			//(A) byte(11:H) = Wind Avrg high byte : low  nibble

			//WIND GUST: [byte10:L + byte09]
			//WindGust in m/s = (((byte(10:L) * 256) + byte(09)) / 10)
			//WindGust in mph = (((byte(10:L) * 256) + byte(09)) / 10) * 2.23693629
			//WindGust in kph = (((byte(10:L) * 256) + byte(09)) / 10) * 3.60000000
			//WindGust in kts = (((byte(10:L) * 256) + byte(09)) / 10) * 1.94384449

			//WIND AVRG: [byte11:H + byte11:Ln + byte10:H]
			//WindAvrg in m/s = (((byte(11) * 16) + byte(10:H)) / 10)
			//WindAvrg in mph = (((byte(11) * 16) + byte(10:H)) / 10) * 2.23693629
			//WindAvrg in kph = (((byte(11) * 16) + byte(10:H)) / 10) * 3.60000000
			//WindAvrg in kts = (((byte(11) * 16) + byte(10:H)) / 10) * 1.94384449

			//Stage 1: Light .........  0- 8 mph ( 3-13 kph)
			//Stage 2: Moderate ......  9-25 mph (14-41 kph)
			//Stage 3: Strong ........ 26-54 mph (42-97 kph)
			//Stage 4: Storm .........   >55 mph (  >88 kph)

			//Byte 12: (WC) WIND CHILL: (in Fahrenheit)
			//.... Reported when temp < 40°F (4.4°C) and wind > 3 mph (4.8 kph) otherwise
			//value is #00 ... to be confirmed.
			//.... According to OS Manual: temp reading has to be <= 10 deg & the wind
			//speed has to be >= 3 mph or 5 km/hr.

			//Wind Chill in °F = byte(12)
			//Wind Chill in °C = ((byte(12) - 32) / 1.8)°C

			//Byte 13: (AL) .. unknown / always [#20] = [0010 0000] = decimal 32
			//.. perhaps Wind Chill Alarm: [#00] = on, [#10] = ?, [#20] = off

			//CHECKSUM ---- To get the check-sum, add up bytes 00 to 13 == ((256*cH) + cL)
			//Byte 20: (cL) Check-sum low  byte
			//Byte 21: (cH) Check-sum high byte

			DateTime now = DateTime.Now;

			// bearing
			int bearing = (int) ((PacketBuffer[7] & 0xF)*22.5);
			// gust
			double gust = ((PacketBuffer[10] & 0xF)*256 + PacketBuffer[9])/10.0;
			// average
			double average = ((PacketBuffer[11]*16) + (PacketBuffer[10]/16))/10.0;

			DoWind(ConvertWindMSToUser(gust), bearing, ConvertWindMSToUser(average), now);

			if ((PacketBuffer[13] & 0x20) == 0x20)
			{
				// no wind chill, use current temp if available
				// note that even if Cumulus is set to calculate wind chill
				// it can't/won't do it if temp isn't available, so don't
				// bother calling anyway

				DoWindChill(OutdoorTemperature,now);
			}
			else
			{
				// wind chill is in Fahrenheit!
				var wc = (PacketBuffer[12] + (PacketBuffer[13] & 0xF)*256)/10.0;

				if ((PacketBuffer[13] & 0x80) == 0x80)
					wc = -wc;

				if (cumulus.TempUnit == 0)
				{
					// convert to C
					wc = MeteoLib.FtoC(wc);
				}

				DoWindChill(wc,now);
			}
		}

		private void ProcessBaroPacket()
		{
			//----> Time/Date----> PRESSURE--> CHSUM
			//00 01 02 03 04 05 06 07 08 09 10 11 12 ...byte nr
			//ID LL mm-hh dd-mm-yy SS-iS AA-xA cL-cH ...notation
			//----- ----- -------- ----- ----- -----
			//D6 0d 06 12 04 0c 0a 4a 63 fa 33 ef 02 ...data
			//--------------------------------------

			//DECODED:
			//Packet Id .......... [D6]
			//Packet Length ...... [0d] 13 bytes
			//Time ............... [12:06] 18:06
			//Date ............... [04-0c-0a] 04-12-2010
			//Pressure/Station ... [34a]  842 hPa
			//Forecast Icon ...... [6] Partly Cloudy (Night)
			//Pressure/Altitude .. [3fa] 1018 hPa
			//Check-sum .......... [02ef]
			//----------------------------------------------------------------------------------------------

			//HEADER ------
			//Byte 00: (ID) [D6] Packet Id
			//Byte 01: (LL) [0d] Packet Length = 13 bytes

			//DATE/TIME ---
			//Byte 02: (mm) Minute
			//Byte 03: (hh) Hour
			//Byte 04: (dd) Day
			//Byte 05: (mm) Month
			//Byte 06: (yy) Year

			//D6 BARO -----
			//Byte 07: (SS) byte(07)   .. Station Pressure low byte  +
			//Byte 08:  (S) byte(08:L) .. Station Pressure high nibble ..

			//Station Pressure in hPa  = ((byte(08:L)*256) + byte(07))
			//Station Pressure in inHg = ((byte(08:L)*256) + byte(07)) * 0.0295333727

			//(i) byte(08:H) .. Forecast Icon Nr: (12-24 hour weather forecast within 30-50 km)
			//[0000] = [#0] = 0: Partly Cloudy (day)
			//[0001] = [#1] = 1: Rainy
			//[0010] = [#2] = 2: Cloudy
			//[0011] = [#3] = 3: Sunny (day)
			//[0100] = [#4] = 4: Clear (night)
			//[0101] = [#5] = 5: Snowy
			//[0110] = [#6] = 6: Partly Cloudy (night)

			//Byte 09: (AA) byte(09)   .. Altitude Pressure low byte  +
			//Byte 10:  (A) byte(10:L) .. Altitude Pressure high nibble ..

			//Altitude Pressure in hPa  = ((byte(10:L)*256) + byte(09))
			//Altitude Pressure in inHg = ((byte(10:L)*256) + byte(09)) * 0.0295333727

			//(x) byte(10:H) .. unknown / always [#3] = [0011] .. maybe trend/alarm?
			//.. or maybe it's always sunny indoors ;-)

			//CHECKSUM ---- Add up the bytes 00 to 10 == ((256*cH) + cL)
			//Byte 11: (cL) Check-sum low  byte
			//Byte 12: (cH) Check-sum high byte

			double slp = ((PacketBuffer[10] & 0xF)*256) + PacketBuffer[9];
			DoPressure(ConvertPressMBToUser(slp),DateTime.Now);

			StationPressure = ConvertPressMBToUser(((PacketBuffer[8] & 0xF)*256) + PacketBuffer[7]);

			UpdatePressureTrendString();

			var forecast = PacketBuffer[8]/16;
			string fcstr;

			switch (forecast)
			{
				case 0:
					fcstr = "Partly Cloudy";
					break;
				case 1:
					fcstr = "Rainy";
					break;
				case 2:
					fcstr = "Cloudy";
					break;
				case 3:
					fcstr = "Sunny";
					break;
				case 4:
					fcstr = "Clear";
					break;
				case 5:
					fcstr = "Snowy";
					break;
				case 6:
					fcstr = "Partly Cloudy";
					break;
				default:
					fcstr = "Unknown";
					break;
			}

			DoForecast(fcstr,false);
		}

		private void ProcessUVPacket()
		{
			//----> Time/Date----> UV CHSUM
			//00 01 02 03 04 05 06 07 08 09 ...byte nr
			//ID LL mm-hh dd-mm-yy xI cL-cH ...notation
			//----- -------------- -- -----
			//D5 0a 24 07 02 03 09 06 11 07 ...data
			//----- -------------- -- -----

			//DECODED:
			//Packet Id .......... [D5]
			//Packet Length ...... [0a] 10 bytes
			//Time ............... [07:24] 07:36
			//Date ............... [02-03-09] 02-03-2009
			//UVI ................ [6] 6
			//Check-sum .......... [0711]
			//----------------------------------------------------------------------------------------------

			//HEADER ------
			//Byte 00: (ID) [D5] Packet Id
			//Byte 01: (LL) [0a] Packet Length = 10 bytes

			//DATE/TIME ---
			//Byte 02: (mm) Minute
			//Byte 03: (hh) Hour
			//Byte 04: (dd) Day
			//Byte 05: (mm) Month
			//Byte 06: (yy) Year

			//D5 UV -------
			//Byte 07: (xI) Reports value of [#ff] in history [D2] records when out of order, not
			//installed or sensor is lost.
			//(x) byte(07:H) .. unknown / always [0] .. maybe alarm?
			//(I) byte(07:L) .. UVI 0-15
			//... (0-2 low / 3-5 moderate / 6-7 high / 8-10 very high / 11+ extremely high)

			//CHECKSUM ---- Add up the bytes 00 to 07 == ((256*cH) + cL)
			//Byte 11: (cL) Check-sum low  byte
			//Byte 12: (cH) Check-sum high byte

			var num = PacketBuffer[7] & 0xF;

			if (num < 0)
				num = 0;

			if (num > 16)
				num = 16;

			DoUV(num,DateTime.Now);

			// UV value is stored as channel 1 of the extra sensors
			WMR200ExtraHumValues[1] = num;

			ExtraSensorsDetected = true;

			WMR200ChannelPresent[1] = true;
		}

		private void ProcessHistoryDataPacket()
		{
			//
			// ==============================================================================================
			// [D2] HISTORY
			// Rain = inch
			// Wind = m/s
			// Temp = °C
			// Dewp = °C
			// Heat Index = °F
			// Wind Chill = °F
			// Pressure = hPa
			// ==============================================================================================
			//
			// ----> Time/Date----> RAIN---------------------------------> WIND--------------->
			// 00 01 02 03 04 05 06 07 08 09 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 ...byte nr
			// D2  L mm-hh dd-mm-yy rL-rH hL-hH dL-dH aL-aH mm-hh dd-mm-yy xD .. gg aG-Aa WC AL ...notation
			// ----- -------------- -------------------------------------- --------------------
			// D2 31 24 07 02 03 09 00 00 00 00 00 00 BC 0A 00 0C 01 01 07 04 0C 0D F0 00 00 20 ...data
			// --------------------------------------------------------------------------------
			//
			// Continued:
			// UV PRESSURE--> SC TEMP0--------------> TEMP1--------------> CHSUM
			// 27 28 29 30 31 32 33 34 35 36 37 38 39 40 41 42 43 44 45 46 47 48 ...byte nr
			// xI SS-iS AA-xA SC tS TT-sT HU DD-sD HI tS TT-sT HU DD-sD HI cL-cH ...notation
			// -- ----------- -- -------------------- -------------------- -----
			// 06 51 33 02 34 01 00 F4 00 2C 78 00 00 01 A6 00 51 82 00 00 11 07 ...data
			// -----------------------------------------------------------------
			//
			//
			// HEADER ---------------------------------------------------------------------------------------
			// Byte 00: [D2] Packet Id
			// Byte 01: [31] Packet Length 49 bytes ...(can be up to 112 bytes with all 10 external
			// sensors .. 7 extra bytes per sensor)
			//
			// DATE/TIME ------------------------------------------------------------------------------------
			// Byte 02: (mm) Minute
			// Byte 03: (hh) Hour
			// Byte 04: (dd) Day
			// Byte 05: (mm) Month
			// Byte 06: (yy) Year
			//
			//
			// D4 RAIN --------------------------------------------------------------------------------------
			// Rainfall rate in inches (and not in millimeters) reported after 2nd tip.
			// Byte 07: (rH) Rainfall Rate high byte +
			// Byte 08: (rL) Rainfall Rate low  byte
			//
			// RainRate in inch/hr = ((rH*256) + rL) / 100)
			// RainRate in mm/hr   = ((rH*256) + rL) / 100) * 25.4
			//
			// Rain This Hour: activated when rain starts being measured.
			// Byte 09: (hH) Rainfall This Hour high byte +
			// Byte 10: (hL) Rainfall This Hour low  byte
			//
			// Rain_this_hour in inch = ((hH*256) + hL) / 100)
			// Rain_this_hour in mm   = ((hH*256) + hL) / 100) * 25.4
			//
			// Rain Past 24 Hours: excluding current hour
			// Byte 11: (dL) Rainfall Past  24H high byte +
			// Byte 12: (dH) Rainfall Past  24H low  byte
			//
			// Rain_last_24h in inch = ((dH*256) + dL) / 100)
			// Rain_last_24h in mm   = ((dH*256) + dL) / 100) * 25.4
			//
			// Accumulated rain since 12h00 1st January 2007
			// Byte 13: (aL) Rainfall Accumulated high byte +
			// Byte 14: (aH) Rainfall Accumulated low  byte
			//
			// Rain_accum in inch = ((aH*256) + aL) / 100)
			// Rain_accum in mm   = ((aH*256) + aL) / 100) * 25.4
			//
			// Date/time since accumulated rainfall (always 12h00 01-01-2007)
			// Byte 15: (mm) Rainfall Accumulated Minute
			// Byte 16: (hh) Rainfall Accumulated Hour
			// Byte 17: (dd) Rainfall Accumulated Day
			// Byte 18: (mm) Rainfall Accumulated Month
			// Byte 19: (yy) Rainfall Accumulated Year
			//
			//
			// D3 WIND --------------------------------------------------------------------------------------
			// Byte 20:  (x) byte(20:H)
			// .. unknown / always [#0] ... maybe icon, alarm or sensor absent?
			//
			// (D) byte(20:L)
			// Wind Direction = (D * 22.5) = degrees
			// 0 = N   (between 348.76° and 011.25°)
			// 1 = NNE (between 011.26° and 033.75°)
			// 2 = NE  (between 033.76° and 056.25°)
			// 3 = ENE (between 056.26° and 078.75°)
			// 4 = E   (between 078.76° and 101.25°)
			// 5 = ESE (between 101.26° and 123.75°)
			// 6 = SE  (between 123.76° and 146.25°)
			// 7 = SSE (between 146.26° and 168.75°)
			// 8 = S   (between 168.76° and 191.25°)
			// 9 = SSW (between 191.26° and 213.75°)
			// 10 = SW  (between 213.76° and 236.25°)
			// 11 = WSW (between 236.26° and 258.75°)
			// 12 = W   (between 258.76° and 281.25°)
			// 13 = WNW (between 281.26° and 303.75°)
			// 14 = NW  (between 303.76° and 326.25°)
			// 15 = NNW (between 326.26° and 348.75°)
			//
			// Byte 21: (..) .. unknown / always [#0C] (0000x1100 = decimal 12)
			//
			// Byte 22: (gg) byte(22)   = Wind Gust low  byte
			// Byte 23:  (G) byte(23:L) = Wind Gust high byte : low  nibble
			// (a) byte(23:H) = Wind Avrg low  byte : low  nibble
			// Byte 24:  (a) byte(24:L) = Wind Avrg low  byte : high nibble
			// (A) byte(24:H) = Wind Avrg high byte : low  nibble
			//
			// WIND GUST: [byte10:L + byte09]
			// WindGust in m/s = (((byte(23:L) * 256) + byte(22)) / 10)
			// WindGust in mph = (((byte(23:L) * 256) + byte(22)) / 10) * 2.23693629
			// WindGust in kph = (((byte(23:L) * 256) + byte(22)) / 10) * 3.60000000
			// WindGust in kts = (((byte(23:L) * 256) + byte(22)) / 10) * 1.94384449
			//
			// WIND AVRG: [byte11:H + byte11:L + byte10:H]
			// WindAvrg in m/s = (((byte(24) * 16) + byte(23:H)) / 10)
			// WindAvrg in mph = (((byte(24) * 16) + byte(23:H)) / 10) * 2.23693629
			// WindAvrg in kph = (((byte(24) * 16) + byte(23:H)) / 10) * 3.60000000
			// WindAvrg in kts = (((byte(24) * 16) + byte(23:H)) / 10) * 1.94384449
			//
			// Byte 25: (WC) WIND CHILL: (in Fahrenheit)
			// .... Reported when temp < 40°F (4.4°C) and wind > 3 mph (4.8 kph) otherwise
			// value is #00 ... to be confirmed.
			// .... According to OS Manual: temp reading has to be <= 10 deg & the wind
			// speed has to be >= 3 mph or 5 km/hr.
			//
			// Wind Chill in °F = byte(25)
			// Wind Chill in °C = ((byte(25) - 32) / 1.8)°C
			//
			// Byte 26: (AL) .. unknown / always [#20] = [0010 0000] = decimal 32
			// Wind Chill Alarm: [#00] = on, [#10] = ?, [#20] = off
			//
			//
			// D5 UV ----------------------------------------------------------------------------------------
			// Byte 27: (xI) Reports value of [#ff] in history [D2] records when out of order, not
			// installed or sensor is lost.
			// (x) byte(27:H) .. unknown / always [#0] .. maybe alarm or sensor absent?
			// (I) byte(27:L) .. UVI 0-15
			// ... (0-2 low / 3-5 moderate / 6-7 high / 8-10 very high / 11+ extremely high)
			//
			//
			// D6 BARO --------------------------------------------------------------------------------------
			// Byte 28: (SS) byte(28)   .. Station Pressure low byte  +
			// Byte 29:  (S) byte(29:L) .. Station Pressure high nibble ..
			//
			// Station Pressure in hPa  = ((byte(29:L)*256) + byte(28))
			// Station Pressure in inHg = ((byte(29:L)*256) + byte(28)) * 0.0295333727
			//
			// (i) byte(29:H) .. Forecast Icon Nr: (12-24 hour weather forecast within 30-50 km)
			// [0000] = [#0] = 0: Partly Cloudy (day)
			// [0001] = [#1] = 1: Rainy
			// [0010] = [#2] = 2: Cloudy
			// [0011] = [#3] = 3: Sunny (day)
			// [0100] = [#4] = 4: Clear (night)
			// [0101] = [#5] = 5: Snowy
			// [0111] = [#6] = 6: Partly Cloudy (night)
			//
			// Byte 30: (AA) byte(30)   .. Altitude Pressure low byte  +
			// Byte 31:  (A) byte(31:L) .. Altitude Pressure high nibble ..
			//
			// Altitude Pressure in hPa  = ((byte(31:L)*256) + byte(30))
			// Altitude Pressure in inHg = ((byte(31:L)*256) + byte(30)) * 0.0295333727
			//
			// (x) byte(31:H) .. unknown / always [#3] = [0011] .. maybe trend/alarm?
			// .. or maybe it's always sunny indoors ;-)
			//
			//
			// EXTERNAL SENSOR COUNT ------------------------------------------------------------------------
			// Byte 32: External sensor count (value 1 to 10)
			// This value will be 0 if your external sensor is not operational.
			// Use this to determine the number of extra sensor blocks in this history packet.
			//
			// packetLengthD2 = 49 + ((byte(32)-1) * 7);
			//
			//
			// D7 CONSOLE ------- SENSOR #0 -----------------------------------------------------------------
			// Note the order of sensors might be reversed i.e. this might be sensor #1's data.
			// Make sure that you use the sensor number when posting this data.
			//
			// Byte 33:  (t) byte(33:H) .. Temp/Hum trend/icon.
			// [00..] = 0: Temperature ...... steady
			// [01..] = 1: Temperature ...... rising
			// [10..] = 2: Temperature ...... falling
			//
			// [..00] = 0: Humidity ......... steady
			// [..01] = 1: Humidity ......... rising
			// [..10] = 2: Humidity ......... falling
			//
			// tTemp = byte(33) >> 6 .......... or: tTemp =  byte(33) / 64
			// tHum  = byte(33) >> 4 & 0x03 ... or: tHum  = (byte(33) / 16) & 0x03
			//
			// (S) byte(33:L) .. Sensor Number
			// 0 = WMR200 Console temp/humidity (indoor)
			// 1 = Extra sensor #1 temp/humidity (outdoor)
			// . ... up to 10 sensors ...
			// 10 = Extra sensor #10 temp/humidity
			//
			// nSensor = byte(33) & 0x0F
			//
			// Byte 34: (TT) byte(34)   .. Temperature low byte  +
			// Byte 35:  (T) byte(35:L) .. Temperature high nibble
			// (s) byte(35:H) .. Temperature sign ...... #0 = positive / #8 = negative
			//
			// Temp_sign = ((byte(35) >> 4)==0x08)? -1 : 1;
			// TEMPERATURE in °C = Temp_sign * (((byte(35:L)*256) + byte(34)) / 10)
			// TEMPERATURE in °F = ((TEMPERATURE in °C) * 1.8) + 32
			//
			// Byte 36: (HU) Humidity %
			//
			// Byte 37: (DD) byte(37)   .. Dew Point low byte  +
			// Byte 38:  (D) byte(38:L) .. Dew Point high nibble
			// (s) byte(38:H) .. Dew Point sign ...... #0 = positif / #8 = negative
			//
			// Dewp_sign = ((byte(38) >> 4)==0x08)? -1 : 1;
			// DEWPOINT in °C = Dewp_sign * (((byte(38:L)*256) + byte(37)) / 10)
			// DEWPOINT in °F = ((DEWPOINT in °C) * 1.8) + 32
			//
			// Byte 39: (HI) Heat Index: (in Fahrenheit)
			// .... Reported when temp is > 80°F (26.7°C) otherwise value is [#00]
			//
			// HEAT INDEX in °C = ((byte(39)-32) / 1.8)
			// HEAT INDEX in °F =   byte(39)
			//
			//
			// D7 EXTERNAL ------ SENSOR #1 -----------------------------------------------------------------
			// Note the order of sensors might be reversed i.e. this might be sensor #2's data.
			// This block will be missing if sensor #1 has been lost i.e. external sensor count will be 0
			// in case of 1 extra external sensor.
			//
			// Byte 40:  (t) byte(40:H) .. Temp/Hum trend/icon.
			// [00..]  = 0: Temperature ...... steady
			// [01..]  = 1: Temperature ...... rising
			// [10..]  = 2: Temperature ...... falling
			//
			// [..00]  = 0: Humidity ......... steady
			// [..01]  = 1: Humidity ......... rising
			// [..10]  = 2: Humidity ......... falling
			//
			// tTemp = byte(40) >> 6 .......... or: tTemp =  byte(40) / 64
			// tHum  = byte(40) >> 4 & 0x03 ... or: tHum  = (byte(40) / 16) & 0x03
			//
			// (S) byte(40:L) .. Sensor Number
			// 0 = WMR200 Console temp/humidity (indoor)
			// 1 = Extra sensor #1 temp/humidity (outdoor)
			// . ... up to 10 sensors ...
			// 10 = Extra sensor #10 temp/humidity
			//
			// nSensor = byte(40) & 0x0F
			//
			// Byte 41: (TT) byte(41)   .. Temperature low byte  +
			// Byte 42:  (T) byte(42:L) .. Temperature high nibble
			// (s) byte(42:H) .. Temperature sign ...... #0 = positif / #8 = negative
			//
			// Temp_sign = ((byte(42) >> 4)==0x08)? -1 : 1;
			// TEMPERATURE in °C = Temp_sign * (((byte(42:L)*256) + byte(41)) / 10)
			// TEMPERATURE in °F = ((TEMPERATURE in °C) * 1.8) + 32
			//
			// Byte 43: (HU) Humidity %
			//
			// Byte 44: (DD) byte(44)   .. Dew Point low byte  +
			// Byte 45:  (D) byte(45:L) .. Dew Point high nibble
			// (s) byte(45:H) .. Dew Point sign ...... #0 = positif / #8 = negative
			//
			// Dewp_sign = ((byte(45) >> 4)==0x08)? -1 : 1;
			// DEWPOINT in °C = Dewp_sign * (((byte(45:L)*256) + byte(44)) / 10)
			// DEWPOINT in °F = ((DEWPOINT in °C) * 1.8) + 32
			//
			// Byte 46: (HI) Heat Index: (in Fahrenheit)
			// .... Only reported when temp is > 80°F (26.7°C) otherwise value is 0x00
			//
			// HEAT INDEX in °C = ((byte(46)-32) / 1.8)
			// HEAT INDEX in °F =   byte(46)
			//
			//
			// D7 EXTERNAL ------ SENSOR #n -----------------------------------------------------------------
			// Note the order of sensors might be reversed i.e. this might be sensor #1's data.
			// .. up to 9 blocks of 7 bytes each for additional external temp/humidity sensors.
			// .. this block will only be supplied if the sensor is operational i.e. if it transmits data.
			// .. byte(47:L) i.e. nSensor takes on the value of the channel number the sensor is set to.
			//
			//
			// CHECKSUM -------------------------------------------------------------------------------------
			// To get the check-sum, add up bytes 00 to 46 which must equal ((cH*256) + cL)
			// Byte 47: (cL) Check-sum low  byte
			// Byte 48: (cH) Check-sum high byte
			DateTime timestamp;
			//int s;
			//int ms;
			int i;
			double num;
			//int intnum;
			int sign;
			int interval;
			double wc;
			double counter;
			double rate;
			int sensorcount;
			int sensornumber;
			int offset;
			//ArchiveDataDownloaded = true;
			// Byte 02: (mm) Minute
			// Byte 03: (hh) Hour
			// Byte 04: (dd) Day
			// Byte 05: (mm) Month
			// Byte 06: (yy) Year
			int m = PacketBuffer[2];
			int h = PacketBuffer[3];
			int d = PacketBuffer[4];
			int mo = PacketBuffer[5];
			int y = PacketBuffer[6];
			//@ Unsupported function or procedure: 'Format'

			//@ Unsupported function or procedure: 'Format'
			//PleaseWaitUnit.Units.PleaseWaitUnit.PleaseWaitForm.timestamplabel.Text = Format("%.4d-%.2d-%.2d %.2d:%.2d", new int[] {y + 2000, mo, d, h, m});
			try
			{
				timestamp = new DateTime(2000 + y, mo, d, h, m, 0, 0);
				cumulus.LogMessage("History data for: " + timestamp);
			}
			catch
			{
				cumulus.LogMessage("Invalid date, ignoring");
				return;
			}
			if (timestamp < cumulus.LastUpdateTime)
			{
				cumulus.LogMessage("Timestamp is earlier than latest data, ignoring");
				return;
			}
			//ArchiveDataCount++;
			if (FirstArchiveData)
			{
				FirstArchiveData = false;
				if (cumulus.LastUpdateTime.AddHours(HourInc).Date == timestamp.AddHours(HourInc).Date)
				{
					cumulus.LogMessage("Todayfile matches start of history data");
					cumulus.LogMessage("LastUpdateTime = " + (cumulus.LastUpdateTime).ToString());
					cumulus.LogMessage("Earliest timestamp = " + (timestamp).ToString());
					int luh = cumulus.LastUpdateTime.Hour;
					//int lum = cumulus.LastUpdateTime.Minute;
					//int lus = cumulus.LastUpdateTime.Second;
					//int lums = cumulus.LastUpdateTime.Millisecond;
					if (luh == rollHour)
					{
						rolloverdone = true;
						cumulus.LogMessage("Rollover already done for start of history data day");
					}
					if (luh == 0)
					{
						midnightraindone = true;
					}
				}
			}
			if (h != rollHour)
			{
				rolloverdone = false;
			}
			if ((h == rollHour) && !rolloverdone)
			{
				// do rollover
				cumulus.LogMessage("Day rollover " + (timestamp).ToString());
				DayReset(timestamp);
				rolloverdone = true;
			}
			if (h != 0)
			{
				midnightraindone = false;
			}
			if ((h == 0) && !midnightraindone)
			{
				ResetMidnightRain(timestamp);
				ResetSunshineHours();
				midnightraindone = true;
			}
			// there seems to be no way of determining the log interval other than subtracting one logMainUnit.Units.MainUnit.cumulus.LogMessage
			// timestamp from another, so we'll have to ignore the first one
			if (PreviousHistoryTimeStamp > DateTime.MinValue)
			{
				interval = MinutesBetween(timestamp, PreviousHistoryTimeStamp);
			}
			else
			{
				interval = 0;
			}
			PreviousHistoryTimeStamp = timestamp;
			// pressure
			StationPressure = ConvertPressMBToUser(((PacketBuffer[29] & 0xF)*256) + PacketBuffer[28]);
			num = ((PacketBuffer[31] & 0xF)*256) + PacketBuffer[30];
			DoPressure(ConvertPressMBToUser(num),timestamp);

			// bearing
			int bearing = (int) ((PacketBuffer[20] & 0xF)*22.5);
			// gust
			double gust = ((PacketBuffer[23] & 0xF)*256 + PacketBuffer[22])/10.0;
			// average
			double average = ((PacketBuffer[24]*16) + (PacketBuffer[23]/16))/10.0;

			DoWind(ConvertWindMSToUser(gust),bearing,ConvertWindMSToUser(average),timestamp);

			// add in 'interval' minutes worth of wind speed to windrun
			WindRunToday += (WindAverage*WindRunHourMult[cumulus.WindUnit]*interval*60)/1000.0;
			// update dominant wind bearing
			CalculateDominantWindBearing(Bearing, WindAverage, interval);
			sensorcount = PacketBuffer[32];
			for (i = 0; i <= sensorcount; i++)
			{
				try
				{
					offset = 33 + (i*7);
					sensornumber = PacketBuffer[offset] & 0xF;
					if (sensornumber == 0)
					{
						// indoor sensor
						// indoor hum
						DoIndoorHumidity(PacketBuffer[offset + 3]);
						// indoor temp
						if ((PacketBuffer[offset + 2] & 0x80) == 0x80)
						{
							sign = -1;
						}
						else
						{
							sign = 1;
						}
						DoIndoorTemp(ConvertTempCToUser(sign*((PacketBuffer[offset + 2] & 0xF)*256 + PacketBuffer[offset + 1]))/10.0);
					}
					else if (sensornumber == cumulus.WMR200TempChannel)
					{
						// channel 1 outdoor sensor
						// outdoor humidity
						DoOutdoorHumidity(PacketBuffer[offset + 3],timestamp);
						// outdoor temp
						if ((PacketBuffer[offset + 2] & 0x80) == 0x80)
						{
							sign = -1;
						}
						else
						{
							sign = 1;
						}
						DoOutdoorTemp(ConvertTempCToUser((sign*((PacketBuffer[offset + 2] & 0xF)*256 + PacketBuffer[offset + 1]))/10.0),timestamp);

						// update heating/cooling degree days
						UpdateDegreeDays(interval);

						// add in 'archivePeriod' minutes worth of temperature to the temp samples
						tempsamplestoday += interval;
						TempTotalToday += (OutdoorTemperature*interval);

						// update chill hours
						if (OutdoorTemperature < cumulus.ChillHourThreshold)
						{
							// add 1 minute to chill hours
							ChillHours += (interval/60);
						}

						// dew point
						if ((PacketBuffer[offset + 5] & 0x80) == 0x80)
						{
							sign = -1;
						}
						else
						{
							sign = 1;
						}

						DoOutdoorDewpoint(ConvertTempCToUser((sign*((PacketBuffer[offset + 5] & 0xF)*256 + PacketBuffer[offset + 4]))/10.0),timestamp);
					}

					if (sensornumber > 1 && sensornumber < 11)
					{
						// extra sensors
						// humidity
						DoExtraHum(PacketBuffer[offset + 3],sensornumber);

						// temperature
						if ((PacketBuffer[offset + 2] & 0x80) == 0x80)
						{
							sign = -1;
						}
						else
						{
							sign = 1;
						}

						DoExtraTemp(ConvertTempCToUser((sign*((PacketBuffer[offset + 2] & 0xF)*256 + PacketBuffer[offset + 1]))/10.0),sensornumber);

						// dew point
						if ((PacketBuffer[offset + 5] & 0x80) == 0x80)
						{
							sign = -1;
						}
						else
						{
							sign = 1;
						}

						DoExtraDP(ConvertTempCToUser((sign*((PacketBuffer[offset + 5] & 0xF)*256 + PacketBuffer[offset + 4]))/10.0),sensornumber);
					}
				}
				catch (Exception Ex)
				{
					cumulus.LogMessage("History packet too short. Sensor count = " + sensorcount);
					cumulus.LogMessage(Ex.Message);
				}
			}

			if ((PacketBuffer[26] & 0x20) == 0x20)
			{
				// no wind chill, use current temp if available
				// note that even if Cumulus is set to calculate wind chill
				// it can't/won't do it if temp isn't available, so don't
				// bother calling anyway
				if (TempReadyToPlot)
				{
					DoWindChill(OutdoorTemperature,timestamp);
				}
			}
			else
			{
				// wind chill is in Fahrenheit!
				wc = MeteoLib.FtoC((PacketBuffer[25] + (PacketBuffer[26] & 0xF)*256)/10.0);

				if (cumulus.TempUnit == 0)
				{
					wc = MeteoLib.FtoC(wc);
				}

				DoWindChill(wc,timestamp);
			}

			// rain
			counter = ((PacketBuffer[14]*256) + PacketBuffer[13])/100.0;

			rate = ((PacketBuffer[8]*256) + PacketBuffer[7])/100.0;

			DoRain(ConvertRainINToUser(counter),ConvertRainINToUser(rate),timestamp);

			// UV
			if (PacketBuffer[27] != 0xFF)
			{
				DoUV(PacketBuffer[27] & 0xFF,timestamp);
			}

			// do solar rad, even though there's no sensor,
			// just to calculate theoretical max for consistency
			DoSolarRad(0, timestamp);

			DoApparentTemp(timestamp);

			cumulus.DoLogFile(timestamp,false);

			if (cumulus.LogExtraSensors)
			{
				cumulus.DoExtraLogFile(timestamp);
			}

			AddLastHourDataEntry(timestamp, Raincounter, OutdoorTemperature);
			AddLast3HourDataEntry(timestamp, Pressure, OutdoorTemperature);
			RemoveOldLHData(timestamp);
			RemoveOldL3HData(timestamp);
			AddGraphDataEntry(timestamp, Raincounter, RainToday, RainRate, OutdoorTemperature, OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex, IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing, OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV);
			RemoveOldGraphData(timestamp);
			AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex, OutdoorHumidity,
							Pressure, RainToday, SolarRad, UV, Raincounter);
			DoTrendValues(timestamp);
			UpdatePressureTrendString();
			UpdateStatusPanel(timestamp);
			// Add current data to the lists of web service updates to be done
			cumulus.AddToWebServiceLists(timestamp);
		}

		private void ProcessWMR200Packet()
		{
			String Str = "";
			for (int i = 0; i < PacketBuffer[1]; i++)
			{
				Str += PacketBuffer[i].ToString("X2");
				Str += " ";
			}

			cumulus.LogDataMessage(DateTime.Now + " Packet:" + Str);

			if (CRCOK())
			{
				switch (PacketBuffer[0])
				{
					case HISTORY_DATA_PACKET_TYPE:
						if (GettingHistory)
						{
							cumulus.LogMessage("Packet:" + Str);
							ProcessHistoryDataPacket();
							LivePacketCount = 0;
						}
						else
						{
							cumulus.LogMessage("WMR200 history packet received when not processing history");
						}
						break;
					case WIND_PACKET_TYPE:

						if (GettingHistory)
						{
							LivePacketCount++;
							cumulus.LogMessage("Wind packet received");
						}
						else
						{
							cumulus.LogDebugMessage("Wind packet received");
							ProcessWindPacket();
							UpdateStatusPanel(DateTime.Now);
						}
						break;
					case RAIN_PACKET_TYPE:
						if (GettingHistory)
						{
							LivePacketCount++;
							cumulus.LogMessage("Rain packet received");
						}
						else
						{
							cumulus.LogDebugMessage("Rain packet received");
							ProcessRainPacket();
							UpdateStatusPanel(DateTime.Now);
						}
						break;
					case UV_PACKET_TYPE:
						if (GettingHistory)
						{
							LivePacketCount++;
							cumulus.LogMessage("UV packet received");
						}
						else
						{
							cumulus.LogDebugMessage("UV packet received");
							ProcessUVPacket();
							UpdateStatusPanel(DateTime.Now);
						}
						break;
					case BARO_PACKET_TYPE:
						if (GettingHistory)
						{
							LivePacketCount++;
							cumulus.LogMessage("Baro packet received");
						}
						else
						{
							cumulus.LogDebugMessage("Baro packet received");
							ProcessBaroPacket();
							UpdateStatusPanel(DateTime.Now);
						}
						break;
					case TEMPHUM_PACKET_TYPE:
						if (GettingHistory)
						{
							LivePacketCount++;
							cumulus.LogMessage("Temp packet received");
						}
						else
						{
							cumulus.LogDebugMessage("Temp packet received");
							ProcessTempHumPacket();
							UpdateStatusPanel(DateTime.Now);
						}
						break;
					case STATUS_PACKET_TYPE:
						cumulus.LogDebugMessage("Status packet received");
						ProcessStatusPacket();
						break;
					default:
						cumulus.LogDebugMessage("Unknown packet received: " + Str);
						return;
				}

				UpdateStatusPanel(DateTime.Now);

				if (GettingHistory)
				{
					cumulus.LogMessage("Sending DA");
					SendDA();
				}
				else
				{
					UpdateMQTT();
				}

				//UpdateStatusPanel;
			}
			else
			{
				cumulus.LogMessage("WMR200: Invalid CRC");
			}

			if (GettingHistory)
			{
				if (LivePacketCount >= 10)
				{
					// we've had 10 consecutive live packets during history download. Assume history download complete
					GettingHistory = false;
					SwitchToNormalRunning();
				}
			}
		}

		private int MinutesBetween(DateTime endTime, DateTime startTime)
		{
			return endTime.Subtract(startTime).Minutes;
		}

		public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			cumulus.LogMessage("Serial data recieved for USB station?");
		}
	}
}
