﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Threading;

namespace CumulusMX.Stations
{
	internal class ImetStation : WeatherStation
	{
		private const string sLineBreak = "\r\n";
		private double prevraintotal = -1;
		private int previousminute = 60;
		private string currentWritePointer = string.Empty;
		private int readCounter = 30;
		private bool stop = false;

		public ImetStation(Cumulus cumulus) : base(cumulus)
		{
			cumulus.LogMessage("ImetUpdateLogPointer=" + cumulus.ImetOptions.UpdateLogPointer);
			cumulus.LogMessage("ImetWaitTime=" + cumulus.ImetOptions.WaitTime);
			cumulus.LogMessage("ImetReadDelay=" + cumulus.ImetOptions.ReadDelay);
			cumulus.LogMessage("ImetBaudRate=" + cumulus.ImetOptions.BaudRate);
			cumulus.LogMessage("Instromet: Attempting to open " + cumulus.ComportName);

			calculaterainrate = true;

			// Imet does not provide average wind speeds
			cumulus.StationOptions.CalcuateAverageWindSpeed = true;
			cumulus.StationOptions.UseSpeedForAvgCalc = false;
			cumulus.StationOptions.UseSpeedForLatest = false;

			// Change the default dps for rain and sunshine from 1 to 2 for IMet stations
			cumulus.RainDPlaces = cumulus.SunshineDPlaces = 2;
			cumulus.RainDPlaceDefaults[0] = 2;  // mm
			cumulus.RainDPlaceDefaults[1] = 3;  // in
			cumulus.RainFormat = cumulus.SunFormat = "F2";

			comport = new SerialPort(cumulus.ComportName, cumulus.ImetOptions.BaudRate, Parity.None, 8, StopBits.One) { Handshake = Handshake.None, RtsEnable = true, DtrEnable = true };

			try
			{
				comport.ReadTimeout = 1000;
				comport.Open();
				cumulus.LogMessage("COM port opened");
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error opening COM port: " + ex.Message);
			}

			if (comport.IsOpen)
			{
				ImetSetLoggerInterval(Cumulus.logints[cumulus.DataLogInterval]);
				if (cumulus.StationOptions.SyncTime)
				{
					SetStationClock();
				}

				// Read the data from the logger
				startReadingHistoryData();
			}
		}

		private void ImetSetLoggerInterval(int interval)
		{
			cumulus.LogMessage($"Setting logger interval to {interval} minutes");

			SendCommand("WRST,11," + interval * 60);
			// read the response
			var response = GetResponse("wrst");

			var data = ExtractText(response, "wrst");
			cumulus.LogDataMessage("Response: " + data);
			cumulus.ImetLoggerInterval = interval;
		}

		private void SetStationClock()
		{
			var datestr = DateTime.Now.ToString("yyyyMMdd");
			var timestr = DateTime.Now.ToString("HHmmss");

			cumulus.LogDataMessage($"WRTM,{datestr},{timestr}");

			SendCommand($"WRTM,{datestr},{timestr}");
			// read the response
			var response = GetResponse("wrtm");

			var data = ExtractText(response, "wrtm");
			cumulus.LogDataMessage("WRTM Response: " + data);
		}

		private void ProgressLogs()
		{
			// advance the pointer
			SendCommand("PRLG,1");
			// read the response
			GetResponse("prlg");
		}

		private void UpdateReadPointer()
		{
			cumulus.LogDebugMessage("Checking the read pointer");
			// If required, update the logger read pointer to match the current write pointer
			// It means the read pointer will always point to the last live record we read.
			SendCommand("RDST,14");
			// read the response
			var response1 = GetResponse("rdst");
			if (ValidChecksum(response1))
			{
				try
				{
					// Response: rdst,adr,dat
					// split the data
					var sl = new List<string>(response1.Split(','));
					var currPtr = sl[2];

					if (currentWritePointer.Equals(currPtr))
						return;

					// The write pointer does not equal the read pointer
					// write it back to the logger memory
					cumulus.LogDebugMessage($"Updating logger read pointer to {currPtr}");
					SendCommand("WRST,13," + currPtr);
					var response2 = GetResponse("wrst");
					if (ValidChecksum(response2))
					{
						// and if it all worked, update our pointer record
						currentWritePointer = currPtr;
					}
					else
					{
						cumulus.LogErrorMessage("WRST: Invalid checksum");
					}
				}
				catch
				{
					// do nothing
				}
			}
			else
			{
				cumulus.LogErrorMessage("RDST: Invalid checksum");
			}
		}

