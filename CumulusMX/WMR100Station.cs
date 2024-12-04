using System;
using System.Text;
using System.Threading;

using HidSharp;

namespace CumulusMX
{
	internal class WMR100Station : WeatherStation
	{
		private readonly HidStream stream;
		private const int Vendorid = 0x0FDE;
		private const int Productid = 0xCA01;

		private const int BARO_PACKET_TYPE = 0x46;
		private const int TEMP_PACKET_TYPE = 0x42;
		private const int WIND_PACKET_TYPE = 0x48;
		private const int RAIN_PACKET_TYPE = 0x41;
		private const int POND_PACKET_TYPE = 0x44;
		private const int UV_PACKET_TYPE = 0x47;
		private const int DATE_PACKET_TYPE = 0x60;

		private const int BARO_PACKET_LENGTH = 8;
		private const int TEMP_PACKET_LENGTH = 12;
		private const int WIND_PACKET_LENGTH = 11;
		private const int RAIN_PACKET_LENGTH = 17;
		private const int UV_PACKET_LENGTH = 6;
		private const int DATE_PACKET_LENGTH = 12;
		private const int POND_PACKET_LENGTH = 7;

		private readonly byte[] packetBuffer;
		private int currentPacketLength;
		private int currentPacketType = 255;
		private const int PacketBufferBound = 255;
		private readonly byte[] usbbuffer = new byte[9];

		private bool stop;

		public WMR100Station(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Manufacturer = Cumulus.OREGONUSB;
			var devicelist = DeviceList.Local;
			var station = devicelist.GetHidDeviceOrNull(Vendorid, Productid);

			if (station != null)
			{
				cumulus.LogMessage("WMR100 station found");

				if (station.TryOpen(out stream))
				{
					cumulus.LogMessage("Stream opened");
				}

				packetBuffer = new byte[PacketBufferBound];

				WMR200ExtraTempValues = new double[11];
				WMR200ExtraHumValues = new double[11];
				WMR200ChannelPresent = new bool[11];
				WMR200ExtraDPValues = new double[11];

				LoadLastHoursFromDataLogs(DateTime.Now);
			}
			else
			{
				cumulus.LogErrorMessage("WMR100 station not found!");
				Cumulus.LogConsoleMessage("WMR100 station not found!", ConsoleColor.Red);
			}
		}

		public override void Start()
		{
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();

			cumulus.LogMessage("Sending reset");
			SendReset();
			cumulus.LogMessage("Start loop");
			int responseLength;
			int startByte;
			int offset;

			// Returns 9-byte USB packet, with report ID in first byte
			responseLength = 9;
			startByte = 1;
			offset = 0;

			try
			{
				while (!stop)
				{
					cumulus.LogDebugMessage("Calling Read, current packet length = " + currentPacketLength);

					try
					{
						stream.Read(usbbuffer, offset, responseLength);

						if (stop) break;

						StringBuilder str = new();

						for (int I = startByte; I < responseLength; I++)
						{
							str.Append(" " + usbbuffer[I].ToString("X2"));
						}

						cumulus.LogDataMessage(str.ToString());

						// Number of valid bytes is in first byte
						int dataLength = usbbuffer[1];
						cumulus.LogDebugMessage("data length = " + dataLength);

						for (int i = 1; i <= dataLength; i++)
						{
							byte c = usbbuffer[i + 1];
							switch (currentPacketLength)
							{
								case 0: // We're looking for the start of a packet
									if (c == 0xFF)
									{
										// Possible start of packet
										currentPacketLength = 1;
									}
									break;
								case 1: // We're looking for the second start-of-packet character
									if (c == 0xFF)
									{
										// Possible continuation
										currentPacketLength = 2;
									}
									else
									{
										// Incorrect sequence, start again
										currentPacketLength = 0;
									}
									break;
								case 2: // This is typically a flags byte, and will be the first byte of our actual data packet
									packetBuffer[0] = c;
									currentPacketLength = 3;
									break;
								default: // We've had the packet header and the flags byte, continue collecting the packet
									packetBuffer[currentPacketLength - 2] = c;
									currentPacketLength++;
									if (currentPacketLength == 4)
									{
										currentPacketType = c;
										cumulus.LogDebugMessage("Current packet type: " + currentPacketType.ToString("X2"));
									}
									if (currentPacketLength - 2 == WMR100PacketLength(currentPacketType))
									{
										// We've collected a complete packet, process it
										ProcessWMR100Packet();
										// Get ready for the next packet
										currentPacketLength = 0;
										currentPacketType = 255;
									}
									break;
							} // end of case for current packet length
						}
					}
					catch (Exception ex)
					{
						// Might just be a timeout, which is normal, so debug log only
						cumulus.LogDebugMessage("Data read loop: " + ex.Message);
					}
				}
				CheckBatteryStatus();
			}

			// Catch the ThreadAbortException
			catch (ThreadAbortException)
			{
				// do nothing
			}
		}

