using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Net;
using System.Timers;
using System.Runtime.Remoting.Messaging;

namespace CumulusMX
{
	internal class GW1000Station : WeatherStation
	{
		private readonly string ipaddr;
		private const int AtPort = 45000;
		private int lastMinute;
		private bool tenMinuteChanged = true;

		private TcpClient socket;
		private bool connectedOk = false;
		private bool dataReceived = false;

		private readonly System.Timers.Timer tmrDataWatchdog;
		private bool stop = false;

		private enum Commands : byte {
			// General order
			CMD_WRITE_SSID = 0x11,// send router SSID and Password to wifi module
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
			CMD_GET_SOILHUMIAD = 0x28,// read Soilmoisture Sensor calibration parameter
			CMD_SET_SOILHUMIAD = 0x29, // write back Soilmoisture Sensor calibration parameter
			CMD_GET_MulCH_OFFSET = 0x2C, // read multi channel sensor OFFSET value
			CMD_SET_MulCH_OFFSET = 0x2D, // write back multi sensor OFFSET value
			CMD_GET_PM25_OFFSET = 0x2E, // read PM2.5OFFSET value
			CMD_SET_PM25_OFFSET = 0x2F, // write back PM2.5OFFSET value
			CMD_READ_SSSS = 0x30,// read sensor setup ( sensor frequency, wh24/wh65 sensor)
			CMD_WRITE_SSSS = 0x31,// write back sensor setup
			CMD_READ_RAINDATA = 0x34,// read rain data
			CMD_WRITE_RAINDATA = 0x35, // write back rain data
			CMD_READ_GAIN = 0x36, // read rain gain
			CMD_WRITE_GAIN = 0x37, // write back rain gain
			CMD_READ_CALIBRATION = 0x38,//  read multiple parameter offset( refer to command description below in detail)
			CMD_WRITE_CALIBRATION = 0x39,//  write back multiple parameter offset
			CMD_READ_SENSOR_ID = 0x3A,//  read Sensors ID
			CMD_WRITE_SENSOR_ID = 0x3B, // write back Sensors ID
			CMD_READ_SENSOR_ID_NEW = 0x3C,
			CMD_WRITE_REBOOT = 0x40,// system rebset
			CMD_WRITE_RESET = 0x41,// system default setting reset
			CMD_GET_CO2_OFFSET = 0x53,
			CMD_SET_CO2_OFFSET = 0x54
		}

		private enum CommandRespSize : int
		{
			CMD_WRITE_SSID = 1,
			CMD_BROADCAST = 2,
			CMD_READ_ECOWITT = 1,
			CMD_WRITE_ECOWITT = 1,
			CMD_READ_WUNDERGROUND = 1,
			CMD_WRITE_WUNDERGROUND = 1,
			CMD_READ_WOW = 1,
			CMD_WRITE_WOW = 1,
			CMD_READ_WEATHERCLOUD = 1,
			CMD_WRITE_WEATHERCLOUD = 1,
			CMD_READ_SATION_MAC = 1,
			CMD_READ_CUSTOMIZED = 1,
			CMD_WRITE_CUSTOMIZED = 1,
			CMD_WRITE_UPDATE = 1,
			CMD_READ_FIRMWARE_VERSION = 1,
			CMD_READ_USER_PATH = 1,
			CMD_WRITE_USER_PATH = 1,
			// the following commands are only valid for GW1000 and WH2650：
			CMD_GW1000_LIVEDATA = 2,
			CMD_GET_SOILHUMIAD = 1,
			CMD_SET_SOILHUMIAD = 1,
			CMD_GET_MulCH_OFFSET = 1,
			CMD_SET_MulCH_OFFSET = 1,
			CMD_GET_PM25_OFFSET = 1,
			CMD_SET_PM25_OFFSET = 1,
			CMD_READ_SSSS = 1,
			CMD_WRITE_SSSS = 1,
			CMD_READ_RAINDATA = 1,
			CMD_WRITE_RAINDATA = 1,
			CMD_READ_GAIN = 1,
			CMD_WRITE_GAIN = 1,
			CMD_READ_CALIBRATION = 1,
			CMD_WRITE_CALIBRATION = 1,
			CMD_READ_SENSOR_ID = 1,
			CMD_WRITE_SENSOR_ID = 1,
			CMD_WRITE_REBOOT = 1,
			CMD_WRITE_RESET = 1,
		}

		[Flags] private enum SigSen : byte
		{
			Wh40 = 1 << 4,
			Wh26 = 1 << 5,
			Wh25 = 1 << 6,
			Wh24 = 1 << 7
		}

		[Flags] private enum Wh31Ch : byte
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

		[Flags] private enum Wh51Ch : UInt32
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

		private enum Wh55Ch : UInt32
		{
			Ch1 = 15 << 0,
			Ch2 = 15 << 4,
			Ch3 = 15 << 8,
			Ch4 = 15 << 12
		}

		private enum SensorIds
		{
			Wh65,			// 0
			Wh68,			// 1
			Wh80,			// 2
			Wh40,			// 3
			Wh25,			// 4
			Wh26,			// 5
			Wh31Ch1,		// 6
			Wh31Ch2,		// 7
			Wh31Ch3,		// 8
			Wh31Ch4,		// 9
			Wh31Ch5,		// 10
			Wh31Ch6,		// 11
			Wh31Ch7,		// 12
			Wh31Ch8,		// 13
			Wh51Ch1,		// 14
			Wh51Ch2,		// 15
			Wh51Ch3,		// 16
			Wh51Ch4,		// 17
			Wh51Ch5,		// 18
			Wh51Ch6,		// 19
			Wh51Ch7,		// 20
			Wh51Ch8,		// 21
			Wh41Ch1,		// 22
			Wh41Ch2,		// 23
			Wh41Ch3,		// 24
			Wh41Ch4,		// 25
			Wh57,			// 26
			Wh55Ch1,		// 27
			Wh55Ch2,		// 28
			Wh55Ch3,		// 29
			Wh55Ch4,		// 30
			Wh34Ch1,		// 31
			Wh34Ch2,		// 32
			Wh34Ch3,		// 33
			Wh34Ch4,		// 34
			Wh34Ch5,		// 35
			Wh34Ch6,		// 36
			Wh34Ch7,		// 37
			Wh34Ch8,		// 38
			Wh45			// 39
		};