		private void SendCommand(string command)
		{
			// First flush the receive buffer
			comport.DiscardInBuffer();
			comport.BaseStream.Flush();

			// Send the command
			cumulus.LogDebugMessage("Sending: " + command);
			comport.Write(command + sLineBreak);

			// Flush the first response - should be the echo of the command
			try
			{
				cumulus.LogDebugMessage("Discarding input: " + comport.ReadTo(sLineBreak));
			}
			catch
			{
				// probably a timeout - do nothing.
			}
			finally
			{
				Thread.Sleep(cumulus.ImetOptions.WaitTime);
			}
		}

		private string GetResponse(string expected)
		{
			var response = string.Empty;
			var attempts = 0;

			// The Instromet is odd, in that the serial connection is configured for human interaction rather than machine.
			// Command to logger...
			//    RDLG,58<CR><LF>
			// What is sent back...
			//    RDLG,58<CR><LF>
			//    rdlg,1,2,3,4,5,6,7,8,9,123<CR><LF>
			//    <CR><LF>
			//    >

			try
			{
				do
				{
					attempts++;
					cumulus.LogDataMessage("Reading response from station, attempt " + attempts);
					response = comport.ReadTo(sLineBreak);
					cumulus.LogDataMessage($"Response from station: '{response}'");
				} while (!response.Contains(expected) && attempts < 6);

				// If we got the response and didn't time out, then wait for the command prompt before
				// returning so we know the logger is ready for the next command
				if (response.Contains(expected) && attempts < 6)
				{
					comport.ReadTo(">"); // just discard this
				}
			}
			catch
			{
				// Probably a timeout, just exit
			}

			return response;
		}

		private List<string> GetArchiveRecord()
		{
			List<string> sl = [];
			cumulus.LogMessage("Get next log - RDLG,1");
			// request the archive data
			SendCommand("RDLG,1");
			// read the response
			var response = GetResponse("rdlg");
			// extract the bit we want from all the other crap (echo, newlines, prompt etc)
			var data = ExtractText(response, "rdlg");
			cumulus.LogMessage(data);

			if (ValidChecksum(data))
			{
				try
				{
					// split the data
					sl = new List<string>(data.Split(','));
				}
				catch
				{
					// do nothing
				}
			}

			return sl;
		}

		private int GetNumberOfLogs()
		{
			var attempts = 0;
			var num = 0;
			bool valid;
			string data;
			do
			{
				attempts++;

				// read number of available archive entries
				SendCommand("LGCT");
				cumulus.LogMessage("Obtaining log count");
				// read the response
				var response = GetResponse("lgct");
				// extract the bit we want from all the other crap (echo, newlines, prompt etc)
				data = ExtractText(response, "lgct");
				cumulus.LogDataMessage("Response from LGCT=" + data);
				valid = ValidChecksum(data);
				cumulus.LogDebugMessage(valid ? "Checksum valid" : "!!! Checksum invalid !!!");
			} while (!valid && attempts < 3);

			if (valid)
			{
				num = 0;
				try
				{
					// split the data
					var st = new List<string>(data.Split(','));

					if (st[1].Length > 0)
					{
						num = Convert.ToInt32(st[1]);
					}
				}
				catch
				{
					num = 0;
				}
			}
			else
			{
				cumulus.LogErrorMessage("Unable to read log count");
			}

			return num;
		}

		private static bool ValidChecksum(string str)
		{
			try
			{
				// split the data
				var sl = new List<string>(str.Split(','));

				// get number of fields in string
				var len = sl.Count;
				// checksum is last field
				var csum = Convert.ToInt32(sl[len - 1]);

				// calculate checksum of string
				uint sum = 0;
				var endpos = str.LastIndexOf(',');

				for (var i = 0; i <= endpos; i++)
				{
					sum = (sum + str[i]) % 256;
				}

				// 8-bit 1's complement
				sum = ~sum % 256;

				return sum == csum;
			}
			catch
			{
				return false;
			}
		}