		private static int WMR100PacketLength(int packettype)
		{
			return packettype switch
			{
				BARO_PACKET_TYPE => BARO_PACKET_LENGTH,
				TEMP_PACKET_TYPE => TEMP_PACKET_LENGTH,
				WIND_PACKET_TYPE => WIND_PACKET_LENGTH,
				RAIN_PACKET_TYPE => RAIN_PACKET_LENGTH,
				UV_PACKET_TYPE => UV_PACKET_LENGTH,
				DATE_PACKET_TYPE => DATE_PACKET_LENGTH,
				POND_PACKET_TYPE => POND_PACKET_LENGTH,
				_ => 255,
			};
		}

		/*
		private void ClearPacketBuffer()
		{
			for (int I = 0; I < PacketBufferBound; I++)
			{
				PacketBuffer[I] = 0;
			}
			CurrentPacketLength = 0;
		}
		*/

		/*
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
				cumulus.LogDebugMessage(Str);
			}
		}
		*/

		private void ProcessWMR100Packet()
		{
			StringBuilder str = new();

			for (int i = 0; i <= currentPacketLength - 3; i++)
			{
				str.Append(" " + packetBuffer[i].ToString("X2"));
			}

			cumulus.LogDataMessage("Packet:" + str);

			if (CRCOK())
			{
				switch (currentPacketType)
				{
					case BARO_PACKET_TYPE:
						ProcessBaroPacket();
						break;
					case TEMP_PACKET_TYPE:
						ProcessTempPacket();
						break;
					case WIND_PACKET_TYPE:
						ProcessWindPacket();
						break;
					case RAIN_PACKET_TYPE:
						ProcessRainPacket();
						break;
					case UV_PACKET_TYPE:
						ProcessUVPacket();
						break;
					case DATE_PACKET_TYPE:
						ProcessDatePacket();
						break;
					case POND_PACKET_TYPE:
						ProcessPondPacket();
						break;
					default:
						cumulus.LogWarningMessage("Unknown packet type: " + currentPacketType.ToString("X2"));
						return;
				}

				UpdateStatusPanel(DateTime.Now);
				UpdateMQTT();
			}
			else
			{
				cumulus.LogDebugMessage("Invalid CRC");
			}
		}

		private void ProcessPondPacket()
		{
			cumulus.LogDebugMessage("Pond packet");
			int sensor = packetBuffer[2] & 0xF;
			int sign;

			if ((sensor > 1) && (sensor < 11))
			{
				WMR200ChannelPresent[sensor] = true;
				// Humidity n/a
				WMR200ExtraHumValues[sensor] = 0;

				// temp
				if ((packetBuffer[4] & 0x80) == 0x80)
					sign = -1;
				else
					sign = 1;

				double num = (sign * ((packetBuffer[4] & 0xF) * 256 + packetBuffer[3])) / 10.0;

				WMR200ExtraTempValues[sensor] = ConvertUnits.TempCToUser(num);
				DoExtraTemp(WMR200ExtraTempValues[sensor], sensor);

				// outdoor dewpoint - n/a

				WMR200ExtraDPValues[sensor] = 0;
				ExtraSensorsDetected = true;
			}
		}

