using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CumulusMX.Tempest;
using ServiceStack.Text;

namespace CumulusMX
{
	internal class TempestStation : WeatherStation
	{


		public TempestStation(Cumulus cumulus) : base(cumulus)
		{
			calculaterainrate = false;

			cumulus.LogMessage("Station type = Tempest");

			// Tempest does not provide pressure trend strings
			cumulus.StationOptions.UseCumulusPresstrendstr = true;

			// Tempest does not provide wind chill
			cumulus.StationOptions.CalculatedWC = true;

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			Task.Run(getAndProcessHistoryData);// grab old data, then start the station

		}

		public override void getAndProcessHistoryData()
		{
			//cumulus.LogDebugMessage("Lock: Station waiting for the lock");
			Cumulus.syncInit.Wait();
			//cumulus.LogDebugMessage("Lock: Station has the lock");
			try
			{
				var stTime = cumulus.LastUpdateTime;
				if (FirstRun)
					stTime = DateTime.Now.AddDays(-cumulus.WeatherFlowOptions.WFDaysHist);

				var recs = StationListener.GetRestPacket(StationListener.REST_URL,
					cumulus.WeatherFlowOptions.WFToken, cumulus.WeatherFlowOptions.WFDeviceId,
					stTime, DateTime.Now, cumulus);

				ProcessHistoryData(recs);
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Exception occurred reading archive data: " + ex.Message);
				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					cumulus.LogMessage($"Base Error getting history data: {ex.Message}");
				}

			}
			//cumulus.LogDebugMessage("Lock: Station releasing the lock");
			Cumulus.syncInit.Release();
			StartLoop();
		}

		private void ProcessHistoryData(List<Observation> datalist)
		{
			var totalentries = datalist.Count;

			cumulus.LogMessage("Processing history data, number of entries = " + totalentries);
			cumulus.LogConsoleMessage(
				$"Processing history data for {totalentries} records. {DateTime.Now.ToLongTimeString()}");

			var rollHour = Math.Abs(cumulus.GetHourInc());
			var luhour = cumulus.LastUpdateTime.Hour;
			var rolloverdone = luhour == rollHour;
			var midnightraindone = luhour == 0;

			var ticks = Environment.TickCount;
			foreach (var historydata in datalist)
			{
				var timestamp = historydata.Timestamp;

				cumulus.LogMessage("Processing data for " + timestamp);

				var h = timestamp.Hour;

				//  if outside rollover hour, rollover yet to be done
				if (h != rollHour) rolloverdone = false;

				// In rollover hour and rollover not yet done
				if (h == rollHour && !rolloverdone)
				{
					// do rollover
					cumulus.LogMessage("Day rollover " + timestamp.ToShortTimeString());
					DayReset(timestamp);

					rolloverdone = true;
				}

				// Not in midnight hour, midnight rain yet to be done
				if (h != 0) midnightraindone = false;

				// In midnight hour and midnight rain (and sun) not yet done
				if (h == 0 && !midnightraindone)
				{
					ResetMidnightRain(timestamp);
					ResetSunshineHours(timestamp);
					ResetMidnightTemperatures(timestamp);
					midnightraindone = true;
				}

				// Pressure =============================================================
				var alt = AltitudeM(cumulus.Altitude);
				var seaLevel = MeteoLib.GetSeaLevelPressure(alt, (double) historydata.StationPressure, (double)historydata.Temperature);
				DoPressure(ConvertPressMBToUser(seaLevel), timestamp);

				// Outdoor Humidity =====================================================
				DoOutdoorHumidity((int) historydata.Humidity, timestamp);

				// Wind =================================================================
				DoWind(ConvertWindMSToUser((double) historydata.WindGust), historydata.WindDirection,
					ConvertWindMSToUser((double) historydata.WindAverage), timestamp);

				// Outdoor Temperature ==================================================
				DoOutdoorTemp(ConvertTempCToUser((double) historydata.Temperature), timestamp);
				// add in 'archivePeriod' minutes worth of temperature to the temp samples
				tempsamplestoday += historydata.ReportInterval;
				TempTotalToday += OutdoorTemperature * historydata.ReportInterval;

				// update chill hours
				if (OutdoorTemperature < cumulus.ChillHourThreshold)
					// add 1 minute to chill hours
					ChillHours += historydata.ReportInterval / 60.0;

				double rainrate = (double) (ConvertRainMMToUser((double) historydata.Precipitation) *
											(60d / historydata.ReportInterval));

				var newRain = Raincounter + ConvertRainMMToUser((double) historydata.Precipitation);
				cumulus.LogMessage(
					$"TempestDoRainHist: New Precip: {historydata.Precipitation}, Type: {historydata.PrecipType}, Rate: {rainrate}, LocalDayRain: {historydata.LocalDayRain}, LocalRainChecked: {historydata.LocalRainChecked}, FinalRainChecked: {historydata.FinalRainChecked}");

				DoRain(newRain, rainrate, timestamp);
				cumulus.LogMessage(
					$"TempestDoRainHist: Total Precip for Day: {Raincounter}");

				OutdoorDewpoint =
					ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(OutdoorTemperature),
						OutdoorHumidity));

