using System;
using System.IO;
using System.Text;

using EmbedIO;

using MQTTnet;

using ServiceStack;
using ServiceStack.Text;


namespace CumulusMX
{
	internal class JsonStation : WeatherStation
	{
		private readonly WeatherStation station;
		private readonly bool mainStation;

		private bool haveTemp = false;
		private bool haveHum = false;
		private bool haveWind = false;

		//private static readonly decimal cm2in = 1 / (decimal) 2.54;
		//private static readonly decimal in2cm = 2.54;
		private static readonly decimal in2mm = (decimal) 25.4;
		//private static readonly decimal mm2in = 1 / (decimal) 25.4;

		private FileSystemWatcher watcher;

		private DateTime lastFileUpdateTime = DateTime.MinValue;

		public JsonStation(Cumulus cumulus, WeatherStation station = null) : base(cumulus, station != null)
		{
			this.station = station;

			mainStation = station == null;

			if (mainStation)
			{
				cumulus.LogMessage("Creating JSON Station");
				this.station = this;
			}
			else
			{
				cumulus.LogMessage("Creating Extra Sensors - JSON Station");
			}

			// Let's decode the Unix ts to DateTime
			JsConfig.Init(new Config
			{
				DateHandler = DateHandler.UnixTime
			});

			// Do not set these if we are only using extra sensors
			if (mainStation)
			{
				// does not provide a forecast, force MX to provide it
				cumulus.UseCumulusForecast = true;
				Start();
			}
		}


		public override void Start()
		{
			if (mainStation)
			{
				DoDayResetIfNeeded();
			}

			var connectionType = mainStation ? cumulus.JsonStationOptions.Connectiontype : cumulus.JsonExtraStationOptions.Connectiontype;

			switch (connectionType)
			{
				case 0:
					GetDataFromFile();
					break;
				case 1:
					// Get data from HTTP
					// Nothing to do, the API will send us the data
					break;
				case 2:
					if (!string.IsNullOrEmpty(cumulus.JsonStationOptions.MqttServer) && !string.IsNullOrEmpty(cumulus.JsonStationOptions.MqttTopic))
					{
						SetupMqttClient();
					}
					else
					{
						cumulus.LogErrorMessage("JSON Data Input: Unable to configure MQTT client - Server or topic name is blank");
						return;
					}
					break;
				default:
					cumulus.LogErrorMessage("Unable to start JSON data input station due to invalid connection type = " + connectionType);
					return;
			}

			timerStartNeeded = true;
		}

		public override void Stop()
		{
			var connectionType = mainStation ? cumulus.JsonStationOptions.Connectiontype : cumulus.JsonExtraStationOptions.Connectiontype;

			switch (connectionType)
			{
				case 0:
					// Get data from file
					watcher.Dispose();
					break;
				case 1:
					// Get data from HTTP
					// Nothing to do, the API is sending us the data
					break;
				case 2:
					JsonStationMqtt.Disconnect();
					break;
			}
		}


		private void GetDataFromFile()
		{
			var srcFile = mainStation ? cumulus.JsonStationOptions.SourceFile : cumulus.JsonExtraStationOptions.SourceFile;

			if (string.IsNullOrEmpty(srcFile))
			{
				cumulus.LogErrorMessage("JSON Station: Using file moniotoring, but not filename provided!");
				return;
			}

			cumulus.LogMessage("JSON Data: Monitoring file = " + srcFile);

			try
			{
				var fullPath = Path.GetFullPath(srcFile);
				var fileName = Path.GetFileName(fullPath);
				var directoryPath = Path.GetDirectoryName(fullPath);

				watcher = new FileSystemWatcher(directoryPath)
				{
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
					Filter = fileName
				};

				watcher.Changed += OnFileChanged;
				watcher.Created += OnFileChanged;
				watcher.EnableRaisingEvents = true;

				cumulus.LogMessage("JSON Data: FileWatcher created to monitor file = " + srcFile);

				// trigger an inital read of the file
				if (File.Exists(srcFile))
				{
					OnFileChanged(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, directoryPath, fileName));
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "GetDataFromFile: Error creating watcher");
			}
		}


