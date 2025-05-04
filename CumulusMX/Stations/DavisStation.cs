using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CumulusMX
{
	internal class DavisStation : WeatherStation
	{
		private readonly bool isSerial;
		private readonly int port;
		private int previousMinuteDisconnect = 60;
		private const int ACK = 6;
		private const int NACK = 33;
		private const int CANCEL = 24;
		private const int ESC = 0x1b;
		private const int CR = 13;
		private const int LF = 10;
		private bool clockSetNeeded;
		private int previousMinuteSetClock = 60;
		private const string Newline = "\n";
		private DateTime lastRecepStatsTime;
		private const int CommWaitTimeMs = 1000;
		private const int TcpWaitTimeMs = 2500;
		private int maxArchiveRuns = 2;
		private bool stop;
		private int loggerInterval;

		private readonly int[,] ForecastLookup =
		{
			{14, 0, 0}, {13, 0, 0}, {12, 0, 0}, {11, 0, 0}, {13, 0, 0}, {25, 0, 0}, {24, 0, 0}, {24, 0, 0}, {10, 0, 0}, {24, 0, 0}, {24, 0, 0},
			{13, 0, 0}, {7, 2, 0}, {24, 0, 0}, {13, 0, 0}, {6, 3, 0}, {13, 0, 0}, {24, 0, 0}, {13, 0, 0}, {6, 6, 0}, {13, 0, 0}, {24, 0, 0},
			{13, 0, 0}, {7, 3, 0}, {10, 0, 6}, {24, 0, 0}, {13, 0, 0}, {7, 6, 6}, {10, 0, 6}, {7, 0, 0}, {24, 0, 0}, {13, 0, 0}, {7, 6, 6},
			{10, 0, 6}, {7, 0, 0}, {24, 0, 0}, {13, 0, 0}, {7, 6, 6}, {24, 0, 0}, {13, 0, 0}, {10, 1, 0}, {10, 0, 0}, {24, 0, 0}, {13, 0, 0},
			{6, 2, 0}, {6, 0, 0}, {24, 0, 0}, {13, 0, 0}, {7, 4, 0}, {24, 0, 0}, {13, 0, 0}, {7, 4, 5}, {24, 0, 0}, {13, 0, 0}, {7, 4, 5},
			{24, 0, 0}, {13, 0, 0}, {7, 7, 0}, {24, 0, 0}, {13, 0, 0}, {7, 7, 5}, {24, 0, 0}, {13, 0, 0}, {7, 4, 5}, {24, 0, 0}, {13, 0, 0},
			{7, 6, 0}, {24, 0, 0}, {13, 0, 0}, {7, 16, 0}, {4, 14, 0}, {24, 0, 0}, {4, 14, 0}, {13, 0, 0}, {4, 14, 0}, {25, 0, 0}, {24, 0, 0},
			{14, 0, 0}, {4, 14, 0}, {13, 0, 0}, {4, 14, 0}, {14, 0, 0}, {24, 0, 0}, {13, 0, 0}, {6, 3, 0}, {2, 18, 0}, {24, 0, 0}, {13, 0, 0},
			{2, 16, 0}, {1, 18, 0}, {1, 16, 0}, {24, 0, 0}, {13, 0, 0}, {5, 9, 0}, {6, 9, 0}, {2, 18, 6}, {24, 0, 0}, {13, 0, 0}, {2, 16, 6},
			{1, 18, 6}, {1, 16, 6}, {24, 0, 0}, {13, 0, 0}, {5, 4, 4}, {6, 4, 4}, {24, 0, 0}, {13, 0, 0}, {5, 10, 4}, {6, 10, 4}, {2, 13, 4},
			{2, 0, 4}, {1, 13, 4}, {1, 0, 4}, {2, 13, 4}, {24, 0, 0}, {13, 0, 0}, {2, 3, 4}, {1, 13, 4}, {1, 3, 4}, {3, 14, 0}, {3, 0, 0},
			{2, 14, 3}, {2, 0, 3}, {3, 0, 0}, {24, 0, 0}, {13, 0, 0}, {1, 6, 5}, {24, 0, 0}, {13, 0, 0}, {5, 5, 5}, {2, 14, 5}, {24, 0, 0},
			{13, 0, 0}, {2, 6, 5}, {2, 11, 0}, {2, 0, 0}, {2, 17, 5}, {24, 0, 0}, {13, 0, 0}, {2, 7, 5}, {1, 17, 5}, {24, 0, 0}, {13, 0, 0},
			{1, 7, 5}, {24, 0, 0}, {13, 0, 0}, {6, 5, 5}, {2, 0, 5}, {2, 17, 5}, {24, 0, 0}, {13, 0, 0}, {2, 15, 5}, {1, 17, 5}, {1, 15, 5},
			{24, 0, 0}, {13, 0, 0}, {5, 10, 5}, {6, 10, 5}, {5, 18, 3}, {24, 0, 0}, {13, 0, 0}, {2, 16, 3}, {1, 18, 3}, {1, 16, 3}, {5, 10, 3},
			{24, 0, 0}, {13, 0, 0}, {5, 10, 4}, {6, 10, 3}, {6, 10, 4}, {24, 0, 0}, {13, 0, 0}, {5, 10, 3}, {6, 10, 3}, {24, 0, 0}, {13, 0, 0},
			{5, 4, 3}, {6, 4, 3}, {2, 12, 3}, {24, 0, 0}, {13, 0, 0}, {2, 8, 3}, {1, 13, 3}, {1, 8, 3}, {2, 18, 0}, {24, 0, 0}, {13, 0, 0},
			{2, 16, 3}, {1, 18, 0}, {1, 16, 0}, {24, 0, 0}, {13, 0, 0}, {2, 5, 5}, {0, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}
		};


		private readonly Stopwatch awakeStopWatch = new();

		private double previousPressStation = 9999;

		private TcpClient socket;

		public DavisStation(Cumulus cumulus) : base(cumulus)
		{
			calculaterainrate = false;

			// VP2 does not provide pressure trend strings
			cumulus.StationOptions.UseCumulusPresstrendstr = true;
			// does not send Wind Chill in LOOP
			cumulus.StationOptions.CalculatedWC = true;

			isSerial = (cumulus.DavisOptions.ConnectionType == 0);

			bool connectedOK;

			cumulus.LogMessage("Station type = Davis");
			cumulus.LogMessage("LOOP2 " + (cumulus.DavisOptions.UseLoop2 ? "enabled" : "disabled"));

			if (isSerial)
			{
				cumulus.LogMessage("Serial device = " + cumulus.ComportName);
				cumulus.LogMessage("Serial speed = " + cumulus.DavisOptions.BaudRate);

				InitSerial();

				connectedOK = comport.IsOpen;
			}
			else
			{
				port = Convert.ToInt32(cumulus.DavisOptions.TCPPort);

				cumulus.LogMessage("IP address = " + cumulus.DavisOptions.IPAddr + " Port = " + port);
				cumulus.LogMessage("periodic disconnect = " + cumulus.DavisOptions.PeriodicDisconnectInterval);

				InitTCP();

				connectedOK = socket != null;

			}

			if (connectedOK)
			{
				cumulus.LogMessage("Connected OK");
				Cumulus.LogConsoleMessage("Connected to station");
			}
			else
			{
				cumulus.LogMessage("Not Connected");
				Cumulus.LogConsoleMessage("Unable to connect to station");
			}

			if (!connectedOK) return;

			DavisFirmwareVersion = GetFirmwareVersion();
			// retry as this command seem particularly unreliable
			if (DavisFirmwareVersion == "???")
			{
				DavisFirmwareVersion = GetFirmwareVersion();
			}
			cumulus.LogMessage("FW version = " + DavisFirmwareVersion);
			try
			{
				if (DavisFirmwareVersion == "???" && cumulus.DavisOptions.UseLoop2)
				{
					cumulus.LogWarningMessage("Unable to determine the firmware version, LOOP2 may not be supported");
				}
				else if ((float.Parse(DavisFirmwareVersion, CultureInfo.InvariantCulture.NumberFormat) < (float) 1.9) && cumulus.DavisOptions.UseLoop2)
				{
					cumulus.LogWarningMessage("LOOP2 is enabled in Cumulus.ini but this firmware version does not support it. Consider disabling it in Cumulus.ini");
					Cumulus.LogConsoleMessage("Your console firmware version does not support LOOP2. Consider disabling it in Cumulus.ini", ConsoleColor.Yellow);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("Error parsing firmware string for version number: " + ex.Message);
			}

			if (cumulus.DavisOptions.ReadReceptionStats)
			{
				var recepStats = GetReceptionStats();
				DecodeReceptionStats(recepStats);
			}

			cumulus.LogMessage("Last update time = " + cumulus.LastUpdateTime);

			var consoleclock = GetTime();
			var nowTime = DateTime.Now;

			if (consoleclock > DateTime.MinValue)
			{
				cumulus.LogMessage("Console clock: " + consoleclock);

				var timeDiff = nowTime.Subtract(consoleclock).TotalSeconds;

				if (Math.Abs(timeDiff) >= 30)
				{
					if (cumulus.StationOptions.SyncTime)
					{
						cumulus.LogWarningMessage($"Console clock: Console is {(int) timeDiff} seconds adrift, resetting it...");

						SetTime();
						// Pause whilst the console sorts itself out
						cumulus.LogMessage("Console clock: Pausing to allow Davis console to process the new date/time");
						Cumulus.LogConsoleMessage("Pausing to allow Davis console to process the new date/time");
						Thread.Sleep(1000 * 5);

						consoleclock = GetTime();

						if (consoleclock > DateTime.MinValue)
						{
							cumulus.LogMessage("Console clock: " + consoleclock);
						}
						else
						{
							cumulus.LogWarningMessage("Console clock: Failed to read console time");
						}
					}
					else
					{
						cumulus.LogWarningMessage($"Console clock: Console is {(int) timeDiff} seconds adrift but automatic setting is disabled - you should set the clock manually.");
					}
				}
				else
				{
					cumulus.LogMessage($"Console clock: Accurate to +/- 30 seconds, no need to set it (diff={(int) nowTime.Subtract(consoleclock).TotalSeconds}s)");
				}
			}
			else
			{
				cumulus.LogWarningMessage("Console clock: Failed to read console time");
			}


			DateTime tooold = new DateTime(0, DateTimeKind.Local);

			if ((cumulus.LastUpdateTime <= tooold) || !cumulus.StationOptions.UseDataLogger)
			{
				// there's nothing in the database, so we haven't got a rain counter
				// we can't load the history data, so we'll just have to go live

				timerStartNeeded = true;
				LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);
				DoDayResetIfNeeded();
				DoTrendValues(DateTime.Now);

				cumulus.LogMessage("Starting Davis ");
				bw = new BackgroundWorker();
				bw.DoWork += bw_DoStart;
				bw.RunWorkerAsync();
			}
			else
			{
				// Read the data from the logger
				startReadingHistoryData();
			}

			// check the logger interval
			// do this after reading the history so we do not wipe it out before we read it!
			CheckLoggerInterval();

		}

		private void DecodeReceptionStats(string recepStats)
		{
			try
			{
				var vals = recepStats.Split(' ');

				DavisTotalPacketsReceived = Convert.ToInt32(vals[0]);
				if (DavisTotalPacketsReceived < 0)
				{
					DavisTotalPacketsReceived += 65536; // The console uses 16 bit signed variable to hold the value so it bit wraps
				}
				DavisTotalPacketsMissed[0] = Convert.ToInt32(vals[1]);
				DavisNumberOfResynchs[0] = Convert.ToInt32(vals[2]);
				DavisMaxInARow[0] = Convert.ToInt32(vals[3]);
				DavisNumCRCerrors[0] = Convert.ToInt32(vals[4]);
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("DecodeReceptionStats: Error - " + ex.Message);
			}
		}

		private string GetFirmwareVersion()
		{
			cumulus.LogMessage("Reading firmware version");
			StringBuilder response = new();
			StringBuilder data = new();
			int ch;

			// expected response - <LF><CR>OK<LF><CR>1.73<LF><CR>

			if (isSerial)
			{
				const string commandString = "NVER";
				if (WakeVP(comport))
				{
					try
					{
						comport.DiscardInBuffer();
						comport.WriteLine(commandString);

						if (WaitForOK(comport))
						{
							// Read the response
							do
							{
								// Read the current character
								ch = comport.ReadChar();
								response.Append(Convert.ToChar(ch));
								data.Append(ch.ToString("X2") + "-");
							} while (ch != CR);

							data.Length--;
						}
					}
					catch (TimeoutException)
					{
						cumulus.LogErrorMessage("GetFirmwareVersion: Timed out waiting for a response");
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("GetFirmwareVersion: Error - " + ex.Message);
						cumulus.LogDebugMessage("GetFirmwareVersion: Attempting to reconnect to logger");
						InitSerial();
						cumulus.LogDebugMessage("GetFirmwareVersion: Reconnected to logger");
					}
				}
			}
			else
			{
				const string commandString = "NVER\n";
				if (WakeVP(socket))
				{
					try
					{
						NetworkStream stream = socket.GetStream();
						stream.ReadTimeout = TcpWaitTimeMs;
						stream.WriteTimeout = TcpWaitTimeMs;

						stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

						if (WaitForOK(stream))
						{
							do
							{
								// Read the current character
								ch = stream.ReadByte();
								response.Append(Convert.ToChar(ch));
								data.Append(ch.ToString("X2") + "-");
							} while (ch != CR);

							data.Length--;
						}
					}
					catch (System.IO.IOException ex)
					{
						if (ex.Message.Contains("did not properly respond after a period of time"))
						{
							cumulus.LogWarningMessage("GetFirmwareVersion: Timed out waiting for a response");
						}
						else
						{
							cumulus.LogErrorMessage("GetFirmwareVersion: Error - " + ex.Message);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("GetFirmwareVersion: Error - " + ex.Message);
						cumulus.LogDebugMessage("GetFirmwareVersion: Attempting to reconnect to logger");
						InitTCP();
						cumulus.LogDebugMessage("GetFirmwareVersion: Reconnected to logger");
					}
				}
			}

			cumulus.LogDataMessage("GetFirmwareVersion: Received - " + data);

			return response.Length >= 5 ? response.ToString()[..^2] : "???";
		}

		private void CheckLoggerInterval()
		{
			cumulus.LogMessage("CheckLoggerInterval: Reading logger interval");
			var bytesRead = 0;
			byte[] readBuffer = new byte[40];

			// default the logger interval to the CMX interval - change it later if we find different
			loggerInterval = Cumulus.logints[cumulus.DataLogInterval];

			// response should be (5 mins):
			// ACK  VAL CKS1 CKS2
			// 0x06-05-50-3F
			if (isSerial)
			{
				const string commandString = "EEBRD 2D 01";
				if (WakeVP(comport))
				{
					try
					{
						comport.WriteLine(commandString);

						if (!WaitForACK(comport))
						{
							cumulus.LogWarningMessage("CheckLoggerInterval: No ACK in response to requesting logger interval");
							return;
						}

						// Read the response
						do
						{
							// Read the current character
							var ch = comport.ReadChar();
							readBuffer[bytesRead] = (byte) ch;
							bytesRead++;
						} while (bytesRead < 3);
					}
					catch (TimeoutException)
					{
						cumulus.LogWarningMessage("CheckLoggerInterval: Timed out waiting for a response");
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("CheckLoggerInterval: Error - " + ex.Message);
						awakeStopWatch.Stop();
					}
				}
			}
			else
			{
				const string commandString = "EEBRD 2D 01\n";
				if (WakeVP(socket))
				{
					try
					{
						NetworkStream stream = socket.GetStream();
						stream.ReadTimeout = TcpWaitTimeMs;
						stream.WriteTimeout = TcpWaitTimeMs;

						stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

						if (!WaitForACK(stream))
						{
							cumulus.LogWarningMessage("CheckLoggerInterval: No ACK in response to requesting logger interval");
							return;
						}

						do
						{
							// Read the current character
							var ch = stream.ReadByte();
							readBuffer[bytesRead] = (byte) ch;
							bytesRead++;
						} while (bytesRead < 3);
					}
					catch (System.IO.IOException ex)
					{
						if (ex.Message.Contains("did not properly respond after a period"))
						{
							cumulus.LogWarningMessage("CheckLoggerInterval: Timed out waiting for a response");
						}
						else
						{
							cumulus.LogErrorMessage("CheckLoggerInterval: Error - " + ex.Message);
							awakeStopWatch.Stop();
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("CheckLoggerInterval: Error - " + ex.Message);
						awakeStopWatch.Stop();
					}
				}
			}

			cumulus.LogDataMessage("CheckLoggerInterval: Received - " + BitConverter.ToString(readBuffer.Take(bytesRead).ToArray()));

			cumulus.LogDebugMessage($"CheckLoggerInterval: Station logger interval is {readBuffer[0]} minutes");

			if (bytesRead > 0 && readBuffer[0] != Cumulus.logints[cumulus.DataLogInterval])
			{
				// change the logger interval to the value we just discovered
				loggerInterval = readBuffer[0];
				var msg = $"** WARNING: Your station logger interval {loggerInterval} mins does not match your Cumulus MX logging interval {Cumulus.logints[cumulus.DataLogInterval]} mins";
				Cumulus.LogConsoleMessage(msg);
				cumulus.LogWarningMessage("CheckLoggerInterval: " + msg);

				if (cumulus.DavisOptions.SetLoggerInterval)
				{
					SetLoggerInterval(Cumulus.logints[cumulus.DataLogInterval]);
				}
			}
		}

		private void SetLoggerInterval(int interval)
		{
			cumulus.LogMessage($"SetLoggerInterval: Setting logger interval to {interval} minutes");

			// response should be just an ACK
			if (isSerial)
			{
				string commandString = $"SETPER {interval}";
				if (WakeVP(comport))
				{
					try
					{
						comport.WriteLine(commandString);

						if (!WaitForACK(comport))
						{
							cumulus.LogWarningMessage("SetLoggerInterval: No ACK in response to setting logger interval");
							return;
						}

						// logger updated OK, so change our internal tracking value too
						loggerInterval = interval;
						cumulus.LogMessage("SetLoggerInterval: Logger interval changed OK");
					}
					catch (TimeoutException)
					{
						cumulus.LogWarningMessage("SetLoggerInterval: Timed out waiting for a response");
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("SetLoggerInterval: Error - " + ex.Message);
						awakeStopWatch.Stop();
					}
				}
			}
			else
			{
				string commandString = $"SETPER {interval}\n";
				if (WakeVP(socket))
				{
					try
					{
						NetworkStream stream = socket.GetStream();
						stream.ReadTimeout = TcpWaitTimeMs;
						stream.WriteTimeout = TcpWaitTimeMs;

						stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

						if (!WaitForACK(stream))
						{
							cumulus.LogWarningMessage("SetLoggerInterval: No ACK in response to setting logger interval");
							return;
						}

						// logger updated OK, so change our internal tracking value too
						loggerInterval = interval;
						cumulus.LogMessage("SetLoggerInterval: Logger interval changed OK");
					}
					catch (System.IO.IOException ex)
					{
						if (ex.Message.Contains("did not properly respond after a period"))
						{
							cumulus.LogWarningMessage("SetLoggerInterval: Timed out waiting for a response");
						}
						else
						{
							cumulus.LogErrorMessage("SetLoggerInterval: Error - " + ex.Message);
							awakeStopWatch.Stop();
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("SetLoggerInterval: Error - " + ex.Message);
						awakeStopWatch.Stop();
					}
				}
			}
		}


		private string GetReceptionStats()
		{
			// e.g. <LF><CR>OK<LF><CR> 21629 15 0 3204 128<LF><CR>
			//       0   1  23 4   5  6
			cumulus.LogMessage("Reading reception stats");
			lastRecepStatsTime = DateTime.Now;
			StringBuilder response = new();
			var bytesRead = 0;
			byte[] readBuffer = new byte[40];
			int ch;

			if (isSerial)
			{
				const string commandString = "RXCHECK";
				if (WakeVP(comport))
				{
					try
					{
						comport.WriteLine(commandString);

						if (WaitForOK(comport))
						{
							// Read the response -  21629 15 0 3204 128<LF><CR>
							do
							{
								// Read the current character
								ch = comport.ReadChar();
								response.Append(Convert.ToChar(ch));
								readBuffer[bytesRead] = (byte) ch;
								bytesRead++;
							} while (ch != CR);
						}
					}
					catch (TimeoutException)
					{
						cumulus.LogWarningMessage("GetReceptionStats: Timed out waiting for a response");
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("GetReceptionStats: Error - " + ex.Message);
						cumulus.LogDebugMessage("GetReceptionStats: Attempting to reconnect to logger");
						InitSerial();
						cumulus.LogDebugMessage("GetReceptionStats: Reconnected to logger");
					}
				}
			}
			else
			{
				const string commandString = "RXCHECK\n";
				if (WakeVP(socket))
				{
					try
					{
						NetworkStream stream = socket.GetStream();
						stream.ReadTimeout = TcpWaitTimeMs;
						stream.WriteTimeout = TcpWaitTimeMs;

						stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

						if (WaitForOK(stream))
						{
							// Read the response -  21629 15 0 3204 128<LF><CR>
							do
							{
								// Read the current character
								ch = stream.ReadByte();
								response.Append(Convert.ToChar(ch));
								readBuffer[bytesRead] = (byte) ch;
								bytesRead++;
							} while (ch != CR);
						}
					}
					catch (System.IO.IOException ex)
					{
						if (ex.Message.Contains("did not properly respond after a period"))
						{
							cumulus.LogWarningMessage("GetReceptionStats: Timed out waiting for a response");
						}
						else
						{
							cumulus.LogErrorMessage("GetReceptionStats: Error - " + ex.Message);
							cumulus.LogDebugMessage("GetReceptionStats: Attempting to reconnect to logger");
							InitTCP();
							cumulus.LogDebugMessage("GetReceptionStats: Reconnected to logger");
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("GetReceptionStats: Error - " + ex.Message);
						cumulus.LogDebugMessage("GetReceptionStats: Attempting to reconnect to logger");
						InitTCP();
						cumulus.LogDebugMessage("GetReceptionStats: Reconnected to logger");
					}
				}
			}

			cumulus.LogDataMessage("GetReceptionStats: Received - " + BitConverter.ToString(readBuffer.Take(bytesRead).ToArray()));

			var resp = response.Length > 10 ? response.ToString()[..^2] : "0 0 0 0 0";

			cumulus.LogDebugMessage($"GetReceptionStats: {resp}");

			return resp;
		}

		// Open a TCP socket.
		private TcpClient OpenTcpPort()
		{
			TcpClient client = null;
			int attempt = 0;

			// Creating the new TCP socket effectively opens it - specify IP address or domain name and port
			while (attempt < 5 && !stop)
			{
				if (cumulus.cancellationToken.IsCancellationRequested)
				{
					stop = true;
					return null;
				}

				attempt++;
				cumulus.LogDebugMessage("OpenTcpPort: TCP Logger Connect attempt " + attempt);
				try
				{
					//client = new TcpClient(ipaddr, port)
					// Force IPv4 only
					client = new TcpClient()
					{
						Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
					};
					client.Connect(cumulus.DavisOptions.IPAddr, port);


					if (!client.Connected)
					{
						client.Close();
					}
					else
					{
						// we're connected, bail out
						break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("OpenTcpPort: Error - " + ex.Message);
				}

				cumulus.cancellationToken.WaitHandle.WaitOne(1000);

				if (cumulus.cancellationToken.IsCancellationRequested)
				{
					stop = true;
					return null;
				}
			}

			// Set the timeout of the underlying stream
			if (client != null && client.Connected)
			{
				client.GetStream().ReadTimeout = TcpWaitTimeMs;
				client.GetStream().WriteTimeout = TcpWaitTimeMs;
				client.ReceiveTimeout = TcpWaitTimeMs;
				client.SendTimeout = TcpWaitTimeMs;
				cumulus.LogDebugMessage("OpenTcpPort: TCP Logger reconnected");
			}
			else
			{
				cumulus.LogDebugMessage("OpenTcpPort: TCP Logger connect failed");
			}

			return client;
		}

		public override void startReadingHistoryData()
		{
			cumulus.LogMessage("Reading history data from log files");

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			cumulus.LogMessage("Reading archive data from logger");
			bw = new BackgroundWorker();
			bw.DoWork += bw_DoWork;
			bw.RunWorkerCompleted += bw_RunWorkerCompleted;
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();
		}

		public override void Stop()
		{
			cumulus.LogMessage("Closing connection");
			try
			{
				stop = true;
				StopMinuteTimer();

				if (isSerial)
				{
					// stop any loop data
					comport.WriteLine("");
				}
				else
				{
					// stop any loop data
					socket.GetStream().WriteByte(10);
				}
			}
			catch
			{
				// do nothing
			}
		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			cumulus.LogMessage("Logger archive reading thread completed");
			if (e.Error != null)
			{
				cumulus.LogErrorMessage("Archive reading thread apparently terminated with an error: " + e.Error.Message);
			}

			cumulus.NormalRunning = true;
			StartLoop();
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();
		}

		private void bw_DoStart(object sender, DoWorkEventArgs e)
		{
			Cumulus.SyncInit.Wait();

			// Wait a short while for Cumulus initialisation to complete
			Thread.Sleep(500);
			StartLoop();

			Cumulus.SyncInit.Release();
		}

		private void bw_DoWork(object sender, DoWorkEventArgs e)
		{
			int archiveRun = 0;
			Cumulus.SyncInit.Wait();
			try
			{
				do
				{
					if (stop)
					{
						return;
					}

					GetArchiveData();

					// The VP2 seems to need a nudge after a DMPAFT command
					if (isSerial)
					{
						WakeVP(comport, true);
					}
					else
					{
						WakeVP(socket, true);
					}


					archiveRun++;
				} while (archiveRun < maxArchiveRuns);
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Exception occurred reading archive data");
			}
			Cumulus.SyncInit.Release();
		}

		private static int calculateCRC(byte[] data)
		{
			ushort crc = 0;
			ushort[] crcTable =
			[
				0x0000,
				0x1021,
				0x2042,
				0x3063,
				0x4084,
				0x50a5,
				0x60c6,
				0x70e7, // 0x00
				0x8108,
				0x9129,
				0xa14a,
				0xb16b,
				0xc18c,
				0xd1ad,
				0xe1ce,
				0xf1ef, // 0x08
				0x1231,
				0x0210,
				0x3273,
				0x2252,
				0x52b5,
				0x4294,
				0x72f7,
				0x62d6, // 0x10
				0x9339,
				0x8318,
				0xb37b,
				0xa35a,
				0xd3bd,
				0xc39c,
				0xf3ff,
				0xe3de, // 0x18
				0x2462,
				0x3443,
				0x0420,
				0x1401,
				0x64e6,
				0x74c7,
				0x44a4,
				0x5485, // 0x20
				0xa56a,
				0xb54b,
				0x8528,
				0x9509,
				0xe5ee,
				0xf5cf,
				0xc5ac,
				0xd58d, // 0x28
				0x3653,
				0x2672,
				0x1611,
				0x0630,
				0x76d7,
				0x66f6,
				0x5695,
				0x46b4, // 0x30
				0xb75b,
				0xa77a,
				0x9719,
				0x8738,
				0xf7df,
				0xe7fe,
				0xd79d,
				0xc7bc, // 0x38
				0x48c4,
				0x58e5,
				0x6886,
				0x78a7,
				0x0840,
				0x1861,
				0x2802,
				0x3823, // 0x40
				0xc9cc,
				0xd9ed,
				0xe98e,
				0xf9af,
				0x8948,
				0x9969,
				0xa90a,
				0xb92b, // 0x48
				0x5af5,
				0x4ad4,
				0x7ab7,
				0x6a96,
				0x1a71,
				0x0a50,
				0x3a33,
				0x2a12, // 0x50
				0xdbfd,
				0xcbdc,
				0xfbbf,
				0xeb9e,
				0x9b79,
				0x8b58,
				0xbb3b,
				0xab1a, // 0x58
				0x6ca6,
				0x7c87,
				0x4ce4,
				0x5cc5,
				0x2c22,
				0x3c03,
				0x0c60,
				0x1c41, // 0x60
				0xedae,
				0xfd8f,
				0xcdec,
				0xddcd,
				0xad2a,
				0xbd0b,
				0x8d68,
				0x9d49, // 0x68
				0x7e97,
				0x6eb6,
				0x5ed5,
				0x4ef4,
				0x3e13,
				0x2e32,
				0x1e51,
				0x0e70, // 0x70
				0xff9f,
				0xefbe,
				0xdfdd,
				0xcffc,
				0xbf1b,
				0xaf3a,
				0x9f59,
				0x8f78, // 0x78
				0x9188,
				0x81a9,
				0xb1ca,
				0xa1eb,
				0xd10c,
				0xc12d,
				0xf14e,
				0xe16f, // 0x80
				0x1080,
				0x00a1,
				0x30c2,
				0x20e3,
				0x5004,
				0x4025,
				0x7046,
				0x6067, // 0x88
				0x83b9,
				0x9398,
				0xa3fb,
				0xb3da,
				0xc33d,
				0xd31c,
				0xe37f,
				0xf35e, // 0x90
				0x02b1,
				0x1290,
				0x22f3,
				0x32d2,
				0x4235,
				0x5214,
				0x6277,
				0x7256, // 0x98
				0xb5ea,
				0xa5cb,
				0x95a8,
				0x8589,
				0xf56e,
				0xe54f,
				0xd52c,
				0xc50d, // 0xA0
				0x34e2,
				0x24c3,
				0x14a0,
				0x0481,
				0x7466,
				0x6447,
				0x5424,
				0x4405, // 0xA8
				0xa7db,
				0xb7fa,
				0x8799,
				0x97b8,
				0xe75f,
				0xf77e,
				0xc71d,
				0xd73c, // 0xB0
				0x26d3,
				0x36f2,
				0x0691,
				0x16b0,
				0x6657,
				0x7676,
				0x4615,
				0x5634, // 0xB8
				0xd94c,
				0xc96d,
				0xf90e,
				0xe92f,
				0x99c8,
				0x89e9,
				0xb98a,
				0xa9ab, // 0xC0
				0x5844,
				0x4865,
				0x7806,
				0x6827,
				0x18c0,
				0x08e1,
				0x3882,
				0x28a3, // 0xC8
				0xcb7d,
				0xdb5c,
				0xeb3f,
				0xfb1e,
				0x8bf9,
				0x9bd8,
				0xabbb,
				0xbb9a, // 0xD0
				0x4a75,
				0x5a54,
				0x6a37,
				0x7a16,
				0x0af1,
				0x1ad0,
				0x2ab3,
				0x3a92, // 0xD8
				0xfd2e,
				0xed0f,
				0xdd6c,
				0xcd4d,
				0xbdaa,
				0xad8b,
				0x9de8,
				0x8dc9, // 0xE0
				0x7c26,
				0x6c07,
				0x5c64,
				0x4c45,
				0x3ca2,
				0x2c83,
				0x1ce0,
				0x0cc1, // 0xE8
				0xef1f,
				0xff3e,
				0xcf5d,
				0xdf7c,
				0xaf9b,
				0xbfba,
				0x8fd9,
				0x9ff8, // 0xF0
				0x6e17,
				0x7e36,
				0x4e55,
				0x5e74,
				0x2e93,
				0x3eb2,
				0x0ed1,
				0x1ef0, // 0xF8
			];

			foreach (var databyte in data)
			{
				crc = (ushort) (crcTable[(crc >> 8) ^ databyte] ^ (crc << 8));
			}

			return crc;
		}

		private static bool CrcOk(byte[] data)
		{
			return (calculateCRC(data) == 0);
		}

		public override void Start()
		{
			cumulus.LogMessage("Start normal reading loop");
			int loopcount = cumulus.DavisOptions.ForceVPBarUpdate ? 20 : 50;
			const int loop2count = 1;
			bool reconnecting = false;

			while (!stop)
			{
				try
				{
					if (clockSetNeeded && !stop)
					{
						// set the console clock
						var consoleclock = GetTime();
						var nowTime = DateTime.Now;

						if (consoleclock > DateTime.MinValue)
						{
							cumulus.LogMessage("Console clock: " + consoleclock);
						}
						else
						{
							cumulus.LogWarningMessage("Console clock: Failed to read console time");
						}

						if (Math.Abs(nowTime.Subtract(consoleclock).TotalSeconds) >= 30)
						{
							SetTime();

							cumulus.LogMessage("Console clock: Pausing to allow console to process the new date/time");
							Thread.Sleep(1000 * 5);

							consoleclock = GetTime();

							if (consoleclock > DateTime.MinValue)
							{
								cumulus.LogMessage("Console clock: " + consoleclock);
							}
							else
							{
								cumulus.LogWarningMessage("Console clock: Failed to read console time");
							}
						}
						else
						{
							cumulus.LogMessage($"Console clock: Accurate to +/- 30 seconds, no need to set it (diff={(int) nowTime.Subtract(consoleclock).TotalSeconds}s)");
						}

						clockSetNeeded = false;
					}

					if (isSerial)
					{
						if (comport != null && comport.IsOpen)
						{
							if (cumulus.DavisOptions.UseLoop2 && SendLoopCommand(comport, "LPS 2 " + loop2count))
							{
								GetAndProcessLoop2Data(loop2count);
							}

							if (SendLoopCommand(comport, "LOOP " + loopcount))
							{
								GetAndProcessLoopData(loopcount);
							}
						}
						else
						{
							// Oh dear our comm port has gone away - USB issues?
							// try opening it again
							try
							{
								cumulus.LogMessage("Attempting to re-open the comm port");
								InitSerial();
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"Failed to open the comm port ({cumulus.ComportName}). Error - {ex.Message}");
							}
							if (comport == null || !comport.IsOpen)
							{
								cumulus.LogMessage("Failed to connect to the station, waiting 30 seconds before trying again");
								Thread.Sleep(30000);
								continue;
							}
						}
					}
					else
					{
						if (socket == null || !socket.Connected)
						{
							reconnecting = true;
							InitTCP();
							reconnecting = false;
						}

						if (socket != null && socket.Connected && cumulus.DavisOptions.UseLoop2 && SendLoopCommand(socket, "LPS 2 " + loop2count + Newline))
						{
							GetAndProcessLoop2Data(loop2count);
						}

						if (socket != null && socket.Connected)
						{
							if (SendLoopCommand(socket, "LOOP " + loopcount + Newline))
							{
								GetAndProcessLoopData(loopcount);
							}
						}
						else
						{
							if (reconnecting)
							{
								cumulus.LogMessage("Failed to connect to the station, waiting 30 seconds before trying again");
								cumulus.cancellationToken.WaitHandle.WaitOne(30000);

								if (cumulus.cancellationToken.IsCancellationRequested)
								{
									stop = true;
									return;
								}
							}
							continue;
						}
					}

					if (cumulus.DavisOptions.ForceVPBarUpdate && !stop)
					{
						SendBarRead();
					}

					if (!cumulus.DavisOptions.ReadReceptionStats || lastRecepStatsTime.AddMinutes(15) >= DateTime.Now || stop)
						continue;

					var recepStats = GetReceptionStats();
					DecodeReceptionStats(recepStats);
				}
				catch (ThreadAbortException) // Catch the ThreadAbortException
				{
					cumulus.LogMessage("Davis Start: ThreadAbortException");
					// and exit
					stop = true;
				}
				catch (Exception ex)
				{
					// any others, log them and carry on
					cumulus.LogErrorMessage("Davis Start: Exception - " + ex.Message);
				}
			}

			cumulus.LogMessage("Ending normal reading loop");

			if (isSerial)
			{
				if (comport != null && comport.IsOpen)
				{
					comport.WriteLine("");
					comport.Close();
				}
			}
			else
			{
				if (socket != null && socket.Connected)
				{
					socket.GetStream().WriteByte(10);
					socket.Close();
				}
			}
		}

		private void SendBarRead()
		{
			cumulus.LogDebugMessage("Sending BARREAD");

			StringBuilder response = new();
			var bytesRead = 0;
			byte[] readBuffer = new byte[64];

			// Expected response = "\n\rOK\n\rNNNNN\n\r" - Where NNNNN = ASCII pressure, inHg * 1000

			if (isSerial)
			{
				const string commandString = "BARREAD";

				if (WakeVP(comport))
				{
					try
					{
						comport.WriteLine(commandString);

						if (WaitForOK(comport))
						{
							// Read the response
							do
							{
								// Read the current character
								var ch = comport.ReadChar();
								response.Append(Convert.ToChar(ch));
								readBuffer[bytesRead] = (byte) ch;
								bytesRead++;

							} while (bytesRead < 7);
						}
					}
					catch (TimeoutException)
					{
						cumulus.LogDebugMessage("SendBarRead: Timed out waiting for a response");
					}
					catch (Exception ex)
					{
						cumulus.LogDebugMessage("SendBarRead: Error - " + ex.Message);
						cumulus.LogDebugMessage("SendBarRead: Attempting to reconnect to logger");
						InitSerial();
						cumulus.LogDebugMessage("SendBarRead: Reconnected to logger");
					}
				}
			}
			else
			{
				const string commandString = "BARREAD\n";
				if (WakeVP(socket))
				{
					try
					{
						NetworkStream stream = socket.GetStream();

						stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

						if (WaitForOK(stream))
						{
							do
							{
								// Read the current character
								var ch = stream.ReadByte();
								response.Append(Convert.ToChar(ch));
								readBuffer[bytesRead] = (byte) ch;
								bytesRead++;
							} while (stream.DataAvailable);
						}
					}
					catch (System.IO.IOException ex)
					{
						if (ex.Message.Contains("did not properly respond after a period"))
						{
							cumulus.LogDebugMessage("SendBarRead: Timed out waiting for a response");
						}
						else
						{
							cumulus.LogDebugMessage("SendBarRead: Error - " + ex.Message);
							cumulus.LogDebugMessage("SendBarRead: Attempting to reconnect to logger");
							InitTCP();
							cumulus.LogDebugMessage("SendBarRead: Reconnected to logger");
						}
					}
					catch (Exception ex)
					{
						cumulus.LogDebugMessage("SendBarRead: Error - " + ex.Message);
						cumulus.LogDebugMessage("SendBarRead: Attempting to reconnect to logger");
						InitTCP();
						cumulus.LogDebugMessage("SendBarRead: Reconnected to logger");
					}
				}
			}

			cumulus.LogDataMessage("BARREAD Received - " + BitConverter.ToString(readBuffer.Take(bytesRead).ToArray()));
			if (response.Length > 2)
			{
				response.Length--;
				cumulus.LogDebugMessage("BARREAD Received - " + response.ToString());
			}
		}

		private bool SendLoopCommand(SerialPort serialPort, string commandString)
		{
			bool foundAck = false;

			cumulus.LogMessage("SendLoopCommand: Starting - " + commandString);

			try
			{
				if (serialPort.IsOpen && !stop)
				{
					WakeVP(serialPort);

					int passCount = 1;
					const int maxPasses = 4;

					// Clear the input buffer
					serialPort.DiscardInBuffer();
					// Clear the output buffer
					serialPort.DiscardOutBuffer();

					// Try the command until we get a clean ACKnowledge from the VP.  We count the number of passes since
					// a timeout will never occur reading from the sockets buffer.  If we try a few times (maxPasses) and
					// we get nothing back, we assume that the connection is broken
					while (!foundAck && passCount < maxPasses && !stop)
					{
						// send the LOOP n command
						cumulus.LogDebugMessage("SendLoopCommand: Sending command " + commandString + ",  attempt " + passCount);
						serialPort.WriteLine(commandString);

						cumulus.LogDebugMessage("SendLoopCommand: Wait for ACK");
						// Wait for the VP to acknowledge the receipt of the command - sometimes we get a '\n\r'
						// in the buffer first or no response is given.  If all else fails, try again.
						foundAck = WaitForACK(serialPort);
						passCount++;
					}

					// return result to indicate success or otherwise
					if (foundAck)
						return true;

					// Failed to get a response from the loop command after all the retries, try resetting the connection
					cumulus.LogDebugMessage($"SendLoopCommand: Failed to get a response after {passCount - 1} attempts, reconnecting the station");
					InitSerial();
					cumulus.LogDebugMessage("SendLoopCommand: Reconnected to station");
				}
			}
			catch (Exception ex)
			{
				if (stop)
					return false;

				cumulus.LogErrorMessage("SendLoopCommand: Error sending LOOP command [" + commandString.Replace("\n", "") + "]: " + ex.Message);
				cumulus.LogDebugMessage("SendLoopCommand: Attempting to reconnect to station");
				InitSerial();
				cumulus.LogDebugMessage("SendLoopCommand: Reconnected to station");
				return false;
			}
			// if we get here it must have failed
			return false;
		}

		private bool SendLoopCommand(TcpClient tcpPort, string commandString)
		{

			bool foundAck = false;
			int passCount = 1;
			const int maxPasses = 4;

			cumulus.LogMessage("SendLoopCommand: Starting - " + commandString.Replace("\n", ""));

			try
			{
				if (!tcpPort.Connected)
				{
					cumulus.LogDebugMessage("SendLoopCommand: Error, TCP not connected!");
					cumulus.LogDebugMessage("SendLoopCommand: Attempting to reconnect to logger");
					InitTCP();
					cumulus.LogDebugMessage("SendLoopCommand: Reconnected to logger");
					return false;
				}

				NetworkStream stream = tcpPort.GetStream();

				// flush the input stream
				stream.WriteByte(10);

				Thread.Sleep(cumulus.DavisOptions.IPResponseTime);

				while (stream.DataAvailable)
				{
					stream.ReadByte();
				}

				// Try the command until we get a clean ACKnowledge from the VP.  We count the number of passes since
				// a timeout will never occur reading from the sockets buffer.  If we try a few times (maxPasses) and
				// we get nothing back, we assume that the connection is broken
				while (!foundAck && passCount < maxPasses && !stop)
				{
					// send the LOOP n command
					cumulus.LogDebugMessage("SendLoopCommand: Sending command - " + commandString.Replace("\n", "") + ", attempt " + passCount);
					stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

					cumulus.LogDebugMessage("SendLoopCommand: Wait for ACK");
					// Wait for the VP to acknowledge the receipt of the command - sometimes we get a '\n\r'
					// in the buffer first or no response is given.  If all else fails, try again.
					foundAck = WaitForACK(stream);
					passCount++;
				}

				if (foundAck) return true;

				// Failed to get a response from the loop command after all the retries, try resetting the connection
				cumulus.LogDebugMessage($"SendLoopCommand: Failed to get a response after {passCount - 1} attempts, reconnecting the station");
				InitTCP();
				cumulus.LogDebugMessage("SendLoopCommand: Reconnected to station");
			}
			catch (Exception ex)
			{
				if (stop) return false;

				cumulus.LogErrorMessage("SendLoopCommand: Error sending LOOP command [" + commandString.Replace("\n", "") + "]: " + ex.Message);
				cumulus.LogDebugMessage("SendLoopCommand: Attempting to reconnect to station");
				InitTCP();
				cumulus.LogDebugMessage("SendLoopCommand: Reconnected to station");
				return false;
			}

			// if we get here it has failed
			return false;
		}

		private void GetAndProcessLoopData(int number)
		{
			const int loopDataLength = 99;

			CommTimer tmrComm = new CommTimer();

			for (int i = 0; i < number; i++)
			{
				if (stop) return;

				// Allocate a byte array to hold the loop data
				byte[] loopString = new byte[loopDataLength];

				int min = DateTime.Now.Minute;

				if (min != previousMinuteSetClock)
				{
					previousMinuteSetClock = min;
					if (cumulus.StationOptions.SyncTime && DateTime.Now.Hour == cumulus.StationOptions.ClockSettingHour && min == 2)
					{
						// set the console clock
						clockSetNeeded = true;
					}
				}

				if (isSerial)
				{
					try
					{
						if (!comport.IsOpen)
						{
							cumulus.LogMessage("LOOP: Comm port is closed");
							cumulus.LogDebugMessage("LOOP: Attempting to reconnect to station");
							InitSerial();
							cumulus.LogDebugMessage("LOOP: Reconnected to station");
							return;
						}

						// wait for the buffer to fill
						tmrComm.Start(3000);
						while (comport.BytesToRead < loopDataLength && !tmrComm.timedout)
						{
							Thread.Sleep(10);
						}
						tmrComm.Stop();
						if (comport.BytesToRead < loopDataLength)
						{
							cumulus.LogWarningMessage($"LOOP: {i + 1} - Expected data not received, expected 99 bytes, got {comport.BytesToRead}");
						}

						comport.Read(loopString, 0, loopDataLength);
					}
					catch (TimeoutException)
					{
						cumulus.LogWarningMessage($"LOOP: {i + 1} - Timed out waiting for LOOP data");
						return;
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("LOOP: Exception - " + ex);
						cumulus.LogDebugMessage("LOOP: Attempting to reconnect to station");
						InitSerial();
						cumulus.LogDebugMessage("LOOP: Reconnected to station");
						return;
					}
				}
				else
				{
					// See if we need to disconnect to allow WeatherLink IP to upload
					if (cumulus.DavisOptions.PeriodicDisconnectInterval > 0)
					{
						min = DateTime.Now.Minute;

						if (min != previousMinuteDisconnect)
						{
							try
							{
								previousMinuteDisconnect = min;

								cumulus.LogDebugMessage("LOOP: Periodic disconnect from logger");
								// time to disconnect - first stop the loop data by sending a newline
								socket.GetStream().WriteByte(10);
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage("LOOP: Periodic disconnect error: " + ex.Message);
							}
							finally
							{
								socket.Client.Close(0);
							}

							// Wait
							Thread.Sleep(cumulus.DavisOptions.PeriodicDisconnectInterval * 1000);

							cumulus.LogDebugMessage("LOOP: Attempting reconnect to logger");
							InitTCP();
							cumulus.LogDebugMessage("LOOP: Reconnected to logger");
							return;
						}
					}

					try
					{
						// wait for the buffer to fill
						tmrComm.Start(3000);
						while (socket.Available < loopDataLength && !tmrComm.timedout)
						{
							Thread.Sleep(10);
						}
						tmrComm.Stop();

						// Read the first 99 bytes of the buffer into the array
						var read = socket.GetStream().Read(loopString, 0, loopDataLength);

						if (read < loopDataLength)
						{
							cumulus.LogWarningMessage($"LOOP: {i + 1} - Expected data not received, expected {loopDataLength} bytes, got {read}");
						}
					}
					catch (System.IO.IOException ex)
					{
						if (ex.Message.Contains("did not properly respond after a period"))
						{
							cumulus.LogWarningMessage("LOOP: Timed out waiting for LOOP data");
						}
						else
						{
							cumulus.LogErrorMessage("LOOP: Receive error - " + ex);
							cumulus.LogMessage("LOOP: Reconnecting to station");
							InitTCP();
							cumulus.LogMessage("LOOP: Reconnected to station");
						}
						return;
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("LOOP: Receive error - " + ex);
						cumulus.LogMessage("LOOP: Reconnecting to station");
						InitTCP();
						cumulus.LogMessage("LOOP: Reconnected to station");
						return;
					}
				}

				cumulus.LogDataMessage($"LOOP: Data - {i + 1}: {BitConverter.ToString(loopString)}");

				// Check it is a LOOP packet, starts with "LOO" and 5th byte == 0: LOOP1
				if (!(loopString[0] == 'L' && loopString[1] == 'O' && loopString[2] == 'O' && Convert.ToByte(loopString[4]) == 0))
				{
					cumulus.LogDebugMessage($"LOOP: {i + 1} - Invalid packet format");
					// Stop the sending of LOOP packets so we can resynch
					if (isSerial)
					{
						try
						{
							comport.WriteLine("");
							Thread.Sleep(3000);
							// read off all data in the pipeline

							if (comport.BytesToRead > 0)
							{
								cumulus.LogDebugMessage("LOOP: Discarding bytes from pipeline: " + comport.BytesToRead);
								comport.DiscardInBuffer();
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage("LOOP: Error discarding bytes - " + ex.Message);
						}
					}
					else
					{
						try
						{
							socket.GetStream().WriteByte(10);
							Thread.Sleep(3000);
							// read off all data in the pipeline
							if (socket.Available > 0)
							{
								cumulus.LogDebugMessage("LOOP: Discarding bytes from pipeline: " + socket.Available);
								do
								{
									socket.GetStream().ReadByte();
								} while (socket.Available > 0);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage("LOOP: Error discarding bytes - " + ex.Message);
						}
					}

					return;
				}

				if (!CrcOk(loopString))
				{
					cumulus.LogErrorMessage($"LOOP: {i + 1} - Packet CRC invalid");
					continue;
				}

				cumulus.LogDebugMessage($"LOOP: {i + 1} - Data packet is good");

				if (stop) return;

				// Allocate a structure for the data
				var loopData = new VPLoopData();

				// ...and load the data into it
				loopData.Load(loopString);

				// Process it
				DateTime now = DateTime.Now;

				if ((loopData.InsideHumidity >= 0) && (loopData.InsideHumidity <= 100))
				{
					DoIndoorHumidity(loopData.InsideHumidity);
				}

				if ((loopData.OutsideHumidity >= 0) && (loopData.OutsideHumidity <= 100))
				{
					DoOutdoorHumidity(loopData.OutsideHumidity, now);
				}
				else
				{
					cumulus.LogDebugMessage($"LOOP: Ignoring outdoor humidity data. RH={loopData.OutsideHumidity} %.");
				}

				if ((loopData.InsideTemperature > -200) && (loopData.InsideTemperature < 300))
				{
					DoIndoorTemp(ConvertUnits.TempFToUser(loopData.InsideTemperature));
				}

				if ((loopData.OutsideTemperature > -200) && (loopData.OutsideTemperature < 300))
				{
					DoOutdoorTemp(ConvertUnits.TempFToUser(loopData.OutsideTemperature), now);
				}
				else
				{
					cumulus.LogDebugMessage($"LOOP: Ignoring outdoor temp data. Temp={loopData.OutsideTemperature} F.");
				}

				if ((loopData.Pressure >= 20) && (loopData.Pressure < 32.5))
				{
					DoPressure(ConvertUnits.PressINHGToUser(loopData.Pressure), now);
				}
				else
				{
					cumulus.LogDebugMessage($"LOOP: Ignoring pressure data. Pressure={loopData.Pressure} inHg.");
				}

				if ((cumulus.StationType == StationTypes.VantagePro2 && !cumulus.DavisOptions.UseLoop2) || cumulus.StationType == StationTypes.VantagePro)
				{
					// Loop2 data not available, just use sea level (for now, anyway)
					AltimeterPressure = Pressure;
				}

				double wind = ConvertUnits.WindMPHToUser(loopData.CurrentWindSpeed);
				double avgwind = ConvertUnits.WindMPHToUser(loopData.AvgWindSpeed);

				// Check for sensible figures (spec says max for large cups is 175 mph, but up to 200 mph)
				// Average = 255 means the console hasn't calculated it yet
				if (loopData.CurrentWindSpeed < 200 && (loopData.AvgWindSpeed < 200 || loopData.AvgWindSpeed == 255))
				{
					int winddir = loopData.WindDirection;

					if (winddir == 0x7FFF) // no reading
					{
						cumulus.LogDebugMessage("LOOP: Wind direction = 0x7FFF = no reading, using zero instead");
						winddir = 0;
					}
					else if (winddir > 360)
					{
						cumulus.LogDebugMessage($"LOOP: Wind direction = {winddir}, using zero instead");
						winddir = 0;
					}

					if (loopData.AvgWindSpeed == 255)
					{
						// The console hasn't calculated an average yet, set to zero
						avgwind = -1;
					}

					DoWind(wind, winddir, avgwind, now);
				}
				else
				{
					cumulus.LogDebugMessage($"LOOP: Ignoring wind data. Speed={loopData.CurrentWindSpeed} mph, Avg={loopData.AvgWindSpeed} mph.");
				}

				double rain = ConvertRainClicksToUser(loopData.YearRain);
				double rainrate = ConvertRainClicksToUser(loopData.RainRate);

				if (rainrate < 0)
				{
					rainrate = 0;
				}

				DoRain(rain, rainrate, now);

				StormRain = ConvertRainClicksToUser(loopData.StormRain);
				StartOfStorm = loopData.StormRainStart;

				if (loopData.UVIndex >= 0 && loopData.UVIndex < 17)
				{
					DoUV(loopData.UVIndex, now);
				}

				if (loopData.SolarRad >= 0 && loopData.SolarRad < 1801)
				{
					DoSolarRad(loopData.SolarRad, now);
				}

				if ((loopData.AnnualET >= 0) && (loopData.AnnualET < 32000))
				{
					DoET(ConvertUnits.RainINToUser(loopData.AnnualET), now);
				}

				DoWindChill(OutdoorTemperature, now);
				DoApparentTemp(now);
				DoFeelsLike(now);
				DoHumidex(now);
				DoCloudBaseHeatIndex(now);

				var forecastRule = loopData.ForecastRule < ForecastLookup.Length ? loopData.ForecastRule : ForecastLookup.Length - 1;

				var key1 = ForecastLookup[forecastRule, 0];
				var key2 = ForecastLookup[forecastRule, 1];
				var key3 = ForecastLookup[forecastRule, 2];

				// Adjust for S hemisphere
				if (cumulus.Latitude < 0)
				{
					if (key3 == 3)
					{
						key3 = 1;
					}
					else if (key3 == 4)
					{
						key3 = 2;
					}
				}

				var forecast = (cumulus.Trans.DavisForecast1[key1] + cumulus.Trans.DavisForecast2[key2] + cumulus.Trans.DavisForecast3[key3]).Trim();

				DoForecast(forecast, false);

				LowBatteryDevices.Clear();

				ConBatText = loopData.ConBatVoltage.ToString("F2");

				if (loopData.ConBatVoltage <= 3.5)
				{
					LowBatteryDevices.Add("Console-" + ConBatText);
				}

				TxBatText = ProcessTxBatt(loopData.TXbattStatus);

				cumulus.BatteryLowAlarm.Triggered = TxBatText.Contains("LOW") || loopData.ConBatVoltage <= 3.5;


				if (cumulus.StationOptions.LogExtraSensors)
				{
					if (loopData.ExtraTemp1 < 255)
					{
						DoExtraTemp(ConvertUnits.TempFToUser(loopData.ExtraTemp1 - 90), 1);
					}

					if (loopData.ExtraTemp2 < 255)
					{
						DoExtraTemp(ConvertUnits.TempFToUser(loopData.ExtraTemp2 - 90), 2);
					}

					if (loopData.ExtraTemp3 < 255)
					{
						DoExtraTemp(ConvertUnits.TempFToUser(loopData.ExtraTemp3 - 90), 3);
					}

					if (loopData.ExtraTemp4 < 255)
					{
						DoExtraTemp(ConvertUnits.TempFToUser(loopData.ExtraTemp4 - 90), 4);
					}

					if (loopData.ExtraTemp5 < 255)
					{
						DoExtraTemp(ConvertUnits.TempFToUser(loopData.ExtraTemp5 - 90), 5);
					}

					if (loopData.ExtraTemp6 < 255)
					{
						DoExtraTemp(ConvertUnits.TempFToUser(loopData.ExtraTemp6 - 90), 6);
					}

					if (loopData.ExtraTemp7 < 255)
					{
						DoExtraTemp(ConvertUnits.TempFToUser(loopData.ExtraTemp7 - 90), 7);
					}

					if (loopData.ExtraHum1 >= 0 && loopData.ExtraHum1 <= 100)
					{
						DoExtraHum(loopData.ExtraHum1, 1);
						if (loopData.ExtraTemp1 < 255)
						{
							ExtraDewPoint[1] = ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[1].Value), ExtraHum[1].Value));
						}
					}

					if (loopData.ExtraHum2 >= 0 && loopData.ExtraHum2 <= 100)
					{
						DoExtraHum(loopData.ExtraHum2, 2);
						if (loopData.ExtraTemp2 < 255)
						{
							ExtraDewPoint[2] = ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[2].Value), ExtraHum[2].Value));
						}
					}

					if (loopData.ExtraHum3 >= 0 && loopData.ExtraHum3 <= 100)
					{
						DoExtraHum(loopData.ExtraHum3, 3);
						if (loopData.ExtraTemp3 < 255)
						{
							ExtraDewPoint[3] = ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[3].Value), ExtraHum[3].Value));
						}
					}

					if (loopData.ExtraHum4 >= 0 && loopData.ExtraHum4 <= 100)
					{
						DoExtraHum(loopData.ExtraHum4, 4);
						if (loopData.ExtraTemp4 < 255)
						{
							ExtraDewPoint[4] = ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[4].Value), ExtraHum[4].Value));
						}
					}

					if (loopData.ExtraHum5 >= 0 && loopData.ExtraHum5 <= 100)
					{
						DoExtraHum(loopData.ExtraHum5, 5);
					}

					if (loopData.ExtraHum6 >= 0 && loopData.ExtraHum6 <= 100)
					{
						DoExtraHum(loopData.ExtraHum6, 6);
					}

					if (loopData.ExtraHum7 >= 0 && loopData.ExtraHum7 <= 100)
					{
						DoExtraHum(loopData.ExtraHum7, 7);
					}

					if (loopData.SoilMoisture1 >= 0 && loopData.SoilMoisture1 <= 250)
					{
						DoSoilMoisture(loopData.SoilMoisture1, 1);
					}

					if (loopData.SoilMoisture2 >= 0 && loopData.SoilMoisture2 <= 250)
					{
						DoSoilMoisture(loopData.SoilMoisture2, 2);
					}

					if (loopData.SoilMoisture3 >= 0 && loopData.SoilMoisture3 <= 250)
					{
						DoSoilMoisture(loopData.SoilMoisture3, 3);
					}

					if (loopData.SoilMoisture4 >= 0 && loopData.SoilMoisture4 <= 250)
					{
						DoSoilMoisture(loopData.SoilMoisture4, 4);
					}

					if (loopData.SoilTemp1 < 255 && loopData.SoilTemp1 > 0)
					{
						DoSoilTemp(ConvertUnits.TempFToUser(loopData.SoilTemp1 - 90), 1);
					}

					if (loopData.SoilTemp2 < 255 && loopData.SoilTemp2 > 0)
					{
						DoSoilTemp(ConvertUnits.TempFToUser(loopData.SoilTemp2 - 90), 2);
					}

					if (loopData.SoilTemp3 < 255 && loopData.SoilTemp3 > 0)
					{
						DoSoilTemp(ConvertUnits.TempFToUser(loopData.SoilTemp3 - 90), 3);
					}

					if (loopData.SoilTemp4 < 255 && loopData.SoilTemp4 > 0)
					{
						DoSoilTemp(ConvertUnits.TempFToUser(loopData.SoilTemp4 - 90), 4);
					}

					if (loopData.LeafWetness1 >= 0 && loopData.LeafWetness1 < 16)
					{
						DoLeafWetness(loopData.LeafWetness1, 1);
					}

					if (loopData.LeafWetness2 >= 0 && loopData.LeafWetness2 < 16)
					{
						DoLeafWetness(loopData.LeafWetness2, 2);
					}

					if (loopData.LeafWetness3 >= 0 && loopData.LeafWetness3 < 16)
					{
						DoLeafWetness(loopData.LeafWetness3, 3);
					}

					if (loopData.LeafWetness4 >= 0 && loopData.LeafWetness4 < 16)
					{
						DoLeafWetness(loopData.LeafWetness4, 4);
					}
				}
				UpdateStatusPanel(DateTime.Now);
				UpdateMQTT();
			}
		}

		private string ProcessTxBatt(byte txStatus)
		{
			StringBuilder response = new();

			for (int i = 0; i < 8; i++)
			{
				var status = (txStatus & (1 << i)) == 0 ? "-ok " : "-LOW ";
				if (status == "-LOW")
				{
					LowBatteryDevices.Add((i + 1) + status);
				}
				response.Append((i + 1) + status);
			}

			response.Length--;
			return response.ToString();
		}

		private void GetAndProcessLoop2Data(int number)
		{
			CommTimer tmrComm = new CommTimer();
			const int loopDataLength = 99;

			cumulus.LogDebugMessage("LOOP2: Waiting for LOOP2 data");

			for (int i = 0; i < number; i++)
			{
				// Allocate a byte array to hold the loop data
				byte[] loopString = new byte[loopDataLength];

				if (isSerial)
				{

					try
					{
						// wait for the buffer to fill
						tmrComm.Start(3000);
						while (comport.BytesToRead < loopDataLength && !tmrComm.timedout)
						{
							Thread.Sleep(10);
						}
						tmrComm.Stop();

						if (comport.BytesToRead < loopDataLength)
						{
							cumulus.LogWarningMessage($"LOOP2: Expected data not received, expected 99 bytes, got {comport.BytesToRead}");
						}

						// Read the data from the buffer into the array
						comport.Read(loopString, 0, loopDataLength);
					}
					catch (TimeoutException)
					{
						cumulus.LogWarningMessage("LOOP2: Timed out waiting for LOOP2 data");
						continue;
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("LOOP2: Error - " + ex);
						cumulus.LogDebugMessage("LOOP2: Attempting to reconnect to logger");
						InitSerial();
						cumulus.LogDebugMessage("LOOP2: Reconnected to logger");
					}
				}
				else
				{
					try
					{
						// wait for the buffer to fill
						tmrComm.Start(3000);
						while (socket.Available < loopDataLength && !tmrComm.timedout)
						{
							Thread.Sleep(10);
						}
						tmrComm.Stop();

						if (socket.Available < loopDataLength)
						{
							cumulus.LogWarningMessage($"LOOP2: Expected data not received, expected 99 bytes got {socket.Available}");
						}
						// Read the first 99 bytes of the buffer into the array
						socket.GetStream().Read(loopString, 0, loopDataLength);
					}
					catch (System.IO.IOException ex)
					{
						if (ex.Message.Contains("did not properly respond after a period"))
						{
							cumulus.LogDebugMessage("LOOP2: Timed out waiting for LOOP2 data");
							continue;
						}

						cumulus.LogDebugMessage("LOOP2: Data: Error - " + ex.Message);
						cumulus.LogDebugMessage("LOOP2: Attempting to reconnect to logger");
						InitTCP();
						cumulus.LogDebugMessage("LOOP2: Reconnected to logger");
						return;
					}
					catch (Exception ex)
					{
						cumulus.LogDebugMessage("LOOP2: Data: Error - " + ex.Message);
						cumulus.LogDebugMessage("LOOP2: Attempting to reconnect to logger");
						InitTCP();
						cumulus.LogDebugMessage("LOOP2: Reconnected to logger");
						return;
					}
				}

				// Check it is a LOOP packet, starts with "LOO" and 5th byte == 1: LOOP2
				if (!(loopString[0] == 'L' && loopString[1] == 'O' && loopString[2] == 'O' && Convert.ToByte(loopString[4]) == 1))
				{
					cumulus.LogDebugMessage("LOOP2: Invalid packet format");
					continue;
				}

				if (!CrcOk(loopString))
				{
					cumulus.LogDebugMessage("LOOP2: Packet CRC invalid");
					continue;
				}

				cumulus.LogDebugMessage("LOOP2: Data packet is good");
				if (stop) return;

				cumulus.LogDataMessage("LOOP2: Data - " + BitConverter.ToString(loopString));

				// Allocate a structure for the data
				var loopData = new VPLoop2Data();

				// ...and load the data into it
				loopData.Load(loopString);

				// Process it
				DateTime now = DateTime.Now;

				// Extract station pressure, and use it to calculate altimeter pressure

				// first sanity check - one user was getting zero values!
				if (loopData.AbsolutePressure < 20)
				{
					cumulus.LogDebugMessage("LOOP2: Ignoring absolute pressure value < 20 inHg");
					// no absolute, so just make altimeter = sl pressure
					AltimeterPressure = Pressure;
					StationPressure = 0;
				}
				else
				{
					// Spike removal is in user units
					var pressUser = ConvertUnits.PressINHGToUser(loopData.AbsolutePressure);
					if ((previousPressStation < 9998) && (Math.Abs(pressUser - previousPressStation) > cumulus.Spike.PressDiff))
					{
						cumulus.LogSpikeRemoval("Station Pressure difference greater than spike value; reading ignored");
						cumulus.LogSpikeRemoval($"NewVal={pressUser.ToString(cumulus.PressFormat)} OldVal={previousPressStation.ToString(cumulus.PressFormat)} SpikePressDiff={cumulus.Spike.PressDiff.ToString(cumulus.PressFormat)}");
						lastSpikeRemoval = DateTime.Now;
						cumulus.SpikeAlarm.LastMessage = $"Station Pressure difference greater than spike value - NewVal={pressUser.ToString(cumulus.PressFormat)} OldVal={previousPressStation.ToString(cumulus.PressFormat)} SpikePressDiff={cumulus.Spike.PressDiff.ToString(cumulus.PressFormat)}";
						cumulus.SpikeAlarm.Triggered = true;
					}
					else if (pressUser > cumulus.Limit.PressHigh)
					{
						cumulus.LogSpikeRemoval("Station Pressure greater than upper limit; reading ignored");
						cumulus.LogSpikeRemoval($"NewVal={pressUser.ToString(cumulus.PressFormat)} HighLimit={cumulus.Limit.PressHigh.ToString(cumulus.PressFormat)}");
						lastSpikeRemoval = DateTime.Now;
						cumulus.SpikeAlarm.LastMessage = $"Station Pressure greater than upper limit - NewVal={pressUser.ToString(cumulus.PressFormat)} HighLimit={cumulus.Limit.PressHigh.ToString(cumulus.PressFormat)}";
						cumulus.SpikeAlarm.Triggered = true;
					}
					else if (pressUser < cumulus.Limit.PressLow)
					{
						cumulus.LogSpikeRemoval("Station Pressure less than lower limit; reading ignored");
						cumulus.LogSpikeRemoval($"NewVal={pressUser.ToString(cumulus.PressFormat)} LowLimit={cumulus.Limit.PressLow.ToString(cumulus.PressFormat)}");
						lastSpikeRemoval = DateTime.Now;
						cumulus.SpikeAlarm.LastMessage = $"Station Pressure less than lower limit - NewVal={pressUser.ToString(cumulus.PressFormat)} LowLimit={cumulus.Limit.PressLow.ToString(cumulus.PressFormat)}";
						cumulus.SpikeAlarm.Triggered = true;
					}
					else
					{
						// all good!
						previousPressStation = pressUser;
						DoStationPressure(ConvertUnits.PressINHGToUser(loopData.AbsolutePressure));
					}
				}

				double wind = ConvertUnits.WindMPHToUser(loopData.CurrentWindSpeed);

				// Use current average as we don't have a new value in LOOP2. Allow for calibration.
				if (loopData.CurrentWindSpeed < 200)
				{
					DoWind(wind, loopData.WindDirection, -1, now);
				}
				else
				{
					cumulus.LogDebugMessage("LOOP2: Ignoring wind speed: " + loopData.CurrentWindSpeed + " mph");
				}

				// Check if the station 10 minute gust value is greater than ours - only if our gust period is 10 minutes or more though
				if (loopData.WindGust10Min < 200 && cumulus.StationOptions.PeakGustMinutes >= 10)
				{
					// Extract 10-min gust and see if it is higher than we have recorded.
					var rawGust10min = ConvertUnits.WindMPHToUser(loopData.WindGust10Min);
					var gust10min = cumulus.Calib.WindGust.Calibrate(rawGust10min);
					var gustdir = (int) cumulus.Calib.WindDir.Calibrate(loopData.WindGustDir);

					cumulus.LogDebugMessage("LOOP2: 10-min gust: " + gust10min.ToString(cumulus.WindFormat));

					if (CheckHighGust(gust10min, gustdir, now))
					{
						cumulus.LogDebugMessage($"LOOP2: Setting max gust from loop2 10-min value: {gust10min.ToString(cumulus.WindFormat)} was: {RecentMaxGust.ToString(cumulus.WindFormat)}");
						RecentMaxGust = gust10min;

						// add to recent values so normal calculation includes this value
						lock (recentwindLock)
						{
							WindRecent[nextwind].Gust = rawGust10min;
							WindRecent[nextwind].Speed = -1;
							WindRecent[nextwind].Timestamp = now;
							nextwind = (nextwind + 1) % MaxWindRecent;
						}
					}
				}

				if (loopData.THSWindex < 32000)
				{
					THSWIndex = ConvertUnits.TempFToUser(loopData.THSWindex);
				}
			}
		}


		private void GetArchiveData()
		{
			cumulus.LogMessage("GetArchiveData: Downloading Archive Data");

			Console.WriteLine("Downloading Archive Data");

			const int maxPasses = 4;
			byte[] ACKstring = [ACK];
			byte[] NAKstring = [NACK];
			byte[] ESCstring = [ESC];
			const int pageSize = 267;
			const int recordSize = 52;
			bool ack;
			bool starting = true;

			NetworkStream stream = null;

			LastDataReadTime = cumulus.LastUpdateTime;
			int luhour = LastDataReadTime.Hour;

			int rollHour = Math.Abs(cumulus.GetHourInc(LastDataReadTime));

			cumulus.LogMessage("GetArchiveData: Roll-over hour = " + rollHour);

			bool rolloverdone = luhour == rollHour;

			bool midnightraindone = luhour == 0;
			bool rollover9amdone = luhour == 9;
			bool snowhourdone = luhour == cumulus.SnowDepthHour;

			// work out the next logger interval after the last CMX update
			var nextLoggerTime = Utils.RoundTimeUpToInterval(cumulus.LastUpdateTime.AddMinutes(-1), TimeSpan.FromMinutes(loggerInterval));

			// check if the calculated logger time is later than now!
			if (nextLoggerTime > DateTime.Now)
			{
				// nothing to do, presumably we were just restarted
				cumulus.LogMessage($"GetArchiveData: Last logger entry is later than our last update time, skipping logger download");
				return;
			}

			// construct date and time of last record read
			int vantageDateStamp = nextLoggerTime.Day + nextLoggerTime.Month * 32 + (nextLoggerTime.Year - 2000) * 512;
			int vantageTimeStamp = (100 * nextLoggerTime.Hour + nextLoggerTime.Minute);

			cumulus.LogMessage($"GetArchiveData: Last Archive Date: {nextLoggerTime}");
			cumulus.LogDebugMessage("GetArchiveData: Date: " + vantageDateStamp);
			cumulus.LogDebugMessage("GetArchiveData: Time: " + vantageTimeStamp);

			if (isSerial)
			{
				int retries = 0;
				do
				{
					comport.DiscardInBuffer();


					if (!WakeVP(comport))
					{
						cumulus.LogWarningMessage("GetArchiveData: Unable to wake VP");
					}

					// send the command
					comport.DiscardInBuffer();

					cumulus.LogMessage("GetArchiveData: Sending DMPAFT");
					comport.WriteLine("DMPAFT");

					// wait for the ACK
					ack = WaitForACK(comport);
					if (!ack)
					{
						cumulus.LogWarningMessage("GetArchiveData: No Ack in response to DMPAFT");
						retries++;
					}
				} while (!ack && retries < 2);
			}
			else
			{
				try
				{
					stream = socket.GetStream();
					int retries = 0;

					do
					{
						if (!WakeVP(socket))
						{
							cumulus.LogWarningMessage("GetArchiveData: Unable to wake VP");
						}

						cumulus.LogMessage("GetArchiveData: Sending DMPAFT");
						const string dmpaft = "DMPAFT\n";
						stream.Write(Encoding.ASCII.GetBytes(dmpaft), 0, dmpaft.Length);

						ack = WaitForACK(stream);
						if (!ack)
						{
							cumulus.LogWarningMessage("GetArchiveData: No Ack in response to DMPAFT");
							retries++;
						}
					} while (!ack && retries < 2);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("GetArchiveData: Error sending LOOP command [DMPAFT]: " + ex.Message);
					cumulus.LogDebugMessage("GetArchiveData: Attempting to reconnect to station");
					InitTCP();
					cumulus.LogDebugMessage("GetArchiveData: Reconnected to station");

					return;
				}
			}

			if (!ack)
			{
				cumulus.LogErrorMessage("GetArchiveData: No Ack in response to DMPAFT, giving up");
				return;
			}

			cumulus.LogMessage("GetArchiveData: Received response to DMPAFT, sending start date and time");

			// Construct date time string to send next
			byte[] data = [(byte) (vantageDateStamp % 256), (byte) (vantageDateStamp / 256), (byte) (vantageTimeStamp % 256), (byte) (vantageTimeStamp / 256), 0, 0];

			// calculate and insert CRC

			byte[] datacopy = new byte[4];

			Array.Copy(data, datacopy, 4);
			int crc = calculateCRC(datacopy);

			data[4] = (byte) (crc / 256);
			data[5] = (byte) (crc % 256);

			cumulus.LogDataMessage("GetArchiveData: Sending: " + BitConverter.ToString(data));

			if (isSerial)
			{
				if (comport == null)
				{
					cumulus.LogErrorMessage("GetArchiveData: No COMM port connection, giving up");
					return;
				}

				// send the data
				comport.Write(data, 0, 6);

				// wait for the ACK, this can take a while if it is going to dump a large number of records
				if (!WaitForACK(comport, 5000))
				{
					cumulus.LogWarningMessage("GetArchiveData: No ACK in response to sending date and time");
					return;
				}

				cumulus.LogMessage("GetArchiveData: Waiting for response");
				// wait for the response
				while (comport.BytesToRead < 6)
				{
					// Wait a short period to let more data load into the buffer
					Thread.Sleep(10);
				}

				// Read the response
				comport.Read(data, 0, 6);

				StringBuilder resp = new("Response:");

				for (int i = 0; i < 6; i++)
				{
					resp.Append(" " + data[i].ToString("X2"));
				}
				cumulus.LogDataMessage("GetArchiveData: " + resp);
			}
			else
			{
				if (stream == null)
				{
					cumulus.LogErrorMessage("GetArchiveData: No TCP connection, giving up");
					return;
				}

				stream.Write(data, 0, 6);

				if (!WaitForACK(stream, 5000))
				{
					cumulus.LogWarningMessage("GetArchiveData: No ACK in response to sending date and time");
					return;
				}

				// Wait until the buffer is full
				while (socket.Available < 6)
				{
					// Wait a short period to let more data load into the buffer
					Thread.Sleep(10);
				}

				// Read the response
				_ = stream.Read(data, 0, 6);

				cumulus.LogDataMessage("GetArchiveData: Response - " + BitConverter.ToString(data));
			}

			// extract number of pages and offset into first page
			int numPages = (data[1] * 256) + data[0];
			int offset = (data[3] * 256) + data[2];
			byte[] buff = new byte[pageSize];

			cumulus.LogMessage("GetArchiveData: Reading data: " + numPages + " pages , offset = " + offset);
			if (numPages == 513)
			{
				cumulus.LogMessage("GetArchiveData: Downloading entire logger contents!");
				Console.WriteLine(" - Downloading entire logger contents!");
			}

			// keep track of how many records processed for percentage display
			// but there may be some old entries in the last page
			int numtodo = (numPages * 5) - offset;
			int numdone = 0;

			if (numtodo == 0)
			{
				cumulus.LogMessage("GetArchiveData: No Archive data available");
				Console.WriteLine(" - No Archive data available");
			}
			else
			{
				for (int p = 0; p < numPages; p++)
				{
					cumulus.LogMessage("GetArchiveData: Reading archive page " + p);
					var passCount = 0;

					// send ACK to get next page
					if (isSerial)
						comport.Write(ACKstring, 0, 1);
					else
					{
						stream.Write(ACKstring, 0, 1);
					}

					bool badCRC;
					do
					{
						passCount++;

						cumulus.LogMessage("GetArchiveData: Waiting for response");
						int responsePasses = 0;
						if (isSerial)
						{
							// wait for the response
							CommTimer tmrComm = new CommTimer();
							tmrComm.Start(CommWaitTimeMs);
							while (!tmrComm.timedout)
							{
								if (comport.BytesToRead < pageSize)
								{
									// Wait a short period to let more data load into the buffer
									Thread.Sleep(20);
								}
								else
								{
									break;
								}
							}

							if (tmrComm.timedout)
							{
								cumulus.LogErrorMessage("GetArchiveData: The station has stopped sending archive data, ending attempts");
								if (!Program.service)
									Console.WriteLine(""); // flush the progress line
								return;
							}
							// Read the response
							cumulus.LogMessage("GetArchiveData: Reading response");
							comport.Read(buff, 0, pageSize);

							cumulus.LogDataMessage("GetArchiveData: Response data - " + BitConverter.ToString(buff));

							if (CrcOk(buff))
								badCRC = false;
							else
							{
								badCRC = true;
								// send NAK to get page again
								comport.Write(NAKstring, 0, 1);
							}
						}
						else
						{
							// wait for the response
							while (socket.Available < pageSize && responsePasses < 20)
							{
								// Wait a short period to let more data load into the buffer
								Thread.Sleep(cumulus.DavisOptions.IPResponseTime);
								responsePasses++;
							}

							if (responsePasses == 20)
							{
								cumulus.LogErrorMessage("The station has stopped sending archive data");
								if (!Program.service)
									Console.WriteLine(""); // flush the progress line
								return;
							}

							// Read the response
							var size = stream.Read(buff, 0, pageSize);

							if (size != pageSize)
							{
								cumulus.LogMessage($"GetArchiveData: Response data ( expecting {pageSize} bytes, got {size} bytes) - " + BitConverter.ToString(buff));
							}
							else
							{
								cumulus.LogDataMessage("GetArchiveData: Response data - " + BitConverter.ToString(buff));
							}

							if (CrcOk(buff))
								badCRC = false;
							else
							{
								badCRC = true;
								// send NAK to get page again
								stream.Write(NAKstring, 0, 1);
							}
						}
					} while ((passCount < maxPasses) && badCRC);

					// if we still got bad data after maxPasses, give up
					if (badCRC)
					{
						cumulus.LogWarningMessage("GetArchiveData: Bad CRC");
						if (isSerial)
							comport.Write(ESCstring, 0, 1);
						else
							stream.Write(ESCstring, 0, 1);

						return;
					}

					// use the offset on the first page only
					int start = (p == 0 ? offset : 0);

					for (int r = start; r < 5; r++)
					{
						try
						{

							VPArchiveData archiveData = new VPArchiveData();

							byte[] record = new byte[recordSize];

							DateTime timestamp;

							// Copy the next record from the buffer...
							Array.Copy(buff, (r * recordSize) + 1, record, 0, recordSize);

							// ...and load it into the archive data...
							archiveData.Load(record, out timestamp);

							cumulus.LogMessage("GetArchiveData: Loaded archive record for Page=" + p + " Record=" + r + " Timestamp=" + archiveData.Timestamp);

							if (timestamp > LastDataReadTime)
							{
								cumulus.LogMessage("GetArchiveData: Processing archive record for " + timestamp);

								DataDateTime = timestamp;

								rollHour = Math.Abs(cumulus.GetHourInc(timestamp));

								int h = timestamp.Hour;

								if (h != rollHour)
								{
									rolloverdone = false;
								}

								int interval;
								if (starting && timestamp > nextLoggerTime)
								{
									interval = loggerInterval;
									starting = false;
								}
								else
								{
									interval = (int) (timestamp - LastDataReadTime).TotalMinutes;
								}

								// ..and then process it

								// Things that really "should" to be done before we reset the day because the roll-over data contains data for the previous day for these values
								// Windrun (done)
								// Dominant wind bearing (done)
								// ET - if MX calculated (done)
								// Degree days (done)
								// Rainfall (done)

								if (h == rollHour && !rolloverdone)
								{
									double lastAvg = ConvertUnits.WindMPHToUser(archiveData.AvgWindSpeed);
									if (archiveData.HiWindSpeed < 250 && archiveData.AvgWindSpeed < 250)
									{
										int bearing = archiveData.WindDirection;
										bearing = bearing == 255 ? 0 : (int) (bearing * 22.5);

										// update dominant wind bearing
										CalculateDominantWindBearing(bearing, WindAverage, interval);
									}

									// add in 'archivePeriod' minutes worth of wind speed to windrun
									WindRunToday += ((lastAvg * WindRunHourMult[cumulus.Units.Wind] * interval) / 60.0);

									var preDayTS = timestamp.AddDays(-1).Date.AddHours(23).AddMinutes(59);

									CheckForWindrunHighLow(preDayTS);


									if (!cumulus.StationOptions.CalculatedET && archiveData.ET >= 0 && archiveData.ET < 32000)
									{
										DoET(ConvertUnits.RainINToUser(archiveData.ET) + AnnualETTotal, preDayTS);
									}


									// Now process the "average" interval temperature - use this as our
									if ((archiveData.OutsideTemperature > -200) && (archiveData.OutsideTemperature < 300))
									{
										var temp = ConvertUnits.TempFToUser(archiveData.OutsideTemperature);
										// add in 'archivePeriod' minutes worth of temperature to the temp samples
										tempsamplestoday += interval;
										TempTotalToday += (temp * interval);

										// update chill hours
										if (temp < cumulus.ChillHourThreshold && temp > cumulus.ChillHourBase)
										{
											// add 1 minute to chill hours
											ChillHours += (interval / 60.0);
										}

										// update heating/cooling degree days
										UpdateDegreeDays(interval);
									}

									var lastRain = ConvertRainClicksToUser(archiveData.Rainfall) + RainCounter;
									double lastRainrate = ConvertRainClicksToUser(archiveData.HiRainRate);

									if (lastRainrate < 0)
									{
										lastRainrate = 0;
									}

									DoRain(lastRain, lastRainrate, preDayTS);
								}


								// In roll-over hour and roll-over not yet done
								if ((h == rollHour) && !rolloverdone)
								{
									// do roll-over
									cumulus.LogMessage("GetArchiveData: Day roll-over " + timestamp.ToShortTimeString());
									// If the roll-over processing takes more that ~10 seconds the station times out sending the archive data
									// If this happens, add another run to the archive processing, so we start it again to pick up records for the next day
									var watch = new Stopwatch();
									watch.Start();
									DayReset(timestamp);
									watch.Stop();
									if (watch.ElapsedMilliseconds > 10000)
									{
										// EOD processing took longer than 10 seconds, add another run
										cumulus.LogDebugMessage("GetArchiveData: End of day processing took more than 10 seconds, adding another archive data run");
										maxArchiveRuns++;
									}
									rolloverdone = true;
								}

								// Not in midnight hour, midnight rain yet to be done
								if (h != 0)
								{
									midnightraindone = false;
								}
								else if (!midnightraindone)
								{
									// In midnight hour and midnight rain (and sun) not yet done
									ResetMidnightRain(timestamp);
									ResetSunshineHours(timestamp);
									ResetMidnightTemperatures(timestamp);
									midnightraindone = true;
								}

								// 9am rollover items
								if (h != 9)
								{
									rollover9amdone = false;
								}
								else if (!rollover9amdone)
								{
									Reset9amTemperatures(timestamp);
									rollover9amdone = true;
								}

								// Not in snow hour, snow yet to be done
								if (h != 0)
								{
									snowhourdone = false;
								}
								else if (!snowhourdone)
								{
									// snowhour items
									if (cumulus.SnowAutomated > 0)
									{
										CreateNewSnowRecord(timestamp);
									}

									// reset the accumulated snow depth(s)
									for (int i = 0; i < Snow24h.Length; i++)
									{
										Snow24h[i] = null;
									}

									snowhourdone = true;
								}

								if ((archiveData.InsideTemperature > -200) && (archiveData.InsideTemperature < 300))
								{
									DoIndoorTemp(ConvertUnits.TempFToUser(archiveData.InsideTemperature));
								}

								if ((archiveData.InsideHumidity >= 0) && (archiveData.InsideHumidity <= 100))
								{
									DoIndoorHumidity(archiveData.InsideHumidity);
								}

								if ((archiveData.OutsideHumidity >= 0) && (archiveData.OutsideHumidity <= 100))
								{
									DoOutdoorHumidity(archiveData.OutsideHumidity, timestamp);
								}

								// Check if the archive hi/lo temps break any records
								if ((archiveData.HiOutsideTemp > -200) && (archiveData.HiOutsideTemp < 300))
								{
									DoOutdoorTemp(ConvertUnits.TempFToUser(archiveData.HiOutsideTemp), timestamp);
								}

								// Check if the archive hi/lo temps break any records
								if ((archiveData.LoOutsideTemp > -200) && (archiveData.LoOutsideTemp < 300))
								{
									DoOutdoorTemp(ConvertUnits.TempFToUser(archiveData.LoOutsideTemp), timestamp);
								}

								// Now process the "average" interval temperature - use this as our
								if ((archiveData.OutsideTemperature > -200) && (archiveData.OutsideTemperature < 300))
								{
									DoOutdoorTemp(ConvertUnits.TempFToUser(archiveData.OutsideTemperature), timestamp);

									// we don't want to do the totals for the first instant of the day
									if (h != rollHour || timestamp.Minute != 0)
									{
										// add in 'archivePeriod' minutes worth of temperature to the temp samples
										tempsamplestoday += interval;
										TempTotalToday += (OutdoorTemperature * interval);

										// update chill hours
										if (OutdoorTemperature < cumulus.ChillHourThreshold && OutdoorTemperature > cumulus.ChillHourBase)
										{
											// add 1 minute to chill hours
											ChillHours += (interval / 60.0);
										}

										// update heating/cooling degree days
										UpdateDegreeDays(interval);
									}
								}

								double wind = ConvertUnits.WindMPHToUser(archiveData.HiWindSpeed);
								double avgwind = ConvertUnits.WindMPHToUser(archiveData.AvgWindSpeed);
								if (archiveData.HiWindSpeed < 250 && archiveData.AvgWindSpeed < 250)
								{
									int bearing = archiveData.WindDirection;
									bearing = bearing == 255 ? 0 : (int) (bearing * 22.5);

									AddValuesToRecentWind(avgwind, avgwind, bearing, timestamp.AddMinutes(-interval), timestamp);
									DoWind(wind, bearing, avgwind, timestamp);
									DoWindChill(OutdoorTemperature, timestamp);

									// update dominant wind bearing
									CalculateDominantWindBearing(bearing, WindAverage, interval);
								}

								DoApparentTemp(timestamp);
								DoFeelsLike(timestamp);
								DoHumidex(timestamp);
								DoCloudBaseHeatIndex(timestamp);

								// add in 'archivePeriod' minutes worth of wind speed to windrun
								// we don't want to do the this for the first instant of the day
								var notFirstRec = timestamp.Minute != 0 || h != rollHour;
								if (notFirstRec)
								{
									WindRunToday += ((WindAverage * WindRunHourMult[cumulus.Units.Wind] * interval) / 60.0);
									CheckForWindrunHighLow(timestamp);
								}


								// we don't want to add rainfall from first record of the day to the current day, it has already been added to the previous day
								if (notFirstRec)
								{
									double rain = ConvertRainClicksToUser(archiveData.Rainfall) + RainCounter;
									double rainrate = ConvertRainClicksToUser(archiveData.HiRainRate);

									if (rainrate < 0)
									{
										rainrate = 0;
									}

									DoRain(rain, rainrate, timestamp);
								}

								if ((archiveData.Pressure > 0) && (archiveData.Pressure < 40))
								{
									DoPressure(ConvertUnits.PressINHGToUser(archiveData.Pressure), timestamp);
								}

								// No station pressure in archive data
								StationPressure = 0;
								AltimeterPressure = Pressure;

								if (archiveData.HiUVIndex >= 0 && archiveData.HiUVIndex < 25)
								{
									DoUV(archiveData.HiUVIndex, timestamp);
								}

								if (archiveData.SolarRad >= 0 && archiveData.SolarRad < 5000)
								{
									DoSolarRad(archiveData.SolarRad, timestamp);

									// add in archive period worth of sunshine, if sunny
									if (IsSunny)
										SunshineHours += (interval / 60.0);
								}

								// we don't want to do the this for the first instant of the day
								if (notFirstRec && !cumulus.StationOptions.CalculatedET && archiveData.ET >= 0 && archiveData.ET < 32000)
								{
									DoET(ConvertUnits.RainINToUser(archiveData.ET) + AnnualETTotal, timestamp);
								}

								if (cumulus.StationOptions.LogExtraSensors)
								{
									if (archiveData.ExtraTemp1 < 255)
									{
										DoExtraTemp(ConvertUnits.TempFToUser(archiveData.ExtraTemp1 - 90), 1);
									}

									if (archiveData.ExtraTemp2 < 255)
									{
										DoExtraTemp(ConvertUnits.TempFToUser(archiveData.ExtraTemp2 - 90), 2);
									}

									if (archiveData.ExtraTemp3 < 255)
									{
										DoExtraTemp(ConvertUnits.TempFToUser(archiveData.ExtraTemp3 - 90), 3);
									}

									if (archiveData.ExtraHum1 >= 0 && archiveData.ExtraHum1 <= 100)
									{
										DoExtraHum(archiveData.ExtraHum1, 1);
										if (archiveData.ExtraTemp1 < 255)
										{
											ExtraDewPoint[1] = ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[1].Value), ExtraHum[1].Value));
										}
									}

									if (archiveData.ExtraHum2 >= 0 && archiveData.ExtraHum2 <= 100)
									{
										DoExtraHum(archiveData.ExtraHum2, 2);
										if (archiveData.ExtraTemp2 < 255)
										{
											ExtraDewPoint[2] = ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[2].Value), ExtraHum[2].Value));
										}
									}

									if (archiveData.SoilMoisture1 >= 0 && archiveData.SoilMoisture1 <= 250)
									{
										DoSoilMoisture(archiveData.SoilMoisture1, 1);
									}

									if (archiveData.SoilMoisture2 >= 0 && archiveData.SoilMoisture2 <= 250)
									{
										DoSoilMoisture(archiveData.SoilMoisture2, 2);
									}

									if (archiveData.SoilMoisture3 >= 0 && archiveData.SoilMoisture3 <= 250)
									{
										DoSoilMoisture(archiveData.SoilMoisture3, 3);
									}

									if (archiveData.SoilMoisture4 >= 0 && archiveData.SoilMoisture4 <= 250)
									{
										DoSoilMoisture(archiveData.SoilMoisture4, 4);
									}

									if (archiveData.SoilTemp1 < 255 && archiveData.SoilTemp1 > 0)
									{
										DoSoilTemp(ConvertUnits.TempFToUser(archiveData.SoilTemp1 - 90), 1);
									}

									if (archiveData.SoilTemp2 < 255 && archiveData.SoilTemp2 > 0)
									{
										DoSoilTemp(ConvertUnits.TempFToUser(archiveData.SoilTemp2 - 90), 2);
									}

									if (archiveData.SoilTemp3 < 255 && archiveData.SoilTemp3 > 0)
									{
										DoSoilTemp(ConvertUnits.TempFToUser(archiveData.SoilTemp3 - 90), 3);
									}

									if (archiveData.SoilTemp4 < 255 && archiveData.SoilTemp4 > 0)
									{
										DoSoilTemp(ConvertUnits.TempFToUser(archiveData.SoilTemp4 - 90), 4);
									}

									if (archiveData.LeafWetness1 >= 0 && archiveData.LeafWetness1 < 16)
									{
										DoLeafWetness(archiveData.LeafWetness1, 1);
									}

									if (archiveData.LeafWetness2 >= 0 && archiveData.LeafWetness2 < 16)
									{
										DoLeafWetness(archiveData.LeafWetness2, 2);
									}
								}

								cumulus.LogMessage("GetArchiveData: Page=" + p + " Record=" + r + " Timestamp=" + archiveData.Timestamp);

								DoWindChill(-999, timestamp);

								DoApparentTemp(timestamp);
								DoFeelsLike(timestamp);
								DoHumidex(timestamp);
								DoTrendValues(timestamp);

								// we don't want to do the this for the first instant of the day
								if (cumulus.StationOptions.CalculatedET && h != rollHour && timestamp.Minute != 0)
								{
									// Start of a new hour, and we want to calculate ET in Cumulus
									CalculateEvapotranspiration(timestamp);
								}

								LastDataReadTime = timestamp;

								_ = cumulus.DoLogFile(timestamp, false);
								cumulus.LogMessage("GetArchiveData: Log file entry written");

								try
								{
									cumulus.MySqlRealtimeFile(999, false, timestamp);
									cumulus.DoCustomIntervalLogs(timestamp);

									if (cumulus.StationOptions.LogExtraSensors)
									{
										_ = cumulus.DoExtraLogFile(timestamp);
									}

									// Custom MySQL update - minutes interval
									if (cumulus.MySqlSettings.CustomMins.Enabled)
									{
										_ = cumulus.CustomMysqlMinutesUpdate(timestamp, false);
									}
									cumulus.AddToWebServiceLists(timestamp);
								}
								catch (Exception ex)
								{
									cumulus.LogExceptionMessage(ex, "GetArchiveData: Error in extra logging etc");
								}

								AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
									OutdoorHumidity, Pressure, RainToday, SolarRad, UV, RainCounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate, -1, -1);


								UpdateStatusPanel(timestamp);
							}
							else
							{
								cumulus.LogMessage("GetArchiveData: Ignoring old archive data");
							}

							numdone++;
							if (!Program.service)
								Console.Write("\r - processed " + (numdone / (double) numtodo).ToString("P0"));
							cumulus.LogMessage(numdone + " archive entries processed");
						}
						catch (Exception ex)
						{
						cumulus.LogExceptionMessage(ex, "GetArchiveData: Unspecified page error");
						}
					}
				}

				if (!Program.service)
				{
					Console.WriteLine(""); // flush the progress line
				}
			}
		}


		// In order to conserve battery power, the console spends as much time “asleep” as possible,
		// waking up only when required. Receiving a character on the serial port will cause the console to
		// wake up, but it might not wake up fast enough to read the first character correctly. Because of
		// this, you should always perform a wakeup procedure before sending commands to the console:
		// Console Wakeup procedure:
		//      1. Send a Line Feed character, ‘\n’ (decimal 10, hex 0x0A).
		//      2. Listen for a returned response of Line Feed and Carriage Return characters, (‘\n\r’).
		//      3. If there is no response within a reasonable interval (say 1.2 seconds), then try steps 1 and
		//         2 again up to a total of 3 attempts.
		//      4. If the console has not woken up after 3 attempts, then signal a connection error
		// After the console has woken up, it will remain awake for 2 minutes. Every time the VP
		// receives another character, the 2 minute timer will be reset.
		private bool WakeVP(SerialPort serialPort, bool force = false)
		{
			// Check if we haven't sent a command within the last two minutes - use 1:50 (110,000 ms) to be safe
			if (awakeStopWatch.IsRunning && awakeStopWatch.ElapsedMilliseconds < 110000 && !force)
			{
				cumulus.LogDebugMessage("WakeVP: Not required");
				awakeStopWatch.Restart();
				return true;
			}

			try
			{
				cumulus.LogDebugMessage("WakeVP: Starting");
				// Clear out both input and output buffers just in case something is in there already
				if (serialPort.BytesToRead > 0)
				{
					cumulus.LogDebugMessage($"WakeVP: Discarding {serialPort.BytesToRead} spurious characters from serial port");
				}
				serialPort.DiscardOutBuffer();

				bool woken = false;
				int i = 1;
				int lastChar = 0;

				while (!woken && (i < 5 || serialPort.BytesToRead > 0))
				{
					cumulus.LogDebugMessage($"WakeVP: Sending wake-up newline ({i}/4)");

					try
					{
						serialPort.DiscardInBuffer();

						// Put a newline character ('\n') out the serial port - the Writeline method terminates with a '\n' of its own
						serialPort.WriteLine("");

						int thisChar;
						do
						{
							thisChar = comport.ReadByte();

							if (thisChar == CR && lastChar == LF)
							{
								woken = true;
								break;
							}

							lastChar = thisChar;
						} while (thisChar > -1);
					}
					catch (TimeoutException)
					{
						cumulus.LogDebugMessage("WakeVP: Timed out waiting for response");
						i++;
					}
				}

				// VP found and awakened
				if (woken)
				{
					// start the stopwatch
					awakeStopWatch.Restart();

					// Now that the VP is awake, clean out the input buffer again
					serialPort.DiscardInBuffer();
					serialPort.DiscardOutBuffer();

					cumulus.LogDebugMessage("WakeVP: Woken");
					return (true);
				}

				cumulus.LogWarningMessage("WakeVP: *** VP2 Not woken");
				return (false);
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("WakeVP: Error - " + ex);
				return (false);
			}
		}

		private bool WakeVP(TcpClient thePort, bool force = false)
		{
			const int maxPasses = 3;
			int retryCount = 0;

			// Check if we haven't sent a command within the last two minutes - use 1:50 () to be safe
			if (awakeStopWatch.IsRunning && awakeStopWatch.ElapsedMilliseconds < 110000 && !force)
			{
				cumulus.LogDebugMessage("WakeVP: Not required");
				awakeStopWatch.Restart();
				return true;
			}

			if (stop)
			{
				return false;
			}

			cumulus.LogDebugMessage("WakeVP: Starting");

			try
			{
				NetworkStream stream;
				try
				{
					stream = thePort.GetStream();
				}
				catch (Exception exStream)
				{
					// There is a problem with the connection, try to disconnect/connect
					// refer back to the socket field of this class
					try
					{
						cumulus.LogDebugMessage("WakeVP: Problem with TCP connection - " + exStream.Message);
						socket.Client.Close(0);
					}
					finally
					{
						// Wait a second
						Thread.Sleep(1000);
						socket = null;

						cumulus.LogDebugMessage("WakeVP: Attempting reconnect to logger");
						// open a new connection
						InitTCP();
						thePort = socket;
					}

					if (thePort == null)
					{
						return (false);
					}

					if (thePort.Connected)
					{
						stream = thePort.GetStream();
					}
					else
					{
						return false;
					}
				}
				stream.ReadTimeout = 2500;
				stream.WriteTimeout = 2500;

				// Pause to allow any data to come in
				Thread.Sleep(250);

				// First flush the stream
				int cnt = 0;
				while (stream.DataAvailable)
				{
					// Read the current character
					stream.ReadByte();
					cnt++;
				}
				if (cnt > 0)
				{
					cumulus.LogDebugMessage($"WakeVP: Flushed {cnt} spurious characters from input stream");
				}


				while (retryCount < 1)
				{
					var passCount = 1;
					int lastChar = 0;

					while (passCount <= maxPasses)
					{
						try
						{
							cumulus.LogDebugMessage($"WakeVP: Sending newline ({passCount}/{maxPasses})");
							stream.WriteByte(LF);

							Thread.Sleep(cumulus.DavisOptions.IPResponseTime);

							int thisChar;
							do
							{
								thisChar = stream.ReadByte();
								if (thisChar == CR && lastChar == LF)
								{
									// start the stopwatch
									awakeStopWatch.Restart();
									return true;
								}

								lastChar = thisChar;
							} while (thisChar > -1);
						}
						catch (System.IO.IOException ex)
						{
							if (ex.Message.Contains("did not properly respond after a period"))
							{
								cumulus.LogDebugMessage("WakeVP: Timed out waiting for a response");
								passCount++;
							}
							else
							{
								cumulus.LogDebugMessage("WakeVP: Problem with TCP connection " + ex.Message);
								cumulus.LogDebugMessage("WakeVP: Attempting reconnect to logger");
								InitTCP();
								cumulus.LogDebugMessage("WakeVP: Reconnected to logger");
								return true;
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage("WakeVP: Problem with TCP connection " + ex.Message);
							cumulus.LogDebugMessage("WakeVP: Attempting reconnect to logger");
							InitTCP();
							cumulus.LogDebugMessage("WakeVP: Reconnected to logger");
							return true;
						}
					}

					// we only get here if we did not receive a LF/CR
					// try reconnecting the TCP session
					cumulus.LogDebugMessage("WakeVP: Attempting reconnect to logger");
					InitTCP();
					cumulus.LogDebugMessage("WakeVP: Reconnected to logger");

					retryCount++;

					// Wait a second
					Thread.Sleep(1000);
				}

				cumulus.LogWarningMessage("WakeVP: *** Console Not woken");
				return (false);
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("WakeVP: Error - " + ex.Message);
				return (false);
			}
		}

		private void InitSerial()
		{
			byte[] readBuffer = new byte[1000];
			int bytesRead = 0;

			awakeStopWatch.Stop();

			do
			{
				try
				{
					if (comport != null && comport.IsOpen)
					{
						comport.Close();
					}
				}
				catch
				{
					// do nothing
				}

				cumulus.LogMessage("InitSerial: Connecting to the station");

				try
				{
					comport = new SerialPort(cumulus.ComportName, cumulus.DavisOptions.BaudRate, Parity.None, 8, StopBits.One)
					{
						Handshake = Handshake.None,
						DtrEnable = true,
						ReadTimeout = 1000,
						WriteTimeout = 1000
					};

					comport.Open();
					comport.NewLine = "\n";
				}
				catch (Exception ex)
				{
					Cumulus.LogConsoleMessage("Error opening serial port - " + ex.Message, ConsoleColor.Red, true);
					Cumulus.LogConsoleMessage("Will retry in 30 seconds...");
					cumulus.LogErrorMessage("InitSerial: Error opening port - " + ex.Message);
				}

				if (comport == null || !comport.IsOpen)
				{
					cumulus.LogMessage("InitSerial: Failed to connect to the station, waiting 30 seconds before trying again");
					cumulus.cancellationToken.WaitHandle.WaitOne(30000);

					if (cumulus.cancellationToken.IsCancellationRequested)
					{
						stop = true;
						return;
					}
				}

			} while (comport != null && !comport.IsOpen);


			try
			{
				if (comport == null)
				{
					cumulus.LogErrorMessage("InitSerial: No COMM port connection, giving up");
					return;
				}

				// stop any loop data that may still be active
				comport.WriteLine("");
				Thread.Sleep(500);

				cumulus.LogDebugMessage("InitSerial: Flushing input stream");
				comport.DiscardInBuffer();
				comport.DiscardOutBuffer();

				// now we have purged any data, test the connection
				int tryCount = 1;
				var resp = string.Empty;
				do
				{
					// write TEST, we expect to get "\n\rTEST\n\r" back
					cumulus.LogDebugMessage($"InitSerial: Sending TEST ({tryCount}) command");
					comport.WriteLine("TEST");

					// pause to allow time for a response
					Thread.Sleep(500);
					try
					{
						do
						{
							// Read the current byte
							var ch = comport.ReadByte();
							readBuffer[bytesRead] = (byte) ch;
							bytesRead++;
						} while (comport.BytesToRead > 0 || bytesRead < 8);

						resp = Encoding.ASCII.GetString(readBuffer);
						cumulus.LogDataMessage($"InitSerial: TEST ({tryCount}) received - '{BitConverter.ToString(readBuffer.Take(bytesRead).ToArray())}'");

						if (resp.Contains("TEST"))
						{
							cumulus.LogDebugMessage($"InitSerial: TEST ({tryCount}) successful");
							break;
						}
					}
					catch (TimeoutException)
					{
						cumulus.LogDebugMessage($"InitSerial: Timed out waiting for a response to TEST ({tryCount})");
					}
					catch (Exception ex)
					{
						cumulus.LogDebugMessage($"InitSerial: Error - {ex.Message}");
					}
					tryCount++;

				} while (tryCount < 5);

				if (resp.Contains("TEST"))
				{
					awakeStopWatch.Restart();
					cumulus.LogMessage("InitSerial: Connection confirmed");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("InitSerial: Error - " + ex.Message);
			}
		}

		private void InitTCP()
		{
			awakeStopWatch.Stop();
			do
			{
				try
				{
					if (socket != null && socket.Connected)
						socket.Close();
				}
				catch
				{
					// do nothing
				}

				cumulus.LogMessage("InitTCP: Connecting to the station");

				socket = OpenTcpPort();

				if ((socket == null || !socket.Connected) && !stop)
				{
					cumulus.LogMessage("InitTCP: Failed to connect to the station, waiting 30 seconds before trying again");
					Cumulus.LogConsoleMessage("Failed to connect to the station, waiting 30 seconds before trying again", ConsoleColor.Red, true);

					cumulus.cancellationToken.WaitHandle.WaitOne(30000);

					if (cumulus.cancellationToken.IsCancellationRequested)
					{
						stop = true;
						return;
					}
				}
			} while ((socket == null || !socket.Connected) && !stop);

			try
			{
				if (socket == null)
				{
					cumulus.LogErrorMessage("InitTCP: No TCP connection, giving up");
					return;
				}

				cumulus.LogMessage("InitTCP: Flushing input stream");
				NetworkStream stream = socket.GetStream();
				stream.ReadTimeout = 2500;
				stream.WriteTimeout = 2500;

				// stop loop data
				stream.WriteByte(0x0A);

				Thread.Sleep(cumulus.DavisOptions.InitWaitTime);

				byte[] buffer1 = new byte[1000];
				byte[] buffer2 = new byte[buffer1.Length];

				while (stream.DataAvailable)
				{
					// Read the current character
					stream.ReadByte();
					Thread.Sleep(10);
				}

				// now we have purged any data, test the connection
				int tryCount = 1;
				do
				{
					var idx = 0;
					// write TEST, we expect to get "TEST\n\r" back
					cumulus.LogDebugMessage($"InitTCP: Sending TEST ({tryCount}) command");
					stream.Write(Encoding.ASCII.GetBytes("TEST\n"), 0, 5);

					Thread.Sleep(cumulus.DavisOptions.InitWaitTime);

					while (stream.DataAvailable)
					{
						var ch = stream.ReadByte();
						if (idx < buffer1.Length)
						{
							buffer1[idx++] = (byte) ch;
						}
						else
						{
							Array.Copy(buffer1, 1, buffer2, 0, buffer1.Length);
							buffer2[9] = (byte) ch;
							Array.Copy(buffer2, buffer1, buffer1.Length);
						}
						Thread.Sleep(50);
					}

					var resp = Encoding.ASCII.GetString(buffer1);
					cumulus.LogDataMessage($"InitTCP: TEST ({tryCount}) received - '{BitConverter.ToString(buffer1.Take(idx).ToArray())}'");

					if (resp.Contains("TEST"))
					{
						cumulus.LogDebugMessage($"InitTCP: TEST ({tryCount}) successful");
						break;
					}
					tryCount++;
				} while (tryCount < 5);

				if (tryCount < 5)
				{
					awakeStopWatch.Restart();
					cumulus.LogMessage("InitTCP: Connection confirmed");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("InitTCP: Error - " + ex.Message);
			}
		}


		private bool WaitForOK(SerialPort serialPort)
		{
			// Waits for OK<LF><CR>
			var readBuffer = new StringBuilder();

			cumulus.LogDebugMessage("WaitForOK: Wait for OK");
			do
			{
				try
				{
					// Read the current character
					readBuffer.Append((char) serialPort.ReadChar());
				}
				catch (TimeoutException)
				{
					cumulus.LogDebugMessage("WaitForOK: Timed out");
					return false;
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage($"WaitForOK: Error - {ex.Message}");
					cumulus.LogDebugMessage("WaitForOK: Attempting to reconnect to logger");
					InitSerial();
					cumulus.LogDebugMessage("WaitForOK: Reconnected to logger");
					return false;
				}

			} while (!readBuffer.ToString().Contains("OK\n\r", StringComparison.InvariantCulture));
			cumulus.LogDebugMessage("WaitForOK: Found OK");
			return true;
		}

		private bool WaitForOK(NetworkStream stream)
		{
			// Waits for OK<LF><CR>
			var readBuffer = new StringBuilder();

			cumulus.LogDebugMessage("WaitForOK: Wait for OK");
			Thread.Sleep(cumulus.DavisOptions.IPResponseTime);

			do
			{
				try
				{
					// Read the current character
					readBuffer.Append((char) stream.ReadByte());
				}
				catch (System.IO.IOException ex)
				{
					if (ex.Message.Contains("did not properly respond after a period"))
					{
						cumulus.LogDebugMessage("WaitForOK: Timed out");
						cumulus.LogDataMessage($"WaitForOK: Received - {BitConverter.ToString(Encoding.UTF8.GetBytes(readBuffer.ToString()))}");
						return false;
					}

					cumulus.LogDebugMessage($"WaitForOK: Error - {ex.Message}");
					cumulus.LogDataMessage($"WaitForOK: Received - {BitConverter.ToString(Encoding.UTF8.GetBytes(readBuffer.ToString()))}");
					cumulus.LogDebugMessage("WaitForOK: Attempting to reconnect to logger");
					InitTCP();
					cumulus.LogDebugMessage("WaitForOK: Reconnected to logger");
					return false;
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage($"WaitForOK: Error - {ex.Message}");
					cumulus.LogDebugMessage("WaitForOK: Attempting to reconnect to logger");
					InitTCP();
					cumulus.LogDebugMessage("WaitForOK: Reconnected to logger");
					return false;
				}

			} while (!readBuffer.ToString().Contains("OK\n\r", StringComparison.InvariantCulture));
			cumulus.LogDebugMessage("WaitForOK: Found OK");
			return true;
		}


		private bool WaitForACK(SerialPort serialPort, int timeoutMs = -1)
		{
			int tryCount = 0;
			// Wait for the VP to acknowledge the receipt of the command - sometimes we get a '\n\r'
			// in the buffer first or no response is given.  If all else fails, try again.
			cumulus.LogDebugMessage("WaitForACK: Wait for ACK");

			serialPort.ReadTimeout = timeoutMs > -1 ? timeoutMs : 1000;

			do
			{
				try
				{
					tryCount++;
					// Read the current character
					var currChar = serialPort.ReadChar();
					switch (currChar)
					{
						case ACK:
							cumulus.LogDebugMessage("WaitForACK: ACK received");
							return true;
						case NACK:
							cumulus.LogDebugMessage("WaitForACK: NACK received");
							return false;
						case CANCEL:
							cumulus.LogDebugMessage("WaitForACK: CANCEL received");
							return false;
						case LF:
						case CR:
							cumulus.LogDataMessage("WaitForACK: Discarding CR or LF - " + currChar.ToString("X2"));
							tryCount--;
							break;
						default:
							cumulus.LogDataMessage($"WaitForACK: ({tryCount}) Received - {currChar:X2}");
							break;
					}
				}
				catch (TimeoutException)
				{
					cumulus.LogDebugMessage($"WaitForAck: ({tryCount}) Timed out");
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage($"WaitForAck: {tryCount} Error - {ex.Message}");
					cumulus.LogDebugMessage("WaitForAck: Attempting to reconnect to logger");
					InitSerial();
					cumulus.LogDebugMessage("WaitForAck: Reconnected to logger");
				}
			} while (tryCount < 2);

			cumulus.LogDebugMessage("WaitForAck: timed out");
			return false;
		}

		private bool WaitForACK(NetworkStream stream, int timeoutMs = -1)
		{
			int tryCount = 0;

			// Wait for the VP to acknowledge the receipt of the command - sometimes we get a '\n\r'
			// in the buffer first or no response is given.  If all else fails, try again.
			cumulus.LogDebugMessage("WaitForACK: Starting");

			Thread.Sleep(cumulus.DavisOptions.IPResponseTime);

			if (timeoutMs > -1)
			{
				try
				{
					stream.ReadTimeout = timeoutMs;
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage($"WaitForAck: {tryCount} Error - {ex.Message}");
					cumulus.LogDebugMessage("WaitForAck: Attempting to reconnect to logger");
					InitTCP();
					cumulus.LogDebugMessage("WaitForAck: Reconnected to logger");
				}
			}

			do
			{
				try
				{
					tryCount++;
					// Read the current character
					var currChar = stream.ReadByte();
					switch (currChar)
					{
						case ACK:
							cumulus.LogDebugMessage("WaitForACK: ACK received");
							return true;
						case NACK:
							cumulus.LogDebugMessage("WaitForACK: NACK received");
							return false;
						case CANCEL:
							cumulus.LogDebugMessage("WaitForACK: CANCEL received");
							return false;
						case LF:
						case CR:
							cumulus.LogDataMessage("WaitForACK: Discarding CR or LF - " + currChar.ToString("X2"));
							tryCount--;
							break;
						default:
							cumulus.LogDataMessage("WaitForACK: Received - " + currChar.ToString("X2"));
							break;
					}
				}
				catch (System.IO.IOException ex)
				{
					if (ex.Message.Contains("did not properly respond after a period"))
					{
						cumulus.LogDebugMessage($"WaitForAck: timed out, attempt {tryCount}");
					}
					else
					{
						cumulus.LogDebugMessage($"WaitForAck: {tryCount} Error - {ex.Message}");
						cumulus.LogDebugMessage("WaitForAck: Attempting to reconnect to logger");
						InitTCP();
						cumulus.LogDebugMessage("WaitForAck: Reconnected to logger");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage($"WaitForAck: {tryCount} Error - {ex.Message}");
					cumulus.LogDebugMessage("WaitForAck: Attempting to reconnect to logger");
					InitTCP();
					cumulus.LogDebugMessage("WaitForAck: Reconnected to logger");
				}
				finally
				{
					if (timeoutMs > -1)
					{
						stream.ReadTimeout = 2500;
					}
				}
			} while (tryCount < 2);

			cumulus.LogDebugMessage("WaitForAck: Timed out");
			return false;
		}

		private DateTime GetTime()
		{
			byte[] readBuffer = new byte[8];
			var bytesRead = 0;

			// Expected response - <ACK><42><17><15><28><11><98><2 Bytes of CRC>
			//                     06   ss  mm  hh  dd  MM  yy

			cumulus.LogMessage("Reading console time");

			if (isSerial)
			{
				const string commandString = "GETTIME";
				if (WakeVP(comport))
				{
					try
					{
						comport.WriteLine(commandString);

						if (!WaitForACK(comport))
						{
							cumulus.LogWarningMessage("getTime: No ACK");
							return DateTime.MinValue;
						}

						// Read the time
						do
						{
							// Read the current character
							var ch = comport.ReadChar();
							readBuffer[bytesRead] = (byte) ch;
							bytesRead++;
						} while (bytesRead < 8);
					}
					catch (TimeoutException)
					{
						cumulus.LogWarningMessage("getTime: Timed out waiting for a response");
						return DateTime.MinValue;
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("getTime: Error - " + ex.Message);
						return DateTime.MinValue;
					}
				}
			}
			else
			{
				const string commandString = "GETTIME\n";
				if (WakeVP(socket))
				{
					try
					{
						NetworkStream stream = socket.GetStream();
						stream.ReadTimeout = 2500;
						stream.WriteTimeout = 2500;

						stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

						if (!WaitForACK(stream))
						{
							cumulus.LogMessage("getTime: No ACK - wait a little longer");
							if (!WaitForACK(stream))
							{
								cumulus.LogMessage("getTime: No ACK, returning");
								return DateTime.MinValue;
							}
						}

						// Read the time
						do
						{
							// Read the current character
							readBuffer[bytesRead] = (byte) stream.ReadByte();
							bytesRead++;
						} while (bytesRead < 8);
					}
					catch (System.IO.IOException ex)
					{
						if (ex.Message.Contains("did not properly respond after a period"))
						{
							cumulus.LogWarningMessage("getTime: Timed out waiting for a response");
						}
						else
						{
							cumulus.LogDebugMessage("getTime: Error - " + ex.Message);
						}
						return DateTime.MinValue;
					}
					catch (Exception ex)
					{
						cumulus.LogDebugMessage("getTime: Error - " + ex.Message);
						return DateTime.MinValue;
					}
				}
			}

			cumulus.LogDataMessage("getTime: Received - " + BitConverter.ToString(readBuffer.Take(bytesRead).ToArray()));
			if (bytesRead != 8)
			{
				cumulus.LogWarningMessage("getTime: Expected 8 bytes, got " + bytesRead);
			}
			else
			{
				try
				{
					return new DateTime(readBuffer[5] + 1900, readBuffer[4], readBuffer[3], readBuffer[2], readBuffer[1], readBuffer[0], DateTimeKind.Local);
				}
				catch (Exception)
				{
					cumulus.LogWarningMessage("getTime: Error in time format");
				}
			}
			return DateTime.MinValue;
		}

		private void SetTime()
		{
			NetworkStream stream = null;

			cumulus.LogMessage("Setting console time");

			try
			{
				if (isSerial)
				{
					const string commandString = "SETTIME";
					if (WakeVP(comport))
					{
						comport.WriteLine(commandString);

						// wait for the ACK
						if (!WaitForACK(comport))
						{
							cumulus.LogWarningMessage("SetTime: No ACK to SETTIME - Not setting the time");
							return;
						}
					}
				}
				else
				{
					const string commandString = "SETTIME\n";
					if (WakeVP(socket))
					{
						stream = socket.GetStream();
						stream.ReadTimeout = 2500;
						stream.WriteTimeout = 2500;

						stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

						// wait for the ACK
						if (!WaitForACK(stream))
						{
							cumulus.LogWarningMessage("SetTime: No ACK to SETTIME - Not setting the time");
							return;
						}
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("SetTime: Error - " + ex.Message);
				return;
			}

			DateTime now = DateTime.Now;

			byte[] writeBuffer = new byte[8];

			writeBuffer[0] = (byte) now.Second;
			writeBuffer[1] = (byte) now.Minute;
			writeBuffer[2] = (byte) now.Hour;
			writeBuffer[3] = (byte) now.Day;
			writeBuffer[4] = (byte) now.Month;
			writeBuffer[5] = (byte) (now.Year - 1900);

			// calculate and insert CRC

			byte[] datacopy = new byte[6];

			Array.Copy(writeBuffer, datacopy, 6);
			int crc = calculateCRC(datacopy);

			writeBuffer[6] = (byte) (crc / 256);
			writeBuffer[7] = (byte) (crc % 256);

			try
			{
				if (isSerial)
				{

					// send the data
					comport.Write(writeBuffer, 0, 8);

					// wait for the ACK
					if (WaitForACK(comport))
					{
						cumulus.LogMessage("SetTime: Console time set OK");
					}
					else
					{
						cumulus.LogWarningMessage("SetTime: Error, console time set failed");
					}
				}
				else if (stream != null)
				{
					stream.Write(writeBuffer, 0, writeBuffer.Length);

					if (WaitForACK(stream))
					{
						cumulus.LogMessage("SetTime: Console time set OK");
					}
					else
					{
						cumulus.LogWarningMessage("SetTime: Error, console time set failed");
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("SetTime: Error - " + ex.Message);
			}
		}


		/// <summary>
		/// Converts VP rain gauge tips/clicks to user units
		/// Assumes
		/// </summary>
		/// <param name="clicks"></param>
		/// <returns></returns>
		private double ConvertRainClicksToUser(double clicks)
		{
			// One click is either 0.01, 0.001 inches or 0.2, 0.1 mm
			return cumulus.DavisOptions.RainGaugeType switch
			{
				0 => ConvertUnits.RainMMToUser(clicks * 0.2),// Rain gauge is metric 0.2 mm
				1 => ConvertUnits.RainINToUser(clicks * 0.01),// Rain gauge is imperial 0.01 in
				2 => ConvertUnits.RainMMToUser(clicks * 0.1),// Rain gauge is metric 0.1 mm
				3 => ConvertUnits.RainMMToUser(clicks * 0.2),// Rain gauge is imperial 0.001 in
				_ => cumulus.Units.Rain == 0 ? clicks * 0.2 : clicks * 0.01,// Rain gauge type not configured, assume it is the same as the station units
																			// Assume standard gauge type of 0.01 in or 0.02 mm
			};
		}
	}
}