				CheckForDewpointHighLow(timestamp);

				// calculate wind chill

				if (ConvertUserWindToMS(WindAverage) < 1.5)
					DoWindChill(OutdoorTemperature, timestamp);
				else
					// calculate wind chill from calibrated C temp and calibrated win in KPH
					DoWindChill(
						ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature),
							ConvertUserWindToKPH(WindAverage))), timestamp);

				DoApparentTemp(timestamp);
				DoFeelsLike(timestamp);
				DoHumidex(timestamp);
				DoCloudBaseHeatIndex(timestamp);

				DoUV((double) historydata.UV, timestamp);

				DoSolarRad(historydata.SolarRadiation, timestamp);

				// add in archive period worth of sunshine, if sunny
				if (SolarRad > CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100 &&
					SolarRad >= cumulus.SolarOptions.SolarMinimum)
					SunshineHours += historydata.ReportInterval / 60.0;

				LightValue = historydata.Illuminance;


				// add in 'following interval' minutes worth of wind speed to windrun
				cumulus.LogMessage("Windrun: " + WindAverage.ToString(cumulus.WindFormat) + cumulus.Units.WindText + " for " + historydata.ReportInterval + " minutes = " +
								   (WindAverage*WindRunHourMult[cumulus.Units.Wind]*historydata.ReportInterval/60.0).ToString(cumulus.WindRunFormat) + cumulus.Units.WindRunText);

				WindRunToday += WindAverage * WindRunHourMult[cumulus.Units.Wind] * historydata.ReportInterval / 60.0;

				// update heating/cooling degree days
				UpdateDegreeDays(historydata.ReportInterval);

				// update dominant wind bearing
				CalculateDominantWindBearing(Bearing, WindAverage, historydata.ReportInterval);

				CheckForWindrunHighLow(timestamp);

				bw?.ReportProgress((totalentries - datalist.Count) * 100 / totalentries, "processing");

				//UpdateDatabase(timestamp.ToUniversalTime(), historydata.interval, false);

				cumulus.DoLogFile(timestamp, false);
				cumulus.DoCustomIntervalLogs(timestamp);

				if (cumulus.StationOptions.LogExtraSensors) cumulus.DoExtraLogFile(timestamp);

				//AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing,
				//    OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
				//    OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex);

				AddRecentDataWithAq(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
					OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);

				if (cumulus.StationOptions.CalculatedET && timestamp.Minute == 0)
				{
					// Start of a new hour, and we want to calculate ET in Cumulus
					CalculateEvaoptranspiration(timestamp);
				}

				DoTrendValues(timestamp);
				UpdatePressureTrendString();
				UpdateStatusPanel(timestamp);
				cumulus.AddToWebServiceLists(timestamp);

			}

			ticks = Environment.TickCount - ticks;
			var rate = ((double)totalentries / ticks) * 1000;
			cumulus.LogMessage($"End processing history data. Rate: {rate:f2}/second");
			cumulus.LogConsoleMessage($"Completed processing history data. {DateTime.Now.ToLongTimeString()}, Rate: {rate:f2}/second");

		}


		public override void Start()
		{

			cumulus.NormalRunning = true;
			StationListener.WeatherPacketReceived = WeatherPacketReceived;
			StationListener.Start(cumulus);
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);

			cumulus.StartTimersAndSensors();
		}

		public override void Stop()
		{
			StationListener.Stop();
		}

		private void WeatherPacketReceived(WeatherPacket wp)
		{
			DateTime ts;
			if (wp != null)
			{
				switch (wp.MsgType)
				{
					case WeatherPacket.MessageType.Observation:
						cumulus.LogDebugMessage("Received an Observation message");
						cumulus.LogDataMessage(
							string.Format($"Observation data - temp: {0}, hum: {1}, gust: {2}, spdvg: {3}, press: {4}, solar: {5}, UV: {6}, rain: {7}, batt: {8}",
								wp.Observation.Temperature,
								wp.Observation.Humidity,
								wp.Observation.WindGust,
								wp.Observation.WindAverage,
								wp.Observation.StationPressure,
								wp.Observation.SolarRadiation,
								wp.Observation.UV,
								wp.Observation.Precipitation,
								wp.Observation.BatteryVoltage
							)
						);

						ts = wp.Observation.Timestamp;
						var userTemp = ConvertTempCToUser(Convert.ToDouble(wp.Observation.Temperature));

						DoOutdoorTemp(userTemp,ts);
						DoWind(ConvertWindMSToUser((double) wp.Observation.WindGust),
							wp.Observation.WindDirection,
							ConvertWindMSToUser((double) wp.Observation.WindAverage),
							ts);

						var alt = AltitudeM(cumulus.Altitude);
						var seaLevel = MeteoLib.GetSeaLevelPressure(alt, (double) wp.Observation.StationPressure,
							(double) wp.Observation.Temperature);
						DoPressure(ConvertPressMBToUser(seaLevel), ts);
						cumulus.LogDebugMessage(
							$"TempestPressure: Station:{wp.Observation.StationPressure} mb, Sea Level:{seaLevel} mb, Altitude:{alt}");

						DoSolarRad(wp.Observation.SolarRadiation, ts);
						DoUV((double) wp.Observation.UV, ts);
						double rainrate = (double) (ConvertRainMMToUser((double) wp.Observation.Precipitation) *
													(60d / wp.Observation.ReportInterval));

						var newRain = Raincounter + ConvertRainMMToUser((double) wp.Observation.Precipitation);
						cumulus.LogDebugMessage(
							$"TempestDoRain: New Precip: {wp.Observation.Precipitation}, Type: {wp.Observation.PrecipType}, Rate: {rainrate}");

						DoRain(newRain, rainrate, ts);
						cumulus.LogDebugMessage(
							$"TempestDoRain: Total Precip for Day: {Raincounter}");

						DoOutdoorHumidity((int)wp.Observation.Humidity,ts);

						OutdoorDewpoint =
							ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(OutdoorTemperature),
								OutdoorHumidity));

						CheckForDewpointHighLow(ts);

						DoApparentTemp(ts);
						DoFeelsLike(ts);
						DoWindChill(userTemp,ts);
						DoHumidex(ts);
						DoCloudBaseHeatIndex(ts);

						UpdateStatusPanel(ts);
						UpdateMQTT();
						DoForecast(string.Empty, false);

						cumulus.BatteryLowAlarm.Triggered = wp.Observation.BatteryVoltage <= 2.355M;

						break;
					case WeatherPacket.MessageType.RapidWind:
						cumulus.LogDebugMessage("Received a Rapid Wind message");

						var rw = wp.RapidWind;
						cumulus.LogDataMessage($"Wind Data - speed: {rw.WindSpeed}, direction: {rw.WindDirection}");

						DoWind(ConvertWindMSToUser((double) rw.WindSpeed),
							rw.WindDirection,
							ConvertWindMSToUser((double) rw.WindSpeed),
							rw.Timestamp);
						UpdateStatusPanel(rw.Timestamp);

						break;
					case WeatherPacket.MessageType.LightningStrike:
						cumulus.LogDebugMessage("Received a Lightning message");

						LightningTime = wp.LightningStrike.Timestamp;
						LightningDistance = ConvertKmtoUserUnits(wp.LightningStrike.Distance);
						LightningStrikesToday++;
						cumulus.LogDebugMessage($"Lightning Detected: {wp.LightningStrike.Timestamp} - {wp.LightningStrike.Distance} km - {LightningStrikesToday} strikes today");
						break;

				}
			}
		}
	}
}