		private void OnFileChanged(object sender, FileSystemEventArgs e)
		{
			cumulus.LogDebugMessage("OnFileChanged: File change type = " + e.ChangeType);
			if (e.ChangeType != WatcherChangeTypes.Changed && e.ChangeType != WatcherChangeTypes.Created)
			{
				cumulus.LogDebugMessage("OnFileChanged: Ignoring file change type = " + e.ChangeType);
				return;
			}

			try
			{
				var timeDiff = DateTime.UtcNow.ToUnixTimeMs() - lastFileUpdateTime.ToUnixTimeMs();
				var delay = mainStation ? cumulus.JsonStationOptions.FileIgnoreTime : cumulus.JsonExtraStationOptions.FileIgnoreTime;

				cumulus.LogDebugMessage($"OnFileChanged: File change time diff = " + timeDiff);

				if (timeDiff < delay)
				{
					cumulus.LogDebugMessage($"OnFileChanged: File update occured within the File Read Delay time ({delay} ms), ignoring this change");
					return;
				}

				lastFileUpdateTime = DateTime.UtcNow;

				if (File.Exists(e.FullPath))
				{
					System.Threading.Thread.Sleep(mainStation ? cumulus.JsonStationOptions.FileReadDelay : cumulus.JsonExtraStationOptions.FileReadDelay);
					var content = File.ReadAllText(e.FullPath);
					cumulus.LogDataMessage($"OnFileChanged: Content = {content}");

					ApplyData(content);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "OnFileChanged Error");
			}
		}

		public string ReceiveDataFromApi(IHttpContext context, bool main)
		{
			string text;

			cumulus.LogDebugMessage("GetDataFromApi: Processing POST data");
			try
			{
				text = new StreamReader(context.Request.InputStream).ReadToEnd();
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "ReceiveDataFromApi: Error reading request stream");
				context.Response.StatusCode = 500;
				return "Error reading request stream";
			}

			cumulus.LogDataMessage($"GetDataFromApi: Payload = {text}");

			var retVal = ApplyData(text);

			if (retVal != "")
			{
				context.Response.StatusCode = 500;
				return retVal;
			}

			cumulus.LogDebugMessage("GetDataFromApi: Complete");

