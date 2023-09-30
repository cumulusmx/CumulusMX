using System;
using System.IO;
using System.Net;

using EmbedIO;

using ServiceStack;
using ServiceStack.Text;

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
				altitude = (int) cumulus.Altitude,
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

			var weatherflow = new JsonStationSettingsWeatherFlow()
			{ deviceid = cumulus.WeatherFlowOptions.WFDeviceId, tcpport = cumulus.WeatherFlowOptions.WFTcpPort, token = cumulus.WeatherFlowOptions.WFToken, dayshistory = cumulus.WeatherFlowOptions.WFDaysHist };

			var gw1000 = new JsonStationSettingsGw1000Conn()
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

			var ecowittapi = new JsonStationSettingsEcowittApi()
			{
				applicationkey = cumulus.EcowittApplicationKey,
				userkey = cumulus.EcowittUserApiKey,
				mac = cumulus.EcowittMacAddress
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
				wmr928 = wmr,
				weatherflow = weatherflow,
				ecowittapi = ecowittapi
			};

			var copy = new JsonWizardInternetCopy()
			{
				localcopy = cumulus.FtpOptions.LocalCopyEnabled,
				localcopyfolder = cumulus.FtpOptions.LocalCopyFolder,
			};

			var ftp = new JsonWizardInternetFtp()
			{
				enabled = cumulus.FtpOptions.Enabled,
				directory = cumulus.FtpOptions.Directory,
				ftpport = cumulus.FtpOptions.Port,
				sslftp = (int) cumulus.FtpOptions.FtpMode,
				hostname = cumulus.FtpOptions.Hostname,
				password = cumulus.FtpOptions.Password,
				username = cumulus.FtpOptions.Username,
				sshAuth = cumulus.FtpOptions.SshAuthen,
				pskFile = cumulus.FtpOptions.SshPskFile,
				phpurl = cumulus.FtpOptions.PhpUrl,
				phpsecret = cumulus.FtpOptions.PhpSecret,
			};
			var internet = new JsonWizardInternet()
			{
				copy = copy,
				ftp = ftp
			};

			var website = new JsonWizardWebSite()
			{
				interval = new JsonWizardWebInterval()
				{
					enabled = cumulus.WebIntervalEnabled,
					enableintervalftp = cumulus.FtpOptions.IntervalEnabled,
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
				var msg = "Error de-serializing Set-up Wizard Settings JSON: " + ex.Message;
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
					cumulus.FtpOptions.Enabled = settings.internet.ftp.enabled;
					if (cumulus.FtpOptions.Enabled)
					{
						cumulus.FtpOptions.FtpMode = (Cumulus.FtpProtocols) settings.internet.ftp.sslftp;
						if (cumulus.FtpOptions.FtpMode == Cumulus.FtpProtocols.FTP || cumulus.FtpOptions.FtpMode == Cumulus.FtpProtocols.FTPS || cumulus.FtpOptions.FtpMode == Cumulus.FtpProtocols.SFTP)
						{
							cumulus.FtpOptions.Directory = string.IsNullOrWhiteSpace(settings.internet.ftp.directory) ? string.Empty : settings.internet.ftp.directory.Trim();
							cumulus.FtpOptions.Port = settings.internet.ftp.ftpport;
							cumulus.FtpOptions.Hostname = string.IsNullOrWhiteSpace(settings.internet.ftp.hostname) ? string.Empty : settings.internet.ftp.hostname.Trim();
							cumulus.FtpOptions.Password = string.IsNullOrWhiteSpace(settings.internet.ftp.password) ? string.Empty : settings.internet.ftp.password.Trim();
							cumulus.FtpOptions.Username = string.IsNullOrWhiteSpace(settings.internet.ftp.username) ? string.Empty : settings.internet.ftp.username.Trim();
						}

						if (cumulus.FtpOptions.FtpMode == Cumulus.FtpProtocols.SFTP)
						{
							cumulus.FtpOptions.SshAuthen = string.IsNullOrWhiteSpace(settings.internet.ftp.sshAuth) ? string.Empty : settings.internet.ftp.sshAuth.Trim();
							cumulus.FtpOptions.SshPskFile = string.IsNullOrWhiteSpace(settings.internet.ftp.pskFile) ? string.Empty : settings.internet.ftp.pskFile.Trim();
						}

						if (cumulus.FtpOptions.FtpMode == Cumulus.FtpProtocols.PHP)
						{
							cumulus.FtpOptions.PhpUrl = settings.internet.ftp.phpurl;
							cumulus.FtpOptions.PhpSecret = settings.internet.ftp.phpsecret;
						}
					}

					cumulus.FtpOptions.LocalCopyEnabled = settings.internet.copy.localcopy;
					if (cumulus.FtpOptions.LocalCopyEnabled)
					{
						cumulus.FtpOptions.LocalCopyFolder = string.IsNullOrWhiteSpace(settings.internet.copy.localcopyfolder) ? string.Empty : settings.internet.copy.localcopyfolder.Trim();
					}

					// Now flag all the standard files to FTP/Copy or not
					// do not process last entry = wxnow.txt, it is not used by the standard site
					for (var i = 0; i < cumulus.StdWebFiles.Length - 1; i++)
					{
						cumulus.StdWebFiles[i].FTP = cumulus.FtpOptions.Enabled;
						cumulus.StdWebFiles[i].Copy = cumulus.FtpOptions.LocalCopyEnabled;
					}
					// and graph data files
					for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
					{
						cumulus.GraphDataFiles[i].FTP = cumulus.FtpOptions.Enabled;
						cumulus.GraphDataFiles[i].Copy = cumulus.FtpOptions.LocalCopyEnabled;
					}
					// and EOD data files
					for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
					{
						cumulus.GraphDataEodFiles[i].FTP = cumulus.FtpOptions.Enabled;
						cumulus.GraphDataEodFiles[i].Copy = cumulus.FtpOptions.LocalCopyEnabled;
					}
					// and Realtime files

					// realtime.txt is not used by the standard site
					//cumulus.RealtimeFiles[0].Create = cumulus.FtpOptions.Enabled || cumulus.FtpOptions.LocalCopyEnabled;
					//cumulus.RealtimeFiles[0].FTP = cumulus.FtpOptions.Enabled;
					//cumulus.RealtimeFiles[0].Copy = cumulus.FtpOptions.LocalCopyEnabled;

					// realtimegauges.txt IS used by the standard site
					cumulus.RealtimeFiles[1].FTP = cumulus.FtpOptions.Enabled;
					cumulus.RealtimeFiles[1].Copy = cumulus.FtpOptions.LocalCopyEnabled;

					// and Moon image
					cumulus.MoonImage.Enabled = cumulus.FtpOptions.Enabled || cumulus.FtpOptions.LocalCopyEnabled;
					cumulus.MoonImage.Ftp = cumulus.FtpOptions.Enabled;
					cumulus.MoonImage.Copy = cumulus.FtpOptions.LocalCopyEnabled;
					if (cumulus.MoonImage.Enabled)
						cumulus.MoonImage.CopyDest = cumulus.FtpOptions.LocalCopyFolder + "images" + cumulus.DirectorySeparator + "moon.png";

					// and NOAA reports
					cumulus.NOAAconf.Create = cumulus.FtpOptions.Enabled || cumulus.FtpOptions.LocalCopyEnabled;
					cumulus.NOAAconf.AutoFtp = cumulus.FtpOptions.Enabled;
					cumulus.NOAAconf.AutoCopy = cumulus.FtpOptions.LocalCopyEnabled;
					if (cumulus.NOAAconf.AutoCopy)
					{
						cumulus.NOAAconf.CopyFolder = cumulus.FtpOptions.LocalCopyFolder + "Reports";
					}
					if (cumulus.NOAAconf.AutoFtp)
					{
						cumulus.NOAAconf.FtpFolder = cumulus.FtpOptions.Directory + (cumulus.FtpOptions.Directory.EndsWith("/") ? "" : "/") + "Reports";
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
						cumulus.RealtimeTimer.Stop();
						cumulus.RealtimeFTPDisconnect();
					}

					cumulus.WebIntervalEnabled = settings.website.interval.enabled;
					if (cumulus.WebIntervalEnabled)
					{
						cumulus.FtpOptions.IntervalEnabled = settings.website.interval.enableintervalftp;
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
					cumulus.LocationName = string.IsNullOrWhiteSpace(settings.location.sitename) ? string.Empty : settings.location.sitename.Trim();
					cumulus.LocationDesc = string.IsNullOrWhiteSpace(settings.location.description) ? string.Empty : settings.location.description.Trim();

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
						cumulus.WindDPlaces = cumulus.StationOptions.RoundWindSpeed ? 0 : cumulus.WindDPlaceDefaults[cumulus.Units.Wind];
						cumulus.WindAvgDPlaces = cumulus.WindDPlaces;
					}
					if (cumulus.Units.Press != settings.units.pressure)
					{
						cumulus.Units.Press = settings.units.pressure;
						cumulus.ChangePressureUnits();
						cumulus.PressDPlaces = cumulus.PressDPlaceDefaults[cumulus.Units.Press];
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
						cumulus.RainDPlaces = cumulus.RainDPlaceDefaults[cumulus.Units.Rain];
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Units settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// data logging
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
						cumulus.LogConsoleMessage("*** Station type changed, restart required ***", ConsoleColor.Yellow);
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
							cumulus.DavisOptions.IPAddr = string.IsNullOrWhiteSpace(settings.station.davisvp2.tcpsettings.ipaddress) ? string.Empty : settings.station.davisvp2.tcpsettings.ipaddress.Trim();
							cumulus.DavisOptions.PeriodicDisconnectInterval = settings.station.davisvp2.tcpsettings.disconperiod;
						}
						if (cumulus.DavisOptions.ConnectionType == 0)
						{
							cumulus.ComportName = string.IsNullOrWhiteSpace(settings.station.davisvp2.comportname) ? string.Empty : settings.station.davisvp2.comportname.Trim();
						}

						// set defaults for Davis
						cumulus.UVdecimals = 1;

						if (settings.units.rain == 1)
						{
							cumulus.RainDPlaces = 2;
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
						cumulus.DavisOptions.IPAddr = string.IsNullOrWhiteSpace(settings.station.daviswll.network.ipaddress) ? string.Empty : settings.station.daviswll.network.ipaddress.Trim();

						cumulus.WllApiKey = string.IsNullOrWhiteSpace(settings.station.daviswll.api.apiKey) ? string.Empty : settings.station.daviswll.api.apiKey.Trim();
						cumulus.WllApiSecret = string.IsNullOrWhiteSpace(settings.station.daviswll.api.apiSecret) ? string.Empty : settings.station.daviswll.api.apiSecret.Trim();
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
						cumulus.Gw1000IpAddress = string.IsNullOrWhiteSpace(settings.station.gw1000.ipaddress) ? string.Empty : settings.station.gw1000.ipaddress.Trim();
						cumulus.Gw1000AutoUpdateIpAddress = settings.station.gw1000.autoDiscover;
						cumulus.Gw1000MacAddress = string.IsNullOrWhiteSpace(settings.station.gw1000.macaddress) ? string.Empty : settings.station.gw1000.macaddress.Trim();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing GW1000 settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// weatherflow connection details
				try
				{
					if (settings.station.weatherflow != null)
					{
						cumulus.WeatherFlowOptions.WFDeviceId = settings.station.weatherflow.deviceid;
						cumulus.WeatherFlowOptions.WFTcpPort = settings.station.weatherflow.tcpport;
						cumulus.WeatherFlowOptions.WFToken = string.IsNullOrWhiteSpace(settings.station.weatherflow.token) ? string.Empty : settings.station.weatherflow.token.Trim();
						cumulus.WeatherFlowOptions.WFDaysHist = settings.station.weatherflow.dayshistory;
					}
				}
				catch (Exception ex)
				{
					var msg = $"Error processing WeatherFlow settings: {ex.Message}";
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
						cumulus.EwOptions.Filename = string.IsNullOrWhiteSpace(settings.station.easyw.filename) ? string.Empty : settings.station.easyw.filename.Trim();
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
						cumulus.ComportName = string.IsNullOrWhiteSpace(settings.station.imet.comportname) ? cumulus.ComportName : settings.station.imet.comportname.Trim();
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
						cumulus.ComportName = string.IsNullOrWhiteSpace(settings.station.wmr928.comportname) ? cumulus.ComportName : settings.station.wmr928.comportname.Trim();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WMR928 settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Ecowitt API
				try
				{
					if (settings.station.ecowittapi != null)
					{
						cumulus.EcowittApplicationKey = string.IsNullOrWhiteSpace(settings.station.ecowittapi.applicationkey) ? null : settings.station.ecowittapi.applicationkey.Trim();
						cumulus.EcowittUserApiKey = string.IsNullOrWhiteSpace(settings.station.ecowittapi.userkey) ? null : settings.station.ecowittapi.userkey.Trim();
						cumulus.EcowittMacAddress = string.IsNullOrWhiteSpace(settings.station.ecowittapi.mac) ? null : settings.station.ecowittapi.mac.Trim();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt API settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}


				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Wizard settings: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Station Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		private string degToString(decimal degrees, bool lat)
		{
			var degs = (int) Math.Floor(Math.Abs(degrees));
			var minsF = (Math.Abs(degrees) - degs) * 60;
			var secs = (int) Math.Round((minsF - Math.Floor(minsF)) * 60);
			var mins = (int) Math.Floor(minsF);
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
		public JsonWizardInternet internet { get; set; }
		public JsonWizardWebSite website { get; set; }
	}

	internal class JsonWizardLocation
	{
		public decimal latitude { get; set; }
		public decimal longitude { get; set; }
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
		public JsonStationSettingsGw1000Conn gw1000 { get; set; }
		public JsonWizardFineOffset fineoffset { get; set; }
		public JsonWizardEasyWeather easyw { get; set; }
		public JsonWizardImet imet { get; set; }
		public JsonStationSettingsWMR928 wmr928 { get; set; }
		public JsonStationSettingsWeatherFlow weatherflow { get; set; }
		public JsonStationSettingsEcowittApi ecowittapi { get; set; }
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

	internal class JsonWizardInternet
	{
		public JsonWizardInternetCopy copy { get; set; }
		public JsonWizardInternetFtp ftp { get; set; }
	}

	internal class JsonWizardInternetCopy
	{
		public bool localcopy { get; set; }
		public string localcopyfolder { get; set; }

	}

	internal class JsonWizardInternetFtp
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
		public string phpurl { get; set; }
		public string phpsecret { get; set; }
	}

	internal class JsonWizardWebSite
	{
		public JsonWizardWebInterval interval { get; set; }
		public JsonWizardWebRealtime realtime { get; set; }
	}

	internal class JsonWizardWebInterval
	{
		public bool enabled { get; set; }
		public bool enableintervalftp { get; set; }
		public int ftpinterval { get; set; }
	}

	internal class JsonWizardWebRealtime
	{
		public bool enabled { get; set; }
		public bool enablerealtimeftp { get; set; }
		public int realtimeinterval { get; set; }
	}

}
