using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;

using Org.BouncyCastle.Ocsp;

using ServiceStack;
using ServiceStack.Text;

using static System.Net.Mime.MediaTypeNames;

namespace CumulusMX
{
	internal class JsonStation : WeatherStation
	{


		public JsonStation(Cumulus cumulus) : base(cumulus)
		{

			// does not provide a forecast, force MX to provide it
			cumulus.UseCumulusForecast = true;


			// Let's decode the Unix ts to DateTime
			JsConfig.Init(new Config
			{
				DateHandler = DateHandler.UnixTime
			});
		}


		public override void Start()
		{

		}


		public override void Stop()
		{

		}


		private void GetDataFromFile()
		{

		}


		public string GetDataFromApi(IHttpContext context, bool main)
		{
			cumulus.LogDebugMessage("GetDataFromApi: Processing POST data");
			var text = new StreamReader(context.Request.InputStream).ReadToEnd();

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

		private void GetDataFromMqtt()
		{

		}

		private string ApplyData(string dataString)
		{
			var retStr = string.Empty;

			var data = dataString.FromJson<DataObject>();

			if (data == null)
			{
				cumulus.LogErrorMessage("ApplyData: Unable to convert data string to data. String = " + dataString);
				return "Unable to convert data string to data.";
			}

			// Temperature
			if (data.temperature != null && data.units != null)
			{
				if (data.units.temperature == null)
				{
					cumulus.LogErrorMessage("ApplyData: No temperature units supplied!");
					retStr = "No temperature units\n";
				}
				else if (data.units.temperature == "C")
				{
					if (data.temperature.outdoor.HasValue)
					{
						DoOutdoorTemp(ConvertUnits.TempCToUser(data.temperature.outdoor.Value), data.lastupdated);
					}
					if (data.temperature.indoor.HasValue)
					{
						DoIndoorTemp(ConvertUnits.TempCToUser(data.temperature.indoor.Value));
					}
				}
				else if (data.units.temperature == "F")
				{
					if (data.temperature.outdoor.HasValue)
					{
						DoOutdoorTemp(ConvertUnits.TempFToUser(data.temperature.outdoor.Value), data.lastupdated);
					}
					if (data.temperature.indoor.HasValue)
					{
						DoIndoorTemp(ConvertUnits.TempFToUser(data.temperature.indoor.Value));
					}
				}
				else
				{
					cumulus.LogErrorMessage("ApplyData: Invalid temperature units supplied = " + data.units.temperature);
					retStr = $"Invalid temperature units: {data.units.temperature}\n";
				}
			}

			// Humidity
			if (data.humidity != null)
			{
				if (data.humidity.outdoor != null)
				{
					DoOutdoorHumidity(data.humidity.outdoor.Value, data.lastupdated);
				}
				if (data.humidity.indoor != null)
				{
					DoIndoorHumidity(data.humidity.indoor.Value);
				}
			}

			// Wind
			if (data.wind != null && data.units != null)
			{
				if (data.units.windspeed == null)
				{
					cumulus.LogErrorMessage("ApplyData: No windspeed units supplied!");
					retStr += "No windspeed units\n";
				}
				else
				{
					var avg = data.wind.speed ?? -1;
					var gust = data.wind.gust10m ?? -1;

					if (gust < 0)
					{
						cumulus.LogErrorMessage("ApplyData: No gust value supplied in wind data");
						retStr += "No gust value in wind data\n";
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
							default:
								cumulus.LogErrorMessage("ApplyData: Invalid windspeed units supplied: " + data.units.windspeed);
								retStr += "Invalid windspeed units\n";
								doit = false;
								break;
						}

						if (doit)
						{
							DoWind(gust, data.wind.direction ?? 0, avg, data.lastupdated);
						}
					}
				}
			}

			// Rain
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
					retStr += "No rainfall counter/year value supplied\n";
					doit = false;
				}