			context.Response.StatusCode = 200;
			return "success";
		}

		private void SetupMqttClient()
		{
			JsonStationMqtt.Setup(cumulus, this);
		}

		public void ReceiveDataFromMqtt(MQTTnet.MqttApplicationMessage appMessage)
		{
			cumulus.LogDebugMessage("GetDataFromMqtt: Processing data");
			cumulus.LogDataMessage("GetDataFromMqtt: data = " + appMessage.ConvertPayloadToString());

			if (!appMessage.Payload.IsEmpty && appMessage.Payload.Length > 0)
			{
				ApplyData(appMessage.ConvertPayloadToString());
			}
			else
			{
				cumulus.LogErrorMessage("GetDataFromMqtt: No content!");
			}
		}

		private string ApplyData(string dataString)
		{
			var procName = mainStation ? "ApplyData" : "ApplyExtraData";

			if (DayResetInProgress)
			{
				cumulus.LogMessage(procName + ": Day reset in progress, ignoring incoming data");
				return string.Empty;
			}

			var retStr = new StringBuilder();

			var data = dataString.FromJson<DataObject>();

			if (data == null)
			{
				cumulus.LogErrorMessage(procName + ": Unable to convert data string to data. String = " + dataString);
				return "Unable to convert data string to data.";
			}

			// Only do the primary sensors if running as the main station
			if (mainStation)
			{
				// Temperature
				if (data.temperature != null && data.units != null)
				{
					try
					{
						if (data.units.temperature == null)
						{
							cumulus.LogErrorMessage("ApplyData: No temperature units supplied!");
							retStr.AppendLine("No temperature units");
						}
						else if (data.units.temperature == "C")
						{
							if (data.temperature.outdoor.HasValue)
							{
								DoOutdoorTemp(ConvertUnits.TempCToUser(data.temperature.outdoor.Value), data.lastupdated);
								haveTemp = true;
							}
							if (data.temperature.indoor.HasValue)
							{
								DoIndoorTemp(ConvertUnits.TempCToUser(data.temperature.indoor.Value));
							}
							if (!cumulus.StationOptions.CalculatedDP && data.temperature.dewpoint.HasValue)
							{
								DoOutdoorDewpoint(ConvertUnits.TempCToUser(data.temperature.dewpoint.Value), data.lastupdated);
							}
						}
						else if (data.units.temperature == "F")
						{
							if (data.temperature.outdoor.HasValue)
							{
								DoOutdoorTemp(ConvertUnits.TempFToUser(data.temperature.outdoor.Value), data.lastupdated);
								haveTemp = true;
							}
							if (data.temperature.indoor.HasValue)
							{
								DoIndoorTemp(ConvertUnits.TempFToUser(data.temperature.indoor.Value));
							}
						}
						else
						{
							cumulus.LogErrorMessage("ApplyData: Invalid temperature units supplied = " + data.units.temperature);
							retStr.AppendLine($"Invalid temperature units: {data.units.temperature}");
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "ApplyData: Error processing tempertaure");
						retStr.AppendLine("Error processing tempertaure");
					}
				}

				// Humidity
				if (data.humidity != null)
				{
					try
					{
						if (data.humidity.outdoor != null)
						{
							DoOutdoorHumidity(data.humidity.outdoor.Value, data.lastupdated);
							haveHum = true;
						}
						if (data.humidity.indoor != null)
						{
							DoIndoorHumidity(data.humidity.indoor.Value);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "ApplyData: Error processing humidity");
						retStr.AppendLine("Error processing humidity");
					}
				}

				// Wind
				if (data.wind != null && data.units != null)
				{
					try
					{
						if (data.units.windspeed == null)
						{
							cumulus.LogErrorMessage("ApplyData: No windspeed units supplied!");
							retStr.AppendLine("No windspeed units");
						}
						else
						{
							var avg = data.wind.speed ?? -1;
							var gust = data.wind.gust10m ?? -1;

							if (gust < 0)
							{
								cumulus.LogErrorMessage("ApplyData: No gust value supplied in wind data");
								retStr.AppendLine("No gust value supplied in wind data");
							}
							else
							{
								var doit = true;
								switch (data.units.windspeed)
								{
									case "mph":
										avg = ConvertUnits.WindMPHToUser(avg);
										gust = ConvertUnits.WindMPHToUser(gust);
										break;
									case "ms":
										avg = ConvertUnits.WindMSToUser(avg);
										gust = ConvertUnits.WindMSToUser(gust);
										break;
									case "kph":
										avg = ConvertUnits.WindKPHToUser(avg);
										gust = ConvertUnits.WindKPHToUser(gust);
										break;
									case "knots":
										avg = ConvertUnits.WindKnotsToUser(avg);
										gust = ConvertUnits.WindKnotsToUser(gust);
										break;
									default:
										cumulus.LogErrorMessage("ApplyData: Invalid windspeed units supplied: " + data.units.windspeed);
										retStr.AppendLine("Invalid windspeed units");
										doit = false;
										break;
								}

								if (doit)
								{
									DoWind(gust, data.wind.direction ?? 0, avg, data.lastupdated);
									haveWind = true;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "ApplyData: Error processing wind");
						retStr.AppendLine("Error processing wind");
					}
				}


				// Rain
				if (data.rain != null && data.units != null)
				{
					try
					{
						var doit = true;
						double counter = 0;
						if (data.rain.counter.HasValue)
						{
							counter = data.rain.counter.Value;
						}
						else if (data.rain.year.HasValue)
						{
							counter = data.rain.year.Value;
						}
						else
						{
							cumulus.LogErrorMessage("ApplyData: No rainfall counter/year value supplied!");
							retStr.AppendLine("No rainfall counter/year value supplied");
							doit = false;
						}

						if (doit)
						{
							if (data.units.rainfall == null)
							{
								cumulus.LogErrorMessage("ApplyData: No rainfall units supplied!");
								retStr.AppendLine("No rainfall units");
							}
							else if (data.units.rainfall == "mm")
							{
								var rate = ConvertUnits.RainMMToUser(data.rain.rate ?? 0);

								if (data.rain.counter.HasValue)
								{
									DoRain(ConvertUnits.RainMMToUser(counter), rate, data.lastupdated);
								}
							}
							else if (data.units.rainfall == "in")
							{
								var rate = ConvertUnits.RainINToUser(data.rain.rate ?? 0);

								if (data.rain.counter.HasValue)
								{
									DoRain(ConvertUnits.RainINToUser(counter), rate, data.lastupdated);
								}
							}
							else
							{
								cumulus.LogErrorMessage("ApplyData: Invalid rainfall units supplied = " + data.units.rainfall);
								retStr.AppendLine($"Invalid rainfall units: {data.units.rainfall}");
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "ApplyData: Error processing rain");
						retStr.AppendLine("Error processing rain");
					}
				}

				// Pressure
				if (data.pressure != null && data.units != null)
				{
					try
					{
						if (data.units.pressure == null)
						{
							cumulus.LogErrorMessage("ApplyData: No pressure units supplied!");
							retStr.AppendLine("No pressure units");
						}
						else
						{
							var slp = data.pressure.sealevel ?? -1;
							var abs = data.pressure.absolute ?? -1;

							if (slp < 0 && abs < 0)
							{
								cumulus.LogErrorMessage("ApplyData: No pressure values in data");
								retStr.AppendLine("No pressure values in data");
							}
							else
							{
								var doit = true;

								if (cumulus.StationOptions.CalculateSLP && abs < 0)
								{
									cumulus.LogErrorMessage("ApplyData: Calculate SLP is enabled, but no absolute pressure value in data");
									retStr.AppendLine("Calculate SLP is enabled, but no absolute pressure value in data");
								}
								else
								{
									switch (data.units.pressure)
									{
										case "hPa":
											slp = ConvertUnits.PressMBToUser(slp);
											DoStationPressure(ConvertUnits.PressMBToUser(abs));
											break;
										case "kPa":
											slp = ConvertUnits.PressKPAToUser(slp);
											DoStationPressure(ConvertUnits.PressKPAToUser(abs));
											break;
										case "inHg":
											slp = ConvertUnits.PressINHGToUser(slp);
											DoStationPressure(ConvertUnits.PressINHGToUser(abs));
											break;
										default:
											cumulus.LogErrorMessage("ApplyData: Invalid pressure units supplied: " + data.units.pressure);
											retStr.AppendLine("Invalid pressure units");
											doit = false;
											break;
									}

									if (doit)
									{
										if (cumulus.StationOptions.CalculateSLP)
										{
											slp = MeteoLib.GetSeaLevelPressure(cumulus.Altitude, ConvertUnits.UserPressToHpa(StationPressure), OutdoorTemperature, cumulus.Latitude);
											slp = ConvertUnits.PressMBToUser(slp);
										}

										DoPressure(slp, data.lastupdated);
									}
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "ApplyData: Error processing pressure");
						retStr.AppendLine("Error processing pressure");
					}
				}
			}

			// Solar
			if (data.solar != null && (mainStation || cumulus.ExtraSensorUseSolar))
			{
				try
				{
					if (data.solar.irradiation != null)
					{
						station.DoSolarRad(data.solar.irradiation.Value, data.lastupdated);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, procName + ": Error processing solar");
					retStr.AppendLine("Error processing solar");
				}
			}

			// UV
			if (data.solar != null && (mainStation || cumulus.ExtraSensorUseUv))
			{
				try
				{
					if (data.solar.uvi != null)
					{
						station.DoUV(data.solar.uvi.Value, data.lastupdated);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, procName + ": Error processing solar");
					retStr.AppendLine("Error processing solar");
				}
			}



			// Extra Temp
			if (data.extratemp != null && data.units != null && (mainStation || cumulus.ExtraSensorUseTempHum))
			{
				if (data.units.temperature == null)
				{
					cumulus.LogErrorMessage(procName + ": No temperature units supplied!");
					retStr.AppendLine("No temperature units");
				}
				else
				{
					foreach (var rec in data.extratemp)
					{
						try
						{
							if (rec.temperature.HasValue)
							{
								var temp = data.units.temperature == "C" ? ConvertUnits.TempCToUser(rec.temperature.Value) : ConvertUnits.TempFToUser(rec.temperature.Value);
								station.DoExtraTemp(temp, rec.index);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, procName + ": Error processing Extra Temperature");
							retStr.AppendLine("Error processing Extra Temperature");
						}
					}
				}
			}

			// Extra Humidity
			if (data.extratemp != null && data.units != null && (mainStation || cumulus.ExtraSensorUseTempHum))
			{
				foreach (var rec in data.extratemp)
				{
					try
					{
						if (rec.humidity.HasValue)
						{
							station.DoExtraHum(rec.humidity.Value, rec.index);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, procName + ": Error processing Extra Humidity");
						retStr.AppendLine("Error processing Extra Humidity");
					}
				}
			}

			// User Temps
			if (data.usertemp != null && data.units != null && (mainStation || cumulus.ExtraSensorUseUserTemp))
			{
				if (data.units.temperature == null)
				{
					cumulus.LogErrorMessage(procName + ": No temperature units supplied!");
					retStr.AppendLine("No temperature units");
				}
				else
				{
					foreach (var rec in data.usertemp)
					{
						try
						{
							if (rec.temperature.HasValue)
							{
								var temp = data.units.temperature == "C" ? ConvertUnits.TempCToUser(rec.temperature.Value) : ConvertUnits.TempFToUser(rec.temperature.Value);
								station.DoUserTemp(temp, rec.index);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, procName + ": Error processing User Temperature");
							retStr.AppendLine("Error processing User Temperature");
						}
					}
				}
			}

			// Soil Temps
			if (data.soiltemp != null && data.units != null && (mainStation || cumulus.ExtraSensorUseSoilTemp))
			{
				if (data.units.temperature == null)
				{
					cumulus.LogErrorMessage(procName + ": No temperature units supplied!");
					retStr.AppendLine("No temperature units");
				}
				else
				{
					foreach (var rec in data.soiltemp)
					{
						try
						{
							if (rec.temperature.HasValue)
							{
								var temp = data.units.temperature == "C" ? ConvertUnits.TempCToUser(rec.temperature.Value) : ConvertUnits.TempFToUser(rec.temperature.Value);
								station.DoSoilTemp(temp, rec.index);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, procName + ": Error processing Soil Temperature");
							retStr.AppendLine("Error processing Soil Temperature");
						}
					}
				}
			}

			// Soil Moistures
			if (data.soilmoisture != null && (mainStation || cumulus.ExtraSensorUseSoilMoist))
			{
				foreach (var rec in data.soilmoisture)
				{
					try
					{
						if (rec.value.HasValue)
						{
							station.DoSoilMoisture(rec.value.Value, rec.index);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, procName + ": Error processing Soil Moisture");
						retStr.AppendLine("Error processing Soil Moisture");
					}
				}
			}

			// Leaf Wetness
			if (data.leafwetness != null && (mainStation || cumulus.ExtraSensorUseLeafWet))
			{
				foreach (var rec in data.leafwetness)
				{
					try
					{
						if (rec.value.HasValue)
						{
							station.DoLeafWetness(rec.value.Value, rec.index);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, procName + ": Error processing Leaf Wetness");
						retStr.AppendLine("Error processing Leaf Wetness");
					}
				}
			}

			// Air Quality
			if (data.airquality != null && (mainStation || cumulus.ExtraSensorUseAQI))
			{
				foreach (var rec in data.airquality)
				{
					try
					{
						if (rec.pm2p5.HasValue)
						{
							station.DoAirQuality(rec.pm2p5.Value, rec.index);
						}

						if (rec.pm2p5avg24h.HasValue)
						{
							station.DoAirQualityAvg(rec.pm2p5avg24h.Value, rec.index);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, procName + ": Error processing tempertaure");
						retStr.AppendLine("Error processing air quality");
					}
				}
			}

			// CO2
			if (data.co2 != null && (mainStation || cumulus.ExtraSensorUseCo2))
			{
				try
				{
					CO2 = data.co2.co2;
					CO2_24h = data.co2.co2_24h;
					CO2_pm2p5 = data.co2.pm2p5;
					CO2_pm2p5_aqi = GetAqi(WeatherStation.AqMeasure.pm2p5, CO2_pm2p5);
					CO2_pm2p5_24h = data.co2.pm2p5avg24h;
					CO2_pm2p5_24h_aqi = GetAqi(WeatherStation.AqMeasure.pm2p5h24, CO2_pm2p5_24h);
					CO2_pm10 = data.co2.pm10;
					CO2_pm10_aqi = GetAqi(WeatherStation.AqMeasure.pm10, CO2_pm10);
					CO2_pm10_24h = data.co2.pm10avg24h;
					CO2_pm10_24h_aqi = GetAqi(WeatherStation.AqMeasure.pm10h24, CO2_pm10_24h);
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, procName + ": Error processing CO2");
					retStr.AppendLine("Error processing CO2");
				}
			}

			// Laser distance
			if (data.laserdist != null && data.units != null && (mainStation || cumulus.ExtraSensorUseLaserDist))
			{
				if (data.units.laserdist == null)
				{
					cumulus.LogErrorMessage(procName + ": No laser distance units supplied!");
					retStr.AppendLine("No laser distance units");
				}
				else
				{
					decimal multiplier = data.units.laserdist switch
					{
						"mm" => 1,
						"in" => in2mm,
						"cm" => 10,
						_ => 1,
					};

					foreach (var rec in data.laserdist)
					{
						try
						{
							decimal? range = rec.range.HasValue ? ConvertUnits.LaserMmToUser(rec.range.Value * multiplier) : null;
							station.DoLaserDistance(range, rec.index);

							if (cumulus.LaserDepthBaseline[rec.index] == -1)
							{
								// MX is not calculating depth

								decimal? depth = rec.depth.HasValue ? ConvertUnits.LaserMmToUser(rec.depth.Value * multiplier) : null;
								station.DoLaserDepth(depth, rec.index);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, procName + ": Error processing Laser Distance");
							retStr.AppendLine("Error processing Laser Distance");
						}
					}
				}
			}

			// Do derived values after the primary values

			if (mainStation)
			{
				// === Wind Chill ===
				try
				{
					// windchillf
					if (cumulus.StationOptions.CalculatedWC && haveTemp && haveWind)
					{
						station.DoWindChill(-999, data.lastupdated);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("ApplyData: Error in wind chill data - " + ex.Message);
					retStr.AppendLine("Failed: Error in wind chill");
				}


				// === Humidex ===
				if (haveTemp && haveHum)
				{
					station.DoHumidex(data.lastupdated);
					station.DoCloudBaseHeatIndex(data.lastupdated);

					// === Apparent === - requires temp, hum, and windspeed
					if (haveWind)
					{
						station.DoApparentTemp(data.lastupdated);
						station.DoFeelsLike(data.lastupdated);
					}
				}

				station.DoForecast(string.Empty, false);
			}

			station.UpdateStatusPanel(data.lastupdated);
			station.UpdateMQTT();
			LastDataReadTime = data.lastupdated;

			return retStr.ToString();
		}


#pragma warning disable S3459, S1144 // Unused private types or members should be removed
		private sealed class DataObject
		{
			public UnitsObject units { get; set; }
			public DateTime lastupdated { get; set; }
			public Temperature temperature { get; set; }
			public Humidity humidity { get; set; }
			public Wind wind { get; set; }
			public Rain rain { get; set; }
			public PressureJson pressure { get; set; }
			public Solar solar { get; set; }
			public ExtraTempHum[] extratemp { get; set; }
			public ExtraTempJson[] usertemp { get; set; }
			public ExtraTempJson[] soiltemp { get; set; }
			public ExtraValue[] soilmoisture { get; set; }
			public ExtraValue[] leafwetness { get; set; }
			public PmData[] airquality { get; set; }
			public Co2Data co2 { get; set; }
			public Lds[] laserdist { get; set; }
		}

		private sealed class UnitsObject
		{
			public string temperature { get; set; }
			public string windspeed { get; set; }
			public string rainfall { get; set; }
			public string pressure { get; set; }
			public string soilmoisture { get; set; }
			public string laserdist { get; set; }
		}

		private sealed class Temperature
		{
			public double? outdoor { get; set; }
			public double? indoor { get; set; }
			public double? dewpoint { get; set; }
		}
		private sealed class Humidity
		{
			public int? outdoor { get; set; }
			public int? indoor { get; set; }
		}
		private sealed class Wind
		{
			public double? speed { get; set; }
			public int? direction { get; set; }
			public double? gust10m { get; set; }
		}
		private sealed class Rain
		{
			public double? counter { get; set; }
			public double? year { get; set; }
			public double? rate { get; set; }
		}
		private sealed class PressureJson
		{
			public double? absolute { get; set; }
			public double? sealevel { get; set; }
		}
		private sealed class Solar
		{
			public int? irradiation { get; set; }
			public double? uvi { get; set; }
		}
		private class ExtraTempJson
		{
			public int index { get; set; }
			public double? temperature { get; set; }
		}
		private sealed class ExtraTempHum : ExtraTempJson
		{
			public int? humidity { get; set; }
		}
		private sealed class ExtraValue
		{
			public int index { get; set; }
			public int? value { get; set; }
		}
		private sealed class ExtraValueDbl
		{
			public int index { get; set; }
			public double? value { get; set; }
		}
		private class PmData
		{
			public int index { get; set; }
			public double? pm2p5 { get; set; }
			public double? pm2p5avg24h { get; set; }
			public double? pm10 { get; set; }
			public double? pm10avg24h { get; set; }
		}
		private sealed class Co2Data : PmData
		{
			public int? co2 { get; set; }
			public int? co2_24h { get; set; }
		}
		private sealed class Lds
		{
			public int index { get; set; }
			public decimal? range { get; set; }
			public decimal? depth { get; set; }
		}

	}
}
