using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace CumulusMX.Stations
{
	internal class WS2300Station : WeatherStation
	{
		//private const int MAXWINDRETRIES = 20;
		private const int ERROR = -1000;
		//private const byte WRITEACK = 0x10;
		//private const byte SETBIT = 0x12;
		//private const byte SETACK = 0x04;
		//private const byte UNSETBIT = 0x32;
		//private const byte UNSETACK = 0x0C;
		private const int MAXRETRIES = 50;

		private List<HistoryData> datalist;
		private double rainref;
		private double raincountref;
		private int previoushum = 999;
		private double previoustemp = 999;
		private double previouspress = 9999;
		private double previouswind = 999;

		private bool stop;

		public WS2300Station(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Manufacturer = Cumulus.StationManufacturer.LACROSSE;
			calculaterainrate = true;

			cumulus.LogMessage("WS2300: Attempting to open " + cumulus.ComportName);

			comport = new SerialPort(cumulus.ComportName, 2400, Parity.None, 8, StopBits.One)
			{
				Handshake = Handshake.None,
				DtrEnable = false,
				RtsEnable = true,
				ReadTimeout = 500,
				WriteTimeout = 1000
			};

			try
			{
				comport.Open();
				cumulus.LogMessage("COM port opened");
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("error opening COM port: " + ex.Message);
				//MessageBox.Show(ex.Message);
			}

			if (comport.IsOpen)
			{
				// Read the data from the logger
				cumulus.NormalRunning = true;
				startReadingHistoryData();
			}
		}

		public override void startReadingHistoryData()
		{
			cumulus.LogMessage("Start reading history data");
			//lastArchiveTimeUTC = getLastArchiveTime();

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			bw = new BackgroundWorker();
			//histprog = new historyProgressWindow();
			//histprog.Owner = mainWindow;
			//histprog.Show();
			bw.DoWork += bw_DoWork;
			//bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
			bw.RunWorkerCompleted += bw_RunWorkerCompleted;
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
			//histprog.histprogTB.Text = "Processed 100%";
			//histprog.histprogPB.Value = 100;
			//histprog.Close();
			//mainWindow.FillLastHourGraphData();
			cumulus.NormalRunning = true;
			StartLoop();
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();
		}

		private void bw_DoWork(object sender, DoWorkEventArgs e)
		{
			getAndProcessHistoryData();
		}

		public override void getAndProcessHistoryData()
		{
			int interval;
			Timestamp ts;
			int numrecs;

			cumulus.LogMessage("Reading history info");
			var rec = Ws2300ReadHistoryDetails(out interval, out _, out ts, out numrecs);

			if (rec < 0)
				cumulus.LogMessage("Failed to read history data");
			else
			{
				cumulus.LogMessage("History info obtained");
				datalist = [];

				/*
				double pressureoffset = ws2300PressureOffset();

				if (pressureoffset > -1000)
					cumulus.LogMessage("Pressure offset = " + pressureoffset);
				else
				{
					pressureoffset = 0;
					cumulus.LogMessage("Failed to read pressure offset, using zero");
				}
				*/

				cumulus.LogMessage("Downloading history data");

				datalist.Clear();

				DateTime recordtime;// = ws2300TimestampToDateTime(ts);

				if (cumulus.StationOptions.WS2300IgnoreStationClock)
				{
					// assume latest archive record is 'now'
					recordtime = DateTime.Now;
				}
				else
				{
					// use time from station
					recordtime = ws2300TimestampToDateTime(ts);
				}

				while (numrecs > 0 && recordtime > cumulus.LastUpdateTime)
				{
					int address, inhum, outhum;
					double intemp, outtemp, press, raincount, windspeed, bearing, dewpoint, windchill;

					if (
						Ws2300ReadHistoryRecord(rec, out address, out intemp, out outtemp, out press, out inhum, out outhum, out raincount, out windspeed, out bearing, out dewpoint,
							out windchill) < 0)
					{
						cumulus.LogMessage("Error reading history record");
						numrecs = 0;
						datalist.Clear();
					}
					else
					{
						var histData = new HistoryData
						{
							timestamp = recordtime,
							interval = interval,
							address = address,
							inHum = inhum,
							inTemp = intemp,
							outHum = outhum,
							outTemp = outtemp,
							pressure = press,
							rainTotal = raincount,
							windBearing = (int) bearing,
							windGust = windspeed,
							windSpeed = windspeed,
							dewpoint = dewpoint,
							windchill = windchill
						};

						datalist.Add(histData);
						recordtime = recordtime.AddMinutes(-interval);
						numrecs--;
						rec--;

						if (rec < 0) rec = 0xAE;

						bw.ReportProgress(datalist.Count, "collecting");
					}
				}
			}

			cumulus.LogMessage("Number of history entries = " + datalist.Count);

			if (datalist.Count > 0)
			{
				ProcessHistoryData();
			}

			//using (cumulusEntities dataContext = new cumulusEntities())
			//{
			//    UpdateHighsAndLows(dataContext);
			//}
		}

		private void ProcessHistoryData()
		{
			// history data is already in correct units
			var totalentries = datalist.Count;
			var rollHour = Math.Abs(cumulus.GetHourInc());
			var luhour = cumulus.LastUpdateTime.Hour;

			var rolloverdone = luhour == rollHour;

			var midnightraindone = luhour == 0;
			var rollover9amdone = luhour == 9;
			var snowhourdone = luhour == cumulus.SnowDepthHour;

			double prevraintotal = -1;
			double raindiff, rainrate;

			var pressureoffset = ConvertUnits.PressMBToUser(Ws2300PressureOffset());

			while (datalist.Count > 0)
			{
				var historydata = datalist[^1];

				var timestamp = historydata.timestamp;

				cumulus.LogMessage("Processing data for " + timestamp);
				// Check for roll-over

				var h = timestamp.Hour;

				rollHour = Math.Abs(cumulus.GetHourInc(timestamp));

				if (h != rollHour)
				{
					rolloverdone = false;
				}
				else if (!rolloverdone)
				{
					// do roll-over
					cumulus.LogMessage("WS2300: Day roll-over " + timestamp);
					DayReset(timestamp);

					rolloverdone = true;
				}

				// handle rain since midnight reset
				if (h != 0)
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
				if (h == 9 )
				{
					rollover9amdone = false;
				}
				if (!rollover9amdone)
				{
					Reset9amTemperatures(timestamp);
					rollover9amdone = true;
				}

				// Not in snow hour, snow yet to be done
				if (h != cumulus.SnowDepthHour)
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
					for (var i = 0; i < Snow24h.Length; i++)
					{
						Snow24h[i] = null;
					}

					snowhourdone = true;
				}

				// Humidity ====================================================================
				if (historydata.inHum > 0 && historydata.inHum <= 100)
					DoIndoorHumidity(historydata.inHum);
				if (historydata.outHum > 0 && historydata.outHum <= 100)
					DoOutdoorHumidity(historydata.outHum, timestamp);

				// Wind ========================================================================
				if (historydata.windSpeed < cumulus.LCMaxWind)
				{
					DoWind(historydata.windGust, historydata.windBearing, historydata.windSpeed, timestamp);
				}

				// Temperature ==================================================================
				if (historydata.outTemp > -50 && historydata.outTemp < 50)
				{
					DoOutdoorTemp(historydata.outTemp, timestamp);

					tempsamplestoday += historydata.interval;
					TempTotalToday += OutdoorTemperature * historydata.interval;

					if (OutdoorTemperature < cumulus.ChillHourThreshold && OutdoorTemperature > cumulus.ChillHourBase)
					// add 1 minute to chill hours
					{
						ChillHours += historydata.interval / 60.0;
					}
				}

				if (historydata.inTemp > -50 && historydata.inTemp < 50)
					DoIndoorTemp(historydata.inTemp);

				// Rain ==========================================================================
				if (prevraintotal < 0)
				{
					raindiff = 0;
				}
				else
				{
					raindiff = historydata.rainTotal - prevraintotal;
				}

				if (historydata.interval > 0)
				{
					rainrate = raindiff * (60.0 / historydata.interval);
				}
				else
				{
					rainrate = 0;
				}

				cumulus.LogMessage("WS2300: History rain total = " + historydata.rainTotal);

				DoRain(historydata.rainTotal, rainrate, timestamp);

				prevraintotal = historydata.rainTotal;

				// Dewpoint ====================================================================
				if (cumulus.StationOptions.CalculatedDP)
				{
					var tempC = ConvertUnits.UserTempToC(OutdoorTemperature);
					DoOutdoorDewpoint(ConvertUnits.TempCToUser(MeteoLib.DewPoint(tempC, OutdoorHumidity)), timestamp);
					CheckForDewpointHighLow(timestamp);
				}
				else
				{
					if (historydata.dewpoint < ConvertUnits.UserTempToC(60))
					{
						DoOutdoorDewpoint(cumulus.Calib.Temp.Calibrate(historydata.dewpoint), timestamp);
					}
				}

				// Wind chill ==================================================================
				if (historydata.windchill < ConvertUnits.TempCToUser(60))
				{
					DoWindChill(historydata.windchill, timestamp);
				}
				else
				{
					DoWindChill(-999, timestamp);
				}

				// Wind run ======================================================================
				cumulus.LogMessage("Windrun: " + WindAverage.ToString(cumulus.WindAvgFormat) + cumulus.Units.WindText + " for " + historydata.interval + " minutes = " +
								(WindAverage * WindRunHourMult[cumulus.Units.Wind] * historydata.interval / 60.0).ToString(cumulus.WindRunFormat) + cumulus.Units.WindRunText);

				WindRunToday += WindAverage * WindRunHourMult[cumulus.Units.Wind] * historydata.interval / 60.0;

				CheckForWindrunHighLow(timestamp);

				// Pressure ======================================================================
				var slpress = historydata.pressure + pressureoffset;

				if (slpress > ConvertUnits.PressMBToUser(900.0) && slpress < ConvertUnits.PressMBToUser(1200.0))
				{
					DoPressure(slpress, timestamp);
				}

				// update heating/cooling degree days
				UpdateDegreeDays(historydata.interval);

				DoApparentTemp(timestamp);
				DoFeelsLike(timestamp);
				DoHumidex(timestamp);
				DoCloudBaseHeatIndex(timestamp);
				DoTrendValues(timestamp);

				CalculateDominantWindBearing(Bearing, WindAverage, historydata.interval);

				bw.ReportProgress((totalentries - datalist.Count) * 100 / totalentries, "processing");

				_ = cumulus.DoLogFile(timestamp, false);
				cumulus.DoCustomIntervalLogs(timestamp);

				if (cumulus.StationOptions.LogExtraSensors)
				{
					_ = cumulus.DoExtraLogFile(timestamp);
				}
				cumulus.MySqlRealtimeFile(999, false, timestamp);

				// Custom MySQL update - minutes interval
				if (cumulus.MySqlFuncs.MySqlSettings.CustomMins.Enabled)
				{
					_ = cumulus.CustomMysqlMinutesUpdate(timestamp, false);
				}

				AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex, OutdoorHumidity, Pressure, RainToday, SolarRad, UV, RainCounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, rainrate, -1, -1);
				UpdateStatusPanel(timestamp.ToUniversalTime());
				cumulus.AddToWebServiceLists(timestamp);

				datalist.RemoveAt(datalist.Count - 1);
			}
		}

		private static DateTime ws2300TimestampToDateTime(Timestamp ts)
		{
			return new DateTime(ts.year, ts.month, ts.day, ts.hour, ts.minute, 0);
		}

		/// <summary>
		/// Opens the serial port and starts the reading loop
		/// </summary>
		public override void Start()
		{
			try
			{
				while (!stop)
				{
					GetAndProcessData();
					Thread.Sleep(5000);
				}
			}
			// Catch the ThreadAbortException
			catch (ThreadAbortException)
			{
			}
		}

		private struct Timestamp
		{
			public int minute;
			public int hour;
			public int day;
			public int month;
			public int year;
		};

		/// <summary>
		/// Read history info - interval, countdown etc
		/// </summary>
		/// <param name="interval">Current interval in minutes</param>
		/// <param name="countdown">Countdown to next measurement</param>
		/// <param name="timelast">Time/Date for last measurement</param>
		/// <param name="numrecords">number of valid records</param>
		/// <returns>address of last written record</returns>
		private int Ws2300ReadHistoryDetails(out int interval, out int countdown, out Timestamp timelast, out int numrecords)
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x6B2;
			var bytes = 10;

			var loctimelast = new Timestamp();

			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
			{
				interval = 0;
				countdown = 0;
				timelast = new Timestamp();
				numrecords = 0;
				return ERROR;
			}

			rainref = Ws2300RainTotal();

			if (rainref < 0)
			{
				cumulus.LogMessage("WS2300: Unable to read current rain total");
				interval = 0;
				countdown = 0;
				timelast = new Timestamp();
				numrecords = 0;
				return -1000;
			}
			else
			{
				cumulus.LogMessage("WS2300: current rain total from station = " + rainref);
				raincountref = Ws2300RainHistoryRef();

				if (raincountref < 0)
				{
					cumulus.LogMessage("WS2300: Unable to read current rain counter");
					interval = 0;
					countdown = 0;
					timelast = new Timestamp();
					numrecords = 0;
					return -1000;
				}
				else
				{
					cumulus.LogMessage("WS2300: current rain counter from station = " + raincountref);

					interval = (data[1] & 0xF) * 256 + data[0] + 1;
					countdown = data[2] * 16 + (data[1] >> 4) + 1;
					loctimelast.minute = (data[3] >> 4) * 10 + (data[3] & 0xF);
					loctimelast.hour = (data[4] >> 4) * 10 + (data[4] & 0xF);
					loctimelast.day = (data[5] >> 4) * 10 + (data[5] & 0xF);
					loctimelast.month = (data[6] >> 4) * 10 + (data[6] & 0xF);
					loctimelast.year = 2000 + (data[7] >> 4) * 10 + (data[7] & 0xF);
					numrecords = data[9];

					timelast = loctimelast;
					return data[8];
				}
			}
		}

		private double Ws2300RainHistoryRef()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x440;
			var bytes = 2;

			cumulus.LogMessage("WS2300: Reading rain history ref");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return -1000;
			else
				return ((data[1] & 0x0F) << 8) + data[0];
		}

		private int Ws2300ReadHistoryRecord(int record, out int address, out double tempindoor, out double tempoutdoor, out double pressure, out int humindoor, out int humoutdoor,
			out double raincount, out double windspeed, out double winddir, out double dewpoint, out double windchill)
		{
			var data = new byte[20];
			var command = new byte[25];

			var bytes = 10;
			int tempint;

			address = 0x6C6 + record * 19;
			cumulus.LogMessage("Reading history record " + record);
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
			{
				cumulus.LogMessage("Failed to read history record");
				tempindoor = 0;
				tempoutdoor = 0;
				pressure = 0;
				humindoor = 0;
				humoutdoor = 0;
				raincount = 0;
				windspeed = 0;
				winddir = 0;
				dewpoint = 0;
				windchill = 0;
				return -1000;
			}

			var msg = new StringBuilder("History record read: ", 256);
			for (var n = 0; n < bytes; n++)
			{
				msg.Append(data[n].ToString("X2"));
				msg.Append(' ');
			}

			cumulus.LogMessage(msg.ToString());
			tempint = (data[4] << 12) + (data[3] << 4) + (data[2] >> 4);

			pressure = 1000 + tempint % 10000 / 10.0;

			if (pressure >= 1502.2)
				pressure -= 1000;

			pressure = ConvertUnits.PressMBToUser(pressure);

			humindoor = (int) ((tempint - tempint % 10000) / 10000.0);

			humoutdoor = (data[5] >> 4) * 10 + (data[5] & 0xF);

			raincount = ConvertUnits.RainMMToUser(rainref + ((data[7] & 0xF) * 256 + data[6] - raincountref) * 0.518);

			windspeed = (data[8] * 16 + (data[7] >> 4)) / 10.0;

			if (windspeed > 49.9)
			{
				// probably lost sensor contact
				windspeed = 0;
			}

			// need wind in kph for chill calc
			var windkmh = 3.6 * windspeed;

			tempint = ((data[2] & 0xF) << 16) + (data[1] << 8) + data[0];
			tempindoor = ConvertUnits.TempCToUser(tempint % 1000 / 10.0f - 30.0);
			tempoutdoor = (tempint - tempint % 1000) / 10000.0f - 30.0;

			windchill = ConvertUnits.TempCToUser(MeteoLib.WindChill(tempoutdoor, windkmh));
			dewpoint = ConvertUnits.TempCToUser(MeteoLib.DewPoint(tempoutdoor, humoutdoor));

			tempoutdoor = ConvertUnits.TempCToUser(tempoutdoor);

			windspeed = ConvertUnits.WindMSToUser(windspeed);
			winddir = (data[9] & 0xF) * 22.5;

			return ++record % 0xAF;
		}

		private void GetAndProcessData()
		{
			string presstrend, forecast;
			double direction;

			var now = DateTime.Now;

			// Indoor humidity =====================================================================
			if (!stop)
			{
				var inhum = Ws2300IndoorHumidity();
				if (inhum > -1 && inhum < 101)
				{
					DoIndoorHumidity(inhum);
				}
			}

			// Outdoor humidity ====================================================================
			if (!stop)
			{
				var outhum = Ws2300OutdoorHumidity();
				if (outhum > 0 && outhum <= 100 && (previoushum == 999 || Math.Abs(outhum - previoushum) < cumulus.Spike.HumidityDiff))
				{
					previoushum = outhum;
					DoOutdoorHumidity(outhum, now);
				}
			}

			// Indoor temperature ==================================================================
			if (!stop)
			{
				var intemp = Ws2300IndoorTemperature();
				if (intemp > -20)
				{
					DoIndoorTemp(ConvertUnits.TempCToUser(intemp));
				}
			}

			// Outdoor temperature ================================================================
			if (!stop)
			{
				var outtemp = Ws2300OutdoorTemperature();
				if (outtemp > -60 && outtemp < 60 && (previoustemp == 999 || Math.Abs(outtemp - previoustemp) < ConvertUnits.UserTempToC(cumulus.Spike.TempDiff)))
				{
					previoustemp = outtemp;
					DoOutdoorTemp(ConvertUnits.TempCToUser(outtemp), now);
				}
			}

			// Outdoor dewpoint ==================================================================
			if (!stop)
			{
				var dp = Ws2300OutdoorDewpoint();
				if (dp > -100 && dp < 60)
				{
					DoOutdoorDewpoint(ConvertUnits.TempCToUser(dp), now);
				}
			}

			// Pressure ==========================================================================
			if (!stop)
			{
				var pressure = Ws2300RelativePressure();
				if (pressure > 900 && pressure < 1200 && (previouspress == 9999 || Math.Abs(pressure - previouspress) < ConvertUnits.UserPressToHpa(cumulus.Spike.PressDiff)))
				{
					previouspress = pressure;
					DoPressure(ConvertUnits.PressMBToUser(pressure), now);
				}

				pressure = Ws2300AbsolutePressure();

				if (Pressure > 850 && Pressure < 1200)
				{
					DoStationPressure(ConvertUnits.PressMBToUser(pressure));
				}
			}

			// Pressure trend and forecast =======================================================
			if (!stop)
			{
				var res = Ws2300PressureTrendAndForecast(out presstrend, out forecast);
				if (res > ERROR)
				{
					DoForecast(forecast, false);
					DoPressTrend(presstrend);
				}
			}

			// Wind ============================================================================
			if (!stop)
			{
				var wind = Ws2300CurrentWind(out direction);
				if (wind > -1 && (previouswind == 999 || Math.Abs(wind - previouswind) < cumulus.Spike.WindDiff))
				{
					previouswind = wind;
					DoWind(ConvertUnits.WindMSToUser(wind), (int) direction, -1, now);
				}
				else
				{
					cumulus.LogDebugMessage("Ignoring wind reading: wind=" + wind.ToString("F1") + " previouswind=" + previouswind.ToString("F1") + " sr=" +
											cumulus.Spike.WindDiff.ToString("F1"));
				}

				// wind chill
				var wc = Ws2300WindChill();
				if (wc > -100 && wc < 60)
				{
					DoWindChill(ConvertUnits.TempCToUser(wc), now);
				}
			}

			// Rain ===========================================================================
			if (!stop)
			{
				var raintot = Ws2300RainTotal();
				if (raintot > -1)
				{
					DoRain(ConvertUnits.RainMMToUser(raintot), -1, now);
				}
			}

			if (!stop)
			{
				DoApparentTemp(now);
				DoFeelsLike(now);
				DoHumidex(now);
				DoCloudBaseHeatIndex(now);

				UpdateStatusPanel(now.ToUniversalTime());
				UpdateMQTT();
			}
		}

		/// <summary>
		/// Read indoor temperature
		/// </summary>
		/// <returns>Indoor temp in C</returns>
		private double Ws2300IndoorTemperature()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x346;
			var bytes = 2;
			cumulus.LogDataMessage("Reading indoor temp");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10.0F + (data[0] & 0xF) / 100.0F - 30.0;
				cumulus.LogDataMessage("Indoor temp = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}

		}

		/// <summary>
		/// Read outdoor temperature
		/// </summary>
		/// <returns>Outdoor temp in C</returns>
		private double Ws2300OutdoorTemperature()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x373;
			var bytes = 2;
			cumulus.LogDataMessage("Reading outdoor temp");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10.0F + (data[0] & 0xF) / 100.0F - 30.0;
				cumulus.LogDataMessage("Outdoor temp = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Read outdoor dew point
		/// </summary>
		/// <returns>dew point in C</returns>
		private double Ws2300OutdoorDewpoint()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x3CE;
			var bytes = 2;

			cumulus.LogDataMessage("Reading outdoor dewpoint");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10.0F + (data[0] & 0xF) / 100.0F - 30.0;
				cumulus.LogDataMessage("Dewpoint = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Read indoor humidity
		/// </summary>
		/// <returns>humidity in %</returns>
		private int Ws2300IndoorHumidity()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x3FB;
			var bytes = 1;

			cumulus.LogDataMessage("Reading indoor humidity");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[0] >> 4) * 10 + (data[0] & 0xF);
				cumulus.LogDataMessage("Indoor humidity = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Read outdoor humidity
		/// </summary>
		/// <returns>humidity in %</returns>
		private int Ws2300OutdoorHumidity()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x419;
			var bytes = 1;

			cumulus.LogDataMessage("Reading outdoor humidity");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[0] >> 4) * 10 + (data[0] & 0xF);
				cumulus.LogDataMessage("Outdoor humidity = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Get current wind speed and direction
		/// </summary>
		/// <param name="direction">returns direction in degrees</param>
		/// <returns>speed in m/s</returns>
		private double Ws2300CurrentWind(out double direction)
		{
			// 0527  0 Wind overflow flag: 0 = normal, 5=wind sensor disconnected
			// 0528  0 Wind minimum code: 0=min, 1=--.-, 2=OFL (overflow)
			// 0529  1 Windspeed: binary nibble 0 [m/s * 10]
			// 052A  1 Windspeed: binary nibble 1 [m/s * 10]
			// 052B  2 Windspeed: binary nibble 2 [m/s * 10]
			// 052C  2 Wind Direction = nibble * 22.5 degrees, clockwise from North

			cumulus.LogDataMessage("Reading wind data");
			var data = new byte[20];
			var command = new byte[25];

			var address = 0x527; //Windspeed and direction
			var bytes = 3;

			direction = 0;

			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				if ((data[0] & 0xF7) != 0x00 || //Invalid wind data
					data[1] == 0xFF && ((data[2] & 0xF) == 0 || (data[2] & 0xF) == 1))
					return ERROR;

				//Calculate wind direction
				direction = (data[2] >> 4) * 22.5;

				//Calculate wind speed
				var val = (((data[2] & 0xF) << 8) + data[1]) / 10.0;
				cumulus.LogDataMessage($"Wind data: Speed = {val}, Direction = {direction}");
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Read wind chill
		/// </summary>
		/// <returns>wind chill in C</returns>
		private double Ws2300WindChill()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x3A0;
			var bytes = 2;

			cumulus.LogDataMessage("Reading wind chill");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10F + (data[0] & 0xF) / 100F - 30;
				cumulus.LogDataMessage("Wind chill = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Read rain total
		/// </summary>
		/// <returns>Rain total in mm</returns>
		private double Ws2300RainTotal()
		{
			//cumulus.LogMessage("Reading rain total");
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x4D2;
			var bytes = 3;

			cumulus.LogDataMessage("Reading rain total");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[2] >> 4) * 1000 + (data[2] & 0xF) * 100 + (data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10F + (data[0] & 0xF) / 100.0;
				cumulus.LogDataMessage("Rain total = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Read sea-level pressure
		/// </summary>
		/// <returns>SLP in mb</returns>
		private double Ws2300RelativePressure()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x5E2;
			var bytes = 3;

			cumulus.LogDataMessage("Reading relative pressure");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[2] & 0xF) * 1000 + (data[1] >> 4) * 100 + (data[1] & 0xF) * 10 + (data[0] >> 4) + (data[0] & 0xF) / 10.0;
				cumulus.LogDataMessage("Rel pressure = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Read local pressure
		/// </summary>
		/// <returns>abs press in mb</returns>
		private double Ws2300AbsolutePressure()
		{
			var data = new byte[20];
			var command = new byte[25];
			var address = 0x5D8;
			var bytes = 3;

			cumulus.LogDataMessage("Reading absolute pressure");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[2] & 0xF) * 1000 + (data[1] >> 4) * 100 + (data[1] & 0xF) * 10 + (data[0] >> 4) + (data[0] & 0xF) / 10.0;
				cumulus.LogDataMessage("Abs pressure = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Get pressure offset (sea level - station)
		/// </summary>
		/// <returns>offset in mb</returns>
		private double Ws2300PressureOffset()
		{
			var data = new byte[20];
			var command = new byte[25];

			var address = 0x5EC;
			var bytes = 3;

			cumulus.LogDataMessage("Reading pressure offset");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
				return ERROR;

			try
			{
				var val = (data[2] & 0xF) * 1000 + (data[1] >> 4) * 100 + (data[1] & 0xF) * 10 + (data[0] >> 4) + (data[0] & 0xF) / 10.0 - 1000;
				cumulus.LogDataMessage("Pressure offset = " + val);
				return val;
			}
			catch
			{
				return ERROR;
			}
		}

		/// <summary>
		/// Read pressure trend and forecast
		/// </summary>
		/// <param name="pressuretrend"></param>
		/// <param name="forecast"></param>
		/// <returns></returns>
		private int Ws2300PressureTrendAndForecast(out string pressuretrend, out string forecast)
		{
			var data = new byte[20];
			var command = new byte[25];

			var address = 0x26B;
			var bytes = 1;
			string[] presstrendstrings = ["Steady", "Rising", "Falling"];
			string[] forecaststrings = ["Rainy", "Cloudy", "Sunny"];

			cumulus.LogDataMessage("Reading press trend and forecast");
			if (Ws2300ReadWithRetries(address, bytes, data, command) != bytes)
			{
				pressuretrend = string.Empty;
				forecast = string.Empty;
				return ERROR;
			}

			try
			{
				pressuretrend = presstrendstrings[data[0] >> 4];
				forecast = forecaststrings[data[0] & 0xF];
				cumulus.LogDataMessage($"Pressure trend = {pressuretrend}, forecast = {forecast}");
				return 0;
			}
			catch
			{
				pressuretrend = string.Empty;
				forecast = string.Empty;
				return ERROR;
			}
		}

		/*
		/// <summary>
		/// Writes to serial port with retries
		/// </summary>
		/// <param name="address"></param>
		/// <param name="number"></param>
		/// <param name="encode_constant"></param>
		/// <param name="writedata"></param>
		/// <param name="commanddata"></param>
		/// <returns></returns>
		///

		private int ws2300WriteWithRetries(int address, int number, byte encode_constant, byte[] writedata, byte[] commanddata)
		{
			int i;

			for (i = 0; i < MAXRETRIES; i++)
			{
				// reset before writing
				ws2300SendReset();

				// Read the data. If expected number of bytes read break out of loop.
				if (ws2300WriteData(address, number, encode_constant, writedata, commanddata) == number)
				{
					break;
				}
			}

			// If we have tried MAXRETRIES times to read we expect not to have valid data
			if (i == MAXRETRIES)
			{
				return -1;
			}

			return number;
		}
		*/

		/*
		/// <summary>
		/// Writes data to the station
		/// </summary>
		/// <param name="address"></param>
		/// <param name="number"></param>
		/// <param name="encodeConstant"></param>
		/// <param name="writeData"></param>
		/// <param name="commandData"></param>
		/// <returns></returns>

		private int ws2300WriteData(int address, int number, byte encodeConstant, byte[] writeData, byte[] commandData)
		{
			byte answer;
			byte[] encodedData = new byte[80];
			int i = 0;
			byte ackConstant = WRITEACK;

			if (encodeConstant == SETBIT)
			{
				ackConstant = SETACK;
			}
			else if (encodeConstant == UNSETBIT)
			{
				ackConstant = UNSETACK;
			}

			cumulus.LogDataMessage("ws2300WriteData");
			// First 4 bytes are populated with converted address range 0000-13XX
			ws2300EncodeAddress(address, commandData);
			// populate the encoded_data array
			ws2300DataEncoder(number, encodeConstant, writeData, encodedData);

			//Write the 4 address bytes
			for (i = 0; i < 4; i++)
			{
				if (ws2300WriteSerial(commandData[i]) != 1)
					return -1;
				if (ws2300ReadSerial(out answer) != 1)
					return -1;
				if (answer != ws2300commandChecksum0to3(commandData[i], i))
					return -1;
			}

			//Write the data nibbles or set/unset the bits
			for (i = 0; i < number; i++)
			{
				if (ws2300WriteSerial(encodedData[i]) != 1)
					return -1;
				if (ws2300ReadSerial(out answer) != 1)
					return -1;
				if (answer != (writeData[i] + ackConstant))
					return -1;
				commandData[i + 4] = encodedData[i];
			}

			cumulus.LogDataMessage("Exit ws2300WriteData with success");

			return i;
		}
		*/

		/// <summary>
		/// Read data, retry until success or maxretries
		/// </summary>
		/// <param name="address"></param>
		/// <param name="number"></param>
		/// <param name="readdata"></param>
		/// <param name="commanddata"></param>
		/// <returns></returns>
		private int Ws2300ReadWithRetries(int address, int number, byte[] readdata, byte[] commanddata)
		{
			int i;

			for (i = 0; i < MAXRETRIES; i++)
			{
				Ws2300SendReset();

				// Read the data. If expected number of bytes read break out of loop.
				if (Ws2300ReadData(address, number, readdata, commanddata) == number)
				{
					break;
				}
			}

			// If we have tried MAXRETRIES times to read we expect not to
			// have valid data
			if (i == MAXRETRIES)
			{
				cumulus.LogDebugMessage("Max read retries exceeded");
				return -1;
			}

			var msg = "Data read: ";
			for (var n = 0; n < number; n++)
			{
				msg += readdata[n].ToString("X2");
				msg += " ";
			}

			cumulus.LogDataMessage(msg);

			return number;
		}

		/// <summary>
		/// Read data from the station
		/// </summary>
		/// <param name="address"></param>
		/// <param name="numberofbytes"></param>
		/// <param name="readData"></param>
		/// <param name="commandData"></param>
		/// <returns>number of bytes read</returns>
		private int Ws2300ReadData(int address, int numberofbytes, byte[] readData, byte[] commandData)
		{
			byte answer;
			int i;

			// First 4 bytes are populated with converted address range 0000-13B0
			ws2300EncodeAddress(address, commandData);
			// Now populate the 5th byte with the converted number of bytes
			commandData[4] = ws2300encodeNumberOfBytes(numberofbytes);

			//cumulus.LogMessage("WS2300ReadData");
			for (i = 0; i < 4; i++)
			{
				if (Ws2300WriteSerial(commandData[i]) != 1)
					return -1;
				if (Ws2300ReadSerial(out answer) != 1)
					return -1;
				if (answer != ws2300commandChecksum0to3(commandData[i], i))
					return -1;
			}

			// Send the final command that asks for number of bytes and check answer
			if (Ws2300WriteSerial(commandData[4]) != 1)
				return -1;
			if (Ws2300ReadSerial(out answer) != 1)
				return -1;
			if (answer != ws2300commandChecksum4(numberofbytes))
				return -1;

			// Read the data bytes
			for (i = 0; i < numberofbytes; i++)
			{
				if (Ws2300ReadSerial(out readData[i]) != 1)
					return -1;
			}

			// Read and verify checksum
			if (Ws2300ReadSerial(out answer) != 1)
				return -1;
			if (answer != dataChecksum(readData, numberofbytes))
				return -1;

			return i;
		}

		/// <summary>
		/// Calculates checksum for final command byte
		/// </summary>
		/// <param name="number"></param>
		/// <returns></returns>
		private static byte ws2300commandChecksum4(int number)
		{
			return (byte) (number + 0x30);
		}

		/// <summary>
		/// Calculates the checksum for the data received from the station
		/// </summary>
		/// <param name="data"></param>
		/// <param name="numberofbytes"></param>
		/// <returns></returns>
		private static byte dataChecksum(byte[] data, int numberofbytes)
		{
			var checksum = 0;

			for (var i = 0; i < numberofbytes; i++)
			{
				checksum += data[i];
			}

			return (byte) (checksum & 0xFF);
		}

		/// <summary>
		/// Converts 'number of bytes to read' to form expected by station
		/// </summary>
		/// <param name="number">number to be encoded</param>
		/// <returns></returns>
		private static byte ws2300encodeNumberOfBytes(int number)
		{
			byte encodednumber;

			encodednumber = (byte) (0xC2 + number * 4);

			if (encodednumber > 0xfe)
				encodednumber = 0xfe;

			return encodednumber;
		}

		/// <summary>
		/// calculates the checksum for the first 4 commands sent to the station
		/// </summary>
		/// <param name="command"></param>
		/// <param name="sequence"></param>
		/// <returns></returns>
		private static byte ws2300commandChecksum0to3(byte command, int sequence)
		{
			return (byte) (sequence * 16 + (command - 0x82) / 4);
		}

		/*
		/// <summary>
		/// Converts up to 15 data bytes to the form needed when sending write commands
		/// </summary>
		/// <param name="number"></param>
		/// <param name="encodeConstant"></param>
		/// <param name="dataIn"></param>
		/// <param name="dataOut"></param>
		private void ws2300DataEncoder(int number, byte encodeConstant, byte[] dataIn, byte[] dataOut)
		{
			for (int i = 0; i < number; i++)
			{
				dataOut[i] = (byte)(encodeConstant + (dataIn[i] * 4));
			}
		}
		*/

		/// <summary>
		/// Converts addresses to the form required by the station when sending commands
		/// </summary>
		/// <param name="addressIn">Address to be encoded</param>
		/// <param name="addressOut">Encoded address</param>
		private static void ws2300EncodeAddress(int addressIn, byte[] addressOut)
		{
			const int numbytes = 4;

			for (var i = 0; i < numbytes; i++)
			{
				var nibble = (byte) (addressIn >> 4 * (3 - i) & 0x0F);
				addressOut[i] = (byte) (0x82 + nibble * 4);
			}
		}

		/// <summary>
		/// Reset the station by sending command 06
		/// </summary>
		/// <returns>True if successful</returns>
		private bool Ws2300SendReset()
		{
			byte command = 0x06;
			byte answer;

			cumulus.LogDataMessage("Sending reset");

			for (var i = 0; i < 100; i++)
			{
				comport.DiscardInBuffer();

				Ws2300WriteSerial(command);

				// Occasionally 0, then 2 is returned.  If zero comes back, continue
				// reading as this is more efficient than sending an out-of sync
				// reset and letting the data reads restore synchronization.
				// Occasionally, multiple 2's are returned.  Read with a fast timeout
				// until all data is exhausted, if we got a two back at all, we
				// consider it a success

				while (Ws2300ReadSerial(out answer) == 1)
				{
					if (answer == 2)
					{
						// clear anything that might come after the response
						comport.DiscardInBuffer();

						cumulus.LogDataMessage("Reset done, retries = " + i);
						return true;
					}
				}

				Thread.Sleep(5 * i);
			}

			return false;
		}

		/// <summary>
		/// Read a byte from the serial port
		/// </summary>
		/// <param name="answer">The byte that was read</param>
		/// <returns>Number of bytes read</returns>
		private int Ws2300ReadSerial(out byte answer)
		{
			cumulus.LogDataMessage("ReadSerial");
			try
			{
				answer = (byte) comport.ReadByte();
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("ReadSerial error " + ex.Message);
				answer = 0;
				return 0;
			}

			cumulus.LogDataMessage("ReadSerial success, data = " + answer.ToString("X2"));
			return 1;
		}

		/// <summary>
		/// Write a byte to the serial port
		/// </summary>
		/// <param name="command">The byte to be written</param>
		/// <returns>Number of bytes written</returns>
		private int Ws2300WriteSerial(byte command)
		{
			cumulus.LogDataMessage("Writing command " + command.ToString("X2"));
			byte[] towrite = [command];
			try
			{
				comport.Write(towrite, 0, 1);
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("WriteSerial error " + ex.Message);
				return 0;
			}

			cumulus.LogDataMessage("WriteSerial success");
			return 1;
		}

		private sealed class HistoryData
		{
			public DateTime timestamp;

			public int address;

			public int interval;

			public int inHum;

			public int outHum;

			public double inTemp;

			public double outTemp;

			public double windGust;

			public double windSpeed;

			public int windBearing;

			public double pressure;

			public double rainTotal;

			public double dewpoint;

			public double windchill;
		}
	}
}