		public GW1000Station(Cumulus cumulus) : base(cumulus)
		{

			cumulus.Manufacturer = cumulus.ECOWITT;
			cumulus.AirQualityUnitText = "µg/m³";
			cumulus.SoilMoistureUnitText = "%";
			// GW1000 does not provide average wind speeds
			cumulus.StationOptions.UseWind10MinAve = true;
			cumulus.StationOptions.UseSpeedForAvgCalc = false;

			tmrDataWatchdog = new System.Timers.Timer();

			// GW1000 does not send DP, so force MX to calculate it
			cumulus.StationOptions.CalculatedDP = true;

			ipaddr = cumulus.Gw1000IpAddress;

			if (cumulus.Gw1000AutoUpdateIpAddress)
			{
				var discoveredIp = DiscoverGW1000();

				if (discoveredIp != "0" && discoveredIp != ipaddr)
				{
					cumulus.LogMessage("Discovered a new IP address for the GW1000 that does not match our current one");
					cumulus.LogMessage($"Changing previous IP address: {ipaddr} to {discoveredIp}");
					ipaddr = discoveredIp;
					cumulus.Gw1000IpAddress = ipaddr;
					cumulus.WriteIniFile();
				}
			}

			cumulus.LogMessage("Using IP address = " + ipaddr + " Port = " + AtPort);
			socket = OpenTcpPort();

			connectedOk = socket != null;

			if (connectedOk)
			{
				cumulus.LogMessage("Connected OK");
				//Console.WriteLine("Connected to station");
				cumulus.LogConsoleMessage("Connected to station");
			}
			else
			{
				cumulus.LogMessage("Not Connected");
				cumulus.LogConsoleMessage("Unable to connect to station");
			}

			if (connectedOk)
			{
				// Get the firmware version as check we are communicating
				GW1000FirmwareVersion = GetFirmwareVersion();
				cumulus.LogMessage($"GW1000 firmware version: {GW1000FirmwareVersion}");

				GetSensorIds();
			}

			timerStartNeeded = true;
			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);
			DoTrendValues(DateTime.Now);

			// WLL does not provide a forecast string, so use the Cumulus forecast
			cumulus.UseCumulusForecast = true;

			cumulus.LogMessage("Starting GW1000");

			StartLoop();
			bw = new BackgroundWorker();
		}

		private TcpClient OpenTcpPort()
		{
			TcpClient client = null;
			int attempt = 0;

			// Creating the new TCP socket effectively opens it - specify IP address or domain name and port
			while (attempt < 5 && client == null)
			{
				attempt++;
				cumulus.LogDebugMessage("GW1000 Connect attempt " + attempt);
				try
				{
					client = new TcpClient(ipaddr, AtPort);

					if (!client.Connected)
					{
						client = null;
					}

					Thread.Sleep(1000);
				}
				catch
				{
					//MessageBox.Show(ex.Message);
				}
			}

			// Set the timeout of the underlying stream
			if (client != null)
			{
				client.GetStream().ReadTimeout = 2500;
				cumulus.LogDebugMessage("GW1000 reconnected");
			}
			else
			{
				cumulus.LogDebugMessage("GW1000 connect failed");
			}

			return client;
		}

		public override void Start()
		{
			// Wait for the lock
			cumulus.LogDebugMessage("Lock: Station waiting for lock");
			Cumulus.syncInit.Wait();
			cumulus.LogDebugMessage("Lock: Station has the lock");

			cumulus.LogMessage("Start normal reading loop");

			cumulus.LogDebugMessage("Lock: Station releasing lock");
			Cumulus.syncInit.Release();

			tenMinuteChanged = true;
			lastMinute = DateTime.Now.Minute;

			// Start a broadcast watchdog to warn if messages are not being received
			tmrDataWatchdog.Elapsed += DataTimeout;
			tmrDataWatchdog.Interval = 1000 * 30; // timeout after 30 seconds
			tmrDataWatchdog.AutoReset = true;
			tmrDataWatchdog.Start();

			try
			{
				while (!stop)
				{
					if (connectedOk)
					{
						GetLiveData();
					}
					else
					{
						cumulus.LogMessage("Attempting to reconnect to GW1000...");
						socket = OpenTcpPort();
						connectedOk = socket != null;
						if (connectedOk)
						{
							cumulus.LogMessage("Reconnected to GW1000");
							GetLiveData();
						}
					}
					Thread.Sleep(1000 * 10);
				}
			}
			// Catch the ThreadAbortException
			catch (ThreadAbortException)
			{
			}
			finally
			{
				if (socket != null)
				{
					socket.GetStream().WriteByte(10);
					socket.Close();
				}
			}
		}

		public override void Stop()
		{
			cumulus.LogMessage("Closing connection");
			try
			{
				stop = true;
				socket.GetStream().WriteByte(10);
				socket.Close();
				tmrDataWatchdog.Stop();
				StopMinuteTimer();
			}
			catch
			{
			}
		}

