using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using ServiceStack.Text;

namespace CumulusMX
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
	internal class GW1000Station : WeatherStation
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
	{
		private string ipaddr;
		private readonly string macaddr;
		private const int AtPort = 45000;
		private int updateRate = 10000; // 10 seconds by default
		private int lastMinute;
		private bool tenMinuteChanged = true;

		private readonly EcowittApi ecowittApi;
		private readonly GW1000Api Api;

		private int maxArchiveRuns = 1;

		//private bool connectedOk = false;
		private bool dataReceived = false;

		private readonly System.Timers.Timer tmrDataWatchdog;

		private readonly Task historyTask;
		private Task liveTask;

		//private readonly NumberFormatInfo invNum = CultureInfo.InvariantCulture.NumberFormat;

		private Version fwVersion;
		internal static readonly char[] dotSeparator = new char[] { '.' };
		internal static readonly string[] underscoreV = new string[] { "_V" };

		//private string gatewayType;

		public GW1000Station(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Units.AirQualityUnitText = "µg/m³";
			cumulus.Units.SoilMoistureUnitText = "%";
			cumulus.Units.LeafWetnessUnitText = "%";

			// GW1000 does not provide 10 min average wind speeds
			cumulus.StationOptions.CalcuateAverageWindSpeed = true;

			// GW1000 does not provide an interval gust value, it gives us a 30 second high
			// so force using the wind speed for the average calculation
			cumulus.StationOptions.UseSpeedForAvgCalc = true;
			// also use it for the Latest value
			cumulus.StationOptions.UseSpeedForLatest = true;

			LightningTime = DateTime.MinValue;
			LightningDistance = -1.0;

			tmrDataWatchdog = new System.Timers.Timer();

			// GW1000 does not send DP, so force MX to calculate it
			cumulus.StationOptions.CalculatedDP = true;

			// does not provide a forecast, force MX to provide it
			cumulus.UseCumulusForecast = true;

			// GW1000 does not provide pressure trend strings
			cumulus.StationOptions.UseCumulusPresstrendstr = true;

			if (cumulus.Gw1000PrimaryTHSensor == 0)
			{
				// We are using the primary T/H sensor
				cumulus.LogMessage("Using the default outdoor temp/hum sensor data");
			}
			else if (cumulus.Gw1000PrimaryTHSensor == 99)
			{
				// We are overriding the outdoor with the indoor T/H sensor
				cumulus.LogMessage("Overriding the default outdoor temp/hum data with Indoor temp/hum sensor");
				cumulus.StationOptions.CalculatedDP = true;
				cumulus.StationOptions.CalculatedWC = true;
			}
			else
			{
				// We are not using the primary T/H sensor so MX must calculate the wind chill as well
				cumulus.StationOptions.CalculatedDP = true;
				cumulus.StationOptions.CalculatedWC = true;
				cumulus.LogMessage("Overriding the default outdoor temp/hum data with Extra temp/hum sensor #" + cumulus.Gw1000PrimaryTHSensor);
			}

			if (cumulus.Gw1000PrimaryRainSensor == 0)
			{
				// We are using the traditional rain tipper
				cumulus.LogMessage("Using the default traditional rain sensor data");
			}
			else
			{
				cumulus.LogMessage("Using the piezo rain sensor data");
			}

			ipaddr = cumulus.Gw1000IpAddress;
			macaddr = cumulus.Gw1000MacAddress;

			Api = new GW1000Api(cumulus);

			ecowittApi = new EcowittApi(cumulus, this);

			if (DoDiscovery())
			{
				PostDiscovery();
			}

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			historyTask = Task.Run(getAndProcessHistoryData, cumulus.cancellationToken);
		}


		public override void Start()
		{
			tenMinuteChanged = true;
			lastMinute = DateTime.Now.Minute;

			// Start a broadcast watchdog to warn if messages are not being received
			tmrDataWatchdog.Elapsed += DataTimeout;
			tmrDataWatchdog.Interval = 1000 * 30; // timeout after 30 seconds
			tmrDataWatchdog.AutoReset = true;
			tmrDataWatchdog.Start();


			// just incase we did not catch-up any history
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);

			cumulus.LogMessage("Starting Ecowitt Local API");

			cumulus.StartTimersAndSensors();

			liveTask = Task.Run(() =>
			{
				try
				{
					var piezoLastRead = DateTime.MinValue;
					var dataLastRead = DateTime.MinValue;
					double delay;

					while (!cumulus.cancellationToken.IsCancellationRequested)
					{
						if (Api.Connected)
						{
							GetLiveData();
							dataLastRead = DateTime.Now;

							// every 30 seconds read the rain rate
							if ((cumulus.Gw1000PrimaryRainSensor == 1 || cumulus.StationOptions.UseRainForIsRaining == 2) && (DateTime.Now - piezoLastRead).TotalSeconds >= 30 && !cumulus.cancellationToken.IsCancellationRequested)
							{
								GetPiezoRainData();
								piezoLastRead = DateTime.Now;
							}

							var minute = DateTime.Now.Minute;
							if (minute != lastMinute)
							{
								lastMinute = minute;

								// at the start of every 20 minutes to trigger battery status check
								if ((minute % 20) == 0 && !cumulus.cancellationToken.IsCancellationRequested)
								{
									GetSensorIdsNew();
								}

								// every day dump the clock drift at midday each day
								if (minute == 0 && DateTime.Now.Hour == 12)
								{
									GetSystemInfo(true);
								}
							}
						}
						else
						{
							cumulus.LogMessage("Attempting to reconnect to Ecowitt device...");
							Api.OpenTcpPort(cumulus.Gw1000IpAddress, AtPort);
							if (Api.Connected)
							{
								cumulus.LogMessage("Reconnected to Ecowitt device");
								GetLiveData();
							}
							else
							{
								// add a small extra delay before trying again
								cumulus.LogMessage("Delaying before attempting reconnect");
								if (cumulus.cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(20000)))
								{
									break;
								}
							}
						}

						delay = Math.Min(updateRate - (dataLastRead - DateTime.Now).TotalMilliseconds, updateRate);

						if (cumulus.cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(delay)))
						{
							break;
						}
					}
				}
				// Catch the ThreadAbortException
				catch (ThreadAbortException)
				{
				}
				finally
				{
					Api.CloseTcpPort();
					cumulus.LogMessage("Local API task ended");
				}
			}, cumulus.cancellationToken);
		}

		public override void Stop()
		{
			cumulus.LogMessage("Closing connection");
			try
			{
				tmrDataWatchdog.Stop();
				StopMinuteTimer();
				Task.WaitAll(historyTask, liveTask);
			}
			catch
			{
			}
		}


		public override void getAndProcessHistoryData()
		{
			//cumulus.LogDebugMessage("Lock: Station waiting for the lock");
			Cumulus.syncInit.Wait();
			//cumulus.LogDebugMessage("Lock: Station has the lock");

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogWarningMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
			}
			else
			{
				int archiveRun = 0;

				try
				{
					do
					{
						GetHistoricData();
						archiveRun++;
					} while (archiveRun < maxArchiveRuns && !cumulus.cancellationToken.IsCancellationRequested);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Exception occurred reading archive data: " + ex.Message);
				}

				// get the station list
				ecowittApi.GetStationList(cumulus.cancellationToken);
			}

			//cumulus.LogDebugMessage("Lock: Station releasing the lock");
			_ = Cumulus.syncInit.Release();

			if (cumulus.cancellationToken.IsCancellationRequested)
			{
				return;
			}

			StartLoop();
		}

		private void GetHistoricData()
		{
			cumulus.LogMessage("GetHistoricData: Starting Historic Data Process");

			// add one minute to the time to avoid duplicating the last log entry
			var startTime = cumulus.LastUpdateTime.AddMinutes(1);
			var endTime = DateTime.Now;

			// The API call is limited to fetching 24 hours of data
			if ((endTime - startTime).TotalHours > 24.0)
			{
				// only fetch 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime.AddHours(24);
				maxArchiveRuns++;
			}

			ecowittApi.GetHistoricData(startTime, endTime, cumulus.cancellationToken);
		}

		public override string GetEcowittCameraUrl()
		{
			if (!string.IsNullOrEmpty(cumulus.EcowittCameraMacAddress))
			{
				try
				{
					EcowittCameraUrl = ecowittApi.GetCurrentCameraImageUrl(EcowittCameraUrl, cumulus.cancellationToken);
					return EcowittCameraUrl;
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "Error runing Ecowitt Camera URL");
				}
			}

			return null;
		}

		public override string GetEcowittVideoUrl()
		{
			if (!string.IsNullOrEmpty(cumulus.EcowittCameraMacAddress))
			{
				try
				{
					EcowittVideoUrl = ecowittApi.GetLastCameraVideoUrl(EcowittVideoUrl, cumulus.cancellationToken);
					return EcowittVideoUrl;
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "Error running Ecowitt Video URL");
				}
			}

			return null;
		}


		private Discovery DiscoverGW1000()
		{
			// We only want unique IP addresses
			var discovered = new Discovery();
			const int broadcastPort = 46000;

			try
			{
				using (var client = new UdpClient())
				{
					var recvEp = new IPEndPoint(0, 0);
					var sendEp = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
					var sendBytes = new byte[] { 0xff, 0xff, 0x12, 0x00, 0x04, 0x16 };


					// Get the primary IP address
					var myIP = Utils.GetIpWithDefaultGateway();
					cumulus.LogDebugMessage($"Using local IP address {myIP} to discover the Ecowitt device");

					// bind the cient to the primary address - broadcast does not work with .Any address :(
					client.Client.Bind(new IPEndPoint(myIP, broadcastPort));
					// time out listening after 1.5 second
					client.Client.ReceiveTimeout = 1500;

					// we are going to attempt discovery twice
					var retryCount = 1;
					do
					{
						cumulus.LogDebugMessage("Discovery Run #" + retryCount);
						// each time we wait 1.5 second for any responses
						var endTime = DateTime.Now.AddSeconds(1.5);

						try
						{
							// Send the request
							client.Send(sendBytes, sendBytes.Length, sendEp);

							do
							{
								try
								{
									// get a response
									var recevBuffer = client.Receive(ref recvEp);

									// sanity check the response size - we may see our request back as a receive packet
									if (recevBuffer.Length > 20)
									{
										string ipAddr = $"{recevBuffer[11]}.{recevBuffer[12]}.{recevBuffer[13]}.{recevBuffer[14]}";
										var macArr = new byte[6];

										Array.Copy(recevBuffer, 5, macArr, 0, 6);
										var macHex = BitConverter.ToString(macArr).Replace('-', ':');

										var nameLen = recevBuffer[17];
										var nameArr = new byte[nameLen];
										Array.Copy(recevBuffer, 18, nameArr, 0, nameLen);
										var name = Encoding.UTF8.GetString(nameArr, 0, nameArr.Length);

										if (ipAddr.Split(dotSeparator, StringSplitOptions.RemoveEmptyEntries).Length == 4)
										{
											IPAddress ipAddr2;
											if (IPAddress.TryParse(ipAddr, out ipAddr2))
											{
												cumulus.LogDebugMessage($"Discovered Ecowitt device: {name}, IP={ipAddr}, MAC={macHex}");
												if (!discovered.IP.Contains(ipAddr) && !discovered.Mac.Contains(macHex))
												{
													discovered.Name.Add(name);
													discovered.IP.Add(ipAddr);
													discovered.Mac.Add(macHex);
												}
											}
										}
										else
										{
											cumulus.LogDebugMessage($"Discovered an unsupported device: {name}, IP={ipAddr}, MAC={macHex}");
										}
									}
								}
								catch
								{ }

							} while (DateTime.Now < endTime);
						}
						catch (Exception ex)
						{
							cumulus.LogMessage("DiscoverGW1000: Error sending discovery request");
							cumulus.LogErrorMessage("DiscoverGW1000: Error: " + ex.Message);
						}
						retryCount++;

					} while (retryCount <= 2);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("An error occurred during Ecowitt auto-discovery");
				cumulus.LogMessage("Error: " + ex.Message);
			}

			return discovered;
		}

		private bool DoDiscovery()
		{
			if (cumulus.Gw1000AutoUpdateIpAddress || string.IsNullOrWhiteSpace(cumulus.Gw1000IpAddress))
			{
				string msg;
				cumulus.LogMessage("Running Ecowitt Local API auto-discovery...");
				cumulus.LogMessage($"Current IP address={cumulus.Gw1000IpAddress}, current MAC={cumulus.Gw1000MacAddress}");

				var discoveredDevices = DiscoverGW1000();

				if (discoveredDevices.IP.Count == 0)
				{
					// We didn't find anything on the network
					msg = "Failed to discover any Ecowitt devices";
					cumulus.LogWarningMessage(msg);
					Cumulus.LogConsoleMessage(msg, ConsoleColor.DarkYellow, true);
					return false;
				}
				else if (discoveredDevices.IP.Count == 1 && (string.IsNullOrEmpty(macaddr) || discoveredDevices.Mac[0] == macaddr))
				{
					cumulus.LogDebugMessage("Discovered one Ecowitt device");
					// If only one device is discovered, and its MAC address matches (or our MAC is blank), then just use it
					if (cumulus.Gw1000IpAddress != discoveredDevices.IP[0])
					{
						cumulus.LogWarningMessage("Discovered a new IP address for the Ecowitt device that does not match our current one");
						cumulus.LogMessage($"Changing previous IP address: {ipaddr} to {discoveredDevices.IP[0]}");
						ipaddr = discoveredDevices.IP[0].Trim();
						cumulus.Gw1000IpAddress = ipaddr;
						if (discoveredDevices.Mac[0] != macaddr)
						{
							cumulus.Gw1000MacAddress = discoveredDevices.Mac[0];
						}
						cumulus.WriteIniFile();
					}
					else
					{
						cumulus.LogMessage("The discovered IP address for the GW1000 matches our current one");
					}
				}
				else if (discoveredDevices.Mac.Contains(macaddr))
				{
					// Multiple devices discovered, but we have a MAC address match

					cumulus.LogDebugMessage("Matching Ecowitt MAC address found on the network");

					var idx = discoveredDevices.Mac.IndexOf(macaddr);

					if (discoveredDevices.IP[idx] != ipaddr)
					{
						cumulus.LogMessage("Discovered a new IP address for the Ecowitt device that does not match our current one");
						cumulus.LogMessage($"Changing previous IP address: {ipaddr} to {discoveredDevices.IP[idx]}");
						ipaddr = discoveredDevices.IP[idx];
						cumulus.Gw1000IpAddress = ipaddr;
						cumulus.WriteIniFile();
					}
				}
				else
				{
					// Multiple devices discovered, and we do not have a clue!

					string iplist = "";
					msg = "Discovered more than one potential Ecowitt device.";
					cumulus.LogWarningMessage(msg);
					Cumulus.LogConsoleMessage(msg);
					msg = "Please select the IP address from the list and enter it manually into the configuration";
					cumulus.LogMessage(msg);
					Cumulus.LogConsoleMessage(msg);

					for (var i = 0; i < discoveredDevices.IP.Count; i++)
					{
						msg = $"Device={discoveredDevices.Name[i]}, IP={discoveredDevices.IP[i]}";
						Cumulus.LogConsoleMessage(msg);
						cumulus.LogMessage(msg);
						iplist += discoveredDevices.IP[i] + " ";
					}
					msg = "  discovered IPs = " + iplist;
					cumulus.LogMessage(msg);
					return false;
				}
			}

			if (string.IsNullOrWhiteSpace(ipaddr))
			{
				var msg = "No IP address configured or discovered for your GW1000, please remedy and restart Cumulus MX";
				cumulus.LogErrorMessage(msg);
				Cumulus.LogConsoleMessage(msg);
				return false;
			}

			return true;
		}

		private void PostDiscovery()
		{
			cumulus.LogMessage("Using IP address = " + ipaddr + " Port = " + AtPort);

			Api.OpenTcpPort(ipaddr, AtPort);

			if (Api.Connected)
			{
				cumulus.LogMessage("Connected OK");
				Cumulus.LogConsoleMessage("Connected to station", ConsoleColor.White, true);
			}
			else
			{
				cumulus.LogErrorMessage("Not Connected");
				Cumulus.LogConsoleMessage("Unable to connect to station", ConsoleColor.Red, true);
			}

			if (Api.Connected)
			{
				// Get the firmware version as check we are communicating
				GW1000FirmwareVersion = GetFirmwareVersion();
				cumulus.LogMessage($"Ecowitt firmware version: {GW1000FirmwareVersion}");
				if (GW1000FirmwareVersion != "???")
				{
					var fwString = GW1000FirmwareVersion.Split(underscoreV, StringSplitOptions.None);
					if (fwString.Length > 1)
					{
						//gatewayType = fwString[0];
						fwVersion = new Version(fwString[1]);
					}
					else
					{
						// failed to get the version, lets assume it's fairly new
						fwVersion = new Version("1.6.5");
					}
				}

				GetSystemInfo(false);

				GetSensorIdsNew();
			}
		}

		private string GetFirmwareVersion()
		{
			var response = "???";
			cumulus.LogMessage("Reading firmware version");
			try
			{
				var data = Api.DoCommand(GW1000Api.Commands.CMD_READ_FIRMWARE_VERSION);
				if (null != data && data.Length > 0)
				{
					response = Encoding.ASCII.GetString(data, 5, data[4]);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"GetFirmwareVersion: Error retrieving/processing firmware version. Message - {ex.Message}");
			}
			return response;
		}

		private bool GetSensorIdsNew()
		{
			cumulus.LogMessage("Reading sensor ids");

			var data = Api.DoCommand(GW1000Api.Commands.CMD_READ_SENSOR_ID_NEW);

			// expected response
			// 0   - 0xff - header
			// 1   - 0xff - header
			// 2   - 0x3C - sensor id command
			// 3-4 - 0x?? - size of response
			// 5   - wh65
			// 6-9 - wh65 id
			// 10   - wh65 battery
			// 11  - wh65 signal 0-4
			// 12  - wh68
			//       ... etc
			// (??) - 0x?? - checksum

			var batteryLow = false;

			try
			{
				if (null != data && data.Length > 200)
				{
					var len = GW1000Api.ConvertBigEndianUInt16(data, 3);

					// Only loop as far as last record (7 bytes) minus the checksum byte
					for (int i = 5; i <= len - 6; i += 7)
					{
						if (PrintSensorInfoNew(data, i))
						{
							batteryLow = true;
						}
					}

					cumulus.BatteryLowAlarm.Triggered = batteryLow;

					return true;
				}
				else
				{
					return false;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetSensorIdsNew: Unexpected error - " + ex.Message);
				// no idea, so report battery as good
				return false;
			}
		}

		private bool PrintSensorInfoNew(byte[] data, int idx)
		{
			// expected response
			// 0   - 0xff - header
			// 1   - 0xff - header
			// 2   - 0x3C - sensor id command
			// 3-4 - 0x?? - size of response
			// 5   - wh65				0
			// 6-9 - wh65 id			1-4
			// 10  - wh65 battery		5
			// 11  - wh65 signal 0-4	6
			// 12  - wh68
			//       ... etc
			// (??) - 0x?? - checksum

			var batteryLow = false;

			try
			{
				var id = GW1000Api.ConvertBigEndianUInt32(data, idx + 1);
				var type = Enum.GetName(typeof(GW1000Api.SensorIds), data[idx]).ToUpper();
				var battPos = idx + 5;
				var sigPos = idx + 6;
				if (string.IsNullOrEmpty(type))
				{
					type = $"unknown type = {id}";
				}
				// Wh65 could be a Wh65 or a Wh24, we found out using the System Info command
				if (type == "WH65")
				{
					type = "WH24/WH65";
				}

				switch (id)
				{
					case 0xFFFFFFFE:
						cumulus.LogDebugMessage($" - {type} sensor = disabled");
						return false;
					case 0xFFFFFFFF:
						cumulus.LogDebugMessage($" - {type} sensor = registering");
						return false;
					default:
						//cumulus.LogDebugMessage($" - {type} sensor id = {id} signal = {data[sigPos]} battery = {data[battPos]}");
						break;
				}

				string batt;
				double battV;
				switch (type)
				{
					case "WH40":
						// Older WH40 units do not send battery info
						// Old ones report a dummy value of 16 = 1.6V
						// Newer ones report volts * 100!
						battV = data[battPos] / 10.0;
						if (battV > 2)
						{
							battV /= 10.0;
							batt = $"{battV:f2}V ({TestBatteryWh40(data[battPos], battV)})"; // low = 1.2V
						}
						else
						{
							batt = $"{battV:f2}V ({TestBatteryWh40(data[battPos], battV)})"; // low = 1.2V
							if (battV == 1.6)
							{
								batt += " dummy value?";
							}
						}

						break;

					case "WH65":
					case "WH24":
					case "WH26":
						batt = TestBattery1(data[battPos], 1);  // 0 or 1
						break;

					case string wh34 when wh34.StartsWith("WH34"):  // ch 1-8
					case string wh35 when wh35.StartsWith("WH35"):  // ch 1-8
					case "WH90":
						// if a WS90 is connected, it has a 8.8 second update rate, so reduce the MX update rate from the default 10 seconds
						if (updateRate > 8000 && updateRate != 8000)
						{
							cumulus.LogMessage($"PrintSensorInfoNew: WS90 sensor detected, changing the update rate from {(updateRate / 1000):D} seconds to 8 seconds");
							updateRate = 8000;
						}
						battV = data[battPos] * 0.02;
						batt = $"{battV:f2}V ({(battV > 2.4 ? "OK" : "Low")})";
						break;

					case string wh31 when wh31.StartsWith("WH31"):  // ch 1-8
						batt = $"{data[battPos]} ({TestBattery1(data[battPos], 1)})";
						break;

					case "WH68":
					case string wh51 when wh51.StartsWith("WH51"):  // ch 1-8
						battV = data[battPos] * 0.1;
						batt = $"{battV:f2}V ({TestBattery10(data[battPos])})"; // volts/10, low = 1.2V
						break;

					case "WH25":
					case "WH45":
					case "WH57":
					case string wh41 when wh41.StartsWith("WH41"): // ch 1-4
					case string wh55 when wh55.StartsWith("WH55"): // ch 1-4
						batt = $"{data[battPos]} ({TestBattery3(data[battPos])})"; // 0-5, low = 1
						break;

					case "WH80":
					case "WS80":
						// if a WS80 is connected, it has a 4.75 second update rate, so reduce the MX update rate from the default 10 seconds
						if (updateRate > 4000 && updateRate != 4000)
						{
							cumulus.LogMessage($"PrintSensorInfoNew: WS80 sensor detected, changing the update rate from {(updateRate / 1000):D} seconds to 4 seconds");
							updateRate = 4000;
						}
						battV = data[battPos] * 0.02;
						batt = $"{battV:f2}V ({(battV > 2.4 ? "OK" : "Low")})";
						break;

					default:
						batt = "???";
						break;
				}

				if (batt.Contains("Low"))
					batteryLow = true;

				SensorReception[type] = data[sigPos];

				cumulus.LogDebugMessage($" - {type} sensor id = {id} signal = {data[sigPos]} battery = {batt}");
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("PrintSensorInfoNew: Error - " + ex.Message);
			}

			return batteryLow;
		}

		private void GetLiveData()
		{
			cumulus.LogDebugMessage("Reading live data");

			// set a flag at the start of every 10 minutes to trigger battery status check
			var minute = DateTime.Now.Minute;
			if (minute != lastMinute)
			{
				tenMinuteChanged = (minute % 10) == 0;
			}

			byte[] data = Api.DoCommand(GW1000Api.Commands.CMD_GW1000_LIVEDATA);

			// sample data = in-temp, in-hum, abs-baro, rel-baro, temp, hum, dir, speed, gust, light, UV uW, UV-I, rain-rate, rain-day, rain-week, rain-month, rain-year, PM2.5, PM-ch1, Soil-1, temp-2, hum-2, temp-3, hum-3, batt
			//byte[] data = new byte[] { 0xFF,0xFF,0x27,0x00,0x5D,0x01,0x00,0x83,0x06,0x55,0x08,0x26,0xE7,0x09,0x26,0xDC,0x02,0x00,0x5D,0x07,0x61,0x0A,0x00,0x89,0x0B,0x00,0x19,0x0C,0x00,0x25,0x15,0x00,0x00,0x00,0x00,0x16,0x00,0x00,0x17,0x00,0x0E,0x00,0x3C,0x10,0x00,0x1E,0x11,0x01,0x4A,0x12,0x00,0x00,0x02,0x68,0x13,0x00,0x00,0x14,0xDC,0x2A,0x01,0x90,0x4D,0x00,0xE3,0x2C,0x34,0x1B,0x00,0xD3,0x23,0x3C,0x1C,0x00,0x60,0x24,0x5A,0x4C,0x04,0x00,0x00,0x00,0xFF,0x5C,0xFF,0x00,0xF4,0xFF,0xFF,0xFF,0xFF,0xFF,0x00,0x00,0xBA };
			//byte[] data = new byte[] { 0xFF, 0xFF, 0x27, 0x00, 0x6D, 0x01, 0x00, 0x96, 0x06, 0x3C, 0x08, 0x27, 0x00, 0x09, 0x27, 0x49, 0x02, 0x00, 0x16, 0x07, 0x61, 0x0A, 0x00, 0x62, 0x0B, 0x00, 0x00, 0x0C, 0x00, 0x06, 0x15, 0x00, 0x01, 0x7D, 0x40, 0x16, 0x00, 0x00, 0x17, 0x00, 0x0E, 0x00, 0x00, 0x10, 0x00, 0x00, 0x11, 0x00, 0xF7, 0x12, 0x00, 0x00, 0x01, 0x5C, 0x13, 0x00, 0x00, 0x15, 0x54, 0x2A, 0x06, 0x40, 0x4D, 0x00, 0xAB, 0x1A, 0xFF, 0x3E, 0x22, 0x39, 0x1B, 0x00, 0x3D, 0x23, 0x51, 0x1C, 0x00, 0xA0, 0x24, 0x45, 0x1D, 0x00, 0xA4, 0x25, 0x3C, 0x1E, 0x00, 0x9D, 0x26, 0x3E, 0x4C, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xA4, 0x00, 0xF4, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x19, 0x00, 0x1A, 0x8F };
			//byte[] data = new byte[] { 0xFF, 0xFF, 0x27, 0x00, 0x6D, 0x01, 0x00, 0xF8, 0x06, 0x35, 0x08, 0x27, 0xD6, 0x09, 0x27, 0xE1, 0x02, 0x00, 0xD2, 0x07, 0x5E, 0x0A, 0x00, 0x79, 0x0B, 0x00, 0x05, 0x0C, 0x00, 0x05, 0x15, 0x00, 0x00, 0x00, 0x00, 0x16, 0x00, 0x02, 0x17, 0x00, 0x2A, 0x01, 0x71, 0x4D, 0x00, 0xC4, 0x1A, 0x00, 0xE4, 0x22, 0x3B, 0x4C, 0x05, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x06, 0xF5, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x19, 0x00, 0x33, 0x0E, 0x00, 0x00, 0x10, 0x00, 0x00, 0x11, 0x00, 0x0D, 0x12, 0x00, 0x00, 0x01, 0x0B, 0x13, 0x00, 0x00, 0x3A, 0x62, 0x0D, 0x00, 0x00, 0x70, 0x00, 0xED, 0x3A, 0x00, 0x2B, 0x00, 0x11, 0x00, 0x1E, 0x00, 0x0D, 0x03, 0x7B, 0x03, 0xD2, 0x06, 0x02 };

			// expected response
			// 0 - 0xff - header
			// 1 - 0xff
			// 2 - 0x27 - live data command
			// 3 - 0x?? - size of response1
			// 4 - 0x?? - size of response2
			// 5-X      - data - NOTE format is Bigendian
			// Y - 0x?? - checksum

			// See: https://osswww.ecowitt.net/uploads/20210716/WN1900%20GW1000,1100%20WH2680,2650%20telenet%20v1.6.0%20.pdf

			try
			{
				if (null != data && data.Length > 16)
				{
					/*
					 * debugging code with example data
					 *
					var hex = "FFFF27004601009D06220821A509270D02001707490A00B40B002F0C0069150001F07C16006317012A00324D00341900AA0E0000100000110000120000009D130000072C0D0000F8";
					int NumberChars = hex.Length;
					byte[] bytes = new byte[NumberChars / 2];
					for (int i = 0; i < NumberChars; i += 2)
						bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);

					data = bytes;
					*/

					// now decode it
					Int16 tempInt16;
					UInt16 tempUint16;
					UInt32 tempUint32;
					var idx = 5;
					var dateTime = DateTime.Now;
					var size = GW1000Api.ConvertBigEndianUInt16(data, 3);

					double windSpeedLast = -999, rainRateLast = -999, rainLast = -999, gustLast = -999;
					int windDirLast = -999;
					double outdoortemp = -999;
					double dewpoint = -999;
					double windchill = -999;

					bool batteryLow = false;

					// We check the new value against what we have already, if older then ignore it!
					double newLightningDistance = 999;
					var newLightningTime = new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

					do
					{
						int chan;
						switch (data[idx++])
						{
							case 0x01:  //Indoor Temperature (℃)
								tempInt16 = GW1000Api.ConvertBigEndianInt16(data, idx);
								// user has mapped indoor temp to outdoor temp
								if (cumulus.Gw1000PrimaryTHSensor == 99)
								{
									// do not process temperature here as if "MX calculates DP" is enabled, we have not yet read the humidity value. Have to do it at the end.
									outdoortemp = tempInt16 / 10.0;
								}
								DoIndoorTemp(ConvertUnits.TempCToUser(tempInt16 / 10.0));
								idx += 2;
								break;
							case 0x02: //Outdoor Temperature (℃)
								if (cumulus.Gw1000PrimaryTHSensor == 0)
								{
									tempInt16 = GW1000Api.ConvertBigEndianInt16(data, idx);
									// do not process temperature here as if "MX calculates DP" is enabled, we have not yet read the humidity value. Have to do it at the end.
									outdoortemp = tempInt16 / 10.0;
								}
								idx += 2;
								break;
							case 0x03: //Dew point (℃)
								tempInt16 = GW1000Api.ConvertBigEndianInt16(data, idx);
								dewpoint = tempInt16 / 10.0;
								idx += 2;
								break;
							case 0x04: //Wind chill (℃)
								if (cumulus.Gw1000PrimaryTHSensor == 0)
								{
									tempInt16 = GW1000Api.ConvertBigEndianInt16(data, idx);
									windchill = tempInt16 / 10.0;
								}
								idx += 2;
								break;
							case 0x05: //Heat index (℃)
									   // cumulus calculates this
								idx += 2;
								break;
							case 0x06: //Indoor Humidity(%)
									   // user has mapped indoor hum to outdoor hum
								if (cumulus.Gw1000PrimaryTHSensor == 99)
								{
									DoOutdoorHumidity(data[idx], dateTime);
								}
								DoIndoorHumidity(data[idx]);
								idx += 1;
								break;
							case 0x07: //Outdoor Humidity (%)
								if (cumulus.Gw1000PrimaryTHSensor == 0)
								{
									DoOutdoorHumidity(data[idx], dateTime);
								}
								idx += 1;
								break;
							case 0x08: //Absolute Barometric (hPa)
								tempUint16 = GW1000Api.ConvertBigEndianUInt16(data, idx);
								StationPressure = ConvertUnits.PressMBToUser(tempUint16 / 10.0);
								idx += 2;
								break;
							case 0x09: //Relative Barometric (hPa)
								tempUint16 = GW1000Api.ConvertBigEndianUInt16(data, idx);
								DoPressure(ConvertUnits.PressMBToUser(tempUint16 / 10.0), dateTime);
								idx += 2;
								break;
							case 0x0A: //Wind Direction (360°)
								windDirLast = GW1000Api.ConvertBigEndianUInt16(data, idx);
								idx += 2;
								break;
							case 0x0B: //Wind Speed (m/s)
								windSpeedLast = ConvertUnits.WindMSToUser(GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0);
								idx += 2;
								break;
							case 0x0C: // Gust speed (m/s)
								gustLast = ConvertUnits.WindMSToUser(GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0);
								idx += 2;
								break;
							case 0x0D: //Rain Event (mm)
								if (cumulus.Gw1000PrimaryRainSensor == 0)
								{
									StormRain = ConvertUnits.RainMMToUser(GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0);
								}
								idx += 2;
								break;
							case 0x0E: //Rain Rate (mm/h)
								if (cumulus.Gw1000PrimaryRainSensor == 0)
								{
									rainRateLast = ConvertUnits.RainMMToUser(GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0);
								}
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
								if (cumulus.Gw1000PrimaryRainSensor == 0)
								{
									rainLast = ConvertUnits.RainMMToUser(GW1000Api.ConvertBigEndianUInt32(data, idx) / 10.0);
								}
								idx += 4;
								break;
							case 0x14: //Rain Totals (mm)
								idx += 4;
								break;
							case 0x15: //Light (lux)
									   // Save the Lux value
								LightValue = GW1000Api.ConvertBigEndianUInt32(data, idx) / 10.0;
								// convert Lux to W/m² - approximately!
								DoSolarRad((int) (LightValue * cumulus.SolarOptions.LuxToWM2), dateTime);
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
									   // does not appear to be implemented
								idx += 6;
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
								tempInt16 = GW1000Api.ConvertBigEndianInt16(data, idx);
								if (cumulus.Gw1000PrimaryTHSensor == chan)
								{
									outdoortemp = tempInt16 / 10.0;
								}
								DoExtraTemp(ConvertUnits.TempCToUser(tempInt16 / 10.0), chan);
								idx += 2;
								break;
							case 0x22: //Humidity 1, 0-100%
							case 0x23: //Humidity 2, 0-100%
							case 0x24: //Humidity 3, 0-100%
							case 0x25: //Humidity 4, 0-100%
							case 0x26: //Humidity 5, 0-100%
							case 0x27: //Humidity 6, 0-100%
							case 0x28: //Humidity 7, 0-100%
							case 0x29: //Humidity 8, 0-100%
								chan = data[idx - 1] - 0x22 + 1;
								if (cumulus.Gw1000PrimaryTHSensor == chan)
								{
									DoOutdoorHumidity(data[idx], dateTime);
								}
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
								tempInt16 = GW1000Api.ConvertBigEndianInt16(data, idx);
								DoSoilTemp(ConvertUnits.TempCToUser(tempInt16 / 10.0), chan);
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
									   // This has been deprecated since v1.6.5 - now use CMD_READ_SENSOR_ID_NEW
								/*
								if (tenMinuteChanged && fwVersion.CompareTo(new Version("1.6.5")) >= 0)
								{
									batteryLow = batteryLow || DoBatteryStatus(data, idx);
								}
								*/
								idx += 16;
								break;
							case 0x2A: //PM2.5 Air Quality Sensor(μg/m³)
								tempUint16 = GW1000Api.ConvertBigEndianUInt16(data, idx);
								DoAirQuality(tempUint16 / 10.0, 1);
								idx += 2;
								break;
							case 0x4D: //for pm25_ch1
							case 0x4E: //for pm25_ch2
							case 0x4F: //for pm25_ch3
							case 0x50: //for pm25_ch4
								chan = data[idx - 1] - 0x4D + 1;
								tempUint16 = GW1000Api.ConvertBigEndianUInt16(data, idx);
								DoAirQualityAvg(tempUint16 / 10.0, chan);
								idx += 2;
								break;
							case 0x51: //PM2.5 ch_2 Air Quality Sensor(μg/m³)
							case 0x52: //PM2.5 ch_3 Air Quality Sensor(μg/m³)
							case 0x53: //PM2.5 ch_4 Air Quality Sensor(μg/m³)
								chan = data[idx - 1] - 0x51 + 2;
								tempUint16 = GW1000Api.ConvertBigEndianUInt16(data, idx);
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
								newLightningDistance = data[idx] == 0xFF ? 999 : ConvertUnits.KmtoUserUnits(data[idx]);
								idx += 1;
								break;
							case 0x61: //Lightning time (UTC)
									   // Sends a default value until the first strike is detected of 0xFFFFFFFF
								tempUint32 = GW1000Api.ConvertBigEndianUInt32(data, idx);
								if (tempUint32 == 0xFFFFFFFF)
								{
									newLightningTime = new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
								}
								else
								{
									var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
									dtDateTime = dtDateTime.AddSeconds(tempUint32).ToLocalTime();
									//cumulus.LogDebugMessage($"Lightning time={dtDateTime}");
									newLightningTime = dtDateTime;
								}
								idx += 4;
								break;
							case 0x62: //Lightning strikes today
								tempUint32 = GW1000Api.ConvertBigEndianUInt32(data, idx);
								//cumulus.LogDebugMessage($"Lightning count={tempUint32}");
								if (tempUint32 == 0 && dateTime.Minute == 59 && dateTime.Hour == 23)
								{
									// Ecowitt clock drift - if the count resets in the minute before midnight, ignore it until after midnight
								}
								else
								{
									LightningStrikesToday = (int) tempUint32;
								}
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
								chan = data[idx - 1] - 0x63 + 1;
								tempInt16 = GW1000Api.ConvertBigEndianInt16(data, idx);
								if (cumulus.EcowittMapWN34[chan] == 0) // false = user temp, true = soil temp
								{
									DoUserTemp(ConvertUnits.TempCToUser(tempInt16 / 10.0), chan);
								}
								else
								{
									DoSoilTemp(ConvertUnits.TempCToUser(tempInt16 / 10.0), cumulus.EcowittMapWN34[chan]);
								}
								// Firmware version 1.5.9 uses 2 data bytes, 1.6.0+ uses 3 data bytes
								if (fwVersion.CompareTo(new Version("1.6.0")) >= 0)
								{
									if (tenMinuteChanged)
									{
										var volts = TestBattery10V(data[idx + 2]);
										if (volts <= 1.2)
										{
											batteryLow = true;
											cumulus.LogWarningMessage($"WN34 channel #{chan} battery LOW = {volts}V");
										}
										else
										{
											cumulus.LogDebugMessage($"WN34 channel #{chan} battery OK = {volts}V");
										}
									}
									idx += 3;
								}
								else
								{
									idx += 2;
								}
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
							case 0x71: // Ambient ONLY - AQI
									   //TODO: Not doing anything with this yet
									   //idx += 2; // SEEMS TO BE VARIABLE
								cumulus.LogDebugMessage("Found a device 0x71 - Ambient AQI. No decode for this yet");
								// We will have lost our place now, so bail out
								idx = size;
								break;
							case 0x72: // WH35 Leaf Wetness ch1
							case 0x73: // WH35 Leaf Wetness ch2
							case 0x74: // WH35 Leaf Wetness ch3
							case 0x75: // WH35 Leaf Wetness ch4
							case 0x76: // WH35 Leaf Wetness ch5
							case 0x77: // WH35 Leaf Wetness ch6
							case 0x78: // WH35 Leaf Wetness ch7
							case 0x79: // WH35 Leaf Wetness ch8
								chan = data[idx - 1] - 0x72 + 2;  // -> 2,4,6,8...
								chan /= 2;  // -> 1,2,3,4...
								DoLeafWetness(data[idx], chan);
								idx += 1;
								break;
							case 0x80: // Piezo Rain Rate
								if (cumulus.Gw1000PrimaryRainSensor == 1)
								{
									rainRateLast = ConvertUnits.RainMMToUser(GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0);
								}
								idx += 2;
								break;
							case 0x81: // Piezo Rain Event
								if (cumulus.Gw1000PrimaryRainSensor == 1)
								{
									StormRain = ConvertUnits.RainMMToUser(GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0);
								}
								idx += 2;
								break;
							case 0x82: // Piezo Hourly Rain
								idx += 2;
								break;
							case 0x83: // Piezo Daily Rain
								idx += 2;
								break;
							case 0x84: // Piezo Weekly Rain
								idx += 2;
								break;
							case 0x85: // Piezo Monthly Rain
								idx += 4;
								break;
							case 0x86: // Piezo Yearly Rain
								if (cumulus.Gw1000PrimaryRainSensor == 1)
								{
									rainLast = ConvertUnits.RainMMToUser(GW1000Api.ConvertBigEndianUInt32(data, idx) / 10.0);
								}
								idx += 4;
								break;
							case 0x87: // Piezo Gain - doc says size = 2*10 ?
								idx += 20;
								break;
							case 0x88: // Piezo Rain Reset Time
								idx += 3;
								break;
							default:
								cumulus.LogDebugMessage($"Error: Unknown sensor id found = {data[idx - 1]}, at position = {idx - 1}");
								// We will have lost our place now, so bail out
								idx = size;
								break;
						}
					} while (idx < size);

					// Some debugging info
					cumulus.LogDebugMessage($"LiveData: Wind Decode >> Last={windSpeedLast:F1}, LastDir={windDirLast}, Gust={gustLast:F1}, (MXAvg={WindAverage:F1})");

					// Now do the stuff that requires more than one input parameter

					// Only set the lightning time/distance if it is newer than what we already have - the GW1000 seems to reset this value
					if (newLightningTime > LightningTime)
					{
						LightningTime = newLightningTime;
						if (newLightningDistance != 999)
							LightningDistance = newLightningDistance;
					}

					// Process outdoor temperature here, as GW1000 currently does not supply Dew Point so we have to calculate it in DoOutdoorTemp()
					if (outdoortemp > -999)
						DoOutdoorTemp(ConvertUnits.TempCToUser(outdoortemp), dateTime);

					// Same for extra T/H sensors
					for (var i = 1; i <= 8; i++)
					{
						if (ExtraHum[i] > 0)
						{
							var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[i]), ExtraHum[i]);
							ExtraDewPoint[i] = ConvertUnits.TempCToUser(dp);
						}
					}

					if (tenMinuteChanged) tenMinuteChanged = false;


					//cumulus.BatteryLowAlarm.Triggered = batteryLow;

					if (gustLast > -999 && windSpeedLast > -999 && windDirLast > -999)
					{
						DoWind(gustLast, windDirLast, windSpeedLast, dateTime);

						/*
						// The protocol does not provide an average value
						// so feed in current MX average
						DoWind(windSpeedLast, windDirLast, WindAverage / cumulus.Calib.WindSpeed.Mult, dateTime);
						var gustLastCal = gustLast * cumulus.Calib.WindGust.Mult;
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
						*/
					}

					if (rainLast > -999 && rainRateLast > -999)
					{
						DoRain(rainLast, rainRateLast, dateTime);
					}

					if (outdoortemp > -999)
					{
						DoWindChill(windchill, dateTime);
						DoApparentTemp(dateTime);
						DoFeelsLike(dateTime);
						DoHumidex(dateTime);
						DoCloudBaseHeatIndex(dateTime);
					}

					DoForecast("", false);

					cumulus.BatteryLowAlarm.Triggered = batteryLow;

					UpdateStatusPanel(dateTime);
					UpdateMQTT();

					dataReceived = true;
					DataStopped = false;
					cumulus.DataStoppedAlarm.Triggered = false;
				}
				else
				{
					cumulus.LogWarningMessage("GetLiveData: Invalid response");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetLiveData: Error - " + ex.Message);
			}
		}

		private void GetSystemInfo(bool driftOnly)
		{
			cumulus.LogMessage("Reading Ecowitt system info");

			var data = Api.DoCommand(GW1000Api.Commands.CMD_READ_SSSS);

			// expected response
			// 0   - 0xff - header
			// 1   - 0xff - header
			// 2   - 0x30 - system info
			// 3   - 0x?? - size of response
			// 4   - frequency - 0=433, 1=868MHz, 2=915MHz, 3=920MHz
			// 5   - sensor type - 0=WH24, 1=WH65
			// 6-9 - UTC time
			// 10  - time zone index (?)
			// 11  - DST 0-1 - false/true
			// 12  - 0x?? - checksum

			var now = DateTime.Now;

			if (data == null)
			{
				cumulus.LogWarningMessage("Nothing returned from System Info!");
				return;
			}

			if (data.Length != 13)
			{
				cumulus.LogWarningMessage("Unexpected response to System Info!");
				return;
			}
			try
			{
				string freq;
				if (data[4] == 0)
					freq = "433MHz";
				else if (data[4] == 1)
					freq = "868MHz";
				else if (data[4] == 2)
					freq = "915MHz";
				else if (data[4] == 3)
					freq = "920MHz";
				else
					freq = $"Unknown [{data[4]}]";

				var mainSensor = data[5] == 0 ? "WH24" : "Other than WH24";

				var unix = (int) GW1000Api.ConvertBigEndianUInt32(data, 6);
				var date = Utils.FromUnixTime(unix);
				var autoDST = data[11] != 0;

				// Ecowitt do not understand Unix time and add the local TZ offset and DST to it!
				var offset = TimeZone.CurrentTimeZone.GetUtcOffset(now);
				if (autoDST && TimeZoneInfo.Local.IsDaylightSavingTime(date))
				{
					unix -= (int) Math.Round(offset.TotalSeconds);
					date = date.AddSeconds(-offset.TotalSeconds);
				}

				var clockdiff = now.ToUnixTime() - unix;

				string slowfast;

				if (clockdiff == 0)
					slowfast = "off";
				else if (clockdiff > 0)
					slowfast = "slow";
				else
					slowfast = "fast";

				if (!driftOnly)
					cumulus.LogMessage($"Gateway Info: frequency: {freq}, main sensor: {mainSensor}, date/time: {date:F}, Automatic DST adjustment: {autoDST}");

				cumulus.LogMessage($"Gateway Info: Gateway clock is {Math.Abs(clockdiff)} secs {slowfast} compared to Cumulus");
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error processing System Info: " + ex.Message);
			}
		}

		private void GetPiezoRainData()
		{
			cumulus.LogDebugMessage("GetPiezoRainData: Reading piezo rain data");

			var data = Api.DoCommand(GW1000Api.Commands.CMD_READ_RAIN);


			// expected response - units mm
			// 0     - 0xff - header
			// 1     - 0xff - header
			// 2     - 0x57 - rain data
			// 3-4   - size(2)
			//
			// Field Value
			// - data size
			//
			// 0E = rain rate
			// - data(2)
			// 10 = rain day
			// - data(4)
			// 11 = rain week
			// - data(4)
			// 12 = rain month
			// - data(4)
			// 13 = rain year
			// - data(4)
			// 0D = rain event
			// - data(2)
			// 0F = rain gain
			// - data(2)
			// 80 = piezo rain rate
			// - data(2)
			// 83 = piezo rain day
			// - data(4)
			// 84 = piezo rain week
			// - data(4)
			// 85 = piezo rain month
			// - data(4)
			// 86 = piezo rain year
			// - data(4)
			// 81 = piezo rain event
			// - data(2)
			// 87 =  piezo gain 0-9
			// - data(2x10)
			// 88 = rain reset time (hr, day [sun-0], month [jan=0])
			// - data(3)
			// 7A = primary rain selection (0=No sensor, 1=Tipper, 2=Piezo)
			// - data(1)
			// 7B = solar gain compensation
			// - data(1)
			// 85 - checksum

			//data = new byte[] { 0xFF, 0xFF, 0x57, 0x00, 0x54, 0x0E, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x11, 0x00, 0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x02, 0xF2, 0x13, 0x00, 0x00, 0x0B, 0x93, 0x0D, 0x00, 0x00, 0x0F, 0x00, 0x64, 0x80, 0x00, 0x00, 0x83, 0x00, 0x00, 0x00, 0x00, 0x84, 0x00, 0x00, 0x00, 0x00, 0x85, 0x00, 0x00, 0x01, 0xDE, 0x86, 0x00, 0x00, 0x0B, 0xF2, 0x81, 0x00, 0x00, 0x87, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x88, 0x00, 0x00, 0x00, 0xF7 };

			if (data == null)
			{
				cumulus.LogErrorMessage("GetPiezoRainData: Nothing returned from Read Rain!");
				return;
			}

			if (data.Length < 8)
			{
				cumulus.LogErrorMessage("GetPiezoRainData: Unexpected response to Read Rain!");
				return;
			}
			try
			{
				// There are reports of different sized messages from different gateways,
				// so parse it sequentially like the live data rather than using fixed offsets
				var idx = 5;
				var size = GW1000Api.ConvertBigEndianUInt16(data, 3);
				double? rRate = null;
				double? rain = null;

				do
				{
					switch (data[idx++])
					{
						// all the two byte values we are ignoring
						case 0x0E: // rain rate
						case 0x0D: // rain event
						case 0x0F: // rain gain
						case 0x81: // piezo rain event
							idx += 2;
							break;
						// all the four byte values we are ignoring
						case 0x10: // rain day
						case 0x11: // rain week
						case 0x12: // rain month
						case 0x13: // rain year
						case 0x83: // piezo rain day
						case 0x84: // piezo rain week
						case 0x85: // piezo rain month
							idx += 4;
							break;
						case 0x80: // piezo rain rate
							if (cumulus.StationOptions.UseRainForIsRaining == 2 && cumulus.Gw1000PrimaryRainSensor != 1)
							{
								IsRaining = GW1000Api.ConvertBigEndianUInt16(data, idx) > 0;
								cumulus.IsRainingAlarm.Triggered = IsRaining;
							}
							else
							{
								rRate = GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0;
							}
							idx += 2;
							break;
						case 0x86: // piezo rain year
							if (cumulus.Gw1000PrimaryRainSensor == 1)
								rain = GW1000Api.ConvertBigEndianUInt32(data, idx) / 10.0;
							idx += 4;
							break;
						case 0x87: // piezo gain 0-9
							idx += 20;
							break;
						case 0x88: // rain reset time
#if DEBUG
							cumulus.LogDebugMessage($"GetPiezoRainData: Rain reset times - hour:{data[idx++]}, day:{data[idx++]}, month:{data[idx++]}");
#else
							idx += 3;
#endif
							break;
						case 0x7A: // Preferred rain sensor on station
							var sensor = data[idx++];
#if DEBUG
							if (sensor == 0)
								cumulus.LogDebugMessage("GetPiezoRainData: No rain sensor available");
							else if (sensor == 1)
								cumulus.LogDebugMessage("GetPiezoRainData: Traditional rain sensor selected");
							else if (sensor == 2)
								cumulus.LogDebugMessage("GetPiezoRainData: Piezo rain sensor selected");
							else
								cumulus.LogDebugMessage("GetPiezoRainData: Unkown rain sensor selection value = " + sensor);
#endif
							break;
						case 0x7B: // Solar gain compensation
#if DEBUG
							cumulus.LogDebugMessage($"GetPiezoRainData: Solar gain compensation = {(data[idx] == '0' ? "disabled" : "enabled")}");
#endif
							idx += 1;
							break;
						default:
							cumulus.LogDebugMessage($"GetPiezoRainData: Error: Unknown value type found = {data[idx - 1]}, at position = {idx - 1}");
							// We will have lost our place now, so bail out
							idx = size;
							break;
					}

				} while (idx < size);

				if (cumulus.Gw1000PrimaryRainSensor == 1)
				{
					if (rRate.HasValue && rain.HasValue)
					{
#if DEBUG
						cumulus.LogDebugMessage($"GetPiezoRainData: Rain Year: {rain:f1} mm, Rate: {rRate:f1} mm/hr");
#endif
						DoRain(ConvertUnits.RainMMToUser(rain.Value), ConvertUnits.RainMMToUser(rRate.Value), DateTime.Now);
					}
					else
					{
						cumulus.LogErrorMessage("GetPiezoRainData: Error, no piezo rain data found in the response");
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetPiezoRainData: Error processing Rain Info: " + ex.Message);
			}
		}


		private bool DoCO2Decode(byte[] data, int index)
		{
			bool batteryLow = false;
			int idx = index;
			cumulus.LogDebugMessage("WH45 CO₂: Decoding...");
			//CO2Data co2Data = (CO2Data)RawDeserialize(data, index, typeof(CO2Data));

			try
			{
				CO2_temperature = ConvertUnits.TempCToUser(GW1000Api.ConvertBigEndianInt16(data, idx) / 10.0);
				idx += 2;
				CO2_humidity = data[idx++];
				CO2_pm10 = GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0;
				idx += 2;
				CO2_pm10_24h = GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0;
				idx += 2;
				CO2_pm2p5 = GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0;
				idx += 2;
				CO2_pm2p5_24h = GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0;
				idx += 2;
				CO2 = GW1000Api.ConvertBigEndianUInt16(data, idx);
				idx += 2;
				CO2_24h = GW1000Api.ConvertBigEndianUInt16(data, idx);
				idx += 2;
				var batt = TestBattery3(data[idx]);
				var msg = $"WH45 CO₂: temp={CO2_temperature.ToString(cumulus.TempFormat)}, hum={CO2_humidity}, pm10={CO2_pm10:F1}, pm10_24h={CO2_pm10_24h:F1}, pm2.5={CO2_pm2p5:F1}, pm2.5_24h={CO2_pm2p5_24h:F1}, CO₂={CO2}, CO₂_24h={CO2_24h}";
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
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("DoCO2Decode: Error - " + ex.Message);
			}

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

		private static string TestBattery3(byte value)
		{
			if (value == 6)
				return "DC";
			return value > 1 ? "OK" : "Low";
		}

		private static string TestBattery10(byte value)
		{
			// consider 1.2V as low
			if (value == 255)
				return "n/a";

			return value > 12 ? "OK" : "Low";
		}

		private static double TestBattery10V(byte value)
		{
			return value / 10.0;
		}

		private static string TestBatteryWh40(byte value, double volts)
		{
			if (value == 255)
				return "n/a";

			return volts > 1.2 ? "OK" : "Low";
		}


		private class Discovery
		{
			public List<string> IP { get; set; }
			public List<string> Name { get; set; }
			public List<string> Mac { get; set; }

			public Discovery()
			{
				IP = new List<string>();
				Name = new List<string>();
				Mac = new List<string>();
			}
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
				cumulus.LogErrorMessage($"ERROR: No data received from the GW1000 for {tmrDataWatchdog.Interval / 1000} seconds");
				if (!DataStopped)
				{
					DataStoppedTime = DateTime.Now;
					DataStopped = true;
				}
				cumulus.DataStoppedAlarm.LastMessage = $"No data received from the GW1000 for {tmrDataWatchdog.Interval / 1000} seconds";
				cumulus.DataStoppedAlarm.Triggered = true;
				if (DoDiscovery())
				{
					PostDiscovery();
				}
			}
		}
	}
}
