using System;
using System.IO;
using System.Net;
using ServiceStack;
using ServiceStack.Text;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class Wizard
	{
		private readonly Cumulus cumulus;

		public Wizard(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string GetAlpacaFormData()
		{
			var location = new JsonWizardLocation()
			{
				sitename = cumulus.LocationName,
				description = cumulus.LocationDesc,
				latitude = cumulus.Latitude,
				longitude = cumulus.Longitude,
				altitude = (int)cumulus.Altitude,
				altitudeunit = cumulus.AltitudeInFeet ? "feet" : "metres",
			};

			var units = new JsonWizardUnits()
			{
				wind = cumulus.Units.Wind,
				pressure = cumulus.Units.Press,
				temp = cumulus.Units.Temp,
				rain = cumulus.Units.Rain
			};

			var logs = new JsonWizardLogs()
			{
				loginterval = cumulus.DataLogInterval,
				logrollover = new JsonStationSettingsLogRollover()
				{
					time = cumulus.RolloverHour == 9 ? "9am" : "midnight",
					summer10am = cumulus.Use10amInSummer
				}
			};

			var davisvp = new JsonWizardDavisVp()
			{
				conntype = cumulus.DavisOptions.ConnectionType,
				comportname = cumulus.ComportName,
				tcpsettings = new JsonStationSettingsTCPsettings()
				{
					ipaddress = cumulus.DavisOptions.IPAddr,
					disconperiod = cumulus.DavisOptions.PeriodicDisconnectInterval
				}
			};

			var daviswll = new JsonWizardDavisWll()
			{
				network = new JsonStationSettingsWLLNetwork()
				{
					autoDiscover = cumulus.WLLAutoUpdateIpAddress,
					ipaddress = cumulus.DavisOptions.IPAddr
				},
				api = new JsonStationSettingsWLLApi()
				{
					apiKey = cumulus.WllApiKey,
					apiSecret = cumulus.WllApiSecret,
					apiStationId = cumulus.WllStationId
				},
				primary = new JsonStationSettingsWllPrimary()
				{
					wind = cumulus.WllPrimaryWind,
					temphum = cumulus.WllPrimaryTempHum,
					rain = cumulus.WllPrimaryRain,
					solar = cumulus.WllPrimarySolar,
					uv = cumulus.WllPrimaryUV
				}
			};

			var gw1000 = new JSonStationSettingsGw1000Conn()
			{
				ipaddress = cumulus.Gw1000IpAddress,
				autoDiscover = cumulus.Gw1000AutoUpdateIpAddress,
				macaddress = cumulus.Gw1000MacAddress
			};

			var fineoffset = new JsonWizardFineOffset()
			{
				syncreads = cumulus.FineOffsetOptions.SyncReads,
				readavoid = cumulus.FineOffsetOptions.ReadAvoidPeriod
			};

			var easyweather = new JsonWizardEasyWeather()
			{
				interval = cumulus.EwOptions.Interval,
				filename = cumulus.EwOptions.Filename
			};

			var imet = new JsonWizardImet()
			{
				comportname = cumulus.ComportName,
				baudrate = cumulus.ImetOptions.BaudRate
			};

			var wmr = new JsonStationSettingsWMR928()
			{
				comportname = cumulus.ComportName
			};


			var station = new JsonWizardStation()
			{
				stationtype = cumulus.StationType,
				stationmodel = cumulus.StationModel,
				davisvp2 = davisvp,
				daviswll = daviswll,
				gw1000 = gw1000,
				fineoffset = fineoffset,
				easyw = easyweather,
				imet = imet,
				wmr928 = wmr
			};

			var internet = new JsonWiazrdInternet()
			{
				enabled = cumulus.FtpOptions.Enabled,
				directory = cumulus.FtpOptions.Directory,
				ftpport = cumulus.FtpOptions.Port,
				sslftp = (int)cumulus.FtpOptions.FtpMode,
				hostname = cumulus.FtpOptions.Hostname,
				password = cumulus.FtpOptions.Password,
				username = cumulus.FtpOptions.Username,
				sshAuth = cumulus.FtpOptions.SshAuthen,
				pskFile = cumulus.FtpOptions.SshPskFile
			};

			var website = new JsonWizardWebSite()
			{
				interval = new JsonWizardWebInterval()
				{
					enabled = cumulus.WebIntervalEnabled,
					autoupdate = cumulus.WebAutoUpdate,
					ftpinterval = cumulus.UpdateInterval
				},
				realtime = new JsonWizardWebRealtime()
				{
					enabled = cumulus.RealtimeIntervalEnabled,
					enablerealtimeftp = cumulus.FtpOptions.RealtimeEnabled,
					realtimeinterval = cumulus.RealtimeInterval / 1000
				}
			};

			var settings = new JsonWizard()
			{
				location = location,
				units = units,
				station = station,
				logs = logs,
				internet = internet,
				website = website
			};

			return JsonSerializer.SerializeToString(settings);
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = "";
			var json = "";
			JsonWizard settings;

			cumulus.LogMessage("Updating settings from the First Time Wizard");

			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonWizard>();
			}
			catch (Exception ex)
			{
				var msg = "Error deserializing Setup Wizard Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Wizard Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.LogMessage("Updating internet settings");

				// website settings
				try
				{
					cumulus.FtpOptions.Enabled = settings.internet.enabled;
					if (cumulus.FtpOptions.Enabled)
					{
						cumulus.FtpOptions.Directory = settings.internet.directory ?? string.Empty;
						cumulus.FtpOptions.Port = settings.internet.ftpport;
						cumulus.FtpOptions.Hostname = settings.internet.hostname ?? string.Empty;
						cumulus.FtpOptions.FtpMode = (Cumulus.FtpProtocols)settings.internet.sslftp;
						cumulus.FtpOptions.Password = settings.internet.password ?? string.Empty;
						cumulus.FtpOptions.Username = settings.internet.username ?? string.Empty;
						cumulus.FtpOptions.SshAuthen = settings.internet.sshAuth ?? string.Empty;
						cumulus.FtpOptions.SshPskFile = settings.internet.pskFile ?? string.Empty;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing internet settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// web settings
				try
				{
					cumulus.RealtimeIntervalEnabled = settings.website.realtime.enabled;
					if (cumulus.RealtimeIntervalEnabled)
					{
						cumulus.FtpOptions.RealtimeEnabled = settings.website.realtime.enablerealtimeftp;
						cumulus.RealtimeInterval = settings.website.realtime.realtimeinterval * 1000;
						if (cumulus.RealtimeTimer.Interval != cumulus.RealtimeInterval)
							cumulus.RealtimeTimer.Interval = cumulus.RealtimeInterval;
					}
					cumulus.RealtimeTimer.Enabled = cumulus.RealtimeIntervalEnabled;
					if (!cumulus.RealtimeTimer.Enabled || !cumulus.FtpOptions.RealtimeEnabled)
					{
						cumulus.RealtimeFTPDisconnect();
					}

					cumulus.WebIntervalEnabled = settings.website.interval.enabled;
					if (cumulus.WebIntervalEnabled)
					{
						cumulus.WebAutoUpdate = settings.website.interval.autoupdate;
						cumulus.UpdateInterval = settings.website.interval.ftpinterval;
						if (cumulus.WebTimer.Interval != cumulus.UpdateInterval * 60 * 1000)
							cumulus.WebTimer.Interval = cumulus.UpdateInterval * 60 * 1000;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing web settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Location
				try
				{
					cumulus.Altitude = settings.location.altitude;
					cumulus.AltitudeInFeet = (settings.location.altitudeunit == "feet");
					cumulus.LocationName = settings.location.sitename ?? string.Empty;
					cumulus.LocationDesc = settings.location.description ?? string.Empty;

					cumulus.Latitude = settings.location.latitude;

					cumulus.LatTxt = degToString(cumulus.Latitude, true);

					cumulus.Longitude = settings.location.longitude;

					cumulus.LonTxt = degToString(cumulus.Longitude, false);
				}
				catch (Exception ex)
				{
					var msg = "Error processing Location settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Units
				try
				{
					if (cumulus.Units.Wind != settings.units.wind)
					{
						cumulus.Units.Wind = settings.units.wind;
						cumulus.ChangeWindUnits();
					}
					if (cumulus.Units.Press != settings.units.pressure)
					{
						cumulus.Units.Press = settings.units.pressure;
						cumulus.ChangePressureUnits();
					}
					if (cumulus.Units.Temp != settings.units.temp)
					{
						cumulus.Units.Temp = settings.units.temp;
						cumulus.ChangeTempUnits();
					}
					if (cumulus.Units.Rain != settings.units.rain)
					{
						cumulus.Units.Rain = settings.units.rain;
						cumulus.ChangeRainUnits();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Units settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// logging
				try
				{
					cumulus.DataLogInterval = settings.logs.loginterval;
					cumulus.RolloverHour = settings.logs.logrollover.time == "9am" ? 9 : 0;
					if (cumulus.RolloverHour == 9)
						cumulus.Use10amInSummer = settings.logs.logrollover.summer10am;

				}
				catch (Exception ex)
				{
					var msg = "Error processing Logging setting: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Station type
				try
				{
					if (cumulus.StationType != settings.station.stationtype)
					{
						cumulus.LogMessage("Station type changed, restart required");
						cumulus.LogConsoleMessage("*** Station type changed, restart required ***");
					}
					cumulus.StationType = settings.station.stationtype;
					cumulus.StationModel = settings.station.stationmodel;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Station Type setting: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Davis VP/VP2/Vue
				try
				{
					if (settings.station.davisvp2 != null)
					{
						cumulus.DavisOptions.ConnectionType = settings.station.davisvp2.conntype;
						if (settings.station.davisvp2.tcpsettings != null)
						{
							cumulus.DavisOptions.IPAddr = settings.station.davisvp2.tcpsettings.ipaddress ?? string.Empty;
							cumulus.DavisOptions.PeriodicDisconnectInterval = settings.station.davisvp2.tcpsettings.disconperiod;
						}
						if (cumulus.DavisOptions.ConnectionType == 0)
						{
							cumulus.ComportName = settings.station.davisvp2.comportname;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Davis VP/VP2/Vue settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WLL
				try
				{
					if (settings.station.daviswll != null)
					{
						cumulus.DavisOptions.ConnectionType = 2; // Always TCP/IP for WLL
						cumulus.WLLAutoUpdateIpAddress = settings.station.daviswll.network.autoDiscover;
						cumulus.DavisOptions.IPAddr = settings.station.daviswll.network.ipaddress ?? string.Empty;

						cumulus.WllApiKey = settings.station.daviswll.api.apiKey;
						cumulus.WllApiSecret = settings.station.daviswll.api.apiSecret;
						cumulus.WllStationId = settings.station.daviswll.api.apiStationId;

						cumulus.WllPrimaryRain = settings.station.daviswll.primary.rain;
						cumulus.WllPrimarySolar = settings.station.daviswll.primary.solar;
						cumulus.WllPrimaryTempHum = settings.station.daviswll.primary.temphum;
						cumulus.WllPrimaryUV = settings.station.daviswll.primary.uv;
						cumulus.WllPrimaryWind = settings.station.daviswll.primary.wind;

					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WLL settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// GW1000 connection details
				try
				{
					if (settings.station.gw1000 != null)
					{
						cumulus.Gw1000IpAddress = settings.station.gw1000.ipaddress;
						cumulus.Gw1000AutoUpdateIpAddress = settings.station.gw1000.autoDiscover;
						cumulus.Gw1000MacAddress = settings.station.gw1000.macaddress;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing GW1000 settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// FineOffset
				try
				{
					if (settings.station.fineoffset != null)
					{
						cumulus.FineOffsetOptions.SyncReads = settings.station.fineoffset.syncreads;
						cumulus.FineOffsetOptions.ReadAvoidPeriod = settings.station.fineoffset.readavoid;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Fine Offset settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// EasyWeather
				try
				{
					if (settings.station.easyw != null)
					{
						cumulus.EwOptions.Interval = settings.station.easyw.interval;
						cumulus.EwOptions.Filename = settings.station.easyw.filename;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing EasyWeather settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Instromet
				try
				{
					if (settings.station.imet != null)
					{
						cumulus.ComportName = settings.station.imet.comportname ?? cumulus.ComportName;
						cumulus.ImetOptions.BaudRate = settings.station.imet.baudrate;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Instromet settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WMR928
				try
				{
					if (settings.station.wmr928 != null)
					{
						cumulus.ComportName = settings.station.wmr928.comportname ?? cumulus.ComportName;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WMR928 settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Station settings: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Station Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		private string degToString(double degrees, bool lat)
		{
			var degs = (int)Math.Floor(Math.Abs(degrees));
			var minsF = (Math.Abs(degrees) - degs) * 60.0;
			var secs = (int)Math.Round((minsF - Math.Floor(minsF)) * 60.0);
			var mins = (int)Math.Floor(minsF);
			string hemi;
			if (lat)
				hemi = degrees >= 0 ? "N" : "S";
			else
				hemi = degrees <= 0 ? "W" : "E";

			return $"{hemi}&nbsp;{degs:D2}&deg;&nbsp;{mins:D2}&#39;&nbsp;{secs:D2}&quot;";
		}
	}

	internal class JsonWizard
	{
		public JsonWizardLocation location { get; set; }
		public JsonWizardUnits units { get; set; }
		public JsonWizardStation station { get; set; }
		public JsonWizardLogs logs { get; set; }
		public JsonWiazrdInternet internet { get; set; }
		public JsonWizardWebSite website { get; set; }
	}

	internal class JsonWizardLocation
	{
		public double latitude { get; set; }
		public double longitude { get; set; }
		public int altitude { get; set; }
		public string altitudeunit { get; set; }
		public string sitename { get; set; }
		public string description { get; set; }
	}


	internal class JsonWizardUnits
	{
		public int wind { get; set; }
		public int pressure { get; set; }
		public int temp { get; set; }
		public int rain { get; set; }
	}

	internal class JsonWizardLogs
	{
		public int loginterval { get; set; }
		public JsonStationSettingsLogRollover logrollover { get; set; }
	}

	internal class JsonWizardStation
	{
		public int stationtype { get; set; }
		public string stationmodel { get; set; }
		public JsonWizardDavisVp davisvp2 { get; set; }
		public JsonWizardDavisWll daviswll { get; set; }
		public JSonStationSettingsGw1000Conn gw1000 { get; set; }
		public JsonWizardFineOffset fineoffset { get; set; }
		public JsonWizardEasyWeather easyw { get; set; }
		public JsonWizardImet imet { get; set; }
		public JsonStationSettingsWMR928 wmr928 { get; set; }
	}

	internal class JsonWizardDavisVp
	{
		public int conntype { get; set; }
		public string comportname { get; set; }
		public JsonStationSettingsTCPsettings tcpsettings { get; set; }
	}

	internal class JsonWizardDavisWll
	{
		public JsonStationSettingsWLLNetwork network { get; set; }
		public JsonStationSettingsWLLApi api { get; set; }
		public JsonStationSettingsWllPrimary primary { get; set; }
	}

	internal class JsonWizardFineOffset
	{
		public bool syncreads { get; set; }
		public int readavoid { get; set; }
	}

	internal class JsonWizardEasyWeather
	{
		public double interval { get; set; }
		public string filename { get; set; }
	}

	internal class JsonWizardImet
	{
		public string comportname { get; set; }
		public int baudrate { get; set; }
	}

	internal class JsonWiazrdInternet
	{
		public bool enabled { get; set; }
		public string hostname { get; set; }
		public int ftpport { get; set; }
		public int sslftp { get; set; }
		public string directory { get; set; }
		public string username { get; set; }
		public string password { get; set; }
		public string sshAuth { get; set; }
		public string pskFile { get; set; }
	}

	internal class JsonWizardWebSite
	{
		public JsonWizardWebInterval interval { get; set; }
		public JsonWizardWebRealtime realtime { get; set; }
	}

	internal class JsonWizardWebInterval
	{
		public bool enabled { get; set; }
		public bool autoupdate { get; set; }
		public int ftpinterval { get; set; }
	}

	internal class JsonWizardWebRealtime
	{
		public bool enabled { get; set; }
		public bool enablerealtimeftp { get; set; }
		public int realtimeinterval { get; set; }
	}

}
