﻿using System;
using System.Globalization;
using System.IO;
using System.Net;

using EmbedIO;

using ServiceStack;
using ServiceStack.Text;

namespace CumulusMX
{
	public class Wizard(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string GetAlpacaFormData()
		{
			var location = new JsonLocation()
			{
				sitename = cumulus.LocationName,
				description = cumulus.LocationDesc,
				latitude = cumulus.Latitude,
				longitude = cumulus.Longitude,
				altitude = (int) cumulus.Altitude,
				altitudeunit = cumulus.AltitudeInFeet ? "feet" : "metres",
			};

			var units = new JsonUnits()
			{
				wind = cumulus.Units.Wind,
				pressure = cumulus.Units.Press,
				temp = cumulus.Units.Temp,
				rain = cumulus.Units.Rain,
				snow = cumulus.Units.SnowDepth
			};

			var logs = new JsonLogs()
			{
				loginterval = cumulus.DataLogInterval,
				logrollover = new StationSettings.JsonLogRollover()
				{
					time = cumulus.RolloverHour == 9 ? "9am" : "midnight",
					summer10am = cumulus.Use10amInSummer
				}
			};

			var davisvp = new JsonDavisVp()
			{
				conntype = cumulus.DavisOptions.ConnectionType,
				comportname = cumulus.ComportName,
				tcpsettings = new StationSettings.JsonTCPsettings()
				{
					ipaddress = cumulus.DavisOptions.IPAddr,
					disconperiod = cumulus.DavisOptions.PeriodicDisconnectInterval
				}
			};

			var daviswll = new JsonDavisWll()
			{
				network = new StationSettings.JsonWllNetwork()
				{
					autoDiscover = cumulus.WLLAutoUpdateIpAddress,
					ipaddress = cumulus.DavisOptions.IPAddr
				},
				api = new StationSettings.JsonWllApi()
				{
					apiKey = cumulus.WllApiKey,
					apiSecret = cumulus.WllApiSecret,
					apiStationId = cumulus.WllStationId
				},
				primary = new StationSettings.JsonWllPrimary()
				{
					wind = cumulus.WllPrimaryWind,
					temphum = cumulus.WllPrimaryTempHum,
					rain = cumulus.WllPrimaryRain,
					solar = cumulus.WllPrimarySolar,
					uv = cumulus.WllPrimaryUV
				}
			};

			var daviscloud = new JsonDavisWll()
			{
				api = new StationSettings.JsonWllApi()
				{
					apiKey = cumulus.WllApiKey,
					apiSecret = cumulus.WllApiSecret,
					apiStationId = cumulus.WllStationId
				},
				primary = new StationSettings.JsonWllPrimary()
				{
					wind = cumulus.WllPrimaryWind,
					temphum = cumulus.WllPrimaryTempHum,
					rain = cumulus.WllPrimaryRain,
					solar = cumulus.WllPrimarySolar,
					uv = cumulus.WllPrimaryUV
				}
			};

			var weatherflow = new StationSettings.JsonWeatherFlow()
			{
				deviceid = cumulus.WeatherFlowOptions.WFDeviceId,
				tcpport = cumulus.WeatherFlowOptions.WFTcpPort,
				token = cumulus.WeatherFlowOptions.WFToken,
				dayshistory = cumulus.WeatherFlowOptions.WFDaysHist
			};

			var gw1000 = new StationSettings.JsonGw1000Conn()
			{
				ipaddress = cumulus.Gw1000IpAddress,
				autoDiscover = cumulus.Gw1000AutoUpdateIpAddress,
				macaddress = cumulus.Gw1000MacAddress
			};

			var ecowittHttpApi = new StationSettings.JsonHttpApi()
			{
				ipaddress = cumulus.Gw1000IpAddress,
				password = cumulus.EcowittHttpPassword,
				usesdcard = cumulus.EcowittUseSdCard
			};

			var fineoffset = new JsonFineOffset()
			{
				syncreads = cumulus.FineOffsetOptions.SyncReads,
				readavoid = cumulus.FineOffsetOptions.ReadAvoidPeriod
			};

			var easyweather = new JsonEasyWeather()
			{
				interval = cumulus.EwOptions.Interval,
				filename = cumulus.EwOptions.Filename
			};

			var imet = new JsonImet()
			{
				comportname = cumulus.ComportName,
				baudrate = cumulus.ImetOptions.BaudRate
			};

			var wmr = new StationSettings.JsonWmr928()
			{
				comportname = cumulus.ComportName
			};

			var ecowittapi = new StationSettings.JsonEcowittApi()
			{
				applicationkey = cumulus.EcowittApplicationKey,
				userkey = cumulus.EcowittUserApiKey,
				mac = cumulus.EcowittMacAddress,
				interval = cumulus.EcowittCloudDataUpdateInterval
			};

			var jsonstn = new StationSettings.JsonJsonStation()
			{
				conntype = cumulus.JsonStationOptions.Connectiontype,
				filename = cumulus.JsonStationOptions.SourceFile,
				mqttserver = cumulus.JsonStationOptions.MqttServer,
				mqttport = cumulus.JsonStationOptions.MqttPort,
				mqttuser = cumulus.JsonStationOptions.MqttUsername,
				mqttpass = cumulus.JsonStationOptions.MqttPassword,
				mqtttopic = cumulus.JsonStationOptions.MqttTopic
			};


			var station = new JsonStation()
			{
				manufacturer = (int) cumulus.Manufacturer,
				stationtype = cumulus.StationType,
				stationmodel = cumulus.StationModel,
				firstRun = cumulus.FirstRun,
				beginDate = cumulus.RecordsBeganDateTime.ToString("yyyy-MM-dd"),
				davisvp2 = davisvp,
				daviswll = daviswll,
				daviscloud = daviscloud,
				gw1000 = gw1000,
				ecowitthttpapi = ecowittHttpApi,
				fineoffset = fineoffset,
				easyw = easyweather,
				imet = imet,
				wmr928 = wmr,
				weatherflow = weatherflow,
				ecowittapi = ecowittapi,
				jsonstation = jsonstn
			};

			var copy = new JsonInternetCopy()
			{
				localcopy = cumulus.FtpOptions.LocalCopyEnabled,
				localcopyfolder = cumulus.FtpOptions.LocalCopyFolder,
			};

			var ftp = new JsonInternetFtp()
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
			var internet = new JsonInternet()
			{
				copy = copy,
				ftp = ftp
			};

			var website = new JsonWebSite()
			{
				interval = new JsonWebInterval()
				{
					enabled = cumulus.WebIntervalEnabled,
					enableintervalftp = cumulus.FtpOptions.IntervalEnabled,
					ftpinterval = cumulus.UpdateInterval
				},
				realtime = new JsonWebRealtime()
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
			var errorMsg = string.Empty;
			var json = string.Empty;
			JsonWizard settings;

			cumulus.LogMessage("Updating settings from the First Time Wizard");

			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonWizard>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Set-up Wizard Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
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
						cumulus.NOAAconf.FtpFolder = cumulus.FtpOptions.Directory + (cumulus.FtpOptions.Directory.EndsWith('/') ? "" : "/") + "Reports";
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing internet settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
						if ((int) cumulus.RealtimeTimer.Interval != cumulus.RealtimeInterval)
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
						if ((int) cumulus.WebTimer.Interval != cumulus.UpdateInterval * 60 * 1000)
							cumulus.WebTimer.Interval = cumulus.UpdateInterval * 60 * 1000;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing web settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Units
				try
				{
					if (cumulus.Units.Wind != settings.units.wind)
					{
						cumulus.Limit.WindHigh = settings.units.wind switch
						{
							0 => ConvertUnits.UserWindToMS(cumulus.Limit.WindHigh),
							1 => ConvertUnits.UserWindToMPH(cumulus.Limit.WindHigh),
							2 => ConvertUnits.UserWindToKPH(cumulus.Limit.WindHigh),
							3 => ConvertUnits.UserWindToKnots(cumulus.Limit.WindHigh),
							_ => cumulus.Limit.WindHigh
						};

						cumulus.Units.Wind = settings.units.wind;
						cumulus.ChangeWindUnits();
						cumulus.WindDPlaces = cumulus.StationOptions.RoundWindSpeed ? 0 : cumulus.WindDPlaceDefaults[cumulus.Units.Wind];
						cumulus.WindAvgDPlaces = cumulus.WindDPlaces;
					}
					if (cumulus.Units.Press != settings.units.pressure)
					{
						switch (settings.units.pressure)
						{
							case 0:
							case 1:
								cumulus.Limit.PressHigh = ConvertUnits.UserPressToHpa(cumulus.Limit.PressHigh);
								cumulus.Limit.PressLow = ConvertUnits.UserPressToHpa(cumulus.Limit.PressLow);
								break;
							case 2:
								cumulus.Limit.PressHigh = ConvertUnits.UserPressToIN(cumulus.Limit.PressHigh);
								cumulus.Limit.PressLow = ConvertUnits.UserPressToIN(cumulus.Limit.PressLow);
								break;
							case 3:
								cumulus.Limit.PressHigh = ConvertUnits.UserPressToKpa(cumulus.Limit.PressHigh);
								cumulus.Limit.PressLow = ConvertUnits.UserPressToKpa(cumulus.Limit.PressLow);
								break;
						}

						cumulus.Limit.StationPressHigh = ConvertUnits.PressMBToUser(MeteoLib.SeaLevelToStation(ConvertUnits.UserPressToHpa(cumulus.Limit.PressHigh), ConvertUnits.AltitudeM(cumulus.Altitude)));
						cumulus.Limit.StationPressLow = ConvertUnits.PressMBToUser(MeteoLib.SeaLevelToStation(ConvertUnits.UserPressToHpa(cumulus.Limit.PressLow), ConvertUnits.AltitudeM(cumulus.Altitude)));

						cumulus.Units.Press = settings.units.pressure;
						cumulus.ChangePressureUnits();
						cumulus.PressDPlaces = cumulus.PressDPlaceDefaults[cumulus.Units.Press];
					}
					if (cumulus.Units.Temp != settings.units.temp)
					{
						switch (settings.units.temp)
						{
							case 0:
								cumulus.Limit.TempHigh = ConvertUnits.UserTempToC(cumulus.Limit.TempHigh);
								cumulus.Limit.TempLow = ConvertUnits.UserTempToC(cumulus.Limit.TempLow);
								cumulus.Limit.DewHigh = ConvertUnits.UserTempToC(cumulus.Limit.DewHigh);
								break;
							case 1:
								cumulus.Limit.TempHigh = ConvertUnits.UserTempToF(cumulus.Limit.TempHigh);
								cumulus.Limit.TempLow = ConvertUnits.UserTempToF(cumulus.Limit.TempLow);
								cumulus.Limit.DewHigh = ConvertUnits.UserTempToF(cumulus.Limit.DewHigh);
								break;
						}
						cumulus.Units.Temp = settings.units.temp;
						cumulus.ChangeTempUnits();
					}
					if (cumulus.Units.Rain != settings.units.rain)
					{
						cumulus.Units.Rain = settings.units.rain;
						cumulus.ChangeRainUnits();
						cumulus.RainDPlaces = cumulus.RainDPlaceDefaults[cumulus.Units.Rain];
					}

					cumulus.Units.SnowDepth = settings.units.snow;
					cumulus.SnowDPlaces = settings.units.snow == 0 ? 1 : 2;
					cumulus.SnowFormat = "F" + cumulus.SnowDPlaces;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Units settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Station type
				try
				{
					if (cumulus.StationType != settings.station.stationtype)
					{
						cumulus.LogWarningMessage("Station type changed, restart required");
						Cumulus.LogConsoleMessage("*** Station type changed, restart required ***", ConsoleColor.Yellow);
					}
					cumulus.Manufacturer = (Cumulus.StationManufacturer) settings.station.manufacturer;
					cumulus.StationType = settings.station.stationtype;
					cumulus.StationModel = settings.station.stationmodel;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Station Type setting: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Davis Cloud
				try
				{
					if (settings.station.daviscloud != null)
					{
						cumulus.WllApiKey = string.IsNullOrWhiteSpace(settings.station.daviscloud.api.apiKey) ? string.Empty : settings.station.daviscloud.api.apiKey.Trim();
						cumulus.WllApiSecret = string.IsNullOrWhiteSpace(settings.station.daviscloud.api.apiSecret) ? string.Empty : settings.station.daviscloud.api.apiSecret.Trim();
						cumulus.WllStationId = settings.station.daviscloud.api.apiStationId;

						if (settings.station.daviscloud.primary != null)
						{
							cumulus.WllPrimaryRain = settings.station.daviscloud.primary.rain;
							cumulus.WllPrimarySolar = settings.station.daviscloud.primary.solar;
							cumulus.WllPrimaryTempHum = settings.station.daviscloud.primary.temphum;
							cumulus.WllPrimaryUV = settings.station.daviscloud.primary.uv;
							cumulus.WllPrimaryWind = settings.station.daviscloud.primary.wind;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing davis cloud settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
						cumulus.Gw1000MacAddress = string.IsNullOrWhiteSpace(settings.station.gw1000.macaddress) ? string.Empty : settings.station.gw1000.macaddress.Trim().ToUpper();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing GW1000 settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}


				// HTTP Local API connection details
				try
				{
					if (settings.station.ecowitthttpapi != null)
					{
						cumulus.Gw1000IpAddress = string.IsNullOrWhiteSpace(settings.station.ecowitthttpapi.ipaddress) ? null : settings.station.ecowitthttpapi.ipaddress.Trim();
						cumulus.EcowittHttpPassword = string.IsNullOrWhiteSpace(settings.station.ecowitthttpapi.password) ? null : settings.station.ecowitthttpapi.password.Trim();
						cumulus.EcowittUseSdCard = settings.station.ecowitthttpapi.usesdcard;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt Local HTTP API settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
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
						cumulus.EcowittMacAddress = string.IsNullOrWhiteSpace(settings.station.ecowittapi.mac) ? null : settings.station.ecowittapi.mac.Trim().ToUpper();
						cumulus.EcowittCloudDataUpdateInterval = cumulus.StationType == 18 ? settings.station.ecowittapi.interval : 1;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt API settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// JSON data input
				try
				{
					if (settings.station.jsonstation != null)
					{
						cumulus.JsonStationOptions.Connectiontype = settings.station.jsonstation.conntype;
						if (cumulus.JsonStationOptions.Connectiontype == 0)
						{
							cumulus.JsonStationOptions.SourceFile = settings.station.jsonstation.filename.Trim();
						}
						else if (cumulus.JsonStationOptions.Connectiontype == 2)
						{
							cumulus.JsonStationOptions.MqttServer = string.IsNullOrWhiteSpace(settings.station.jsonstation.mqttserver) ? null : settings.station.jsonstation.mqttserver.Trim();
							cumulus.JsonStationOptions.MqttPort = settings.station.jsonstation.mqttport;
							cumulus.JsonStationOptions.MqttUsername = string.IsNullOrWhiteSpace(settings.station.jsonstation.mqttuser) ? null : settings.station.jsonstation.mqttuser.Trim();
							cumulus.JsonStationOptions.MqttPassword = string.IsNullOrWhiteSpace(settings.station.jsonstation.mqttpass) ? null : settings.station.jsonstation.mqttpass.Trim();
							cumulus.JsonStationOptions.MqttTopic = string.IsNullOrWhiteSpace(settings.station.jsonstation.mqtttopic) ? null : settings.station.jsonstation.mqtttopic.Trim();
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing JSON Data Input settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// BeginDate
				try
				{
					if (settings.station.firstRun)
					{
						cumulus.RecordsBeganDateTime = DateTime.ParseExact(settings.station.beginDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

						var startDateTime = cumulus.RecordsBeganDateTime.AddHours(-cumulus.GetHourInc(cumulus.RecordsBeganDateTime));
						using StreamWriter today = new StreamWriter(cumulus.TodayIniFile);
						today.WriteLine("[General]");
						today.WriteLine("Timestamp=" + startDateTime.ToString("s"));
						today.Close();

						Cumulus.LogConsoleMessage("First run. Cumulus will attempt to backfill data from " + startDateTime.ToString(), ConsoleColor.Yellow, true);
						cumulus.LogMessage("First run. Cumulus will attempt to backfill data from " + startDateTime.ToString());
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing first run date setting: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}


				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Wizard settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
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
				hemi = degrees >= 0 ? cumulus.Trans.compassp[0] : cumulus.Trans.compassp[8];
			else
				hemi = degrees <= 0 ? cumulus.Trans.compassp[12] : cumulus.Trans.compassp[4];

			return $"{hemi}&nbsp;{degs:D2}&deg;&nbsp;{mins:D2}&#39;&nbsp;{secs:D2}&quot;";
		}

		private sealed class JsonWizard
		{
			public JsonLocation location { get; set; }
			public JsonUnits units { get; set; }
			public JsonStation station { get; set; }
			public JsonLogs logs { get; set; }
			public JsonInternet internet { get; set; }
			public JsonWebSite website { get; set; }
		}

		private sealed class JsonLocation
		{
			public decimal latitude { get; set; }
			public decimal longitude { get; set; }
			public int altitude { get; set; }
			public string altitudeunit { get; set; }
			public string sitename { get; set; }
			public string description { get; set; }
		}

		private sealed class JsonUnits
		{
			public int wind { get; set; }
			public int pressure { get; set; }
			public int temp { get; set; }
			public int rain { get; set; }
			public int snow { get; set; }
		}

		private sealed class JsonLogs
		{
			public int loginterval { get; set; }
			public StationSettings.JsonLogRollover logrollover { get; set; }
		}

		private sealed class JsonStation
		{
			public int manufacturer { get; set; }
			public int stationtype { get; set; }
			public string stationmodel { get; set; }
			public bool firstRun { get; set; }
			public string beginDate { get; set; }
			public JsonDavisVp davisvp2 { get; set; }
			public JsonDavisWll daviswll { get; set; }
			public JsonDavisWll daviscloud { get; set; }
			public StationSettings.JsonGw1000Conn gw1000 { get; set; }
			public StationSettings.JsonHttpApi ecowitthttpapi { get; set; }
			public JsonFineOffset fineoffset { get; set; }
			public JsonEasyWeather easyw { get; set; }
			public JsonImet imet { get; set; }
			public StationSettings.JsonWmr928 wmr928 { get; set; }
			public StationSettings.JsonWeatherFlow weatherflow { get; set; }
			public StationSettings.JsonEcowittApi ecowittapi { get; set; }
			public StationSettings.JsonJsonStation jsonstation { get; set; }
		}

		private sealed class JsonDavisVp
		{
			public int conntype { get; set; }
			public string comportname { get; set; }
			public StationSettings.JsonTCPsettings tcpsettings { get; set; }
		}

		private sealed class JsonDavisWll
		{
			public StationSettings.JsonWllNetwork network { get; set; }
			public StationSettings.JsonWllApi api { get; set; }
			public StationSettings.JsonWllPrimary primary { get; set; }
		}

		private sealed class JsonFineOffset
		{
			public bool syncreads { get; set; }
			public int readavoid { get; set; }
		}

		private sealed class JsonEasyWeather
		{
			public double interval { get; set; }
			public string filename { get; set; }
		}

		private sealed class JsonImet
		{
			public string comportname { get; set; }
			public int baudrate { get; set; }
		}

		private sealed class JsonInternet
		{
			public JsonInternetCopy copy { get; set; }
			public JsonInternetFtp ftp { get; set; }
		}

		private sealed class JsonInternetCopy
		{
			public bool localcopy { get; set; }
			public string localcopyfolder { get; set; }

		}

		private sealed class JsonInternetFtp
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

		private sealed class JsonWebSite
		{
			public JsonWebInterval interval { get; set; }
			public JsonWebRealtime realtime { get; set; }
		}

		private sealed class JsonWebInterval
		{
			public bool enabled { get; set; }
			public bool enableintervalftp { get; set; }
			public int ftpinterval { get; set; }
		}

		private sealed class JsonWebRealtime
		{
			public bool enabled { get; set; }
			public bool enablerealtimeftp { get; set; }
			public int realtimeinterval { get; set; }
		}
	}
}