		private static string ExtractText(string input, string after)
		{
			// return string after supplied string
			// used for extracting actual response from reply from station
			// assumes that the terminating CRLF is not present, as
			// readto() should have stripped this off
			var pos1 = input.IndexOf(after);
			return pos1 >= 0 ? input[pos1..] : "";
		}

		public override void startReadingHistoryData()
		{
			cumulus.LogMessage("Start reading history data");
			Cumulus.LogConsoleMessage("Start reading history data...");

			// use the wind speeds averages from the logger data
			cumulus.StationOptions.CalcuateAverageWindSpeed = false;
			cumulus.StationOptions.UseSpeedForAvgCalc = true;

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			bw = new BackgroundWorker();
			bw.DoWork += new DoWorkEventHandler(bw_DoWork);
			bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();
		}

		public override void Stop()
		{
			stop = true;
			StopMinuteTimer();
		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			// normal running
			cumulus.StationOptions.CalcuateAverageWindSpeed = true;
			cumulus.StationOptions.UseSpeedForAvgCalc = false;

			cumulus.NormalRunning = true;
			cumulus.LogMessage("Archive reading thread completed");
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();
			StartLoop();
		}

		private void bw_DoWork(object sender, DoWorkEventArgs e)
		{
			getAndProcessHistoryData();
			// Do it again in case it took a long time and there are new entries
			getAndProcessHistoryData();
		}