namespace CumulusMX.Tempest
{
	#region UDP Comms

	public class EventClient : UdpClient
	{
		private readonly Task _listenTask;
		private readonly CancellationTokenSource tokenSource;

		public EventClient(int port) : base(port)
		{
			tokenSource = new CancellationTokenSource();
			_listenTask = new Task(() => ListenForPackets(tokenSource.Token));
			ExclusiveAddressUse = false;
		}

		public event EventHandler<PacketReceivedArgs> PacketReceived;

		public void StartListening()
		{
			_listenTask.Start();
		}

		private async void ListenForPackets(CancellationToken token)
		{
			try
			{
				while (!token.IsCancellationRequested)
				{
					while (!token.IsCancellationRequested && Available == 0) await Task.Delay(10);
					while (!token.IsCancellationRequested && Available > 0)
					{
						IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
						var data = Receive(ref endPoint);
						PacketReceived?.Invoke(this, new PacketReceivedArgs(data));
					}
				}
			}
			catch (Exception e)
			{
				var ex = e;
				while (ex != null)
				{
					Console.WriteLine($"TempestExceptions:{ex.GetType()}:{ex.Message}:{ex.StackTrace}");
					ex = ex.InnerException;
				}
			}
		}

		public void StopListening()
		{
			tokenSource.Cancel();
		}
	}