		private void ProcessUVPacket()
		{
			cumulus.LogDebugMessage("UV packet");
			var num = packetBuffer[3] & 0xF;

			UVBattStatus = packetBuffer[0] & 0x4;

			if (num < 0)
				num = 0;

			if (num > 16)
				num = 16;

			DoUV(num, DateTime.Now);

			// UV value is stored as channel 1 of the extra sensors
			WMR200ExtraHumValues[1] = num;

			ExtraSensorsDetected = true;

			WMR200ChannelPresent[1] = true;
		}

		private void ProcessRainPacket()
		{
			cumulus.LogDebugMessage("Rain packet");

			RainBattStatus = packetBuffer[0] & 0x4;

			double counter = ((packetBuffer[9] * 256) + packetBuffer[8]) / 100.0;

			double rate = ((packetBuffer[3] * 256) + packetBuffer[2]) / 100.0;

			// check for overflow  (9999 mm = approx 393 in) and set to 999 mm/hr
			if (rate > 393)
			{
				rate = 39.33;
			}

			DoRain(ConvertUnits.RainINToUser(counter), ConvertUnits.RainINToUser(rate), DateTime.Now);
		}

		private void ProcessWindPacket()
		{
			cumulus.LogDebugMessage("Wind packet");

			WindBattStatus = packetBuffer[0] & 0x4;

			DateTime now = DateTime.Now;

			double wc;

			// bearing
			double b = (packetBuffer[2] & 0xF) * 22.5;
			// gust
			double g = ((packetBuffer[5] & 0xF) * 256 + packetBuffer[4]) / 10.0;
			// average
			double a = ((packetBuffer[6] * 16) + (packetBuffer[5] / 16)) / 10.0;

			DoWind(ConvertUnits.WindMSToUser(g), (int) (b), ConvertUnits.WindMSToUser(a), now);

			if ((packetBuffer[8] & 0x20) == 0x20)
			{
				// no wind chill, use current temp if available
				// note that even if (Cumulus is set to calculate wind chill
				// it can't/won't do it if (temp isn't available, so don't
				// bother calling anyway

				if (TempReadyToPlot)
				{
					DoWindChill(-999, now);
				}
			}
			else
			{
				// wind chill is in Fahrenheit!
				wc = (packetBuffer[7] + (packetBuffer[8] & 0xF) * 256) / 10.0;

				if ((packetBuffer[8] & 0x80) == 0x80)
					// wind chill negative
					wc = -wc;

				if ((cumulus.Units.Rain == 0))
					// convert to C
					wc = (wc - 32) / 1.8;

				DoWindChill(wc, now);
			}
		}

		private void ProcessTempPacket()
		{
			TempBattStatus = packetBuffer[0] & 0x4;

			// which sensor is this for? 0 = indoor, 1 = outdoor, n = extra
			int sensor = packetBuffer[2] & 0xF;
			DateTime Now = DateTime.Now;

			int sign;
			double num;

			cumulus.LogDebugMessage("Temp/hum packet, ch = " + sensor);

			if (sensor == cumulus.WMR200TempChannel)
			{
				// outdoor hum
				DoOutdoorHumidity(packetBuffer[5], DateTime.Now);

				// outdoor temp
				if ((packetBuffer[4] & 0x80) == 0x80)
					sign = -1;
				else
					sign = 1;

				num = (sign * ((packetBuffer[4] & 0xF) * 256 + packetBuffer[3])) / 10.0;
				DoOutdoorTemp(ConvertUnits.TempCToUser(num), Now);

				// outdoor dewpoint
				if ((packetBuffer[7] & 0x80) == 0x80)
					sign = -1;
				else
					sign = 1;

				num = (sign * ((packetBuffer[7] & 0xF) * 256 + packetBuffer[6])) / 10.0;
				DoOutdoorDewpoint(ConvertUnits.TempCToUser(num), Now);

				DoApparentTemp(Now);
				DoFeelsLike(Now);
				DoHumidex(Now);
				DoCloudBaseHeatIndex(Now);
			}
			else if (sensor == 0)
			{
				// indoor hum
				DoIndoorHumidity(packetBuffer[5]);

				// outdoor temp
				if ((packetBuffer[4] & 0x80) == 0x80)
					sign = -1;
				else
					sign = 1;

				num = (sign * ((packetBuffer[4] & 0xF) * 256 + packetBuffer[3])) / 10.0;
				DoIndoorTemp(ConvertUnits.TempCToUser(num));
			}

			if ((sensor > 1) && (sensor < 11))
			{
				WMR200ChannelPresent[sensor] = true;
				// outdoor hum
				WMR200ExtraHumValues[sensor] = packetBuffer[5];

				DoExtraHum(WMR200ExtraHumValues[sensor], sensor);

				// outdoor temp
				if ((packetBuffer[4] & 0x80) == 0x80)
					sign = -1;
				else
					sign = 1;

				num = (sign * ((packetBuffer[4] & 0xF) * 256 + packetBuffer[3])) / 10.0;

				WMR200ExtraTempValues[sensor] = ConvertUnits.TempCToUser(num);
				DoExtraTemp(WMR200ExtraTempValues[sensor], sensor);

				// outdoor dewpoint
				if ((packetBuffer[7] & 0x80) == 0x80)
					sign = -1;
				else
					sign = 1;

				num = (sign * ((packetBuffer[7] & 0xF) * 256 + packetBuffer[6])) / 10.0;
				WMR200ExtraDPValues[sensor] = ConvertUnits.TempCToUser(num);
				DoExtraDP(WMR200ExtraDPValues[sensor], sensor);
				ExtraSensorsDetected = true;
			}
		}

