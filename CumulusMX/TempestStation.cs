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

#pragma warning disable IDE0025

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
			cumulus.StationOptions.CalculatedDP = true;

			// Tempest does not provide average wind speeds
			cumulus.StationOptions.CalcuateAverageWindSpeed = true;

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			Task.Run(getAndProcessHistoryData);// grab old data, then start the station

		}

		public override void getAndProcessHistoryData()
		{
			Cumulus.SyncInit.Wait();
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
				cumulus.LogErrorMessage("Exception occurred reading archive data: " + ex.Message);
				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					cumulus.LogMessage($"Base Error getting history data: {ex.Message}");
				}

			}

			Cumulus.SyncInit.Release();
			StartLoop();
		}

		private void ProcessHistoryData(List<Observation> datalist)
		{
			var totalentries = datalist.Count;

			if (totalentries == 0)
			{
				cumulus.LogMessage("No history data to process");
				Cumulus.LogConsoleMessage("No history data to process");
				return;
			}

			cumulus.LogMessage("Processing history data, number of entries = " + totalentries);
			Cumulus.LogConsoleMessage(
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

				rollHour = Math.Abs(cumulus.GetHourInc(timestamp));

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
				var seaLevel = MeteoLib.GetSeaLevelPressure(alt, (double) historydata.StationPressure, (double) historydata.Temperature);
				DoPressure(ConvertUnits.PressMBToUser(seaLevel), timestamp);

				// Outdoor Humidity =====================================================
				DoOutdoorHumidity((int) historydata.Humidity, timestamp);

				// Wind =================================================================
				DoWind(ConvertUnits.WindMSToUser((double) historydata.WindGust), historydata.WindDirection,
					ConvertUnits.WindMSToUser((double) historydata.WindAverage), timestamp);

				// Outdoor Temperature ==================================================
				DoOutdoorTemp(ConvertUnits.TempCToUser((double) historydata.Temperature), timestamp);
				// add in 'archivePeriod' minutes worth of temperature to the temp samples
				tempsamplestoday += historydata.ReportInterval;
				TempTotalToday += OutdoorTemperature * historydata.ReportInterval;

				// update chill hours
				if (OutdoorTemperature < cumulus.ChillHourThreshold)
					// add 1 minute to chill hours
					ChillHours += historydata.ReportInterval / 60.0;

				double rainrate = ConvertUnits.RainMMToUser((double) historydata.Precipitation) * (60d / historydata.ReportInterval);

				var newRain = RainCounter + ConvertUnits.RainMMToUser((double) historydata.Precipitation);
				cumulus.LogMessage(
					$"TempestDoRainHist: New Precip: {historydata.Precipitation}, Type: {historydata.PrecipType}, Rate: {rainrate}, LocalDayRain: {historydata.LocalDayRain}, LocalRainChecked: {historydata.LocalRainChecked}, FinalRainChecked: {historydata.FinalRainChecked}");

				DoRain(newRain, rainrate, timestamp);
				cumulus.LogMessage(
					$"TempestDoRainHist: Total Precip for Day: {RainCounter}");

				// calculate dp
				DoOutdoorDewpoint(-999, timestamp);
				// calculate wind chill
				DoWindChill(-999, timestamp);
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
								   (WindAverage * WindRunHourMult[cumulus.Units.Wind] * historydata.ReportInterval / 60.0).ToString(cumulus.WindRunFormat) + cumulus.Units.WindRunText);

				WindRunToday += WindAverage * WindRunHourMult[cumulus.Units.Wind] * historydata.ReportInterval / 60.0;

				// update heating/cooling degree days
				UpdateDegreeDays(historydata.ReportInterval);

				// update dominant wind bearing
				CalculateDominantWindBearing(Bearing, WindAverage, historydata.ReportInterval);

				CheckForWindrunHighLow(timestamp);

				bw?.ReportProgress((totalentries - datalist.Count) * 100 / totalentries, "processing");

				_ = cumulus.DoLogFile(timestamp, false);
				cumulus.DoCustomIntervalLogs(timestamp);

				if (cumulus.StationOptions.LogExtraSensors)
				{
					_ = cumulus.DoExtraLogFile(timestamp);
				}

				AddRecentDataWithAq(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
					OutdoorHumidity, Pressure, RainToday, SolarRad, UV, RainCounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);

				if (cumulus.StationOptions.CalculatedET && timestamp.Minute == 0)
				{
					// Start of a new hour, and we want to calculate ET in Cumulus
					CalculateEvapotranspiration(timestamp);
				}

				DoTrendValues(timestamp);
				UpdatePressureTrendString();
				UpdateStatusPanel(timestamp);
				cumulus.AddToWebServiceLists(timestamp);

			}

			ticks = Environment.TickCount - ticks;
			var rate = ((double) totalentries / ticks) * 1000;
			cumulus.LogMessage($"End processing history data. Rate: {rate:f2}/second");
			Cumulus.LogConsoleMessage($"Completed processing history data. {DateTime.Now.ToLongTimeString()}, Rate: {rate:f2}/second");

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

						DoOutdoorHumidity((int) wp.Observation.Humidity, ts);

						var userTemp = ConvertUnits.TempCToUser(Convert.ToDouble(wp.Observation.Temperature));

						DoOutdoorTemp(userTemp, ts);
						DoWind(ConvertUnits.WindMSToUser((double) wp.Observation.WindGust),
							wp.Observation.WindDirection,
							ConvertUnits.WindMSToUser((double) wp.Observation.WindAverage),
							ts);

						var alt = AltitudeM(cumulus.Altitude);
						var seaLevel = MeteoLib.GetSeaLevelPressure(alt, (double) wp.Observation.StationPressure,
							(double) wp.Observation.Temperature);
						DoPressure(ConvertUnits.PressMBToUser(seaLevel), ts);
						cumulus.LogDebugMessage($"TempestPressure: Station:{wp.Observation.StationPressure} mb, Sea Level:{seaLevel} mb, Altitude:{alt}");

						DoSolarRad(wp.Observation.SolarRadiation, ts);
						DoUV((double) wp.Observation.UV, ts);
						double rainrate = ConvertUnits.RainMMToUser((double) wp.Observation.Precipitation) * (60d / wp.Observation.ReportInterval);

						var newRain = RainCounter + ConvertUnits.RainMMToUser((double) wp.Observation.Precipitation);
						cumulus.LogDebugMessage($"TempestDoRain: New Precip: {wp.Observation.Precipitation}, Type: {wp.Observation.PrecipType}, Rate: {rainrate}");

						DoRain(newRain, rainrate, ts);
						cumulus.LogDebugMessage($"TempestDoRain: Total Precip for Day: {RainCounter}");

						DoOutdoorDewpoint(-999, ts);
						DoApparentTemp(ts);
						DoFeelsLike(ts);
						DoWindChill(-999, ts);
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

						DoWind(ConvertUnits.WindMSToUser((double) rw.WindSpeed),
							rw.WindDirection,
							-1,
							rw.Timestamp);
						UpdateStatusPanel(rw.Timestamp);

						break;
					case WeatherPacket.MessageType.LightningStrike:
						cumulus.LogDebugMessage("Received a Lightning message");

						LightningTime = wp.LightningStrike.Timestamp;
						LightningDistance = ConvertUnits.KmtoUserUnits(wp.LightningStrike.Distance);
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
		// Track whether Dispose has been called.
		private bool disposed = false;

		public EventClient(int port)
		{
			tokenSource = new CancellationTokenSource();
			_listenTask = new Task(() => _ = ListenForPackets(tokenSource.Token));
			// force shared port as Mono defaults to exclusive
			ExclusiveAddressUse = false;
			Client.Bind(new IPEndPoint(IPAddress.Any, port));
		}

		public event EventHandler<PacketReceivedEventArgs> PacketReceived;

		public void StartListening()
		{
			_listenTask.Start();
		}


		protected override void Dispose(bool disposing)
		{
			// Check to see if Dispose has already been called.
			if (!disposed)
			{
				// If disposing equals true, dispose all managed
				// and unmanaged resources.
				if (disposing)
				{
					// Dispose managed resources.
					tokenSource.Dispose();
				}

				// Note disposing has been done.
				disposed = true;

				// Call base class implementation.
				base.Dispose(disposing);
			}
		}

		private async Task ListenForPackets(CancellationToken token)
		{
			try
			{
				while (!token.IsCancellationRequested)
				{
					while (!token.IsCancellationRequested && Available == 0) await Task.Delay(10, token);
					while (!token.IsCancellationRequested && Available > 0)
					{
						IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
						var data = Receive(ref endPoint);
						PacketReceived?.Invoke(this, new PacketReceivedEventArgs(data));
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

	public class PacketReceivedEventArgs(byte[] packet) : EventArgs
	{
		public byte[] Packet { get; } = packet;
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
			cumulus = c;
			Task.Run(StartUdpListen);
		}

		public static void Stop()
		{
			_udpListener?.StopListening();
			_udpListener?.Dispose();
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

		private static void _udpListener_PacketReceived(object sender, PacketReceivedEventArgs e)
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

		public static List<Observation> GetRestPacket(string url, string token, int deviceId, DateTime start, DateTime end, Cumulus c)
		{
			List<Observation> ret = [];
			cumulus = c;

			using (var httpClient = new HttpClient())
			{
				var tpStart = start;
				var tpEnd = end;
				double ts = tpEnd.Subtract(tpStart).TotalDays;

				while (ts > 0)
				{
					long st;
					long end_time;
					st = WeatherPacket.ToUnixTimeSeconds(tpStart);
					end_time = WeatherPacket.ToUnixTimeSeconds(end);
					if (ts > 4)// load max 4 days at a time
					{
						tpStart = tpStart.AddDays(4);
						end_time = WeatherPacket.ToUnixTimeSeconds(tpStart) - 1;// subtract a second so we don't overlap
						ts = tpEnd.Subtract(tpStart).TotalDays;
					}
					else
					{
						ts = 0;
					}

					cumulus.LogDebugMessage($"GetRestPacket: Requesting from URL - {url}device/{deviceId}?token=<<token>>&time_start={st}&time_end={end_time}");

					using var response = httpClient.GetAsync($"{url}device/{deviceId}?token={token}&time_start={st}&time_end={end_time}");
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
					}
					else
					{
						var msg = $"Error downloading tempest history: {apiResponse}";
						cumulus.LogErrorMessage(msg);
						Cumulus.LogConsoleMessage(msg, ConsoleColor.Red);
						if (rp != null && rp.status.status_code == 404)
						{
							Cumulus.LogConsoleMessage("Normally indicates incorrect Device ID");
							ts = -1;// force a stop, fatal error
						}

						if (rp != null && rp.status.status_code == 401)
						{
							Cumulus.LogConsoleMessage("Normally indicates incorrect Token");
							ts = -1;// force a stop, fatal error
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

		internal DeviceStatus DeviceStatus;

		internal HubStatus HubStatus;

		internal LightningStrike LightningStrike;

		internal Observation Observation;

		internal PrecipEvent PrecipEvent;

		internal RapidWind RapidWind;

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
			System.DateTime dtDateTime = DateTime.UnixEpoch;
			dtDateTime = dtDateTime.AddSeconds(epoch).ToLocalTime();
			return dtDateTime;
		}

		public static long ToUnixTimeSeconds(DateTime dt)
		{
			TimeSpan t = dt.ToUniversalTime() - DateTime.UnixEpoch;
			return (long) t.TotalSeconds;
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
		public WeatherPacket.MessageType MsgType { get; set; } = WeatherPacket.MessageType.DeviceStatus;

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
			catch
			{
				// do nothing
			}

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


				RadioStatus = packet.radio_stats[3] switch
				{
					0 => WeatherPacket.RadioStatus.RadioOff,
					1 => WeatherPacket.RadioStatus.RadioOn,
					3 => WeatherPacket.RadioStatus.RadioActive,
					_ => WeatherPacket.RadioStatus.RadioOff
				};
			}
		}

		public static WeatherPacket.MessageType MsgType => WeatherPacket.MessageType.HubStatus;


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
		public WeatherPacket.MessageType MsgType { get; set; } = WeatherPacket.MessageType.Observation;

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
			catch
			{
				// do nothing
			}

			if (packet.obs[0].Length >= 18)
			{
				LoadObservation(this, packet.obs[0]);
			}
		}

		public Observation(List<decimal?> obs)
		{
			LoadObservation(this, obs.ToArray());
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

			o.PrecipType = WeatherPacket.GetInt(ob[13]) switch
			{
				0 => WeatherPacket.PrecipType.None,
				1 => WeatherPacket.PrecipType.Rain,
				2 => WeatherPacket.PrecipType.Hail,
				_ => WeatherPacket.PrecipType.None,
			};
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
	public class PrecipEvent(WeatherPacket packet)
	{
		public string SerialNumber { get; set; } = packet.serial_number;
		public string HubSN { get; set; } = packet.hub_sn;
		public DateTime Timestamp { get; set; } = WeatherPacket.FromUnixTimeSeconds(packet.evt[0]);
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

		public static WeatherPacket.MessageType MsgType => WeatherPacket.MessageType.RapidWind;

		public string SerialNumber { get; set; }
		public string HubSN { get; set; }
		public DateTime Timestamp { get; set; }
		public decimal WindSpeed { get; set; } // mps
		public int WindDirection { get; set; }
	}
	#endregion
}