	public class PacketReceivedArgs : EventArgs
	{
		public PacketReceivedArgs(byte[] packet)
		{
			Packet = packet;
		}

		public byte[] Packet { get; }
	}

	#endregion

	#region Station Listener
	public static class StationListener
	{
		private static EventClient _udpListener;
		private static Cumulus cumulus;

		public delegate void WPReceived(WeatherPacket wp);

		public static WPReceived WeatherPacketReceived { get; set; }

		public static void Start(Cumulus c)
		{
			cumulus=c;
			Task.Run(StartUdpListen);
		}

		public static void Stop()
		{
			_udpListener?.StopListening();
		}

		public static void StartUdpListen()
		{
			if (_udpListener == null)
			{
				try
				{
					_udpListener = new EventClient(cumulus.WeatherFlowOptions.WFTcpPort);
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
				}

				if (_udpListener != null)
				{
					_udpListener.PacketReceived += _udpListener_PacketReceived;
					_udpListener.StartListening();
				}
			}
		}

		private static void _udpListener_PacketReceived(object sender, PacketReceivedArgs e)
		{
			if (e.Packet.Length > 0)
			{
				var s = Encoding.ASCII.GetString(e.Packet);
				Debug.WriteLine(s);
				WeatherPacket wp = null;
				try
				{
					wp = JsonSerializer.DeserializeFromString<WeatherPacket>(s);
					if (wp != null)
					{
						wp.FullString = s;
						wp.SetMessageType();
						if (wp.MsgType != WeatherPacket.MessageType.Unknown) WeatherPacketReceived?.Invoke(wp);
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
				}

				if ((wp?.timestamp ?? 0) != 0) Debug.WriteLine(wp?.PacketTime.ToString());
			}
		}

		public const string REST_URL = "https://swd.weatherflow.com/swd/rest/observations/";

		public static List<Observation> GetRestPacket(string url, string token,int deviceId, DateTime start, DateTime end, Cumulus c)
		{
			List<Observation> ret = new List<Observation>();
			cumulus = c;

			using (var httpClient = new HttpClient())
			{
				var tpStart = start;
				var tpEnd = end;
				double ts = tpEnd.Subtract(tpStart).TotalDays;

				while (ts  > 0)
				{
					long st;
					long end_time;
					st = WeatherPacket.ToUnixTimeSeconds(tpStart);
					end_time = WeatherPacket.ToUnixTimeSeconds(end);
					if (ts > 4)// load max 4 days at a time
					{
						tpStart = tpStart.AddDays(4);
						end_time = WeatherPacket.ToUnixTimeSeconds(tpStart)-1;// subtract a second so we don't overlap
						ts = tpEnd.Subtract(tpStart).TotalDays;
					}
					else
					{
						ts = 0;
					}

					cumulus.LogDebugMessage($"GetRestPacket: Requesting from URL - {url}device/{deviceId}?token=<<token>>&time_start={st}&time_end={end_time}");

					using (var response =
						httpClient.GetAsync($"{url}device/{deviceId}?token={token}&time_start={st}&time_end={end_time}")
					)
					{
						string apiResponse = response.Result.Content.ReadAsStringAsync().Result;
						var rp = JsonSerializer.DeserializeFromString<RestPacket>(apiResponse);
						if (rp != null && rp.status.status_message.Equals("SUCCESS") && rp.obs != null)
						{
							foreach (var ob in rp.obs)
							{
								ret.Add(new Observation(ob));
							}
						}
						else if (rp != null && rp.status.status_message.Equals("SUCCESS"))
						{
							// no data for time period, ignore
							//cumulus.LogConsoleMessage($"No data for time period from {tpStart} to {end}");
						}
						else
						{
							var msg = $"Error downloading tempest history: {apiResponse}";
							cumulus.LogMessage(msg);
							cumulus.LogConsoleMessage(msg, ConsoleColor.Red);
							if (rp.status.status_code == 404)
							{
								cumulus.LogConsoleMessage("Normally indicates incorrect Device ID");
								ts = -1;// force a stop, fatal error
							}

							if (rp.status.status_code == 401)
							{
								cumulus.LogConsoleMessage("Normally indicates incorrect Token");
								ts = -1;// force a stop, fatal error
							}
						}

					}
				}
			}

			return ret;
		}
	}