		private void ProcessBaroPacket()
		{
			cumulus.LogDebugMessage("Barometer packet");
			double num = ((packetBuffer[5] & 0xF) * 256) + packetBuffer[4];

			double slp = ConvertUnits.PressMBToUser(num);

			num = ((packetBuffer[3] & 0xF) * 256) + packetBuffer[2];

			DoStationPressure(ConvertUnits.PressMBToUser(num));

			DoPressure(slp, DateTime.Now);

			int forecast = packetBuffer[3] / 16;
			var fcstr = forecast switch
			{
				0 => "Partly Cloudy",
				1 => "Rainy",
				2 => "Cloudy",
				3 => "Sunny",
				4 => "Clear",
				5 => "Snowy",
				6 => "Partly Cloudy",
				_ => "Unknown",
			};
			DoForecast(fcstr, false);
		}

		private static void ProcessDatePacket()
		{
			// do nothing
		}

		private Boolean CRCOK()
		{
			var packetLen = currentPacketLength - 2;

			if (packetLen < 3)
			{
				return true;
			}
			else
			{
				// packet CRC is in last two bytes, low byte then high byte
				var packetCRC = (packetBuffer[packetLen - 1] * 256) + packetBuffer[packetLen - 2];

				var calculatedCRC = 0;

				// CRC is calculated by summing all but the last two bytes
				for (int i = 0; i <= packetLen - 3; i++)
				{
					calculatedCRC += packetBuffer[i];
				}

				cumulus.LogDebugMessage("Packet CRC = " + packetCRC);
				cumulus.LogDebugMessage("Calculated CRC = " + calculatedCRC);

				return (packetCRC == calculatedCRC);
			}
		}

		private void SendReset()
		{
			cumulus.LogDebugMessage("Sending reset");

			byte[] reset;

			if (cumulus.IsOSX)
			{
				reset = [0x20, 0x00, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00];
			}
			else
			{
				reset = [0x00, 0x20, 0x00, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00];
			}

			try
			{
				stream.Write(reset);
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"SendReset: Error - {ex.Message}");
			}
		}

		private void CheckBatteryStatus()
		{
			if (IndoorBattStatus == 4 || WindBattStatus == 4 || RainBattStatus == 4 || TempBattStatus == 4 || UVBattStatus == 4)
			{
				cumulus.BatteryLowAlarm.Triggered = true;
			}
			else if (cumulus.BatteryLowAlarm.Triggered)
			{
				cumulus.BatteryLowAlarm.Triggered = false;
			}
		}

		public override void Stop()
		{
			stop = true;
			StopMinuteTimer();
		}
	}
}
