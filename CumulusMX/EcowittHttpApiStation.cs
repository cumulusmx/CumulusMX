using System;
using System.Collections.Generic;
using System.Linq;
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
	internal class EcowittHttpApiStation : WeatherStation
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
	{
		private string ipaddr;
		private readonly string macaddr;
		private string deviceModel;
		private string deviceFirmware;
		private int updateRate = 10000; // 10 seconds by default
		private int lastMinute = -1;
		private int lastHour = -1;

		private readonly EcowittApi ecowittApi;
		private readonly EcowittLocalApi localApi;

		private int maxArchiveRuns = 1;

		private bool dataReceived = false;

		private readonly System.Timers.Timer tmrDataWatchdog;

		private readonly Task historyTask;
		private Task liveTask;

		private Version fwVersion;
		internal static readonly char[] dotSeparator = ['.'];
		internal static readonly string[] underscoreV = ["_V"];

		public EcowittHttpApiStation(Cumulus cumulus) : base(cumulus)
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

			localApi = new EcowittLocalApi(cumulus);

			ecowittApi = new EcowittApi(cumulus, this);

			DoDiscovery();

			_ = CheckAvailableFirmware();

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			historyTask = Task.Run(getAndProcessHistoryData, cumulus.cancellationToken);
		}


		public override void Start()
		{
			lastMinute = DateTime.Now.Minute;

			// Start a broadcast watchdog to warn if messages are not being received
			tmrDataWatchdog.Elapsed += DataTimeout;
			tmrDataWatchdog.Interval = 1000 * 30; // timeout after 30 seconds
			tmrDataWatchdog.AutoReset = true;
			tmrDataWatchdog.Start();


			// just incase we did not catch-up any history
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);

			cumulus.LogMessage("Starting Ecowitt Local HTTP API");

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
						var rawData = localApi.GetLiveData(cumulus.cancellationToken);
						dataLastRead = DateTime.Now;

						// every 30 seconds read the rain rate
						if ((cumulus.Gw1000PrimaryRainSensor == 1 || cumulus.StationOptions.UseRainForIsRaining == 2) && (DateTime.UtcNow - piezoLastRead).TotalSeconds >= 30 && !cumulus.cancellationToken.IsCancellationRequested)
						{
							GetPiezoRainData();
							piezoLastRead = DateTime.UtcNow;
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

							var hour = DateTime.Now.Hour;
							if (lastHour != hour)
							{
								lastHour = hour;

								if (hour == 13)
								{
									var fw = GetFirmwareVersion();
									if (fw != "???")
									{
										GW1000FirmwareVersion = fw;
										deviceModel = GW1000FirmwareVersion.Split('_')[0];
										deviceFirmware = GW1000FirmwareVersion.Split('_')[1];

										var fwString = GW1000FirmwareVersion.Split(underscoreV, StringSplitOptions.None);
										if (fwString.Length > 1)
										{
											fwVersion = new Version(fwString[1]);
										}
										else
										{
											// failed to get the version, lets assume it's fairly new
											fwVersion = new Version("1.6.5");
										}
									}

									_ = CheckAvailableFirmware();
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
					//do nothing
				}
				finally
				{
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
				//do nothing
			}
		}


		public override void getAndProcessHistoryData()
		{
			Cumulus.SyncInit.Wait();

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
				ecowittApi.GetStationList(true, cumulus.EcowittMacAddress, cumulus.cancellationToken);
			}

			_ = Cumulus.SyncInit.Release();

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
				using var client = new UdpClient();
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
							{
								//do nothing
							}

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
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("An error occurred during Ecowitt auto-discovery");
				cumulus.LogMessage("Error: " + ex.Message);
			}

			return discovered;
		}

		private void DoDiscovery()
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
					return;
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
						deviceModel = discoveredDevices.Name[0].Split('-')[0];
						deviceFirmware = discoveredDevices.Name[0].Split('-')[1].Split(' ')[1];
						if (discoveredDevices.Mac[0] != macaddr)
						{
							cumulus.Gw1000MacAddress = discoveredDevices.Mac[0].ToUpper();
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
					deviceModel = discoveredDevices.Name[idx].Split('-')[0];
					deviceFirmware = discoveredDevices.Name[idx].Split('-')[1].Split(' ')[1];

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

					StringBuilder iplist = new("  discovered IPs = ");
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
						iplist.Append(discoveredDevices.IP[i] + " ");
					}
					cumulus.LogMessage(iplist.ToString());
					return;
				}
			}

			if (string.IsNullOrWhiteSpace(ipaddr))
			{
				var msg = "No IP address configured or discovered for your GW1000, please remedy and restart Cumulus MX";
				cumulus.LogErrorMessage(msg);
				Cumulus.LogConsoleMessage(msg);
				return;
			}

			return;
		}

		private string GetFirmwareVersion()
		{
			var response = "???";
			return response;
		}

		private async Task CheckAvailableFirmware()
		{
			if (deviceModel == null)
			{
				cumulus.LogMessage("Device Model not determined, firmware check skipped.");
				return;
			}

			if (EcowittApi.FirmwareSupportedModels.Contains(deviceModel[..6]))
			{
				_ = await ecowittApi.GetLatestFirmwareVersion(deviceModel, cumulus.EcowittMacAddress, deviceFirmware, cumulus.cancellationToken);
			}
			else
			{
				var retVal = ecowittApi.GetSimpleLatestFirmwareVersion(deviceModel, cumulus.cancellationToken).Result;
				if (retVal != null)
				{
					var verVer = new Version(retVal[0]);
					if (fwVersion < verVer)
					{
						cumulus.FirmwareAlarm.LastMessage = $"A new firmware version is available: {retVal[0]}.\nChange log:\n{string.Join('\n', retVal[1].Split(';'))}";
						cumulus.FirmwareAlarm.Triggered = true;
						cumulus.LogWarningMessage($"FirmwareVersion: Latest Version {retVal[0]}, Change log:\n{string.Join('\n', retVal[1].Split(';'))}");
					}
					else
					{
						cumulus.FirmwareAlarm.Triggered = false;
						cumulus.LogDebugMessage($"FirmwareVersion: Already on the latest Version {retVal[0]}");
					}
				}
			}
		}

		private void GetSensorIdsNew()
		{
			cumulus.LogMessage("Reading sensor ids");

		}

		private bool PrintSensorInfoNew(byte[] data, int idx)
		{
			var batteryLow = false;

			return batteryLow;
		}

		private void GetLiveData()
		{
			cumulus.LogDebugMessage("Reading live data");

			byte[] data = null; // localApi.DoCommand(GW1000Api.Commands.CMD_GW1000_LIVEDATA);

			try
			{
				if (null != data && data.Length > 16)
				{
#pragma warning disable S125 // Sections of code should not be commented out
					/*
					 * debugging code with example data
					 *
						//var hex = "FFFF27004601009D06220821A509270D02001707490A00B40B002F0C0069150001F07C16006317012A00324D00341900AA0E0000100000110000120000009D130000072C0D0000F8";
						var hex = "ffff27009c0100c806360827500927500200b107630a00710b00000c00001500000a2816000017002c2d2e28303332561c00c224361d00c325361e00c226361f00c3273621001b580059006200000012616609bbfc60011900240e00001000d31100d3120000022f130000022f0d00af6300684d6400cd4465006d4a66ff5b4e6700c74d6b00dc31002700190018001002550265000a0008002200160630";
						int NumberChars = hex.Length;
						byte[] bytes = new byte[NumberChars / 2];
						for (int i = 0; i < NumberChars; i += 2)
							bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
						data = bytes;
					*/
#pragma warning restore S125 // Sections of code should not be commented out

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
					//double dewpoint
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
								//tempInt16 = GW1000Api.ConvertBigEndianInt16(data, idx)
								//dewpoint = tempInt16 / 10.0
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
								AltimeterPressure = ConvertUnits.PressMBToUser(MeteoLib.StationToAltimeter(tempUint16 / 10.0, AltitudeM(cumulus.Altitude)));
								// Leave calculate SLP until the end as it depends on temperature
								idx += 2;
								break;
							case 0x09: //Relative Barometric (hPa)
								if (!cumulus.StationOptions.CalculateSLP)
								{
									tempUint16 = GW1000Api.ConvertBigEndianUInt16(data, idx);
									DoPressure(ConvertUnits.PressMBToUser(tempUint16 / 10.0), dateTime);
								}
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
							case 0x0F: //Rain Gain (mm)
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
									var dtDateTime = DateTime.UnixEpoch;
									dtDateTime = dtDateTime.AddSeconds(tempUint32).ToLocalTime();
									newLightningTime = dtDateTime;
								}
								idx += 4;
								break;
							case 0x62: //Lightning strikes today
								tempUint32 = GW1000Api.ConvertBigEndianUInt32(data, idx);
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
								if ((deviceModel.StartsWith("GW1000") && fwVersion >= new Version("1.6.0")) || !deviceModel.StartsWith("GW1000"))
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
									idx += 3;
								}
								else
								{
									idx += 2;
								}
								break;
							case 0x6B:  //WH34 User temperature battery (8 channels) - No longer used in firmware 1.6.0+
										//Later version from ??? send an extended WH45 CO₂ data block
								if (deviceModel.StartsWith("GW1000") && fwVersion < new Version("1.6.0"))
								{
									batteryLow = batteryLow || DoWH34BatteryStatus(data, idx);
									idx += 8;
								}
								else
								{
									batteryLow = batteryLow || DoCO2DecodeNew(data, idx);
									idx += 23;
								}
								break;
							case 0x6C: // Heap size - has constant offset of +3692 to GW1100 HTTP value????
								StationFreeMemory = (int) GW1000Api.ConvertBigEndianUInt32(data, idx);
								idx += 4;
								break;
							case 0x70: // WH45 CO₂
								batteryLow = batteryLow || DoCO2DecodeNew(data, idx);
								idx += 16;
								break;
							case 0x71: // Ambient ONLY - AQI
									   // Not doing anything with this yet
									   //idx += 2; // SEEMS TO BE VARIABLE?!
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
							case 0x7A: // Rain Priority
								idx += 1;
								break;
							case 0x7B: // Radiation compensation
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
							case 0x82: // Piezo Hourly Rain (not used)
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
						if (newLightningDistance < 999)
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

					if (gustLast > -999 && windSpeedLast > -999 && windDirLast > -999)
					{
						DoWind(gustLast, windDirLast, windSpeedLast, dateTime);
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

						if (cumulus.StationOptions.CalculateSLP)
						{
							var abs = cumulus.Calib.Press.Calibrate(StationPressure);
							var slp = MeteoLib.GetSeaLevelPressure(AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToMB(abs), ConvertUnits.UserTempToC(OutdoorTemperature), cumulus.Latitude);
							DoPressure(ConvertUnits.PressMBToUser(slp), dateTime);
						}
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

		}

		private void GetPiezoRainData()
		{
			cumulus.LogDebugMessage("GetPiezoRainData: Reading piezo rain data");

			byte[] data = null; //localApi.DoCommand(GW1000Api.Commands.CMD_READ_RAIN);


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

			//data = new byte[] { 0xFF, 0xFF, 0x57, 0x00, 0x54, 0x0E, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x11, 0x00, 0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x02, 0xF2, 0x13, 0x00, 0x00, 0x0B, 0x93, 0x0D, 0x00, 0x00, 0x0F, 0x00, 0x64, 0x80, 0x00, 0x00, 0x83, 0x00, 0x00, 0x00, 0x00, 0x84, 0x00, 0x00, 0x00, 0x00, 0x85, 0x00, 0x00, 0x01, 0xDE, 0x86, 0x00, 0x00, 0x0B, 0xF2, 0x81, 0x00, 0x00, 0x87, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x00, 0x64, 0x88, 0x00, 0x00, 0x00, 0xF7 }

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


		private bool DoCO2DecodeNew(byte[] data, int index)
		{
			bool batteryLow = false;
			int idx = index;
			cumulus.LogDebugMessage("WH45 CO₂ New: Decoding...");

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
				CO2_pm1 = GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0;
				idx += 2;
				CO2_pm1_24h = GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0;
				idx += 2;
				CO2_pm4 = GW1000Api.ConvertBigEndianUInt16(data, idx) / 10.0;
				idx += 2;
				CO2_pm4_24h = GW1000Api.ConvertBigEndianUInt16(data, idx)/ 10.0;
				idx += 2;
				var msg = $"WH45 CO₂ New: temp={CO2_temperature.ToString(cumulus.TempFormat)}, hum={CO2_humidity}, pm10={CO2_pm10:F1}, pm10_24h={CO2_pm10_24h:F1}, pm2.5={CO2_pm2p5:F1}, pm2.5_24h={CO2_pm2p5_24h:F1}, CO₂={CO2}, CO₂_24h={CO2_24h}, pm1={CO2_pm1:F1}, pm1_24h={CO2_pm1_24h:F1}, pm4={CO2_pm4:F1}, pm4_24h={CO2_pm4_24h:F1}";
				var batt = TestBattery3(data[idx]);
				batteryLow = batt == "Low";
				msg += $", Battery={batt}";
				cumulus.LogDebugMessage(msg);

				CO2_pm2p5_aqi = GetAqi(AqMeasure.pm2p5, CO2_pm2p5);
				CO2_pm2p5_24h_aqi = GetAqi(AqMeasure.pm2p5h24, CO2_pm2p5_24h);
				CO2_pm10_aqi = GetAqi(AqMeasure.pm10, CO2_pm10);
				CO2_pm10_24h_aqi = GetAqi(AqMeasure.pm10h24, CO2_pm10_24h);
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "DoCO2DecodeNew: Error");
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
			if (value == 9)
				return "OFF";
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


		private sealed class Discovery
		{
			public List<string> IP { get; set; }
			public List<string> Name { get; set; }
			public List<string> Mac { get; set; }

			public Discovery()
			{
				IP = [];
				Name = [];
				Mac = [];
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
				DoDiscovery();
			}
		}
	}
}