		public override void getAndProcessHistoryData()
		{
			// Positions of fields in logger data
			//const int IDPOS = 1
			//const int TYPEPOS = 2
			const int INTERVALPOS = 3;
			const int TIMEPOS = 4;
			const int DATEPOS = 5;
			//const int TEMP1MINPOS = 6
			//const int TEMP1MAXPOS = 7
			const int TEMP1AVGPOS = 8;
			//const int TEMP2MINPOS = 9
			//const int TEMP2MAXPOS = 10
			const int TEMP2AVGPOS = 11;
			//const int RELHUMMINPOS = 12
			//const int RELHUMMAXPOS = 13
			const int RELHUMAVGPOS = 14;
			//const int PRESSMINPOS = 15
			//const int PRESSMAXPOS = 16
			const int PRESSAVGPOS = 17;
			//const int WINDMINPOS = 18
			const int WINDMAXPOS = 19;
			const int WINDAVGPOS = 20;
			const int DIRPOS = 21;
			const int SUNPOS = 22;
			const int RAINPOS = 23;

			var timestamp = DateTime.MinValue;

			var provider = new NumberFormatInfo { NumberDecimalSeparator = "." };

			var startfrom = cumulus.LastUpdateTime;
			var startindex = 0;
			var year = startfrom.Year;
			var month = startfrom.Month;
			var day = startfrom.Day;
			var hour = startfrom.Hour;
			var minute = startfrom.Minute;
			var sec = startfrom.Second;

			cumulus.LogMessage($"Last update time = {year}/{month}/{day} {hour}:{minute}:{sec}");

			var recordsdone = 0;

			if (FirstRun)
			{
				// First time Cumulus has run, "delete" all the log entries as there may be
				// vast numbers and they will take hours to download only to be discarded

				// Do it by updating the read pointer to match the write pointer
				// The recorded value for currentWritePointer will not have been set yet
				UpdateReadPointer();
			}

			cumulus.LogMessage("Downloading history from " + startfrom);
			Cumulus.LogConsoleMessage("Reading archive data from " + startfrom + " - please wait");
			var numrecs = GetNumberOfLogs();
			cumulus.LogMessage("Logs available = " + numrecs);
			if (numrecs > 0)
			{
				cumulus.LogMessage("Number of history records = " + numrecs);
				// get the earliest record
				var sl = GetArchiveRecord();
				bool dataOK;
				try
				{
					hour = Convert.ToInt32(sl[TIMEPOS][..2]);
					minute = Convert.ToInt32(sl[TIMEPOS].Substring(3, 2));
					sec = Convert.ToInt32(sl[TIMEPOS].Substring(6, 2));
					day = Convert.ToInt32(sl[DATEPOS][..2]);
					month = Convert.ToInt32(sl[DATEPOS].Substring(3, 2));
					year = Convert.ToInt32(sl[DATEPOS].Substring(6, 4));
					cumulus.LogMessage("Logger entry : Y = " + year + ", M = " + month + ", D = " + day + ", h = " + hour + ", m = " + minute + ", s = " + sec);

					timestamp = new DateTime(year, month, day, hour, minute, sec, 0, DateTimeKind.Local);
					dataOK = true;
				}
				catch
				{
					cumulus.LogErrorMessage("Error in earliest timestamp, unable to process logger data");
					dataOK = false;
				}

				if (dataOK)
				{
					cumulus.LogMessage("Earliest timestamp " + timestamp);
					if (timestamp < cumulus.LastUpdateTime)
					{
						cumulus.LogMessage("-----Earliest timestamp is earlier than required");
						cumulus.LogMessage("-----Find first entry after " + cumulus.LastUpdateTime);
						startindex++; //  to allow for first log already read
						while (startindex < numrecs && timestamp <= cumulus.LastUpdateTime)
						{
							// Move on to next entry
							ProgressLogs();
							sl = GetArchiveRecord();
							try
							{
								hour = Convert.ToInt32(sl[TIMEPOS][..2]);
								minute = Convert.ToInt32(sl[TIMEPOS].Substring(3, 2));
								sec = Convert.ToInt32(sl[TIMEPOS].Substring(6, 2));
								day = Convert.ToInt32(sl[DATEPOS][..2]);
								month = Convert.ToInt32(sl[DATEPOS].Substring(3, 2));
								year = Convert.ToInt32(sl[DATEPOS].Substring(6, 4));
								cumulus.LogMessage("Logger entry zero: Y = " + year + ", M = " + month + ", D = " + day + ", h = " + hour + ", m = " + minute + ", s = " + sec);

								timestamp = new DateTime(year, month, day, hour, minute, sec, 0, DateTimeKind.Local);
								cumulus.LogMessage("New earliest timestamp " + timestamp);
							}
							catch (Exception E)
							{
								cumulus.LogErrorMessage("Error in timestamp, skipping entry. Error = " + E.Message);
								timestamp = DateTime.MinValue;
							}

							startindex++;
						}
					}
				}

				if (startindex < numrecs)
				{
					// We still have entries to process
					cumulus.LogMessage("-----Actual number of valid history records = " + (numrecs - startindex));
					// Compare earliest timestamp with the update time of the today file
					// and see if (they are on the same day

					// set up controls for end of day roll-over
					int rollHour;
					if (cumulus.RolloverHour == 0)
					{
						rollHour = 0;
					}
					else if (cumulus.Use10amInSummer && TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now))
					{
						// Locale is currently on Daylight time
						rollHour = cumulus.RolloverHour + 1;
					}
					else
					{
						// Locale is currently on Standard time or unknown
						rollHour = cumulus.RolloverHour;
					}

					// Check to see if (today"s roll-over has been done
					// (we might be starting up in the roll-over hour)

					var luhour = cumulus.LastUpdateTime.Hour;

					var rolloverdone = luhour == rollHour;

					var midnightraindone = luhour == 0;
					var rollover9amdone = luhour == 9;
					var snowhourdone = luhour == cumulus.SnowDepthHour;

					for (var i = startindex; i < numrecs; i++)
					{
						try
						{
							recordsdone++;
							sl = GetArchiveRecord();
							ProgressLogs();

							hour = Convert.ToInt32(sl[TIMEPOS][..2]);
							minute = Convert.ToInt32(sl[TIMEPOS].Substring(3, 2));
							sec = Convert.ToInt32(sl[TIMEPOS].Substring(6, 2));
							day = Convert.ToInt32(sl[DATEPOS][..2]);
							month = Convert.ToInt32(sl[DATEPOS].Substring(3, 2));
							year = Convert.ToInt32(sl[DATEPOS].Substring(6, 4));
							timestamp = new DateTime(year, month, day, hour, minute, sec, DateTimeKind.Local);
							cumulus.LogMessage("Processing logger data entry " + i + " for " + timestamp);
							DataDateTime = timestamp;

							var interval = (int) (Convert.ToDouble(sl[INTERVALPOS], provider) / 60);

							if (sl[RELHUMAVGPOS].Length > 0)
							{
								DoOutdoorHumidity((int) Convert.ToDouble(sl[RELHUMAVGPOS], provider), timestamp);
							}

							if (sl[WINDAVGPOS].Length > 0 && sl[WINDMAXPOS].Length > 0 && sl[DIRPOS].Length > 0)
							{
								var windspeed = Convert.ToDouble(sl[WINDAVGPOS], provider);
								var windgust = Convert.ToDouble(sl[WINDMAXPOS], provider);
								var windbearing = Convert.ToInt32(sl[DIRPOS]);

								DoWind(windgust, windbearing, windspeed, timestamp);

								// add in "archivePeriod" minutes worth of wind speed to windrun
								WindRunToday += WindAverage * WindRunHourMult[cumulus.Units.Wind] * interval / 60.0;

								DateTime windruncheckTS;
								if (hour == rollHour && minute == 0)
								// this is the last logger entry before roll-over
								// fudge the timestamp to make sure it falls in the previous day
								{
									windruncheckTS = timestamp.AddMinutes(-1);
								}
								else
								{
									windruncheckTS = timestamp;
								}

								CheckForWindrunHighLow(windruncheckTS);

								// update dominant wind bearing
								CalculateDominantWindBearing(Bearing, WindAverage, interval);
							}

							if (sl[TEMP1AVGPOS].Length > 0)
							{
								DoOutdoorTemp(ConvertUnits.TempCToUser(Convert.ToDouble(sl[TEMP1AVGPOS], provider)), timestamp);

								// add in "archivePeriod" minutes worth of temperature to the temp samples
								tempsamplestoday += interval;
								TempTotalToday += OutdoorTemperature * interval;

								// update chill hours
								if (OutdoorTemperature < cumulus.ChillHourThreshold && OutdoorTemperature > cumulus.ChillHourBase)
								{
									// add 1 minute to chill hours
									ChillHours += interval / 60.0;
								}

								// update heating/cooling degree days
								UpdateDegreeDays(interval);
							}

							if (sl[TEMP2AVGPOS].Length > 0)
							{
								var temp2 = Convert.ToDouble(sl[TEMP2AVGPOS], provider);
								// supply in CELSIUS
								if (cumulus.StationOptions.LogExtraSensors)
								{
									DoExtraTemp(temp2, 1);
								}
								else
								{
									DoWetBulb(temp2, timestamp);
								}
							}

							if (sl[RAINPOS].Length > 0)
							{
								var raintotal = Convert.ToDouble(sl[RAINPOS], provider);
								double raindiff;
								if (prevraintotal < 0)
								{
									raindiff = 0;
								}
								else
								{
									raindiff = raintotal - prevraintotal;
								}

								var rainrate = ConvertUnits.RainMMToUser(raindiff * (60.0 / Cumulus.logints[cumulus.DataLogInterval]));

								DoRain(ConvertUnits.RainMMToUser(raintotal), rainrate, timestamp);

								prevraintotal = raintotal;
							}

							if (sl[WINDAVGPOS].Length > 0 && sl[TEMP1AVGPOS].Length > 0)
							{
								// wind chill
								var tempinC = ConvertUnits.UserTempToC(OutdoorTemperature);
								var windinKPH = ConvertUnits.UserWindToKPH(WindAverage);
								var value = MeteoLib.WindChill(tempinC, windinKPH);
								// value is now in Celsius, convert to units in use
								value = ConvertUnits.TempCToUser(value);
								DoWindChill(value, timestamp);
							}

							if (sl[PRESSAVGPOS].Length > 0)
							{
								DoPressure(ConvertUnits.PressMBToUser(Convert.ToDouble(sl[PRESSAVGPOS], provider)), timestamp);
								AltimeterPressure = Pressure;
							}

							// Cause wind chill calc
							DoWindChill(-999, timestamp);

							DoOutdoorDewpoint(-999, timestamp);
							DoApparentTemp(timestamp);
							DoFeelsLike(timestamp);
							DoHumidex(timestamp);
							DoCloudBaseHeatIndex(timestamp);
							DoTrendValues(timestamp);

							if (cumulus.StationOptions.CalculatedET && timestamp.Minute == 0)
							{
								// Start of a new hour, and we want to calculate ET in Cumulus
								CalculateEvapotranspiration(timestamp);
							}

							// sunshine hours
							if (sl[SUNPOS].Length > 0)
							{
								DoSunHours(Convert.ToDouble(sl[SUNPOS], provider));
							}

							_ = cumulus.DoLogFile(timestamp, false);
							cumulus.MySqlRealtimeFile(999, false, timestamp);
							cumulus.DoCustomIntervalLogs(timestamp);

							// Custom MySQL update - minutes interval
							if (cumulus.MySqlFuncs.MySqlSettings.CustomMins.Enabled)
							{
								_ = cumulus.CustomMysqlMinutesUpdate(timestamp, false);
							}

							AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
								OutdoorHumidity, Pressure, RainToday, SolarRad, UV, RainCounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate, -1, -1);
							UpdateStatusPanel(timestamp.ToUniversalTime());

							// Add current data to the lists of web service updates to be done
							cumulus.AddToWebServiceLists(timestamp);

							// Check for roll-over

							if (hour != rollHour)
							{
								rolloverdone = false;
							}
							else if (!rolloverdone)
							{
								// do roll-over
								cumulus.LogMessage("Day roll-over " + timestamp);
								DayReset(timestamp);

								rolloverdone = true;
							}


							if (hour != 0)
							{
								midnightraindone = false;
							}
							else if (!midnightraindone)
							{
								ResetMidnightRain(timestamp);
								ResetSunshineHours(timestamp);
								ResetMidnightTemperatures(timestamp);
								midnightraindone = true;
							}

							// 9am rollover items
							if (hour != 9)
							{
								rollover9amdone = false;
							}
							else if (!rollover9amdone)
							{
								Reset9amTemperatures(timestamp);
								rollover9amdone = true;
							}

							// Not in snow hour, snow yet to be done
							if (hour != cumulus.SnowDepthHour)
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
								for (var j = 0; j < Snow24h.Length; j++)
								{
									Snow24h[j] = null;
								}

								snowhourdone = true;
							}
						}
						catch (Exception E)
						{
							cumulus.LogErrorMessage("Error in data: " + E.Message);
						}
					}
				}
				else
				{
					cumulus.LogMessage("No history records to process");
				}
			}
			else
			{
				cumulus.LogMessage("No history records to process");
			}
		}

		public override void Start()
		{
			cumulus.LogMessage("Starting Instromet data reading thread");

			try
			{
				while (!stop)
				{
					ImetGetData();
					if (cumulus.ImetLoggerInterval != Cumulus.logints[cumulus.DataLogInterval])
					{
						// logging interval has changed; update station to match
						ImetSetLoggerInterval(Cumulus.logints[cumulus.DataLogInterval]);
					}
					else
					{
						Thread.Sleep(cumulus.ImetOptions.ReadDelay);
					}
				}
			}
			// Catch the ThreadAbortException
			catch (ThreadAbortException)
			{
				// do nothing
			}
			finally
			{
				comport.Close();
			}
		}

		private void ImetGetData()
		{
			const int TEMP1POS = 1;
			const int TEMP2POS = 2;
			const int RELHUMPOS = 3;
			const int PRESSPOS = 4;
			const int WINDPOS = 5;
			const int DIRPOS = 6;
			const int SUNPOS = 7;
			const int RAINPOS = 8;
			//const int CHECKSUMPOS = 9

			var now = DateTime.Now;

			var h = now.Hour;
			var min = now.Minute;

			if (min != previousminute)
			{
				previousminute = min;

				if (cumulus.StationOptions.SyncTime && h == cumulus.StationOptions.ClockSettingHour && min == 2)
				{
					// It's 0400, set the station clock
					SetStationClock();
				}
			}

			SendCommand("RDLV");
			// read the response
			var response = GetResponse("rdlv");

			if (ValidChecksum(response) && !stop)
			{
				// split the data
				var sl = new List<string>(response.Split(','));

				if (sl.Count != 10 && sl[0] != "rdlv")
				{
					cumulus.LogWarningMessage($"RDLV: Unexpected response: {response}");
					return;
				}

				// Parse data using decimal points rather than user's decimal separator
				var provider = new NumberFormatInfo { NumberDecimalSeparator = "." };

				double windspeed = -999;
				double temp1 = -999;
				var humidity = -999;

				double varDbl;
				int varInt;

				if (!string.IsNullOrEmpty(sl[DIRPOS]) && int.TryParse(sl[DIRPOS], out varInt) &&
					!string.IsNullOrEmpty(sl[WINDPOS]) && double.TryParse(sl[WINDPOS], NumberStyles.Float, provider, out varDbl))
				{
					windspeed = varDbl;
					DoWind(ConvertUnits.WindMSToUser(windspeed), varInt, -1, now);
				}
				else
				{
					cumulus.LogWarningMessage($"RDLV: Unexpected wind dir/speed format, found: {sl[DIRPOS]}/{sl[WINDPOS]}");
				}


				if (!string.IsNullOrEmpty(sl[TEMP1POS]) && double.TryParse(sl[TEMP1POS], NumberStyles.Float, provider, out varDbl))
				{
					temp1 = varDbl;
					DoOutdoorTemp(ConvertUnits.TempCToUser(temp1), now);
					if (windspeed > -99)
					{
						var windchill = MeteoLib.WindChill(temp1, windspeed * 3.6);
						DoWindChill(windchill, now);
					}
				}
				else
				{
					cumulus.LogWarningMessage($"RDLV: Unexpected temperature 1 format, found: {sl[TEMP1POS]}");
				}

				if (!string.IsNullOrEmpty(sl[TEMP2POS]))  // TEMP2 is optional
				{
					if (double.TryParse(sl[TEMP2POS], NumberStyles.Float, provider, out varDbl))
					{
						if (cumulus.StationOptions.LogExtraSensors)
						{
							// use second temp as Extra Temp 1
							DoExtraTemp(ConvertUnits.TempCToUser(varDbl), 1);
						}
						else
						{
							// use second temp as wet bulb
							DoWetBulb(ConvertUnits.TempCToUser(varDbl), now);
						}
					}
					else
					{
						cumulus.LogWarningMessage($"RDLV: Unexpected temperature 2 format, found: {sl[TEMP2POS]}");
					}
				}

				if (!string.IsNullOrEmpty(sl[RELHUMPOS]) && double.TryParse(sl[RELHUMPOS], NumberStyles.Float, provider, out varDbl))
				{
					humidity = Convert.ToInt32(varDbl);
					DoOutdoorHumidity(humidity, now);
				}
				else
				{
					cumulus.LogWarningMessage($"RDLV: Unexpected humidity format, found: {sl[RELHUMPOS]}");
				}

				if (!string.IsNullOrEmpty(sl[PRESSPOS]) && double.TryParse(sl[PRESSPOS], NumberStyles.Float, provider, out varDbl))
				{
					DoPressure(ConvertUnits.PressMBToUser(varDbl), now);
					AltimeterPressure = Pressure;
				}
				else
				{
					cumulus.LogWarningMessage($"RDLV: Unexpected pressure format, found: {sl[PRESSPOS]}");
				}


				if (!string.IsNullOrEmpty(sl[RAINPOS]) && double.TryParse(sl[RAINPOS], NumberStyles.Float, provider, out varDbl))
				{
					DoRain(ConvertUnits.RainMMToUser(varDbl), -1, now);
				}
				else
				{
					cumulus.LogWarningMessage($"RDLV: Unexpected rain format, found: {sl[RAINPOS]}");
				}

				if (!string.IsNullOrEmpty(sl[SUNPOS]) && double.TryParse(sl[SUNPOS], NumberStyles.Float, provider, out varDbl))
				{
					DoSunHours(varDbl);
				}
				else
				{
					cumulus.LogWarningMessage($"RDLV: Unexpected rain format, found: {sl[RAINPOS]}");
				}

				if (temp1 > -999 && humidity > -999)
				{
					DoOutdoorDewpoint(-999, now);
					DoHumidex(now);
					DoCloudBaseHeatIndex(now);

					if (windspeed > -999)
					{
						DoApparentTemp(now);
						DoFeelsLike(now);
					}
				}

				DoForecast("", false);

				UpdateStatusPanel(now.ToUniversalTime());
				UpdateMQTT();
			}
			else
			{
				cumulus.LogErrorMessage("RDLV: Invalid checksum:");
				cumulus.LogMessage(response);
			}

			if (!cumulus.ImetOptions.UpdateLogPointer || stop)
				return;

			// Keep the log pointer current, to avoid large numbers of logs
			// being downloaded at next start-up
			// Only do this every 30 read intervals
			if (readCounter > 0)
			{
				readCounter--;
			}
			else
			{
				UpdateReadPointer();
				readCounter = 30;
			}
		}
	}
}
