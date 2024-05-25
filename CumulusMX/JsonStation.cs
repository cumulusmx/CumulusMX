using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;

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


			// Pressure


			// Solar


			// Extra Temp/Hums


			// User Temps


			// Soil Temps


			// Soil Moistures


			// Leaf Wetness


			// Air Quality

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
			public double? irradiation { get; set; }
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
			public CO2 CO2 { get; set; }
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
