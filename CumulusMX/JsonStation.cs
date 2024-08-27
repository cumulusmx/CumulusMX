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

		private bool haveTemp = false;
		private bool haveHum = false;
		private bool haveWind = false;

		private FileSystemWatcher watcher;

		public JsonStation(Cumulus cumulus) : base(cumulus)
		{

			// does not provide a forecast, force MX to provide it
			cumulus.UseCumulusForecast = true;


			// Let's decode the Unix ts to DateTime
			JsConfig.Init(new Config
			{
				DateHandler = DateHandler.UnixTime
			});

			Start();
		}


		public override void Start()
		{
			DoDayResetIfNeeded();

			if (cumulus.JsonStationOptions.Connectiontype == 0)
			{
				// Get data from file
				GetDataFromFile();
			}
			else if (cumulus.JsonStationOptions.Connectiontype == 1)
			{
				// Get data from HTTP
				// Nothing to do, the API will send us the data
			}
			else if (cumulus.JsonStationOptions.Connectiontype == 2)
			{
				// Get data from MQTT
				if (string.IsNullOrEmpty(cumulus.JsonStationOptions.MqttServer))
				{
					cumulus.LogErrorMessage("JSON Data Input: Unable to configure MQTT client - Server name is blank");
					return;
				}
				if (string.IsNullOrEmpty(cumulus.JsonStationOptions.MqttTopic))
				{
					cumulus.LogErrorMessage("JSON Data Input: Unable to configure MQTT client - Topic name is blank");
					return;
				}

				SetupMqttClient();
			}
			else
			{
				cumulus.LogErrorMessage("Unable to start JSON data input station due to invalid connection type = " + cumulus.JsonStationOptions.Connectiontype);
				return;
			}

			timerStartNeeded = true;
		}


		public override void Stop()
		{
			if (cumulus.JsonStationOptions.Connectiontype == 0)
			{
				// Get data from file
				watcher.Dispose();
			}
			else if (cumulus.JsonStationOptions.Connectiontype == 1)
			{
				// Get data from HTTP
				// Nothing to do, the API will send us the data
			}
			else if (cumulus.JsonStationOptions.Connectiontype == 2)
			{
				JsonStationMqtt.Disconnect();
			}
		}


		private void GetDataFromFile()
		{
			if (string.IsNullOrEmpty(cumulus.JsonStationOptions.SourceFile))
			{
				cumulus.LogErrorMessage("JSON Station: Using file moniotoring, but not filename provided!");
				return;
			}

			cumulus.LogMessage("JSON Data: Monitoring file = " + cumulus.JsonStationOptions.SourceFile);

			try
			{
				var fileInfo = new FileInfo(cumulus.JsonStationOptions.SourceFile);

				watcher = new FileSystemWatcher(fileInfo.DirectoryName)
				{
					NotifyFilter = NotifyFilters.LastWrite
				};

				watcher.Filter = fileInfo.Name;

				watcher.Changed += OnFileChanged;
				watcher.Created += OnFileChanged;
				watcher.EnableRaisingEvents = true;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "GetDataFromFile: Error creating watcher");
			}
		}


		private void OnFileChanged(object sender, FileSystemEventArgs e)
		{
			cumulus.LogDebugMessage("OnFileChanged: File change type = " + e.ChangeType);
			if (e.ChangeType != WatcherChangeTypes.Changed)
			{
				return;
			}

			try
			{
				System.Threading.Thread.Sleep(cumulus.JsonStationOptions.FileReadDelay);
				var content = File.ReadAllText(e.FullPath);
				cumulus.LogDataMessage($"OnFileChanged: Content = {content}");

				ApplyData(content);
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

			if (appMessage.PayloadSegment.Count > 0)
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
			var retStr = new StringBuilder();

			var data = dataString.FromJson<DataObject>();

			if (data == null)
			{
				cumulus.LogErrorMessage("ApplyData: Unable to convert data string to data. String = " + dataString);
				return "Unable to convert data string to data.";
			}

			// Temperature
			try
			{
				if (data.temperature != null && data.units != null)
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
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "ApplyData: Error processing tempertaure");
				retStr.AppendLine("Error processing tempertaure");
			}

			// Humidity
			try
			{
				if (data.humidity != null)
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
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "ApplyData: Error processing humidity");
				retStr.AppendLine("Error processing humidity");
			}


			// Wind
			try
			{
				if (data.wind != null && data.units != null)
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
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "ApplyData: Error processing wind");
				retStr.AppendLine("Error processing wind");
			}


			// Rain
			try
			{
				if (data.rain != null && data.units != null)
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
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "ApplyData: Error processing rain");
				retStr.AppendLine("Error processing rain");
			}


			// Pressure
			try
			{
				if (data.pressure != null && data.units != null)
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
								cumulus.LogErrorMessage("ApplyData: Calculate SLP is enabled, but no abosolute pressure value in data");
								retStr.AppendLine("Calculate SLP is enabled, but no abosolute pressure value in data");
							}
							else
							{
								switch (data.units.pressure)
								{
									case "hPa":
										slp = ConvertUnits.PressMBToUser(slp);
										StationPressure = ConvertUnits.PressMBToUser(abs);
										break;
									case "kPa":
										slp = ConvertUnits.PressKPAToUser(slp);
										StationPressure = ConvertUnits.PressKPAToUser(abs);
										break;
									case "inHg":
										slp = ConvertUnits.PressINHGToUser(slp);
										StationPressure = ConvertUnits.PressINHGToUser(abs);
										break;
									default:
										cumulus.LogErrorMessage("ApplyData: Invalid pressure units supplied: " + data.units.pressure);
										retStr.AppendLine("Invalid pressure units");
										doit = false;
										break;
								}

								StationPressure = cumulus.Calib.Press.Calibrate(StationPressure);

								if (doit)
								{
									if (cumulus.StationOptions.CalculateSLP)
									{
										slp = MeteoLib.GetSeaLevelPressure(cumulus.Altitude, ConvertUnits.UserPressToHpa(StationPressure), OutdoorTemperature);
										slp = ConvertUnits.PressMBToUser(slp);
									}

									DoPressure(slp, data.lastupdated);
								}
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


			// Solar
			try
			{
				if (data.solar != null)
				{
					if (data.solar.irradiation != null)
					{
						DoSolarRad(data.solar.irradiation.Value, data.lastupdated);
					}

					if (data.solar.uvi != null)
					{
						DoUV(data.solar.uvi.Value, data.lastupdated);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "ApplyData: Error processing solar");
				retStr.AppendLine("Error processing solar");
			}


			// Extra Temp/Hums
			if (data.extratemp != null && data.units != null)
			{
				if (data.units.temperature == null)
				{
					cumulus.LogErrorMessage("ApplyData: No temperature units supplied!");
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
								DoExtraTemp(temp, rec.index);
							}

							if (rec.humidity.HasValue)
							{
								DoExtraHum(rec.humidity.Value, rec.index);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, "ApplyData: Error processing Extra Temperature/Humidity");
							retStr.AppendLine("Error processing Extra Temperature/Humidity");
						}
					}
				}
			}

			// User Temps
			if (data.usertemp != null && data.units != null)
			{
				if (data.units.temperature == null)
				{
					cumulus.LogErrorMessage("ApplyData: No temperature units supplied!");
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
								DoUserTemp(temp, rec.index);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, "ApplyData: Error processing User Temperature");
							retStr.AppendLine("Error processing User Temperature");
						}
					}
				}
			}

			// Soil Temps
			if (data.soiltemp != null && data.units != null)
			{
				if (data.units.temperature == null)
				{
					cumulus.LogErrorMessage("ApplyData: No temperature units supplied!");
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
								DoSoilTemp(temp, rec.index);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, "ApplyData: Error processing Soil Temperature");
							retStr.AppendLine("Error processing Soil Temperature");
						}
					}
				}
			}

			// Soil Moistures
			if (data.soilmoisture != null)
			{
				foreach (var rec in data.soilmoisture)
				{
					try
					{
						if (rec.value.HasValue)
						{
							DoSoilMoisture(rec.value.Value, rec.index);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "ApplyData: Error processing Soil Moisture");
						retStr.AppendLine("Error processing Soil Moisture");
					}
				}
			}

			// Leaf Wetness
			if (data.leafwetness != null)
			{
				foreach (var rec in data.leafwetness)
				{
					try
					{
						if (rec.value.HasValue)
						{
							DoLeafWetness(rec.value.Value, rec.index);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "ApplyData: Error processing Leaf Wetness");
						retStr.AppendLine("Error processing Leaf Wetness");
					}
				}
			}

			// Air Quality
			if (data.airquality != null)
			{
				foreach (var rec in data.airquality)
				{
					try
					{
						if (rec.pm2p5.HasValue)
						{
							DoAirQuality(rec.pm2p5.Value, rec.index);
						}

						if (rec.pm2p5avg24h.HasValue)
						{
							DoAirQualityAvg(rec.pm2p5avg24h.Value, rec.index);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "ApplyData: Error processing tempertaure");
						retStr.AppendLine("Error processing air quality");
					}
				}
			}

			// CO2
			try
			{
				if (data.co2 != null)
				{
					if (data.co2.co2.HasValue)
					{
						CO2 = data.co2.co2.Value;
					}

					if (data.co2.co2_24h.HasValue)
					{
						CO2_24h = data.co2.co2_24h.Value;
					}

					if (data.co2.pm2p5.HasValue)
					{
						CO2_pm2p5 = data.co2.pm2p5.Value;
						CO2_pm2p5_aqi = GetAqi(WeatherStation.AqMeasure.pm2p5, CO2_pm2p5);
					}

					if (data.co2.pm2p5avg24h.HasValue)
					{
						CO2_pm2p5_24h = data.co2.pm2p5avg24h.Value;
						CO2_pm2p5_24h_aqi = GetAqi(WeatherStation.AqMeasure.pm2p5h24, CO2_pm2p5_24h);
					}

					if (data.co2.pm10.HasValue)
					{
						CO2_pm10 = data.co2.pm10.Value;
						CO2_pm10_aqi = GetAqi(WeatherStation.AqMeasure.pm10, CO2_pm10);
					}

					if (data.co2.pm10avg24h.HasValue)
					{
						CO2_pm10_24h = data.co2.pm10avg24h.Value;
						CO2_pm10_24h_aqi = GetAqi(WeatherStation.AqMeasure.pm10h24, CO2_pm10_24h);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "ApplyData: Error processing CO2");
				retStr.AppendLine("Error processing CO2");
			}

			// Do derived values after the primary values


			// === Wind Chill ===
			try
			{
				// windchillf
				if (cumulus.StationOptions.CalculatedWC && haveTemp && haveWind)
				{
					DoWindChill(-999, data.lastupdated);
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
				DoHumidex(data.lastupdated);
				DoCloudBaseHeatIndex(data.lastupdated);

				// === Apparent === - requires temp, hum, and windspeed
				if (haveWind)
				{
					DoApparentTemp(data.lastupdated);
					DoFeelsLike(data.lastupdated);
				}
			}

			DoForecast(string.Empty, false);

			UpdateStatusPanel(data.lastupdated);
			UpdateMQTT();

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
		}

		private sealed class UnitsObject
		{
			public string temperature { get; set; }
			public string windspeed { get; set; }
			public string rainfall { get; set; }
			public string pressure { get; set; }
			public string soilmoisture { get; set; }
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
	}
}