		private void bw_DoStart(object sender, DoWorkEventArgs e)
		{
			cumulus.LogDebugMessage("Lock: Station waiting for lock");
			Cumulus.syncInit.Wait();
			cumulus.LogDebugMessage("Lock: Station has the lock");

			// Wait a short while for Cumulus initialisation to complete
			Thread.Sleep(500);
			StartLoop();

			cumulus.LogDebugMessage("Lock: Station releasing lock");
			Cumulus.syncInit.Release();
		}

		private string DiscoverGW1000()
		{
			try
			{
				var udp = new UdpClient();
				const int broadcastPort = 46000;
				const int clientPort = 59387;

				var receiveEp = new IPEndPoint(IPAddress.Any, clientPort);
				//var client = new UdpClient(ClientPort);

				var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				socket.ReceiveTimeout = 2000;
				socket.Bind(receiveEp);
				//socket.Listen(1);

				var groupEp = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
				var sendBytes = new byte[] { 0xff, 0xff, 0x12, 0x03, 0x15 };

				udp.EnableBroadcast = true;
				udp.Send(sendBytes, sendBytes.Length, groupEp);

				var receivedBytes = new byte[35];
				socket.Receive(receivedBytes, 0, receivedBytes.Length, SocketFlags.None);

				string ipAddr = $"{receivedBytes[11]}.{receivedBytes[12]}.{receivedBytes[13]}.{receivedBytes[14]}";

				udp.Close();
				socket.Close();

				if (ipAddr.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Length == 4)
				{
					IPAddress ipAddr2;
					if (IPAddress.TryParse(ipAddr, out ipAddr2))
					{
						cumulus.LogMessage($"Discovered a GW1000 at address {ipaddr}");
						return ipAddr;
					}
					else
					{
						return "0";
					}
				}
				else
				{
					return "0";
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("An error occured during GW1000 auto-discovery");
				cumulus.LogMessage("Error: " + ex.Message);
			}
			return "0";
		}

		private string GetFirmwareVersion()
		{
			var response = "???";
			cumulus.LogMessage("Reading firmware version");

			var data = DoCommand((byte)Commands.CMD_READ_FIRMWARE_VERSION);
			if (null != data && data.Length > 0)
			{
				response = Encoding.ASCII.GetString(data, 5, data[4]);
			}
			return response;
		}

		private bool GetSensorIds()
		{
			cumulus.LogMessage("Reading sensor ids");

			var data = DoCommand((byte)Commands.CMD_READ_SENSOR_ID);

			// expected response
			// 0   - 0xff - header
			// 1   - 0xff - header
			// 2   - 0x3A - sensor id command
			// 3   - 0x?? - size of response
			// 4   - wh65
			// 5-8 - wh65 id
			// 9   - wh65 signal
			// 10  - wh65 battery
			// 11  - wh68
			//       ... etc
			// (??) - 0x?? - checksum

			if (null != data && data.Length > 200)
			{
				for (int i = 4; i < data[3]; i += 7)
				{
					PrintSensorInfo(data, i);
				}
				return true;
			}
			else
			{
				return false;
			}
		}

		private void PrintSensorInfo(byte[] data, int idx)
		{
			var id = ConvertBigEndianUInt32(data, idx + 1);
			var type = Enum.GetName(typeof(SensorIds), data[idx]);

			if (string.IsNullOrEmpty(type))
			{
				type = $"unknown type = {id}";
			}
			switch (id)
			{
				case 0xFFFFFFFE:
					cumulus.LogDebugMessage($" - {type} sensor = disabled");
					break;
				case 0xFFFFFFFF:
					cumulus.LogDebugMessage($" - {type} sensor = registering");
					break;
				default:
					cumulus.LogDebugMessage($" - {type} sensor id = {id} signal = {data[idx+5]} battery = {data[idx+6]}");
					break;
			}
		}

		private void GetLiveData()
		{
			cumulus.LogDebugMessage("Reading live data");

			// set a flag at the start of every 10 minutes to trigger battery status check
			var minute = DateTime.Now.Minute;
			if (minute != lastMinute)
			{
				lastMinute = minute;
				tenMinuteChanged = (minute % 10) == 0;
			}

			var data = DoCommand((byte)Commands.CMD_GW1000_LIVEDATA);

			// sample data = in-temp, in-hum, abs-baro, rel-baro, temp, hum, dir, speed, gust, light, UV uW, UV-I, rain-rate, rain-day, rain-week, rain-month, rain-year, PM2.5, PM-ch1, Soil-1, temp-2, hum-2, temp-3, hum-3, batt
			//byte[] data = new byte[] { 0xFF,0xFF,0x27,0x00,0x5D,0x01,0x00,0x83,0x06,0x55,0x08,0x26,0xE7,0x09,0x26,0xDC,0x02,0x00,0x5D,0x07,0x61,0x0A,0x00,0x89,0x0B,0x00,0x19,0x0C,0x00,0x25,0x15,0x00,0x00,0x00,0x00,0x16,0x00,0x00,0x17,0x00,0x0E,0x00,0x3C,0x10,0x00,0x1E,0x11,0x01,0x4A,0x12,0x00,0x00,0x02,0x68,0x13,0x00,0x00,0x14,0xDC,0x2A,0x01,0x90,0x4D,0x00,0xE3,0x2C,0x34,0x1B,0x00,0xD3,0x23,0x3C,0x1C,0x00,0x60,0x24,0x5A,0x4C,0x04,0x00,0x00,0x00,0xFF,0x5C,0xFF,0x00,0xF4,0xFF,0xFF,0xFF,0xFF,0xFF,0x00,0x00,0xBA };
			//byte[] data = new byte[] { 0xFF, 0xFF, 0x27, 0x00, 0x6D, 0x01, 0x00, 0x96, 0x06, 0x3C, 0x08, 0x27, 0x00, 0x09, 0x27, 0x49, 0x02, 0x00, 0x16, 0x07, 0x61, 0x0A, 0x00, 0x62, 0x0B, 0x00, 0x00, 0x0C, 0x00, 0x06, 0x15, 0x00, 0x01, 0x7D, 0x40, 0x16, 0x00, 0x00, 0x17, 0x00, 0x0E, 0x00, 0x00, 0x10, 0x00, 0x00, 0x11, 0x00, 0xF7, 0x12, 0x00, 0x00, 0x01, 0x5C, 0x13, 0x00, 0x00, 0x15, 0x54, 0x2A, 0x06, 0x40, 0x4D, 0x00, 0xAB, 0x1A, 0xFF, 0x3E, 0x22, 0x39, 0x1B, 0x00, 0x3D, 0x23, 0x51, 0x1C, 0x00, 0xA0, 0x24, 0x45, 0x1D, 0x00, 0xA4, 0x25, 0x3C, 0x1E, 0x00, 0x9D, 0x26, 0x3E, 0x4C, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xA4, 0x00, 0xF4, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x19, 0x00, 0x1A, 0x8F };

			// expected response
			// 0 - 0xff - header
			// 1 - 0xff
			// 2 - 0x27 - live data command
			// 3 - 0x?? - size of response1
			// 4 - 0x?? - size of response2
			// 5-X      - data - NOTE format is Bigendian
			// Y - 0x?? - checksum

			try
			{
				if (null != data && data.Length > 20 )
				{
					// now decode it
					Int16 tempInt16;
					UInt16 tempUint16;
					UInt32 tempUint32;
					var idx = 5;
					var dateTime = DateTime.Now;
					var size = ConvertBigEndianUInt16(data, 3);

					double windSpeedLast = -999, rainRateLast = -999, rainLast = -999, gustLast = -999, gustLastCal = -999;
					int windDirLast = -999;
					double outdoortemp = -999;

					bool batteryLow = false;

					do
					{
						int chan;
						switch (data[idx++])
						{
							case 0x01:  //Indoor Temperature (℃)
								tempInt16 = ConvertBigEndianInt16(data, idx);
								DoIndoorTemp(ConvertTempCToUser(tempInt16 / 10.0));
								idx += 2;
								break;
							case 0x02: //Outdoor Temperature (℃)
								tempInt16 = ConvertBigEndianInt16(data, idx);
								// do not process temperature here as if "MX calculates DP" is enabled, we have not yet read the humidity value. Have to do it at the end.
								outdoortemp = tempInt16 / 10.0;
								idx += 2;
								break;
							case 0x03: //Dew point (℃)
								tempInt16 = ConvertBigEndianInt16(data, idx);
								DoOutdoorDewpoint(ConvertTempCToUser(tempInt16 / 10.0), dateTime);
								idx += 2;
								break;
							case 0x04: //Wind chill (℃)
								tempInt16 = ConvertBigEndianInt16(data, idx);
								DoWindChill(ConvertTempCToUser(tempInt16 / 10.0), dateTime);
								idx += 2;
								break;
							case 0x05: //Heat index (℃)
								// cumulus calculates this
								idx += 2;
								break;
							case 0x06: //Indoor Humidity(%)
								DoIndoorHumidity(data[idx]);
								idx += 1;
								break;
							case 0x07: //Outdoor Humidity (%)
								DoOutdoorHumidity(data[idx], dateTime);
								idx += 1;
								break;
							case 0x08: //Absolute Barometric (hpa)
								idx += 2;
								break;
							case 0x09: //Relative Barometric (hpa)
								tempUint16 = ConvertBigEndianUInt16(data, idx);
								DoPressure(ConvertPressMBToUser(tempUint16 / 10.0), dateTime);
								DoPressTrend("Pressure trend");
								idx += 2;
								break;
							case 0x0A: //Wind Direction (360°)
								windDirLast = ConvertBigEndianUInt16(data, idx);
								idx += 2;
								break;
							case 0x0B: //Wind Speed (m/s)
								windSpeedLast = ConvertWindMSToUser(ConvertBigEndianUInt16(data, idx) / 10.0);
								idx += 2;
								break;
							case 0x0C: // Gust speed (m/s)
								gustLast = ConvertWindMSToUser(ConvertBigEndianUInt16(data, idx) / 10.0);
								gustLastCal = gustLast * cumulus.Calib.WindGust.Mult;
								idx += 2;
								break;
							case 0x0D: //Rain Event (mm)
							//TODO: add rain event total
								idx += 2;
								break;
							case 0x0E: //Rain Rate (mm/h)
								rainRateLast = ConvertRainMMToUser(ConvertBigEndianUInt16(data, idx) / 10.0);
								idx += 2;
								break;
							case 0x0F: //Rain hour (mm)
								idx += 2;
								break;
							case 0x10: //Rain Day (mm)
								idx += 2;
								break;
							case 0x11: //Rain Week (mm)
								idx += 2;
								break;
							case 0x12: //Rain Month (mm)
								idx += 4;
								break;
							case 0x13: //Rain Year (mm)
								rainLast = ConvertRainMMToUser(ConvertBigEndianUInt32(data, idx) / 10.0);
								idx += 4;
								break;
							case 0x14: //Rain Totals (mm)
								idx += 4;
								break;
							case 0x15: //Light (lux)
								// Save the Lux value
								LightValue = ConvertBigEndianUInt32(data, idx) / 10.0;
								// convert Lux to W/m2 - approximately!
								DoSolarRad((int)(LightValue * cumulus.LuxToWM2), dateTime);
								idx += 4;
								break;
							case 0x16: //UV (µW/cm²) - what use is this!
								idx += 2;
								break;
							case 0x17: //UVI (0-15 index)
								DoUV(data[idx], dateTime);
								idx += 1;
								break;
							case 0x18: //Date and time
								idx += 7;
								break;
							case 0x19: //Day max wind(m/s)
								idx += 2;
								break;
							case 0x1A: //Temperature 1(℃)
							case 0x1B: //Temperature 2(℃)
							case 0x1C: //Temperature 3(℃)
							case 0x1D: //Temperature 4(℃)
							case 0x1E: //Temperature 5(℃)
							case 0x1F: //Temperature 6(℃)
							case 0x20: //Temperature 7(℃)
							case 0x21: //Temperature 8(℃)
								chan = data[idx - 1] - 0x1A + 1;
								tempInt16 = ConvertBigEndianInt16(data, idx);
								DoExtraTemp(ConvertTempCToUser(tempInt16 / 10.0), chan);
								idx += 2;
								break;
							case 0x22: //Humidity 1, 0-100%
							case 0x23: //Humidity 2, 0-100%
							case 0x24: //Humidity 3, 0-100%
							case 0x25: //Humidity 4, 0-100%
							case 0x26: //Humidity 5, 0-100%
							case 0x27: //Humidity 6, 0-100%
							case 0x28: //Humidity 7, 0-100%
							case 0x29: //Humidity 9, 0-100%
								chan = data[idx - 1] - 0x22 + 1;
								DoExtraHum(data[idx], chan);
								idx += 1;
								break;
							case 0x2B: //Soil Temperature1 (℃)
							case 0x2D: //Soil Temperature2 (℃)
							case 0x2F: //Soil Temperature3 (℃)
							case 0x31: //Soil Temperature4 (℃)
							case 0x33: //Soil Temperature5 (℃)
							case 0x35: //Soil Temperature6 (℃)
							case 0x37: //Soil Temperature7 (℃)
							case 0x39: //Soil Temperature8 (℃)
							case 0x3B: //Soil Temperature9 (℃)
							case 0x3D: //Soil Temperature10 (℃)
							case 0x3F: //Soil Temperature11 (℃)
							case 0x41: //Soil Temperature12 (℃)
							case 0x43: //Soil Temperature13 (℃)
							case 0x45: //Soil Temperature14 (℃)
							case 0x47: //Soil Temperature15 (℃)
							case 0x49: //Soil Temperature16 (℃)
								// figure out the channel number
								chan = data[idx - 1] - 0x2B + 2; // -> 2,4,6,8...
								chan /= 2; // -> 1,2,3,4...
								tempInt16 = ConvertBigEndianInt16(data, idx);
								DoSoilTemp(ConvertTempCToUser(tempInt16 / 10.0), chan);
								idx += 2;
								break;
							case 0x2C: //Soil Moisture1 (%)
							case 0x2E: //Soil Moisture2 (%)
							case 0x30: //Soil Moisture3 (%)
							case 0x32: //Soil Moisture4 (%)
							case 0x34: //Soil Moisture5 (%)
							case 0x36: //Soil Moisture6 (%)
							case 0x38: //Soil Moisture7 (%)
							case 0x3A: //Soil Moisture8 (%)
							case 0x3C: //Soil Moisture9 (%)
							case 0x3E: //Soil Moisture10 (%)
							case 0x40: //Soil Moisture11 (%)
							case 0x42: //Soil Moisture12 (%)
							case 0x44: //Soil Moisture13 (%)
							case 0x46: //Soil Moisture14 (%)
							case 0x48: //Soil Moisture15 (%)
							case 0x4A: //Soil Moisture16 (%)
								// figure out the channel number
								chan = data[idx - 1] - 0x2C + 2; // -> 2,4,6,8...
								chan /= 2; // -> 1,2,3,4...
								DoSoilMoisture(data[idx], chan);
								idx += 1;
								break;
							case 0x4C: //All sensor lowbatt 16 char
								//TODO: battery status, do we need to know which sensors are attached?
								if (tenMinuteChanged)
								{
									batteryLow = batteryLow || DoBatteryStatus(data, idx);
								}
								idx += 16;
								break;
							case 0x2A: //PM2.5 Air Quality Sensor(μg/m3)
								tempUint16 = ConvertBigEndianUInt16(data, idx);
								DoAirQuality(tempUint16 / 10.0, 1);
								idx += 2;
								break;
							case 0x4D: //for pm25_ch1
							case 0x4E: //for pm25_ch2
							case 0x4F: //for pm25_ch3
							case 0x50: //for pm25_ch4
								chan = data[idx - 1] - 0x4D + 1;
								tempUint16 = ConvertBigEndianUInt16(data, idx);
								DoAirQualityAvg(tempUint16 / 10.0, chan);
								idx += 2;
								break;
							case 0x51: //PM2.5 ch_2 Air Quality Sensor(μg/m3)
							case 0x52: //PM2.5 ch_3 Air Quality Sensor(μg/m3)
							case 0x53: //PM2.5 ch_4 Air Quality Sensor(μg/m3)
								chan = data[idx - 1] - 0x51 + 2;
								tempUint16 = ConvertBigEndianUInt16(data, idx);
								DoAirQuality(tempUint16 / 10.0, chan);
								idx += 2;
								break;
							case 0x58: //Leak ch1
							case 0x59: //Leak ch2
							case 0x5A: //Leak ch3
							case 0x5B: //Leak ch4
								chan = data[idx - 1] - 0x58 + 1;
								DoLeakSensor(data[idx], chan);
								idx += 1;
								break;
							case 0x60: //Lightning dist (1-40km)
								// Sends a default value of 255km until the first strike is detected
								LightningDistance = data[idx] == 0xFF ? 999 : ConvertKmtoUserUnits(data[idx]);
								idx += 1;
								break;
							case 0x61: //Lightning time (UTC)
								// Sends a default value until the first strike is detected of 0xFFFFFFFF
								tempUint32 = ConvertBigEndianUInt32(data, idx);
								if (tempUint32 == 0xFFFFFFFF)
								{
									LightningTime = new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
								}
								else
								{
									var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
									dtDateTime = dtDateTime.AddSeconds(tempUint32).ToLocalTime();
									//cumulus.LogDebugMessage($"Lightning time={dtDateTime}");
									LightningTime = dtDateTime;
								}
								idx += 4;
								break;
							case 0x62: //Lightning strikes today
								tempUint32 = ConvertBigEndianUInt32(data, idx);
								//cumulus.LogDebugMessage($"Lightning count={tempUint32}");
								LightningStrikesToday = (int)tempUint32;
								idx += 4;
								break;
							// user temp = WH34 8 channel Soil or Water temperature sensors
							case 0x63: // user temp ch1 (°C)
							case 0x64: // user temp ch2 (°C)
							case 0x65: // user temp ch3 (°C)
							case 0x66: // user temp ch4 (°C)
							case 0x67: // user temp ch5 (°C)
							case 0x68: // user temp ch6 (°C)
							case 0x69: // user temp ch7 (°C)
							case 0x6A: // user temp ch8 (°C)
								chan = data[idx - 1] - 0x63 + 2;  // -> 2,4,6,8...
								chan /= 2; // -> 1,2,3,4...
								tempInt16 = ConvertBigEndianInt16(data, idx);
								DoUserTemp(ConvertTempCToUser(tempInt16 / 10.0), chan);
								if (tenMinuteChanged)
								{
									var volts = TestBattery4V(data[idx + 2]);
									if (volts <= 1.2)
									{
										batteryLow = true;
										cumulus.LogMessage($"WH34 channel #{chan} battery LOW = {volts}V");
									}
									else
									{
										cumulus.LogDebugMessage($"WH34 channel #{chan} battery OK = {volts}V");
									}
								}
								idx += 3; // Firmware version 1.5.9 uses 2 data bytes, 1.6.0+ uses 3 data bytes
								break;
							case 0x6B: //WH34 User temperature battery (8 channels) - No longer used in firmware 1.6.0+
								if (tenMinuteChanged)
								{
									batteryLow = batteryLow || DoWH34BatteryStatus(data, idx);
								}
								idx += 8;
								break;
							case 0x70: // WH45 CO₂
								batteryLow = batteryLow || DoCO2Decode(data, idx);
								idx += 16;
								break;

							default:
								cumulus.LogDebugMessage($"Error: Unknown sensor id found = {data[idx - 1]}, at position = {idx - 1}");
								// We will have lost our place now, so bail out
								idx = size;
								break;
						}
					} while (idx < size);

					// Process outdoor temperature here, as GW1000 currently does not supply Dew Point so we have to calculate it in DoOutdoorTemp()
					if (outdoortemp > -999)
						DoOutdoorTemp(ConvertTempCToUser(outdoortemp), dateTime);

					if (tenMinuteChanged) tenMinuteChanged = false;

					// Now do the stuff that requires more than one input parameter

					cumulus.BatteryLowAlarm.Triggered = batteryLow;

					// No average in the live data, so use last value from cumulus
					if (windSpeedLast > -999 && windDirLast > -999)
					{
						DoWind(windSpeedLast, windDirLast, WindAverage / cumulus.Calib.WindSpeed.Mult, dateTime);
						//DoWind(windSpeedLast, windDirLast, windSpeedLast, dateTime);
					}

					if (gustLastCal > RecentMaxGust)
					{
						cumulus.LogDebugMessage("Setting max gust from current value: " + gustLastCal.ToString(cumulus.WindFormat));
						CheckHighGust(gustLastCal, windDirLast, dateTime);

						// add to recent values so normal calculation includes this value
						WindRecent[nextwind].Gust = gustLast; // use uncalibrated value
						WindRecent[nextwind].Speed = WindAverage / cumulus.Calib.WindSpeed.Mult;
						WindRecent[nextwind].Timestamp = dateTime;
						nextwind = (nextwind + 1) % MaxWindRecent;

						RecentMaxGust = gustLastCal;
					}

					if (rainLast > -999 && rainRateLast > -999)
					{
						DoRain(rainLast, rainRateLast, dateTime);
					}

					if (ConvertUserWindToMS(WindAverage) < 1.5)
					{
						DoWindChill(OutdoorTemperature, dateTime);
					}
					else
					{
						// calculate wind chill from calibrated C temp and calibrated wind in KPH
						DoWindChill(ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToKPH(WindAverage))), dateTime);
					}

					DoApparentTemp(dateTime);
					DoFeelsLike(dateTime);
					DoHumidex(dateTime);

					DoForecast("", false);

					UpdateStatusPanel(dateTime);
					UpdateMQTT();

					dataReceived = true;
					DataStopped = false;
					cumulus.DataStoppedAlarm.Triggered = false;
				}
				else
				{
					cumulus.LogMessage("GetLiveData: Invalid response");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetLiveData: Error - " + ex.Message);
			}
		}