				if (doit)
				{
					if (data.units.rainfall == null)
					{
						cumulus.LogErrorMessage("ApplyData: No rainfall units supplied!");
						retStr += "No rainfall units\n";
					}
					else if (data.units.rainfall == "mm")
					{
						var rate = ConvertUnits.RainMMToUser(data.rain.rate ?? 0);

						if (data.rain.counter.HasValue)
						{
							DoRain(ConvertUnits.RainMMToUser(counter), rate, data.lastupdated);
						}
					}
					else if (data.units.temperature == "in")
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
						retStr = $"Invalid rainfall units: {data.units.rainfall}\n";
					}
				}
			}

			// Pressure
			if (data.pressure != null && data.units != null)
			{
				if (data.units.pressure == null)
				{
					cumulus.LogErrorMessage("ApplyData: No pressure units supplied!");
					retStr += "No pressure units\n";
				}
				else
				{
					var slp = data.pressure.sealevel ?? -1;
					var abs = data.pressure.absolute ?? -1;

					if (slp < 0 && abs < 0)
					{
						cumulus.LogErrorMessage("ApplyData: No pressure values in data");
						retStr += "No pressure values in data\n";
					}
					else
					{
						var doit = true;

						if (cumulus.StationOptions.CalculateSLP && abs < 0)
						{
							cumulus.LogErrorMessage("ApplyData: Calculate SLP is enabled, but no abosolute pressure value in data");
							retStr += "Calculate SLP is enabled, but no abosolute pressure value in data\n";
						}
						else
						{
							switch (data.units.pressure)
							{
								case "hPa":
									slp = ConvertUnits.PressMBToUser(slp);
									StationPressure = ConvertUnits.PressMBToUser(abs);
									break;
								case "inHg":
									slp = ConvertUnits.PressINHGToUser(slp);
									StationPressure = ConvertUnits.PressINHGToUser(abs);
									break;
								default:
									cumulus.LogErrorMessage("ApplyData: Invalid windspeed units supplied: " + data.units.windspeed);
									retStr += "Invalid windspeed units\n";
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


			// Solar
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

			// Extra Temp/Hums
			if (data.extratemp != null && data.units != null)
			{
				if (data.units.temperature == null)
				{
					cumulus.LogErrorMessage("ApplyData: No temperature units supplied!");
					retStr = "No temperature units\n";
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
							retStr = "Error processing Extra Temperature/Humidity\n";
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
					retStr = "No temperature units\n";
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
							retStr = "Error processing User Temperature\n";
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
					retStr = "No temperature units\n";
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
							retStr = "Error processing Soil Temperature\n";
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
						retStr = "Error processing Soil Moisture\n";
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
						retStr = "Error processing Leaf Wetness\n";
					}
				}
			}

			// Air Quality
			if (data.airquality != null)
			{
				if (data.airquality.outdoor != null)
				{

				}

				if (data.airquality.indoor != null)
				{

				}

				if (data.airquality.co2 != null)
				{

				}
			}

			return retStr;
		}


		private sealed class DataObject
		{
			public UnitsObject units { get; set; }
			public DateTime timestamp { get; set; }
			public DateTime lastupdated { get; set; }
			public Temperature temperature { get; set; }
			public Humidity humidity { get; set; }
			public Wind wind { get; set; }
			public Rain rain { get; set; }
			public Pressure pressure { get; set; }
			public Solar solar { get; set; }
			public ExtraTempHum[] extratemp { get; set; }
			public ExtraTemp[] usertemp { get; set; }
			public ExtraTemp[] soiltemp { get; set; }
			public ExtraValue[] soilmoisture { get; set; }
			public ExtraValue[] leafwetness { get; set; }
			public AirQuality airquality { get; set; }
		}

		private sealed class UnitsObject
		{
			public string temperature { get; set; }
			public string windspeed { get; set; }
			public string rainfall { get; set; }
			public string pressure { get; set; }
			public string soilmoisture { get; set;}
		}

		private sealed class Temperature
		{
			public double? outdoor { get; set; }
			public double? indoor { get; set;}
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
			public double? gust10m { get; set;}
		}
		private sealed class Rain
		{
			public double? counter { get; set; }
			public double? year { get; set; }
			public double? rate { get; set; }
		}
		private sealed class Pressure
		{
			public double? absolute { get; set;}
			public double? sealevel { get; set; }
		}
		private sealed class Solar
		{
			public int? irradiation { get; set; }
			public double? uvi { get; set;}
		}
		private class ExtraTemp
		{
			public int index { get; set; }
			public double? temperature { get; set; }
		}
		private sealed class ExtraTempHum : ExtraTemp
		{
			public int? humidity { get; set; }
		}
		private sealed class ExtraValue
		{
			public int index { get; set; }
			public int? value { get; set; }
		}
		private sealed class AirQuality
		{
			public PM outdoor { get; set; }
			public PM indoor { get; set; }
			public CO2 co2 { get; set; }
		}
		private class PM
		{
			public double? pm2p5 { get; set; }
			public double? pm2p5avg24h { get; set; }
			public double? pm10 { get; set; }
			public double? pm10avg24h { get; set;}
		}
		private class CO2 : PM
		{
			public int? co2 { get; set; }
			public int? co2_24h { get; set; }
		}
	}
}
