using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Timers;
using HidSharp;
using Timer = System.Timers.Timer;

namespace CumulusMX
{
	internal class FOStation : WeatherStation
	{
		//private IDevice[] stations;
		//private IDevice device;

		private readonly DeviceList devicelist;
		private readonly double pressureOffset;
		private HidDevice hidDevice;
		private readonly HidStream stream;
		private List<HistoryData> datalist;

		private readonly int maxHistoryEntries;
		private int prevaddr = -1;
		private double previouspress = 9999;
		private double previousgust = 999;
		private double previouswind = 999;
		private int previoushum = 999;
		private int prevraintotal = -1;
		private int ignoreraincount = 0;
		private double previoustemp = 999;
		private int SynchroPhase = 0;
		private DateTime PreviousSensorClock;
		private DateTime PreviousStationClock;
		private bool Synchronising = false;
		//private DateTime lastraintip;
		//private int raininlasttip = 0;
		private int interval = 0;
		private int followinginterval = 0;
		//private readonly double[] WindRunHourMult = {3.6, 1.0, 1.0, 1.0};
		private readonly Timer tmrDataRead;
		private int ReadCounter;
		private bool hadfirstsyncdata;
		private readonly byte[] prevdata = new byte[16];
		private readonly int FOentrysize;
		private readonly int FOMaxAddr;
		//private int FOmaxhistoryentries;
		private readonly bool hasSolar;
		private bool readingData = false;

		const int defaultVID = 0x1941;
		const int defaultPID = 0x8021;


		internal FOStation(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Manufacturer = cumulus.EW;
			var data = new byte[32];

			tmrDataRead = new Timer();

			calculaterainrate = true;

			hasSolar = cumulus.StationType == StationTypes.FineOffsetSolar;

			if (hasSolar)
			{
				FOentrysize = 0x14;
				FOMaxAddr = 0xFFEC;
				maxHistoryEntries = 3264;
			}
			else
			{
				FOentrysize = 0x10;
				FOMaxAddr = 0xFFF0;
				maxHistoryEntries = 4080;
			}

			devicelist = DeviceList.Local;

			int VID = (cumulus.VendorID < 0 ? defaultVID : cumulus.VendorID);
			int PID = (cumulus.ProductID < 0 ? defaultPID : cumulus.ProductID);

			cumulus.LogMessage("Looking for Fine Offset station, VendorID=0x"+VID.ToString("X4")+" ProductID=0x"+PID.ToString("X4"));
			Console.WriteLine("Looking for Fine Offset station");

			hidDevice = devicelist.GetHidDeviceOrNull(vendorID: VID, productID: PID);

			if (hidDevice != null)
			{
				cumulus.LogMessage("Fine Offset station found");
				Console.WriteLine("Fine Offset station found");

				if (hidDevice.TryOpen(out stream))
				{
					cumulus.LogMessage("Stream opened");
					Console.WriteLine("Connected to station");
					// Get the block of data containing the abs and rel pressures
					cumulus.LogMessage("Reading pressure offset");
					ReadAddress(0x20, data);
					double relpressure = (((data[1] & 0x3f)*256) + data[0])/10.0f;
					double abspressure = (((data[3] & 0x3f)*256) + data[2])/10.0f;
					pressureOffset = relpressure - abspressure;
					cumulus.LogMessage("Rel pressure      = " + relpressure);
					cumulus.LogMessage("Abs pressure      = " + abspressure);
					cumulus.LogMessage("Calculated Offset = " + pressureOffset);
					if (cumulus.EWpressureoffset < 9999.0)
					{
						cumulus.LogMessage("Ignoring calculated offset, using offset value from cumulus.ini file");
						cumulus.LogMessage("EWpressureoffset = " + cumulus.EWpressureoffset);
						pressureOffset = cumulus.EWpressureoffset;
					}

					// Read the data from the logger
					startReadingHistoryData();
				}
				else
				{
					cumulus.LogMessage("Stream open failed");
					Console.WriteLine("Unable to connect to station");
				}
			}
			else
			{
				cumulus.LogMessage("Fine Offset station not found");
				Console.WriteLine("Fine Offset station not found");
			}
		}

		public override void startReadingHistoryData()
		{
			cumulus.CurrentActivity = "Getting archive data";
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
			cumulus.LogMessage("Stopping data read timer");
			tmrDataRead.Stop();
			cumulus.LogMessage("Stopping minute timer");
			StopMinuteTimer();
			cumulus.LogMessage("Nulling hidDevice");
			hidDevice = null;
			cumulus.LogMessage("Exit FOStation.Stop()");
		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			cumulus.CurrentActivity = "Normal running";
			cumulus.LogMessage("Archive reading thread completed");
			Start();
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimers();
		}


