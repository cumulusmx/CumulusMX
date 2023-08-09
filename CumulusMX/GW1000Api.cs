using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CumulusMX
{
	internal class GW1000Api
	{
		private readonly Cumulus cumulus;
		private TcpClient socket;
		private string ipAddress = null;
		private int tcpPort = 0;
		private bool connecting = false;

		internal GW1000Api(Cumulus cuml)
		{
			cumulus = cuml;
		}


		internal bool OpenTcpPort(string ipaddr, int port)
		{
			ipAddress = ipaddr;
			tcpPort = port;
			connecting = true;

			CloseTcpPort();
			int attempt = 0;

			// Creating the new TCP socket effectively opens it - specify IP address or domain name and port
			while (attempt < 5 && socket == null)
			{
				attempt++;
				cumulus.LogDebugMessage("Ecowitt Gateway Connect attempt " + attempt);
				try
				{
					socket = new TcpClient(ipaddr, port);

					if (!socket.Connected)
					{
						cumulus.LogDebugMessage("Error: Ecowitt Gateway Connect attempt " + attempt + " FAILED");

						try
						{
							socket.Close();
							socket.Dispose();
						}
						catch
						{ }
						socket = null;
						Thread.Sleep(5000 * attempt);
					}

				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Error opening TCP port: " + ex.Message);
					Thread.Sleep(5000 * attempt);
				}
			}

			// Set the timeout of the underlying stream
			if (socket != null)
			{
				try
				{
					if (socket.Connected)
					{
						cumulus.LogDebugMessage("Ecowitt Gateway reconnected");
						connecting = false;
					}
					else
					{
						cumulus.LogDebugMessage("Ecowitt Gateway failed to reconnect");
					}

				}
				catch (ObjectDisposedException)
				{
					socket = null;
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Error reconnecting Ecowitt Gateway: " + ex.Message);
				}
			}
			else
			{
				cumulus.LogDebugMessage("Ecowitt Gateway connect failed");
				connecting = false;
				return false;
			}

			return true;
		}

		internal void CloseTcpPort()
		{
			try
			{
				if (socket != null)
				{
					if (socket.Connected)
					{
						socket.GetStream().WriteByte(10);
					}
					socket.Close();
				}
			}
			catch (ObjectDisposedException)
			{
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error closing TCP port: " + ex.Message);
			}
			finally
			{
				socket = null;
			}
		}

		internal byte[] DoCommand(Commands command, byte[] data = null)
		{
			if (!Connected)
			{
				// Are we already reconnecting?
				if (connecting)
					// yep - so wait reconnect to complete
					return null;
				// no, try a reconnect
				else if (!OpenTcpPort(ipAddress, tcpPort))
					// that didn;t work, give up and return nothing
					return null;
			}

			var buffer = new byte[2028];
			var bytesRead = 0;
			var cmdName = command.ToString();

			byte[] bytes;
			if (data == null)
			{
				var payload = new CommandPayload(command);
				bytes = payload.Data;
			}
			else
			{
				var payload = new CommandWritePayload(command, data);
				bytes = payload.Data;
			}

			var tmrComm = new WeatherStation.CommTimer();

			try
			{
				var stream = socket.GetStream();
				stream.ReadTimeout = 2500;
				stream.Write(bytes, 0, bytes.Length);

				bytesRead = stream.Read(buffer, 0, buffer.Length);

				// Check the response is to our command and checksum is OK
				if (bytesRead == 0 || buffer[2] != (byte) command || !ChecksumOk(buffer, (int) Enum.Parse(typeof(CommandRespSize), cmdName)))
				{
					if (bytesRead > 0)
					{
						cumulus.LogMessage($"DoCommand({cmdName}): Invalid response");
						cumulus.LogDebugMessage($"command resp={buffer[2]}, checksum=" + (ChecksumOk(buffer, (int) Enum.Parse(typeof(CommandRespSize), cmdName)) ? "OK" : "BAD"));
						cumulus.LogDataMessage("Received " + BitConverter.ToString(buffer, 0, bytesRead - 1));
					}
					else
					{
						cumulus.LogMessage($"DoCommand({cmdName}): No response received");
					}
					return null;
				}
				else
				{
					cumulus.LogDebugMessage($"DoCommand({cmdName}): Valid response");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"DoCommand({cmdName}): Error - " + ex.Message);
				cumulus.LogMessage("Attempting to reopen the TCP port");
				Thread.Sleep(1000);
				OpenTcpPort(ipAddress, tcpPort);
				return null;
			}
			// Return the data we want out of the buffer
			if (bytesRead > 0)
			{
				var data1 = new byte[bytesRead];
				Array.Copy(buffer, data1, data1.Length);
				cumulus.LogDataMessage("Received: " + BitConverter.ToString(data1));
				return data1;
			}

			return null;
		}

		private bool ChecksumOk(byte[] data, int lengthBytes)
		{
			ushort size;

			// general response 1 byte size         2 byte size
			// 0   - 0xff - header                  0   - 0xff - header
			// 1   - 0xff                           1   - 0xff
			// 2   - command                        2   - command
			// 3   - total size of response         3   - size1
			// 4-X - data                           4   - size2
			// X+1 - checksum                       5-X - data
			//                                      X+1 - checksum

			if (lengthBytes == 1)
			{
				size = data[3];
			}
			else
			{
				size = ConvertBigEndianUInt16(data, 3);
			}

			// sanity check the size
			if (size + 3 + lengthBytes > data.Length)
			{
				cumulus.LogMessage($"Ckecksum: Error - Calculated data length [{size}] exceeds the buffer size!");
				return false;
			}

			byte checksum = (byte) (data[2] + data[3]);
			for (var i = 4; i <= size; i++)
			{
				checksum += data[i];
			}

			if (checksum != data[size + 1])
			{
				cumulus.LogMessage("Checksum: Error - Bad checksum");
				return false;
			}

			return true;
		}


		public bool Connected
		{
			get
			{
				if (socket == null)
				{
					return false;
				}
				else
				{
					try
					{
						return socket.Connected;
					}
					catch (ObjectDisposedException)
					{
						return false;
					}
					catch (Exception ex)
					{
						cumulus.LogDebugMessage("Error getting TCPClient connected status: " + ex.Message);
						return false;
					}
				}
			}
		}

		internal enum Commands : byte
		{
			// General order
			CMD_WRITE_SSID = 0x11,// send router SSID and Password to WiFi module
			CMD_BROADCAST = 0x12,//looking for device inside network. Returned data size is 2 Byte
			CMD_READ_ECOWITT = 0x1E,// read setting for Ecowitt.net
			CMD_WRITE_ECOWITT = 0x1F, // write back setting for Ecowitt.net
			CMD_READ_WUNDERGROUND = 0x20,// read back setting for Wunderground
			CMD_WRITE_WUNDERGROUND = 0x21, // write back setting for Wunderground
			CMD_READ_WOW = 0x22, // read setting for WeatherObservationsWebsite
			CMD_WRITE_WOW = 0x23, // write back setting for WeatherObservationsWebsite
			CMD_READ_WEATHERCLOUD = 0x24,// read setting for Weathercloud
			CMD_WRITE_WEATHERCLOUD = 0x25, // write back setting for Weathercloud
			CMD_READ_SATION_MAC = 0x26,// read  module MAC
			CMD_READ_CUSTOMIZED = 0x2A,// read setting for Customized sever
			CMD_WRITE_CUSTOMIZED = 0x2B, // write back customized sever setting
			CMD_WRITE_UPDATE = 0x43,// update firmware
			CMD_READ_FIRMWARE_VERSION = 0x50,// read back firmware version
			CMD_READ_USER_PATH = 0x51,
			CMD_WRITE_USER_PATH = 0x52,
			// the following commands are only valid for GW1000 and WH2650：
			CMD_GW1000_LIVEDATA = 0x27, // read current，return size is 2 Byte
			CMD_GET_SOILHUMIAD = 0x28,// read Soil moisture Sensor calibration parameter
			CMD_SET_SOILHUMIAD = 0x29, // write back Soil moisture Sensor calibration parameter
			CMD_GET_MulCH_OFFSET = 0x2C, // read multi channel sensor OFFSET value
			CMD_SET_MulCH_OFFSET = 0x2D, // write back multi sensor OFFSET value
			CMD_GET_PM25_OFFSET = 0x2E, // read PM2.5OFFSET value
			CMD_SET_PM25_OFFSET = 0x2F, // write back PM2.5OFFSET value
			CMD_READ_SSSS = 0x30,// read sensor set-up ( sensor frequency, wh24/wh65 sensor)
			CMD_WRITE_SSSS = 0x31,// write back sensor set-up
			CMD_READ_RAINDATA = 0x34,// read rain data
			CMD_WRITE_RAINDATA = 0x35, // write back rain data
			CMD_READ_GAIN = 0x36, // read rain gain
			CMD_WRITE_GAIN = 0x37, // write back rain gain
			CMD_READ_CALIBRATION = 0x38,//  read multiple parameter offset( refer to command description below in detail)
			CMD_WRITE_CALIBRATION = 0x39,//  write back multiple parameter offset
			CMD_READ_SENSOR_ID = 0x3A,//  read Sensors ID
			CMD_WRITE_SENSOR_ID = 0x3B, // write back Sensors ID
			CMD_READ_SENSOR_ID_NEW = 0x3C,
			CMD_WRITE_REBOOT = 0x40,// system reset
			CMD_WRITE_RESET = 0x41,// system default setting reset
			CMD_GET_CO2_OFFSET = 0x53,
			CMD_SET_CO2_OFFSET = 0x54,
			CMD_READ_RSTRAIN_TIME = 0x55,// read rain reset time
			CMD_WRITE_RSTRAIN_TIME = 0x56, // write back rain reset time
			CMD_READ_RAIN = 0x57, // *new* read rain data
			CMD_WRITE_RAIN = 0x58 // *new* write rain data
		}

		private enum CommandRespSize : int
		{
			bytes1 = 1,
			bytes2 = 2,
			CMD_WRITE_SSID = bytes1,
			CMD_BROADCAST = bytes2,
			CMD_READ_ECOWITT = bytes1,
			CMD_WRITE_ECOWITT = bytes1,
			CMD_READ_WUNDERGROUND = bytes1,
			CMD_WRITE_WUNDERGROUND = bytes1,
			CMD_READ_WOW = bytes1,
			CMD_WRITE_WOW = bytes1,
			CMD_READ_WEATHERCLOUD = bytes1,
			CMD_WRITE_WEATHERCLOUD = bytes1,
			CMD_READ_SATION_MAC = bytes1,
			CMD_READ_CUSTOMIZED = bytes1,
			CMD_WRITE_CUSTOMIZED = bytes1,
			CMD_WRITE_UPDATE = bytes1,
			CMD_READ_FIRMWARE_VERSION = bytes1,
			CMD_READ_USER_PATH = bytes1,
			CMD_WRITE_USER_PATH = bytes1,
			// the following commands are only valid for GW1000 and WH2650：
			CMD_GW1000_LIVEDATA = bytes2,
			CMD_GET_SOILHUMIAD = bytes1,
			CMD_SET_SOILHUMIAD = bytes1,
			CMD_GET_MulCH_OFFSET = bytes1,
			CMD_SET_MulCH_OFFSET = bytes1,
			CMD_GET_PM25_OFFSET = bytes1,
			CMD_SET_PM25_OFFSET = bytes1,
			CMD_READ_SSSS = bytes1,
			CMD_WRITE_SSSS = bytes1,
			CMD_READ_RAINDATA = bytes1,
			CMD_WRITE_RAINDATA = bytes1,
			CMD_READ_GAIN = bytes1,
			CMD_WRITE_GAIN = bytes1,
			CMD_READ_CALIBRATION = bytes1,
			CMD_WRITE_CALIBRATION = bytes1,
			CMD_READ_SENSOR_ID = bytes1,
			CMD_WRITE_SENSOR_ID = bytes1,
			CMD_WRITE_REBOOT = bytes1,
			CMD_WRITE_RESET = bytes1,
			CMD_READ_SENSOR_ID_NEW = bytes2,
			CMD_READ_RSTRAIN_TIME = bytes1,
			CMD_WRITE_RSTRAIN_TIME = bytes1,
			CMD_READ_RAIN = bytes2,
			CMD_WRITE_RAIN = bytes1
		}

		internal enum SensorIds
		{
			Wh65,           // 0 00
			Wh68,           // 1 01
			Wh80,           // 2 02
			Wh40,           // 3 03
			Wh25,           // 4 04
			Wh26,           // 5 05
			Wh31Ch1,        // 6 06
			Wh31Ch2,        // 7 07
			Wh31Ch3,        // 8 08
			Wh31Ch4,        // 9 09
			Wh31Ch5,        // 10 0A
			Wh31Ch6,        // 11 0B
			Wh31Ch7,        // 12 0C
			Wh31Ch8,        // 13 0D
			Wh51Ch1,        // 14 0E
			Wh51Ch2,        // 15 0F
			Wh51Ch3,        // 16 10
			Wh51Ch4,        // 17 11
			Wh51Ch5,        // 18 12
			Wh51Ch6,        // 19 13
			Wh51Ch7,        // 20 14
			Wh51Ch8,        // 21 15
			Wh41Ch1,        // 22 16
			Wh41Ch2,        // 23 17
			Wh41Ch3,        // 24 18
			Wh41Ch4,        // 25 19
			Wh57,           // 26 1A
			Wh55Ch1,        // 27 1B
			Wh55Ch2,        // 28 1C
			Wh55Ch3,        // 29 1D
			Wh55Ch4,        // 30 1E
			Wh34Ch1,        // 31 1F
			Wh34Ch2,        // 32 20
			Wh34Ch3,        // 33 21
			Wh34Ch4,        // 34 22
			Wh34Ch5,        // 35 23
			Wh34Ch6,        // 36 24
			Wh34Ch7,        // 37 25
			Wh34Ch8,        // 38 26
			Wh45,           // 39 27
			Wh35Ch1,        // 40 28
			Wh35Ch2,        // 41 29
			Wh35Ch3,        // 42 2A
			Wh35Ch4,        // 43 2B
			Wh35Ch5,        // 44 2C
			Wh35Ch6,        // 45 2D
			Wh35Ch7,        // 46 2E
			Wh35Ch8,        // 47 2F
			Wh90            // 48 30
		};


		[Flags]
		internal enum SigSen : byte
		{
			Wh40 = 1 << 4,
			Wh26 = 1 << 5,
			Wh25 = 1 << 6,
			Wh24 = 1 << 7
		}

		[Flags]
		internal enum Wh31Ch : byte
		{
			Ch1 = 1 << 0,
			Ch2 = 1 << 1,
			Ch3 = 1 << 2,
			Ch4 = 1 << 3,
			Ch5 = 1 << 4,
			Ch6 = 1 << 5,
			Ch7 = 1 << 6,
			Ch8 = 1 << 7
		}


		/*
		private enum _wh41_ch : UInt16
		{
			ch1 = 15 << 0,
			ch2 = 15 << 4,
			ch3 = 15 << 8,
			ch4 = 15 << 12
		}
		*/

		[Flags]
		internal enum Wh51Ch : UInt32
		{
			Ch1 = 1 << 0,
			Ch2 = 1 << 1,
			Ch3 = 1 << 2,
			Ch4 = 1 << 3,
			Ch5 = 1 << 4,
			Ch6 = 1 << 5,
			Ch7 = 1 << 6,
			Ch8 = 1 << 7,
			Ch9 = 1 << 8,
			Ch10 = 1 << 9,
			Ch11 = 1 << 10,
			Ch12 = 1 << 11,
			Ch13 = 1 << 12,
			Ch14 = 1 << 13,
			Ch15 = 1 << 14,
			Ch16 = 1 << 15
		}

		/*
		private enum Wh55Ch : UInt32
		{
			Ch1 = 15 << 0,
			Ch2 = 15 << 4,
			Ch3 = 15 << 8,
			Ch4 = 15 << 12
		}
		*/

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		internal struct BatteryStatus
		{
			public byte single;
			public byte wh31;
			public UInt16 wh51;
			public byte wh57;
			public byte wh68;
			public byte wh80;
			public byte wh45;
			public UInt16 wh41;
			public byte wh55_ch1;
			public byte wh55_ch2;
			public byte wh55_ch3;
			public byte wh55_ch4;
		}

		/*
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		private struct BatteryStatusWH34
		{
			public byte single;
			public byte ch1;
			public byte ch2;
			public byte ch3;
			public byte ch4;
			public byte ch5;
			public byte ch6;
			public byte ch7;
			public byte ch8;
		}
		*/

		/*
		private struct SensorInfo
		{
			string type;
			int id;
			int signal;
			int battery;
			bool present;
		}
		*/

		/*
		private class Sensors
		{
			SensorInfo single { get; set; }
			SensorInfo wh26;
			SensorInfo wh31;
			SensorInfo wh40;
			SensorInfo wh41;
			SensorInfo wh51;
			SensorInfo wh65;
			SensorInfo wh68;
			SensorInfo wh80;
			public Sensors()
			{
			}
		}

		private struct CO2Data
		{
			public Int16 temp;          // °C x10
			public byte hum;			// %
			public UInt16 pm10;			// μg/m³ x10
			public UInt16 pm10_24hr;	// μg/m³ x10
			public UInt16 pm2p5;		// μg/m³ x10
			public UInt16 pm2p5_24hr;	// μg/m³ x10
			public UInt16 co2;			// ppm
			public UInt16 co2_24hr;		// ppm
			public byte batt;			// 0-5
		}
		*/


		private struct CommandPayload
		{
			public readonly byte[] Data;

			public CommandPayload(Commands command) : this()
			{
				// header, header, command, size, checksum
				Data = new byte[] { 0xff, 0xff, (byte) command, 3, (byte) (command + 3) };
			}
			public byte[] Serialise()
			{
				// allocate a byte array for the struct data
				return Data;
			}
		}


		private struct CommandWritePayload
		{
			public byte[] Data;

			public CommandWritePayload(Commands command, byte[] data) : this()
			{
				// header, header, command, size, data[], checksum

				Data = new byte[5 + data.Length];

				Data[0] = (byte) 0xff;
				Data[1] = (byte) 0xff;
				Data[2] = (byte) command;
				Data[3] = (byte) (3 + data.Length);
				data.CopyTo(Data, 4);

				var Checksum = (byte) (command + Data[3]);
				for (int i = 0; i < data.Length; i++)
				{
					Checksum += data[i];
				}
				Data[Data.Length - 1] = Checksum;
			}
		}

		internal static UInt16 ConvertBigEndianUInt16(byte[] array, int start)
		{
			return (UInt16) (array[start] << 8 | array[start + 1]);
		}

		internal static Int16 ConvertBigEndianInt16(byte[] array, int start)
		{
			return (Int16) ((array[start] << 8) + array[start + 1]);
		}

		internal static UInt32 ConvertBigEndianUInt32(byte[] array, int start)
		{
			return (UInt32) (array[start++] << 24 | array[start++] << 16 | array[start++] << 8 | array[start]);
		}

		internal static byte[] ConvertUInt16ToLittleEndianByteArray(UInt16 ui16)
		{
			var arr = BitConverter.GetBytes(ui16);
			Array.Reverse(arr);
			return arr;
		}
		internal static byte[] ConvertUInt16ToBigEndianByteArray(UInt16 ui16)
		{
			return BitConverter.GetBytes(ui16);
		}


	}
}