	#endregion

	#region Packets
	public class RestPacket
	{
		public class Status
		{
			public int status_code { get; set; }
			public string status_message { get; set; }
		}

		public Status status { get; set; }
		public int device_id { get; set; }
		public string type { get; set; }
		public int bucket_step_minutes { get; set; }
		public string source { get; set; }
		public List<List<decimal?>> obs { get; set; }

	}

	public class WeatherPacket
	{
		public enum MessageType
		{
			RapidWind,
			HubStatus,
			Unknown,
			Observation,
			DeviceStatus,
			PrecipEvent,
			LightningStrike
		}

		public enum PrecipType
		{
			None = 0,
			Rain = 1,
			Hail = 2
		}

		public enum RadioStatus
		{
			RadioOff,
			RadioOn,
			RadioActive
		}

		public DeviceStatus DeviceStatus;

		public HubStatus HubStatus;

		public LightningStrike LightningStrike;

		public Observation Observation;

		public PrecipEvent PrecipEvent;

		public RapidWind RapidWind;

		public string FullString { get; set; }
		public string serial_number { get; set; }
		public string type { get; set; }
		public MessageType MsgType { get; private set; } = MessageType.Unknown;


		public object firmware_revision { get; set; }
		public int uptime { get; set; }
		public long timestamp { get; set; }