		private void bw_DoWork(object sender, DoWorkEventArgs e)
		{
			//var ci = new CultureInfo("en-GB");
			//System.Threading.Thread.CurrentThread.CurrentCulture = ci;
			cumulus.LogDebugMessage("Lock: Station waiting for the lock");
			Cumulus.syncInit.Wait();
			cumulus.LogDebugMessage("Lock: Station has the lock");
			try
			{
				getAndProcessHistoryData();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Exception occurred reading archive data: " + ex.Message);
			}
			cumulus.LogDebugMessage("Lock: Station releasing the lock");
			Cumulus.syncInit.Release();
		}

		public override void getAndProcessHistoryData()
		{
			var data = new byte[32];
			cumulus.LogMessage("Current culture: " + CultureInfo.CurrentCulture.DisplayName);
			//DateTime now = DateTime.Now;
			cumulus.LogMessage(DateTime.Now.ToString("G"));
			cumulus.LogMessage("Start reading history data");
			Console.WriteLine("Downloading Archive Data");
			DateTime timestamp = DateTime.Now;
			//LastUpdateTime = DateTime.Now; // lastArchiveTimeUTC.ToLocalTime();
			cumulus.LogMessage("Last Update = " + cumulus.LastUpdateTime);
			ReadAddress(0, data);

			// get address of current location
			int addr = ((data[31])*256) + data[30];
			//int previousaddress = addr;

			cumulus.LogMessage("Reading current address " + addr.ToString("X4"));
			ReadAddress(addr, data);

			bool moredata = true;

			datalist = new List<HistoryData>();

			while (moredata)
			{
				followinginterval = interval;
				interval = data[0];

				// calculate timestamp of previous history data
				timestamp = timestamp.AddMinutes(-interval);

				if ((interval != 255) && (timestamp > cumulus.LastUpdateTime) && (datalist.Count < maxHistoryEntries - 2))
				{
					// Read previous data
					addr -= FOentrysize;
					if (addr == 0xF0) addr = FOMaxAddr; // wrap around

					ReadAddress(addr, data);


					// add history data to collection

					var histData = new HistoryData();
					string msg = "Read logger entry for " + timestamp + " address " + addr.ToString("X4");
					int numBytes = hasSolar ? 20 : 16;

					cumulus.LogMessage(msg);

					msg = "Data block: ";

					for (int i = 0; i < numBytes; i++)
					{
						msg += data[i].ToString("X2");
						msg += " ";
					}

					cumulus.LogDataMessage(msg);

					histData.timestamp = timestamp;
					histData.interval = interval;
					histData.followinginterval = followinginterval;
					if (data[1] == 255)
					{
						histData.inHum = 10;
					}
					else
					{
						histData.inHum = data[1];
					}

					if (data[4] == 255)
					{
						histData.outHum = 10;
					}
					else
					{
						histData.outHum = data[4];
					}
					double outtemp = ((data[5]) + (data[6] & 0x7F)*256)/10.0f;
					var sign = (byte) (data[6] & 0x80);
					if (sign == 0x80) outtemp = -outtemp;
					if (outtemp > -200) histData.outTemp = outtemp;
					histData.windGust = (data[10] + ((data[11] & 0xF0)*16))/10.0f;
					histData.windSpeed = (data[9] + ((data[11] & 0x0F)*256))/10.0f;
					histData.windBearing = (int) (data[12]*22.5f);

					histData.rainCounter = data[13] + (data[14]*256);

					double intemp = ((data[2]) + (data[3] & 0x7F)*256)/10.0f;
					sign = (byte) (data[3] & 0x80);
					if (sign == 0x80) intemp = -intemp;
					histData.inTemp = intemp;
					// Get pressure and convert to sea level
					histData.pressure = (data[7] + (data[8]*256))/10.0f + pressureOffset;
					histData.SensorContactLost = (data[15] & 0x40) == 0x40;
					if (hasSolar)
					{
						histData.uvVal = data[19];

						histData.solarVal = (data[16] + (data[17]*256) + (data[18]*65536))/10.0;
					}

					datalist.Add(histData);

					bw.ReportProgress(datalist.Count, "collecting");
				}
				else
				{
					moredata = false;
				}
			}

			cumulus.LogMessage("Number of history entries = " + datalist.Count);

			if (datalist.Count > 0)
			{
				processHistoryData();
			}

			//using (cumulusEntities dataContext = new cumulusEntities())
			//{
			//    UpdateHighsAndLows(dataContext);
			//}
		}