		public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
		}

		private byte[] DoCommand(byte command)
		{
			var buffer = new byte[2028];
			var bytesRead = 0;

			var cmdName = Enum.GetName(typeof(Commands), command);

			var payload = new CommandPayload(command);
			var tmrComm = new CommTimer();

			var bytes = payload.Serialise();

			try
			{
				var stream = socket.GetStream();
				stream.Write(bytes, 0, bytes.Length);

				tmrComm.Start(1000);

				while (tmrComm.timedout == false)
				{
					if (stream.DataAvailable)
					{
						while (stream.DataAvailable)
						{
							// Read the current character
							var ch = stream.ReadByte();
							buffer[bytesRead] = (byte)ch;
							bytesRead++;
							//cumulus.LogMessage("Received " + ch.ToString("X2"));
						}
						tmrComm.Stop();
					}
					else
					{
						Thread.Sleep(20);
					}
				}
				// Check the response is to our command and checksum is OK
				if (bytesRead == 0 || buffer[2] != command || !ChecksumOk(buffer, (int)Enum.Parse(typeof(CommandRespSize), cmdName)))
				{
					if (bytesRead > 0)
					{
						cumulus.LogMessage($"DoCommand({cmdName}): Invalid response");
						cumulus.LogDebugMessage($"command resp={buffer[2]}, checksum=" + (ChecksumOk(buffer, (int)Enum.Parse(typeof(CommandRespSize), cmdName)) ? "OK" : "BAD"));
						cumulus.LogDataMessage("Received 0x" + BitConverter.ToString(buffer, 0, bytesRead - 1));
					}
					else
					{
						cumulus.LogMessage($"DoCommand({cmdName}): No response received");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"DoCommand({cmdName}): Valid response");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"DoCommand({cmdName}): Error - " + ex.Message);
				connectedOk = socket.Connected;
			}
			// Copy the data we want out of the buffer
			if (bytesRead > 0)
			{
				var data = new byte[bytesRead];
				Array.Copy(buffer, data, data.Length);
				cumulus.LogDataMessage("Received 0x" + BitConverter.ToString(data));
				return data;
			}

			return null;
		}

		private bool DoCO2Decode(byte[] data, int index)
		{
			bool batteryLow = false;
			int idx = index;
			cumulus.LogDebugMessage("WH45 CO₂: Decoding...");
			//CO2Data co2Data = (CO2Data)RawDeserialize(data, index, typeof(CO2Data));

			var temp = ConvertBigEndianInt16(data, idx) / 10;
			idx += 2;
			int hum = data[idx++];
			int pm10 = ConvertBigEndianUInt16(data, idx) / 10;
			idx += 2;
			int pm1024H = ConvertBigEndianUInt16(data, idx) / 10;
			idx += 2;
			int pm2P5 = ConvertBigEndianUInt16(data, idx) / 10;
			idx += 2;
			int pm2P524H = ConvertBigEndianUInt16(data, idx) / 10;
			idx += 2;
			CO2 = ConvertBigEndianUInt16(data, idx);
			idx += 2;
			CO2_24h = ConvertBigEndianUInt16(data, idx);
			idx += 2;
			var batt = TestBattery3(data[idx]);
			var msg = $"WH45 CO₂: temp={temp.ToString(cumulus.TempFormat)}, hum={hum}, pm10={pm10:F1}, pm10_24h={pm1024H:F1}, pm2.5={pm2P5:F1}, pm2.5_24h={pm2P524H:F1}, CO₂={CO2}, CO₂_24h={CO2_24h}";
			if (tenMinuteChanged)
			{
				if (batt == "Low")
				{
					batteryLow = true;
					msg += $", Battery={batt}";
				}
				else
				{
					msg += $", Battery={batt}";
				}
			}
			cumulus.LogDebugMessage(msg);

			return batteryLow;
		}

		private bool DoBatteryStatus(byte[] data, int index)
		{
			bool batteryLow = false;
			BatteryStatus status = (BatteryStatus)RawDeserialize(data, index, typeof(BatteryStatus));
			cumulus.LogDebugMessage("battery status...");

			var str = "singles>" +
				" wh24=" + TestBattery1(status.single, (byte)SigSen.Wh24) +
				" wh25=" + TestBattery1(status.single, (byte)SigSen.Wh25) +
				" wh26=" + TestBattery1(status.single, (byte)SigSen.Wh26) +
				" wh40=" + TestBattery1(status.single, (byte)SigSen.Wh40);
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			str = "wh31>" +
				" ch1=" + TestBattery1(status.wh31, (byte)Wh31Ch.Ch1) +
				" ch2=" + TestBattery1(status.wh31, (byte)Wh31Ch.Ch2) +
				" ch3=" + TestBattery1(status.wh31, (byte)Wh31Ch.Ch3) +
				" ch4=" + TestBattery1(status.wh31, (byte)Wh31Ch.Ch4) +
				" ch5=" + TestBattery1(status.wh31, (byte)Wh31Ch.Ch5) +
				" ch6=" + TestBattery1(status.wh31, (byte)Wh31Ch.Ch6) +
				" ch7=" + TestBattery1(status.wh31, (byte)Wh31Ch.Ch7) +
				" ch8=" + TestBattery1(status.wh31, (byte)Wh31Ch.Ch8);
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			str = "wh41>" +
				" ch1=" + TestBattery2(status.wh41, 0x0F) +
				" ch2=" + TestBattery2((UInt16)(status.wh41 >> 4), 0x0F) +
				" ch3=" + TestBattery2((UInt16)(status.wh41 >> 8), 0x0F) +
				" ch4=" + TestBattery2((UInt16)(status.wh41 >> 12), 0x0F);
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			str = "wh51>" +
				" ch1=" + TestBattery1(status.wh51, (byte)Wh51Ch.Ch1) +
				" ch2=" + TestBattery1(status.wh31, (byte)Wh51Ch.Ch2) +
				" ch3=" + TestBattery1(status.wh31, (byte)Wh51Ch.Ch3) +
				" ch4=" + TestBattery1(status.wh31, (byte)Wh51Ch.Ch4) +
				" ch5=" + TestBattery1(status.wh31, (byte)Wh51Ch.Ch5) +
				" ch6=" + TestBattery1(status.wh31, (byte)Wh51Ch.Ch6) +
				" ch7=" + TestBattery1(status.wh31, (byte)Wh51Ch.Ch7) +
				" ch8=" + TestBattery1(status.wh31, (byte)Wh51Ch.Ch8);
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			str = "wh57> " + TestBattery3(status.wh57);
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			str = "wh68> " + TestBattery4S(status.wh68) + " - " + TestBattery4V(status.wh68) + "V";
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			str = "wh80> " + TestBattery4S(status.wh80) + " - " + TestBattery4V(status.wh80) + "V";
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			str = "wh45> " + TestBattery4S(status.wh45) + " - " + TestBattery4V(status.wh45) + "V";
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			str = "wh55>" +
				" ch1=" + TestBattery3(status.wh55_ch1) +
				" ch2=" + TestBattery3(status.wh55_ch2) +
				" ch3=" + TestBattery3(status.wh55_ch3) +
				" ch4=" + TestBattery3(status.wh55_ch4);
			if (str.Contains("Low"))
			{
				batteryLow = true;
				cumulus.LogMessage(str);
			}
			else
				cumulus.LogDebugMessage(str);

			return batteryLow;
		}

		private bool DoWH34BatteryStatus(byte[] data, int index)
		{
			// No longer used in firmware 1.6.0+
			cumulus.LogDebugMessage("WH34 battery status...");
			var str = "wh34>" +
				" ch1=" + TestBattery3(data[index + 1]) +
				" ch2=" + TestBattery3(data[index + 2]) +
				" ch3=" + TestBattery3(data[index + 3]) +
				" ch4=" + TestBattery3(data[index + 4]) +
				" ch5=" + TestBattery3(data[index + 5]) +
				" ch6=" + TestBattery3(data[index + 6]) +
				" ch7=" + TestBattery3(data[index + 7]) +
				" ch8=" + TestBattery3(data[index + 8]);

			cumulus.LogDebugMessage(str);

			return str.Contains("Low");
		}

		private static string TestBattery1(byte value, byte mask)
		{
			return (value & mask) == 0 ? "OK" : "Low";
		}

		private static string TestBattery1(UInt16 value, UInt16 mask)
		{
			return (value & mask) == 0 ? "OK" : "Low";
		}

		private static string TestBattery2(UInt16 value, UInt16 mask)
		{
			return (value & mask) > 1 ? "OK" : "Low";
		}

		private static string TestBattery3(byte value)
		{
			return value > 1 ? "OK" : "Low";
		}
		private static double TestBattery4V(byte value)
		{
			return value * 0.02;
		}
		private static string TestBattery4S(byte value)
		{
			return value * 0.02 > 1.2 ? "OK" : "Low";
		}

		private static object RawDeserialize(byte[] rawData, int position, Type anyType)
		{
			int rawsize = Marshal.SizeOf(anyType);
			if (rawsize > rawData.Length)
				return null;
			IntPtr buffer = Marshal.AllocHGlobal(rawsize);
			Marshal.Copy(rawData, position, buffer, rawsize);
			object retobj = Marshal.PtrToStructure(buffer, anyType);
			Marshal.FreeHGlobal(buffer);
			return retobj;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		private struct CommandPayload
		{
			private readonly ushort Header;
			private readonly byte Command;
			private readonly byte Size;
			//public byte[] Data;
			private readonly byte Checksum;

			//public CommandPayload(byte command, byte[] data) : this()
			public CommandPayload(byte command) : this()
			{
				//ushort header;
				Header = 0xffff;
				Command = command;
				Size = (byte)(Marshal.SizeOf(typeof(CommandPayload)) - 3);
				Checksum = (byte)(Command + Size);
			}
			// This will be serialised in little endian format
			public byte[] Serialise()
			{
				// allocate a byte array for the struct data
				var buffer = new byte[Marshal.SizeOf(typeof(CommandPayload))];

				// Allocate a GCHandle and get the array pointer
				var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
				var pBuffer = gch.AddrOfPinnedObject();

				// copy data from struct to array and unpin the gc pointer
				Marshal.StructureToPtr(this, pBuffer, false);
				gch.Free();

				return buffer;
			}
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		private struct BatteryStatus
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
			public UInt16 pm10;			// ug/m3 x10
			public UInt16 pm10_24hr;	// ug/m3 x10
			public UInt16 pm2p5;		// ug/m3 x10
			public UInt16 pm2p5_24hr;	// ug/m3 x10
			public UInt16 co2;			// ppm
			public UInt16 co2_24hr;		// ppm
			public byte batt;			// 0-5
		}
		*/

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
				size = (ushort)data[3];
			}
			else
			{
				size = ConvertBigEndianUInt16(data, 3);
			}

			byte checksum = (byte)(data[2] + data[3]);
			for (var i = 4; i <= size; i++)
			{
				checksum += data[i];
			}

			if (checksum != data[size + 1])
			{
				cumulus.LogMessage("Bad checksum");
				return false;
			}

			return true;
		}

		private static UInt16 ConvertBigEndianUInt16(byte[] array, int start)
		{
			return (UInt16)(array[start] << 8 | array[start+1]);
		}

		private static Int16 ConvertBigEndianInt16(byte[] array, int start)
		{
			return (Int16)((array[start] << 8) + array[start + 1]);
		}

		private static UInt32 ConvertBigEndianUInt32(byte[] array, int start)
		{
			return (UInt32)(array[start++] << 24 | array[start++] << 16 | array[start++] << 8 | array[start]);
		}

		private void DataTimeout(object source, ElapsedEventArgs e)
		{
			if (dataReceived)
			{
				dataReceived = false;
				DataStopped = false;
				cumulus.DataStoppedAlarm.Triggered = false;
			}
			else
			{
				cumulus.LogMessage($"ERROR: No data received from the GW1000 for {tmrDataWatchdog.Interval / 1000} seconds");
				DataStopped = true;
				cumulus.DataStoppedAlarm.Triggered = true;
			}
		}
	}
}