		public DateTime PacketTime => WeatherPacket.FromUnixTimeSeconds(timestamp);


		public int rssi { get; set; }
		public string hub_sn { get; set; }
		public List<decimal> ob { get; set; }
		public int seq { get; set; }
		public List<int> fs { get; set; }
		public string reset_flags { get; set; }
		public List<int> radio_stats { get; set; }
		public List<int> mqtt_stats { get; set; }
		public decimal voltage { get; set; }
		public int hub_rssi { get; set; }
		public int sensor_status { get; set; }
		public int debug { get; set; }
		public List<decimal?[]> obs { get; set; }
		public List<long> evt { get; set; }

		public void SetMessageType()
		{
			switch (type)
			{
				case "rapid_wind":
					MsgType = MessageType.RapidWind;
					RapidWind = new RapidWind(this);
					break;
				case "hub_status":
					MsgType = MessageType.HubStatus;
					HubStatus = new HubStatus(this);
					break;
				case "obs_st":
					MsgType = MessageType.Observation;
					Observation = new Observation(this);
					break;
				case "device_status":
					MsgType = MessageType.DeviceStatus;
					DeviceStatus = new DeviceStatus(this);
					break;
				case "light_debug":
					break; // documentation for this?
				case "evt_precip":
					MsgType = MessageType.PrecipEvent;
					PrecipEvent = new PrecipEvent(this);
					break;
				case "evt_strike":
					MsgType = MessageType.LightningStrike;
					LightningStrike = new LightningStrike(this);
					break;
				default:
					MsgType = MessageType.Unknown;
					break;
			}
		}

		public static DateTime FromUnixTimeSeconds(long epoch)
		{
			// Unix timestamp is seconds past epoch
			System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
			dtDateTime = dtDateTime.AddSeconds( epoch ).ToLocalTime();
			return dtDateTime;
		}

		public static long ToUnixTimeSeconds(DateTime dt)
		{
			TimeSpan t = dt.ToUniversalTime() - new DateTime(1970, 1, 1);
			return (long)t.TotalSeconds;
		}

		public static decimal GetDecimal(decimal? d)
		{
			return d ?? 0M;
		}

		public static int GetInt(decimal? d)
		{
			var dec = GetDecimal(d);
			int i;
			try
			{
				i = (int) dec;
			}
			catch
			{
				i = 0;
			}

			return i;
		}

		public static long Getlong(decimal? d)
		{
			var dec = GetDecimal(d);
			long i;
			try
			{
				i = (long) dec;
			}
			catch
			{
				i = 0;
			}

			return i;
		}
	}

	public class DeviceStatus
	{
		public WeatherPacket.MessageType MsgType = WeatherPacket.MessageType.DeviceStatus;

		public DeviceStatus(WeatherPacket packet)
		{
			SerialNumber = packet.serial_number;
			HubSN = packet.hub_sn;
			Timestamp = WeatherPacket.FromUnixTimeSeconds(packet.timestamp);
			Uptime = packet.uptime;
			Voltage = packet.voltage;

			try
			{
				if (!int.TryParse(packet.firmware_revision.ToString(), out var i)) i = -1;
				FirmwareRevision = i;
			}
			catch {}

			RSSI = packet.rssi;
			HubRSSI = packet.hub_rssi;
			SensorStatus = packet.sensor_status;
			Debug = packet.debug;
		}