		private void processHistoryData()
		{
			int totalentries = datalist.Count;

			cumulus.LogMessage("Processing history data, number of entries = " + totalentries);

			int rollHour = Math.Abs(cumulus.GetHourInc());

			int luhour = cumulus.LastUpdateTime.Hour;

			bool rolloverdone = luhour == rollHour;

			bool midnightraindone = luhour == 0;

			while (datalist.Count > 0)
			{
				HistoryData historydata = datalist[datalist.Count - 1];

				DateTime timestamp = historydata.timestamp;

				cumulus.LogMessage("Processing data for " + timestamp);

				int h = timestamp.Hour;

				//  if outside rollover hour, rollover yet to be done
				if (h != rollHour)
				{
					rolloverdone = false;
				}

				// In rollover hour and rollover not yet done
				if ((h == rollHour) && !rolloverdone)
				{
					// do rollover
					cumulus.LogMessage("Day rollover " + timestamp.ToShortTimeString());
					DayReset(timestamp);

					rolloverdone = true;
				}

				// Not in midnight hour, midnight rain yet to be done
				if (h != 0)
				{
					midnightraindone = false;
				}

				// In midnight hour and midnight rain (and sun) not yet done
				if ((h == 0) && !midnightraindone)
				{
					ResetMidnightRain(timestamp);
					ResetSunshineHours();
					midnightraindone = true;
				}

				// Indoor Humidity ======================================================
				if ((historydata.inHum > 100) && (historydata.inHum != 255))
				{
					cumulus.LogMessage("Ignoring bad data: inhum = " + historydata.inHum);
				}
				else if ((historydata.inHum > 0) && (historydata.inHum != 255))
				{
					// 255 is the overflow value, when RH gets below 10% - ignore
					DoIndoorHumidity(historydata.inHum);
				}

				// Indoor Temperature ===================================================
				if ((historydata.inTemp > -50) && (historydata.inTemp < 50))
				{
					DoIndoorTemp(ConvertTempCToUser(historydata.inTemp));
				}

				// Pressure =============================================================

				if ((historydata.pressure < cumulus.EWminpressureMB) || (historydata.pressure > cumulus.EWmaxpressureMB))
				{
					cumulus.LogMessage("Ignoring bad data: pressure = " + historydata.pressure);
					cumulus.LogMessage("                   offset = " + pressureOffset);
				}
				else
				{
					DoPressure(ConvertPressMBToUser(historydata.pressure), timestamp);
				}

				if (historydata.SensorContactLost)
				{
					cumulus.LogMessage("Sensor contact lost; ignoring outdoor data");
				}
				else
				{
					// Outdoor Humidity =====================================================
					if ((historydata.outHum > 100) && (historydata.outHum != 255))
					{
						cumulus.LogMessage("Ignoring bad data: outhum = " + historydata.outHum);
					}
					else if ((historydata.outHum > 0) && (historydata.outHum != 255))
					{
						// 255 is the overflow value, when RH gets below 10% - ignore
						DoOutdoorHumidity(historydata.outHum, timestamp);
					}

					// Wind =================================================================
					if ((historydata.windGust > 60) || (historydata.windGust < 0))
					{
						cumulus.LogMessage("Ignoring bad data: gust = " + historydata.windGust);
					}
					else if ((historydata.windSpeed > 60) || (historydata.windSpeed < 0))
					{
						cumulus.LogMessage("Ignoring bad data: speed = " + historydata.windSpeed);
					}
					{
						DoWind(ConvertWindMSToUser(historydata.windGust), historydata.windBearing, ConvertWindMSToUser(historydata.windSpeed), timestamp);
					}

					// Outdoor Temperature ==================================================
					if ((historydata.outTemp < -50) || (historydata.outTemp > 70))
					{
						cumulus.LogMessage("Ignoring bad data: outtemp = " + historydata.outTemp);
					}
					else
					{
						DoOutdoorTemp(ConvertTempCToUser(historydata.outTemp), timestamp);
						// add in 'archivePeriod' minutes worth of temperature to the temp samples
						tempsamplestoday += historydata.interval;
						TempTotalToday += (OutdoorTemperature*historydata.interval);
					}

					// update chill hours
					if (OutdoorTemperature < cumulus.ChillHourThreshold)
						// add 1 minute to chill hours
						ChillHours += (historydata.interval/60);

					int raindiff;
					if (prevraintotal == -1)
					{
						raindiff = 0;
					}
					else
					{
						raindiff = historydata.rainCounter - prevraintotal;
					}

					// record time of last rain tip, to use in
					// normal running rain rate calc NB rain rate calc not currently used
					/*
					if (raindiff > 0)
					{
						lastraintip = timestamp;

						raininlasttip = raindiff;
					}
					else
					{
						lastraintip = DateTime.MinValue;

						raininlasttip = 0;
					}
					*/
					double rainrate;

					if (raindiff > 100)
					{
						cumulus.LogMessage("Warning: large increase in rain gauge tip count: " + raindiff);
						rainrate = 0;
					}
					else
					{
						if (historydata.interval > 0)
							rainrate = ConvertRainMMToUser((raindiff*0.3)*(60.0/historydata.interval));
						else
							rainrate = 0;
					}

					DoRain(ConvertRainMMToUser(historydata.rainCounter*0.3), rainrate, timestamp);

					prevraintotal = historydata.rainCounter;

					OutdoorDewpoint = ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(OutdoorTemperature), OutdoorHumidity));

					CheckForDewpointHighLow(timestamp);


					// calculate wind chill

					if (ConvertUserWindToMS(WindAverage) < 1.5)
					{
						DoWindChill(OutdoorTemperature, timestamp);
					}
					else
					{
						// calculate wind chill from calibrated C temp and calibrated win in KPH
						DoWindChill(ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToKPH(WindAverage))), timestamp);
					}

					DoApparentTemp(timestamp);

					if (hasSolar)
					{
						if (historydata.uvVal == 255)
						{
							// ignore
						}
						else if (historydata.uvVal < 0)
							DoUV(0, timestamp);
						else if (historydata.uvVal > 16)
							DoUV(16, timestamp);
						else
							DoUV(historydata.uvVal, timestamp);

						if ((historydata.solarVal >= 0) && (historydata.solarVal <= 300000))
						{
							DoSolarRad((int) Math.Floor(historydata.solarVal*cumulus.LuxToWM2), timestamp);

							// add in archive period worth of sunshine, if sunny
							if ((SolarRad > CurrentSolarMax*cumulus.SunThreshold/100) && (SolarRad >= cumulus.SolarMinimum))
								SunshineHours += (historydata.interval/60.0);

							LightValue = historydata.solarVal;
						}
					}
				}
				// add in 'following interval' minutes worth of wind speed to windrun
				cumulus.LogMessage("Windrun: " + WindAverage.ToString(cumulus.WindFormat) + cumulus.WindUnitText + " for " + historydata.followinginterval + " minutes = " +
								(WindAverage*WindRunHourMult[cumulus.WindUnit]*historydata.followinginterval/60.0).ToString(cumulus.WindRunFormat) + cumulus.WindRunUnitText);

				WindRunToday += (WindAverage*WindRunHourMult[cumulus.WindUnit]*historydata.followinginterval/60.0);

				// update heating/cooling degree days
				UpdateDegreeDays(historydata.interval);

				// update dominant wind bearing
				CalculateDominantWindBearing(Bearing, WindAverage, historydata.interval);

				CheckForWindrunHighLow(timestamp);


				bw.ReportProgress((totalentries - datalist.Count)*100/totalentries, "processing");

				//UpdateDatabase(timestamp.ToUniversalTime(), historydata.interval, false);

				cumulus.DoLogFile(timestamp,false);
				if (cumulus.LogExtraSensors)
				{
					cumulus.DoExtraLogFile(timestamp);
				}

				AddLastHourDataEntry(timestamp, Raincounter, OutdoorTemperature);
				AddGraphDataEntry(timestamp, Raincounter, RainToday, RainRate, OutdoorTemperature, OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex,
					IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing, OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV);
				AddLast3HourDataEntry(timestamp, Pressure, OutdoorTemperature);
				AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
					OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter);
				RemoveOldLHData(timestamp);
				RemoveOldL3HData(timestamp);
				RemoveOldGraphData(timestamp);
				DoTrendValues(timestamp);
				UpdatePressureTrendString();
				UpdateStatusPanel(timestamp);
				cumulus.AddToWebServiceLists(timestamp);
				datalist.RemoveAt(datalist.Count - 1);
			}
			cumulus.LogMessage("End processing history data");
		}

		public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		///     Read and process data in a loop, sleeping between reads
		/// </summary>
		/// <param name="serialPort">The serial port that the station is connected to</param>
		public override void Start()
		{
			tmrDataRead.Elapsed += DataReadTimerTick;
			tmrDataRead.Interval = 10000;
			tmrDataRead.Enabled = true;
		}

		/// <summary>
		///     Read the 32 bytes starting at 'address'
		/// </summary>
		/// <param name="address">The address of the data</param>
		/// <param name="buff">Where to return the data</param>
		private void ReadAddress(int address, byte[] buff)
		{
			//cumulus.LogMessage("Reading address " + address.ToString("X6"));
			var lowbyte = (byte) (address & 0xFF);
			var highbyte = (byte) (address >> 8);

			byte[] request;
			// Returns 9-byte usb packet, with report ID in first byte
			var response = new byte[9];
			int responseLength = 9;
			int startByte = 1;

			request = new byte[] {0, 0xa1, highbyte, lowbyte, 0x20, 0xa1, highbyte, lowbyte, 0x20};

			int ptr = 0;
			String rec_data;


			if (hidDevice != null)
			{
				//response = device.WriteRead(0x00, request);
				stream.Write(request);
				Thread.Sleep(cumulus.FineOffsetReadTime);
				for (int i = 1; i < 5; i++)
				{
					//cumulus.LogMessage("Reading 8 bytes");
					try
					{
						int count = stream.Read(response, 0, responseLength);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error reading data from station - it may need resetting");
						cumulus.LogMessage(ex.Message);
						cumulus.LogMessage("Error reading data from station - it may need resetting");
					}

					rec_data = " Data" + i + ": ";
					for (int j = startByte; j < responseLength; j++)
					{
						rec_data += response[j].ToString("X2");
						rec_data += " ";
						buff[ptr++] = response[j];
					}
					cumulus.LogDataMessage(rec_data);
				}
			}
		}

		private void DataReadTimerTick(object state, ElapsedEventArgs elapsedEventArgs)
		{
			if (!readingData)
			{
				readingData = true;
				GetAndProcessData();
				readingData = false;
			}
		}

		/// <summary>
		///     Read current data and process it
		/// </summary>
		private void GetAndProcessData()
		{
			//   Curr Reading Loc
			// 0  Time Since Last Save
			// 1  Hum In
			// 2  Temp In
			// 3  "
			// 4  Hum Out
			// 5  Temp Out
			// 6  "
			// 7  Pressure
			// 8  "
			// 9  Wind Speed m/s
			// 10  Wind Gust m/s
			// 11  Speed and Gust top nibbles (Gust top nibble)
			// 12  Wind Dir
			// 13  Rain counter
			// 14  "
			// 15  status

			// 16 Solar (Lux)
			// 17 "
			// 18 "
			// 19 UV

			//var ci = new CultureInfo("en-GB");
			//System.Threading.Thread.CurrentThread.CurrentCulture = ci;


			var data = new byte[32];

			if (cumulus.SyncFOReads && !Synchronising)
			{
				if ((DateTime.Now - FOSensorClockTime).TotalDays > 1)
				{
					// (re)synchronise data reads to try to avoid USB lockup problem

					StartSynchronising();

					return;
				}
				else
				{
					// Check that were not within N seconds of the station updating memory
					bool sensorclockOK = ((int)(Math.Floor((DateTime.Now - FOSensorClockTime).TotalSeconds))%48 >= (cumulus.FOReadAvoidPeriod - 1)) &&
										 ((int)(Math.Floor((DateTime.Now - FOSensorClockTime).TotalSeconds))%48 <= (47 - cumulus.FOReadAvoidPeriod));
					bool stationclockOK = ((int)(Math.Floor((DateTime.Now - FOStationClockTime).TotalSeconds))%60 >= (cumulus.FOReadAvoidPeriod - 1)) &&
										  ((int)(Math.Floor((DateTime.Now - FOStationClockTime).TotalSeconds))%60 <= (59 - cumulus.FOReadAvoidPeriod));

					if (!sensorclockOK || !stationclockOK)
					{
						if (!sensorclockOK)
						{
							cumulus.LogDebugMessage("Within "+cumulus.FOReadAvoidPeriod+" seconds of sensor data change, skipping read");
						}

						if (!stationclockOK)
						{
							cumulus.LogDebugMessage("Within " + cumulus.FOReadAvoidPeriod + " seconds of station clock minute change, skipping read");
						}

						return;
					}
				}
			}

			// get the block of memory containing the current data location

			if (cumulus.DataLogging)
			{
				cumulus.LogMessage("Reading first block");
			}
			ReadAddress(0, data);

			int addr = (data[31]*256) + data[30];

			if (cumulus.DataLogging)
			{
				cumulus.LogMessage("First block read, addr = " + addr.ToString("X8"));
			}

			if (addr != prevaddr)
			{
				// location has changed, skip this read to give it chance to update
				//cumulus.LogMessage("Location changed, skipping");
				if (SynchroPhase == 2)
				{
					cumulus.LogDebugMessage("Address changed");
					cumulus.LogDebugMessage("addr=" + addr.ToString("X4") + "previous=" + prevaddr.ToString("X4"));
					FOStationClockTime = DateTime.Now;
					StopSynchronising();
				}
			}
			else
			{
				if (cumulus.DataLogging)
				{
					cumulus.LogMessage("Reading data, addr = " + addr.ToString("X8"));
				}

				ReadAddress(addr, data);

				if (cumulus.DataLogging)
				{
					cumulus.LogMessage("Data read - " + BitConverter.ToString(data));
				}

				DateTime now = DateTime.Now;

				if (Synchronising)
				{
					if (SynchroPhase == 1)
					{
						// phase 1 - sensor clock
						bool datachanged = false;
						// ReadCounter determines whether we actually process the data (every 10 seconds)
						ReadCounter++;
						if (hadfirstsyncdata)
						{
							for (int i = 0; i < 16; i++)
							{
								if (prevdata[i] != data[i])
								{
									datachanged = true;
								}
							}

							if (datachanged)
							{
								FOSensorClockTime = DateTime.Now;
								SynchroPhase = 2;
							}
						}
					}
					else
					{
						// Phase 2 - station clock
						ReadCounter++;
					}
				}

				hadfirstsyncdata = true;
				for (int i = 0; i < 16; i++)
				{
					prevdata[i] = data[i];
				}

				if ((!Synchronising) || ((ReadCounter%20) == 0))
				{
					LatestFOReading = addr.ToString("X4") + ":";
					for (int i = 0; i < 16; i++)
					{
						LatestFOReading = LatestFOReading + " " + data[i].ToString("X2");
					}

					if (cumulus.DataLogging)
					{
						cumulus.LogMessage(LatestFOReading);
					}

					// Indoor Humidity ====================================================
					int inhum = data[1];
					if ((inhum > 100) && (inhum != 255))
					{
						// bad value
						cumulus.LogMessage("Ignoring bad data: inhum = " + inhum);
					}
					else
					{
						// 255 is the overflow value, when RH gets below 10% - use 10%
						if (inhum == 255)
						{
							inhum = 10;
						}

						if (inhum > 0)
						{
							DoIndoorHumidity(inhum);
						}
					}

					// Indoor temperature ===============================================
					double intemp = ((data[2]) + (data[3] & 0x7F)*256)/10.0f;
					var sign = (byte) (data[3] & 0x80);
					if (sign == 0x80)
					{
						intemp = -intemp;
					}

					if ((intemp > -50) && (intemp < 50))
					{
						DoIndoorTemp(ConvertTempCToUser(intemp));
					}

					// Pressure =========================================================
					double pressure = (data[7] + ((data[8] & 0x3f)*256))/10.0f + pressureOffset;

					if ((pressure < cumulus.EWminpressureMB) || (pressure > cumulus.EWmaxpressureMB))
					{
						// bad value
						cumulus.LogMessage("Ignoring bad data: pressure = " + pressure.ToString());
						cumulus.LogMessage("                   offset = " + pressureOffset.ToString());
					}
					else
					{
						if ((previouspress == 9999) || (Math.Abs(pressure - previouspress) < cumulus.EWpressurediff))
						{
							previouspress = pressure;

							DoPressure(ConvertPressMBToUser(pressure), now);
							// Get station pressure in hPa by subtracting offset and calibrating
							// EWpressure offset is difference between rel and abs in hPa
							// PressOffset is user calibration in user units.
							pressure = (pressure - pressureOffset) * PressureHPa(cumulus.PressMult) + PressureHPa(cumulus.PressOffset);
							StationPressure = ConvertPressMBToUser(pressure);
						}
						else
						{
							LogSpikeRemoval("Pressure difference greater than specified; reading ignored");
							LogSpikeRemoval("Old value = " + previouspress.ToString("F1") + " New value = " + pressure.ToString("F1") + " EWpressurediff = " +
											cumulus.EWpressurediff.ToString("F1"));
						}

						UpdatePressureTrendString();
						UpdateStatusPanel(now);
						DoForecast("", false);
					}
					var status = data[15];
					if ((status & 0x40) != 0)
					{
						SensorContactLost = true;
						cumulus.LogMessage("Sensor contact lost; ignoring outdoor data");
					}
					else
					{
						SensorContactLost = false;

						// Outdoor Humidity ===================================================
						int outhum = data[4];
						if ((outhum > 100) && (outhum != 255))
						{
							// bad value
							cumulus.LogMessage("Ignoring bad data: outhum = " + outhum);
						}
						else
						{
							// 255 is the overflow value, when RH gets below 10% - use 10%
							if (outhum == 255)
							{
								outhum = 10;
							}

							if (outhum > 0)
							{
								if ((previoushum == 999) || (Math.Abs(outhum - previoushum) < cumulus.EWhumiditydiff))
								{
									previoushum = outhum;
									DoOutdoorHumidity(outhum, now);
								}
								else
								{
									LogSpikeRemoval("Humidity difference greater than specified; reading ignored");
									LogSpikeRemoval("Old value = " + previoushum + " New value = " + outhum + " EWhumiditydiff = " + cumulus.EWhumiditydiff.ToString("F1"));
								}
							}
						}

						// Wind =============================================================
						double gust = (data[10] + ((data[11] & 0xF0)*16))/10.0f;
						double windspeed = (data[9] + ((data[11] & 0x0F)*256))/10.0f;
						var winddir = (int) (data[12]*22.5f);

						if ((gust > 60) || (gust < 0))
						{
							// bad value
							cumulus.LogMessage("Ignoring bad data: gust = " + gust.ToString());
						}
						else if ((windspeed > 60) || (windspeed < 0))
						{
							// bad value
							cumulus.LogMessage("Ignoring bad data: speed = " + gust.ToString());
						}
						else
						{
							if (((previousgust == 999) || (Math.Abs(gust - previousgust) < cumulus.EWgustdiff)) &&
								((previouswind == 999) || (Math.Abs(windspeed - previouswind) < cumulus.EWwinddiff)))
							{
								previousgust = gust;
								previouswind = windspeed;
								DoWind(ConvertWindMSToUser(gust), winddir, ConvertWindMSToUser(windspeed), now);
							}
							else
							{
								LogSpikeRemoval("Wind or gust difference greater than specified; reading ignored");
								LogSpikeRemoval("Gust: Old value = " + previousgust.ToString("F1") + " New value = " + gust.ToString("F1") + " EWgustdiff = " +
												cumulus.EWgustdiff.ToString("F1"));
								LogSpikeRemoval("Wind: Old value = " + previouswind.ToString("F1") + " New value = " + windspeed.ToString("F1") + " EWwinddiff = " +
												cumulus.EWwinddiff.ToString("F1"));
							}
						}

						// Outdoor Temperature ==============================================
						double outtemp = ((data[5]) + (data[6] & 0x7F)*256)/10.0f;
						sign = (byte) (data[6] & 0x80);
						if (sign == 0x80) outtemp = -outtemp;

						if ((outtemp < -50) || (outtemp > 70))
						{
							// bad value
							cumulus.LogMessage("Ignoring bad data: outtemp = " + outtemp);
						}
						else
						{
							if ((previoustemp == 999) || (Math.Abs(outtemp - previoustemp) < cumulus.EWtempdiff))
							{
								previoustemp = outtemp;

								DoOutdoorTemp(ConvertTempCToUser(outtemp), now);

								// Use current humidity for dewpoint
								if (OutdoorHumidity > 0)
								{
									OutdoorDewpoint = ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(OutdoorTemperature), OutdoorHumidity));

									CheckForDewpointHighLow(now);
								}
								;

								// calculate wind chill
								if ((outtemp > -50) || (outtemp < 70))
								{
									// The 'global average speed will have been determined by the call of DoWind
									// so use that in the wind chill calculation
									double avgspeedKPH = ConvertUserWindToKPH(WindAverage);

									// windinMPH = calibwind * 2.23693629;
									// calculate wind chill from calibrated C temp and calibrated win in KPH
									double val = MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature), avgspeedKPH);

									DoWindChill(ConvertTempCToUser(val), now);
								}

								DoApparentTemp(now);
							}
							else
							{
								LogSpikeRemoval("Temp difference greater than specified; reading ignored");
								LogSpikeRemoval("Old value = " + previoustemp + " New value = " + outtemp + " EWtempdiff = " + cumulus.EWtempdiff);
							}
						}

						// Rain ============================================================
						int raintot = data[13] + (data[14]*256);
						if (prevraintotal == -1)
						{
							// first reading
							prevraintotal = raintot;
							cumulus.LogMessage("Rain total count from station = " + raintot);
						}

						int raindiff = Math.Abs(raintot - prevraintotal);

						if (raindiff > cumulus.EWMaxRainTipDiff)
						{
							cumulus.LogMessage("Warning: large difference in rain gauge tip count: " + raindiff);

							ignoreraincount++;

							if (ignoreraincount == 6)
							{
								cumulus.LogMessage("Six consecutive readings; accepting value. Adjusting start of day figure to compensate");
								raindaystart += (raindiff*0.3);
								// adjust current rain total counter
								Raincounter += (raindiff*0.3);
								cumulus.LogMessage("Setting raindaystart to " + raindaystart);
								ignoreraincount = 0;
							}
							else
							{
								cumulus.LogMessage("Ignoring reading " + ignoreraincount);
							}
						}
						else
						{
							ignoreraincount = 0;
						}

						if (ignoreraincount == 0)
						{
							DoRain(ConvertRainMMToUser(raintot*0.3), -1, now);
							prevraintotal = raintot;
						}

						// Solar/UV
						if (hasSolar)
						{
							LightValue = (data[16] + (data[17]*256) + (data[18]*65536))/10.0;

							if (LightValue < 300000)
							{
								DoSolarRad((int) (LightValue*cumulus.LuxToWM2), now);
							}

							int UVreading = data[19];

							if (UVreading != 255)
							{
								DoUV(UVreading, now);
							}
						}
					}
					if (cumulus.SensorAlarmEnabled)
					{
						cumulus.SensorAlarmState = SensorContactLost;
					}
				}
			}

			prevaddr = addr;
		}

		private void StartSynchronising()
		{
			PreviousSensorClock = FOSensorClockTime;
			PreviousStationClock = FOStationClockTime;
			Synchronising = true;
			SynchroPhase = 1;
			hadfirstsyncdata = false;
			ReadCounter = 0;
			cumulus.LogMessage("Start Synchronising");

			tmrDataRead.Interval = 500; // half a second
		}

		private void StopSynchronising()
		{
			int secsdiff;

			Synchronising = false;
			SynchroPhase = 0;
			if (PreviousSensorClock == DateTime.MinValue)
			{
				cumulus.LogMessage("Sensor clock  " + FOSensorClockTime.ToLongTimeString());
			}
			else
			{
				secsdiff = (int) Math.Floor((FOSensorClockTime - PreviousSensorClock).TotalSeconds)%48;
				if (secsdiff > 24)
				{
					secsdiff = 48 - secsdiff;
				}
				cumulus.LogMessage("Sensor clock  " + FOSensorClockTime.ToLongTimeString() + " drift = " + secsdiff + " seconds");
			}

			if (PreviousStationClock == DateTime.MinValue)
			{
				cumulus.LogMessage("Station clock " + FOStationClockTime.ToLongTimeString());
			}

			else
			{
				secsdiff = (int) Math.Floor((FOStationClockTime - PreviousStationClock).TotalSeconds)%60;
				if (secsdiff > 30)
				{
					secsdiff = 60 - secsdiff;
				}
				cumulus.LogMessage("Station clock  " + FOStationClockTime.ToLongTimeString() + " drift = " + secsdiff + " seconds");
			}
			tmrDataRead.Interval = 10000; // 10 seconds
			tmrDataRead.Enabled = false;
			// sleep 5 secs to get out of sync with station clock minute change
			Thread.Sleep(5000);

			tmrDataRead.Enabled = true;

			cumulus.LogMessage("Stop Synchronising");
		}

		//public double EWpressureoffset { get; set; }

		private void LogSpikeRemoval(string message)
		{
			if (cumulus.ErrorLogSpikeRemoval)
			{
				cumulus.LogErrorMessage(message);
			}
		}

		private class HistoryData
		{
			public int inHum;

			public double inTemp;
			public int interval;
			public int outHum;

			public double outTemp;

			public double pressure;

			public int rainCounter;
			public DateTime timestamp;
			public int windBearing;
			public double windGust;

			public double windSpeed;
			public int uvVal;
			public double solarVal;
			public bool SensorContactLost;
			public int followinginterval;
		}
	}
}