		public string SerialNumber { get; set; }
		public string HubSN { get; set; }
		public DateTime Timestamp { get; set; }
		public int Uptime { get; set; }
		public decimal Voltage { get; set; }
		public int FirmwareRevision { get; set; }
		public int RSSI { get; set; }
		public int HubRSSI { get; set; }
		public int SensorStatus { get; set; }
		public int Debug { get; set; }
	}
	public class HubStatus
	{
		public HubStatus(WeatherPacket packet)
		{
			//string sn, string hubsn, List<decimal> ob
			SerialNumber = packet.serial_number;
			Timestamp = WeatherPacket.FromUnixTimeSeconds(packet.timestamp);

			if (packet.radio_stats.Count == 5)
			{
				RadioVersion = packet.radio_stats[0];
				RadioReboots = packet.radio_stats[1];
				RadioBusErrors = packet.radio_stats[2];
				FirmwareRevision = packet.firmware_revision.ToString();
				Uptime = packet.uptime;
				Rssi = packet.rssi;
				ResetFlags = packet.reset_flags;
				Seq = packet.seq;


				switch (packet.radio_stats[3])
				{
					case 0:
						RadioStatus = WeatherPacket.RadioStatus.RadioOff;
						break;
					case 1:
						RadioStatus = WeatherPacket.RadioStatus.RadioOn;
						break;
					case 3:
						RadioStatus = WeatherPacket.RadioStatus.RadioActive;
						break;
					default:
						RadioStatus = WeatherPacket.RadioStatus.RadioOff;
						break;
				}
			}
		}

		public WeatherPacket.MessageType MsgType => WeatherPacket.MessageType.HubStatus;


		public string SerialNumber { get; set; }
		public string FirmwareRevision { get; set; }
		public int Uptime { get; set; }
		public int Rssi { get; set; }
		public DateTime Timestamp { get; set; }
		public string ResetFlags { get; set; }
		public int Seq { get; set; }
		public int RadioVersion { get; set; }
		public int RadioReboots { get; set; }
		public int RadioBusErrors { get; set; }
		public WeatherPacket.RadioStatus RadioStatus { get; set; }
	}
	public class LightningStrike
	{
		public LightningStrike(WeatherPacket packet)
		{
			SerialNumber = packet.serial_number;
			HubSN = packet.hub_sn;

			if (packet.evt.Count == 3)
			{
				Timestamp = WeatherPacket.FromUnixTimeSeconds(packet.evt[0]);
				Distance = Convert.ToInt32(packet.evt[1]);
				Energy = Convert.ToInt32(packet.evt[2]);
			}
		}


		public string SerialNumber { get; set; }
		public string HubSN { get; set; }
		public DateTime Timestamp { get; set; }
		public int Distance { get; set; }
		public int Energy { get; set; }
	}

	public class Observation
	{
		public WeatherPacket.MessageType MsgType = WeatherPacket.MessageType.Observation;

		public Observation(WeatherPacket packet)
		{
			SerialNumber = packet.serial_number;
			HubSN = packet.hub_sn;
			try
			{
				int i;
				if (!int.TryParse(packet.firmware_revision.ToString(), out i)) i = -1;
				FirmwareRevision = i;
			}
			catch {}

			if (packet.obs[0].Length >= 18)
			{
				//var ob = packet.obs[0];
				LoadObservation(this, packet.obs[0]);
			}
		}

		public Observation(List<decimal?> obs)
		{
			LoadObservation(this,obs.ToArray());
		}

		private static void LoadObservation(Observation o, decimal?[] ob)
		{
			o.Timestamp = WeatherPacket.FromUnixTimeSeconds(WeatherPacket.Getlong(ob[0]));
			o.WindLull = WeatherPacket.GetDecimal(ob[1]);
			o.WindAverage = WeatherPacket.GetDecimal(ob[2]);
			o.WindGust = WeatherPacket.GetDecimal(ob[3]);
			o.WindDirection = WeatherPacket.GetInt(ob[4]);
			o.WindSampleInt = WeatherPacket.GetInt(ob[5]);
			o.StationPressure = WeatherPacket.GetDecimal(ob[6]);
			o.Temperature = WeatherPacket.GetDecimal(ob[7]);
			o.Humidity = WeatherPacket.GetDecimal(ob[8]);
			o.Illuminance = WeatherPacket.GetInt(ob[9]);
			o.UV = WeatherPacket.GetDecimal(ob[10]);
			o.SolarRadiation = WeatherPacket.GetInt(ob[11]);
			o.Precipitation = WeatherPacket.GetDecimal(ob[12]);

			switch (WeatherPacket.GetInt(ob[13]))
			{
				case 0:
					o.PrecipType = WeatherPacket.PrecipType.None;
					break;
				case 1:
					o.PrecipType = WeatherPacket.PrecipType.Rain;
					break;
				case 2:
					o.PrecipType = WeatherPacket.PrecipType.Hail;
					break;
				default:
					o.PrecipType = WeatherPacket.PrecipType.None;
					break;
			}
			o.LightningAvgDist = WeatherPacket.GetInt(ob[14]);
			o.LightningCount = WeatherPacket.GetInt(ob[15]);
			o.BatteryVoltage = WeatherPacket.GetDecimal(ob[16]);
			o.ReportInterval = WeatherPacket.GetInt(ob[17]);

			if (ob.Length >= 21)
			{
				// these are only available for history data, not from the UDP message
				o.LocalDayRain = WeatherPacket.GetInt(ob[18]);
				o.FinalRainChecked = WeatherPacket.GetInt(ob[19]);
				o.LocalRainChecked = WeatherPacket.GetInt(ob[20]);
			}
		}

		public string SerialNumber { get; set; }
		public string HubSN { get; set; }
		public int FirmwareRevision { get; set; }
		public DateTime Timestamp { get; set; }
		public decimal WindLull { get; set; } // m/s
		public decimal WindAverage { get; set; } // m/s
		public decimal WindGust { get; set; } // m/s
		public int WindDirection { get; set; } // degrees
		public int WindSampleInt { get; set; } // seconds
		public decimal StationPressure { get; set; } // MB
		public decimal Temperature { get; set; } // C
		public decimal TempF => Temperature * 9 / 5 + 32;
		public decimal Humidity { get; set; } // %
		public int Illuminance { get; set; } // Lux
		public decimal UV { get; set; } // Index
		public int SolarRadiation { get; set; } // W/m2
		public decimal Precipitation { get; set; } // mm
		public WeatherPacket.PrecipType PrecipType { get; set; }
		public int LightningAvgDist { get; set; } // km
		public int LightningCount { get; set; }
		public decimal BatteryVoltage { get; set; }
		public int ReportInterval { get; set; } // minutes
		public int LocalDayRain { get; set; }// mm
		public int FinalRainChecked { get; set; }// mm
		public int LocalRainChecked { get; set; }// mm

	}
	public class PrecipEvent
	{
		public PrecipEvent(WeatherPacket packet)
		{
			SerialNumber = packet.serial_number;
			HubSN = packet.hub_sn;
			Timestamp = WeatherPacket.FromUnixTimeSeconds(packet.evt[0]);
		}

		public string SerialNumber { get; set; }
		public string HubSN { get; set; }
		public DateTime Timestamp { get; set; }
	}
	public class RapidWind
	{
		public RapidWind(WeatherPacket packet)
		{
			//string sn, string hubsn, List<decimal> ob
			SerialNumber = packet.serial_number;
			HubSN = packet.hub_sn;
			if (packet.ob.Count == 3)
			{
				Timestamp = WeatherPacket.FromUnixTimeSeconds((long) packet.ob[0]);
				WindSpeed = packet.ob[1];
				WindDirection = (int) packet.ob[2];
			}
		}

		public WeatherPacket.MessageType MsgType => WeatherPacket.MessageType.RapidWind;

		public string SerialNumber { get; set; }
		public string HubSN { get; set; }
		public DateTime Timestamp { get; set; }
		public decimal WindSpeed { get; set; } // mps
		public int WindDirection { get; set; }
	}
	#endregion
}
