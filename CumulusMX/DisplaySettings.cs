using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;

using Org.BouncyCastle.Utilities.Collections;

using ServiceStack.Text;

using static Swan.Terminal;

namespace CumulusMX
{
	internal class DisplaySettings
	{
		private readonly Cumulus cumulus;
		private WeatherStation station;

		internal DisplaySettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		internal void SetStation(WeatherStation station)
		{
			this.station = station;
		}

		internal string GetAlpacaFormData()
		{

			// Display Options
			var displayOptions = new JsonDisplayOptions()
			{
				windrosepoints = cumulus.NumWindRosePoints,
				useapparent = cumulus.DisplayOptions.UseApparent,
				displaysolar = cumulus.DisplayOptions.ShowSolar,
				displayuv = cumulus.DisplayOptions.ShowUV
			};

			var graphVisTemp = new JsonGraphVisTemperature()
			{
				Temp = cumulus.GraphOptions.Visible.Temp.Val,
				InTemp = cumulus.GraphOptions.Visible.InTemp.Val,
				HeatIndex = cumulus.GraphOptions.Visible.HeatIndex.Val,
				DewPoint = cumulus.GraphOptions.Visible.DewPoint.Val,
				WindChill = cumulus.GraphOptions.Visible.WindChill.Val,
				AppTemp = cumulus.GraphOptions.Visible.AppTemp.Val,
				FeelsLike = cumulus.GraphOptions.Visible.FeelsLike.Val,
				Humidex = cumulus.GraphOptions.Visible.Humidex.Val,
				AvgTemp = cumulus.GraphOptions.Visible.AvgTemp.Val,
				MaxTemp = cumulus.GraphOptions.Visible.MaxTemp.Val,
				MinTemp = cumulus.GraphOptions.Visible.MinTemp.Val
			};

			var graphVisHum = new JsonGraphVisHumidity()
			{
				Hum = cumulus.GraphOptions.Visible.OutHum.Val,
				InHum = cumulus.GraphOptions.Visible.InHum.Val
			};

			var graphVisSolar = new JsonGraphVisSolar()
			{
				UV = cumulus.GraphOptions.Visible.UV.Val,
				Solar = cumulus.GraphOptions.Visible.Solar.Val,
				Sunshine = cumulus.GraphOptions.Visible.Sunshine.Val
			};

			var graphVisDegreeDays = new JsonGraphVisDegreeDays()
			{
				GrowingDegreeDays1 = cumulus.GraphOptions.Visible.GrowingDegreeDays1.Val,
				GrowingDegreeDays2 = cumulus.GraphOptions.Visible.GrowingDegreeDays2.Val
			};

			var grapVisTempSum = new JsonGraphVisTempSum()
			{
				TempSum0 = cumulus.GraphOptions.Visible.TempSum0.Val,
				TempSum1 = cumulus.GraphOptions.Visible.TempSum1.Val,
				TempSum2 = cumulus.GraphOptions.Visible.TempSum2.Val

			};

			var graphVisExtraTemp = new JsonGraphVisExtraSensors()
			{
				sensors = cumulus.GraphOptions.Visible.ExtraTemp.Vals
			};

			var graphVisExtraHum = new JsonGraphVisExtraSensors()
			{
				sensors = cumulus.GraphOptions.Visible.ExtraHum.Vals
			};

			var graphVisExtraDP = new JsonGraphVisExtraSensors()
			{
				sensors = cumulus.GraphOptions.Visible.ExtraDewPoint.Vals
			};

			var graphVisSoilTemp = new JsonGraphVisExtraSensors()
			{
				sensors = cumulus.GraphOptions.Visible.SoilTemp.Vals
			};

			var graphVisLeafWet = new JsonGraphVisExtraSensors()
			{
				sensors = cumulus.GraphOptions.Visible.LeafWetness.Vals
			};


			var graphVisSoilMoist = new JsonGraphVisExtraSensors()
			{
				sensors = cumulus.GraphOptions.Visible.SoilMoist.Vals
			};

			var graphVisUserTemp = new JsonGraphVisExtraSensors()
			{
				sensors = cumulus.GraphOptions.Visible.UserTemp.Vals
			};

			var graphVisCo2 = new JsonGraphVisCo2()
			{
				co2 = cumulus.GraphOptions.Visible.CO2Sensor.CO2.Val,
				co2avg = cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.Val,
				pm25 = cumulus.GraphOptions.Visible.CO2Sensor.Pm25.Val,
				pm25avg = cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.Val,
				pm10 = cumulus.GraphOptions.Visible.CO2Sensor.Pm10.Val,
				pm10avg = cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.Val,
				temp = cumulus.GraphOptions.Visible.CO2Sensor.Temp.Val,
				hum = cumulus.GraphOptions.Visible.CO2Sensor.Hum.Val
			};

			var graphVisAq = new JsonGraphVisAq();

			for (var i = 0; i < cumulus.GraphOptions.Visible.AqSensor.Pm.Vals.Length; i++)
			{
				graphVisAq.sensors[i].pm = cumulus.GraphOptions.Visible.AqSensor.Pm.Vals[i];
				graphVisAq.sensors[i].pmavg = cumulus.GraphOptions.Visible.AqSensor.PmAvg.Vals[i];
				graphVisAq.sensors[i].temp = cumulus.GraphOptions.Visible.AqSensor.Temp.Vals[i];
				graphVisAq.sensors[i].hum = cumulus.GraphOptions.Visible.AqSensor.Hum.Vals[i];
			}


			var graphVis = new JsonVisibility()
			{
				temperature = graphVisTemp,
				humidity = graphVisHum,
				solar = graphVisSolar,
				degreedays = graphVisDegreeDays,
				tempsum = grapVisTempSum,
				extratemp = graphVisExtraTemp,
				extrahum = graphVisExtraHum,
				extradew = graphVisExtraDP,
				soiltemp = graphVisSoilTemp,
				soilmoist = graphVisSoilMoist,
				leafwet = graphVisLeafWet,
				usertemp = graphVisUserTemp,
				aq = graphVisAq,
				co2 = graphVisCo2
			};

			var graphColTemp = new JsonGraphColTemperature()
			{
				Temp = cumulus.GraphOptions.Colour.Temp,
				InTemp = cumulus.GraphOptions.Colour.InTemp,
				HeatIndex = cumulus.GraphOptions.Colour.HeatIndex,
				DewPoint = cumulus.GraphOptions.Colour.DewPoint,
				WindChill = cumulus.GraphOptions.Colour.WindChill,
				AppTemp = cumulus.GraphOptions.Colour.AppTemp,
				FeelsLike = cumulus.GraphOptions.Colour.FeelsLike,
				Humidex = cumulus.GraphOptions.Colour.Humidex
			};

			var graphColDailyTemp = new JsonGraphColDailyTemp()
			{
				AvgTemp = cumulus.GraphOptions.Colour.AvgTemp,
				MaxTemp = cumulus.GraphOptions.Colour.MaxTemp,
				MinTemp = cumulus.GraphOptions.Colour.MinTemp,
				MinDewPoint = cumulus.GraphOptions.Colour.MinDew,
				MaxDewPoint = cumulus.GraphOptions.Colour.MaxDew,
				MinAppTemp = cumulus.GraphOptions.Colour.MinApp,
				MaxAppTemp = cumulus.GraphOptions.Colour.MaxApp,
				MinFeelsLike = cumulus.GraphOptions.Colour.MinFeels,
				MaxFeelsLike = cumulus.GraphOptions.Colour.MaxFeels,
				MinWindChill = cumulus.GraphOptions.Colour.MinWindChill,
				MaxHeatIndex = cumulus.GraphOptions.Colour.MaxHeatIndex,
				MaxHumidex = cumulus.GraphOptions.Colour.MaxHumidex
			};

			var graphColHum = new JsonGraphColHumidity()
			{
				Hum = cumulus.GraphOptions.Colour.OutHum,
				InHum = cumulus.GraphOptions.Colour.InHum
			};

			var graphColDailyHum = new JsonGraphColDailyMaxMin()
			{
				Max = cumulus.GraphOptions.Colour.MaxOutHum,
				Min = cumulus.GraphOptions.Colour.MinOutHum
			};


			var graphColPress = new JsonGraphColPress()
			{
				Press = cumulus.GraphOptions.Colour.Press
			};

			var graphColDailyPress = new JsonGraphColDailyMaxMin()
			{
				Max = cumulus.GraphOptions.Colour.MaxPress,
				Min = cumulus.GraphOptions.Colour.MinPress
			};

			var graphColWind = new JsonGraphColWind()
			{
				WindAvg = cumulus.GraphOptions.Colour.WindAvg,
				WindGust = cumulus.GraphOptions.Colour.WindGust,
				WindRun = cumulus.GraphOptions.Colour.WindRun
			};

			var graphColBearing = new JsonGraphColWindBearing()
			{
				Bearing = cumulus.GraphOptions.Colour.WindBearing,
				BearingAvg = cumulus.GraphOptions.Colour.WindBearingAvg
			};

			var grapColRain = new jsonGraphColRain()
			{
				Rain = cumulus.GraphOptions.Colour.Rainfall,
				RainRate = cumulus.GraphOptions.Colour.RainRate
			};

			var graphColSolar = new JsonGraphColSolar()
			{
				UV = cumulus.GraphOptions.Colour.UV,
				Solar = cumulus.GraphOptions.Colour.Solar,
				CurrentSolarMax = cumulus.GraphOptions.Colour.SolarTheoretical,
				Sunshine = cumulus.GraphOptions.Colour.Sunshine
			};

			var graphColAQ = new JsonGraphColAQ()
			{
				Pm2p5 = cumulus.GraphOptions.Colour.Pm2p5,
				Pm10 = cumulus.GraphOptions.Colour.Pm10
			};

			var graphColExtraTemp = new JsonGraphColExtraSensors()
			{
				sensors = cumulus.GraphOptions.Colour.ExtraTemp
			};

			var graphColExtraHum = new JsonGraphColExtraSensors()
			{
				sensors = cumulus.GraphOptions.Colour.ExtraHum
			};

			var graphColExtraDP = new JsonGraphColExtraSensors()
			{
				sensors = cumulus.GraphOptions.Colour.ExtraDewPoint
			};

			var graphColSoilTemp = new JsonGraphColExtraSensors()
			{
				sensors = cumulus.GraphOptions.Colour.SoilTemp
			};

			var graphColSoilMoist = new JsonGraphColExtraSensors()
			{
				sensors = cumulus.GraphOptions.Colour.SoilMoist
			};

			var graphColLeafWet = new JsonGraphColExtraSensors()
			{
				sensors = cumulus.GraphOptions.Colour.LeafWetness
			};

			var graphColUserTemp = new JsonGraphColExtraSensors()
			{
				sensors = cumulus.GraphOptions.Colour.UserTemp
			};

			var graphColCo2 = new JsonGraphColCo2()
			{
				co2 = cumulus.GraphOptions.Colour.CO2Sensor.CO2,
				co2avg = cumulus.GraphOptions.Colour.CO2Sensor.CO2Avg,
				pm25 = cumulus.GraphOptions.Colour.CO2Sensor.Pm25,
				pm25avg = cumulus.GraphOptions.Colour.CO2Sensor.Pm25Avg,
				pm10 = cumulus.GraphOptions.Colour.CO2Sensor.Pm10,
				pm10avg = cumulus.GraphOptions.Colour.CO2Sensor.Pm10Avg,
				temp = cumulus.GraphOptions.Colour.CO2Sensor.Temp,
				hum = cumulus.GraphOptions.Colour.CO2Sensor.Hum
			};

			var graphCol = new JsonColour()
			{
				temperature = graphColTemp,
				dailytemp = graphColDailyTemp,
				humidity = graphColHum,
				dailyhum = graphColDailyHum,
				press = graphColPress,
				dailypress = graphColDailyPress,
				wind = graphColWind,
				bearing = graphColBearing,
				rain = grapColRain,
				solar = graphColSolar,
				aq = graphColAQ,
				extratemp = graphColExtraTemp,
				extrahum = graphColExtraHum,
				extradew = graphColExtraDP,
				soiltemp = graphColSoilTemp,
				soilmoist = graphColSoilMoist,
				leafwet = graphColLeafWet,
				usertemp = graphColUserTemp,
				co2 = graphColCo2
			};

			var graphs = new JsonGraphs()
			{
				graphdays = cumulus.GraphDays,
				graphhours = cumulus.GraphHours,
				colour = graphCol
			};

			var data = new JsonData()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				Graphs = graphs,
				DisplayOptions = displayOptions,
				DataVisibility = graphVis
			};

			//return JsonConvert.SerializeObject(data);
			return JsonSerializer.SerializeToString(data);
		}


		internal string UpdateConfig(IHttpContext context)
		{
			var errorMsg = "";
			var json = "";
			context.Response.StatusCode = 200;
			JsonData settings;

			// get the response
			try
			{
				cumulus.LogMessage("Updating display settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json=" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = JsonSerializer.DeserializeFromString<JsonData>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Station Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Station Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				// Graph Config
				try
				{
					cumulus.GraphOptions.Visible.Temp.Val = settings.DataVisibility.temperature.Temp;
					cumulus.GraphOptions.Visible.InTemp.Val = settings.DataVisibility.temperature.InTemp;
					cumulus.GraphOptions.Visible.HeatIndex.Val = settings.DataVisibility.temperature.HeatIndex;
					cumulus.GraphOptions.Visible.DewPoint.Val = settings.DataVisibility.temperature.DewPoint;
					cumulus.GraphOptions.Visible.WindChill.Val = settings.DataVisibility.temperature.WindChill;
					cumulus.GraphOptions.Visible.AppTemp.Val = settings.DataVisibility.temperature.AppTemp;
					cumulus.GraphOptions.Visible.FeelsLike.Val = settings.DataVisibility.temperature.FeelsLike;
					cumulus.GraphOptions.Visible.Humidex.Val = settings.DataVisibility.temperature.Humidex;
					cumulus.GraphOptions.Visible.OutHum.Val = settings.DataVisibility.humidity.Hum;
					cumulus.GraphOptions.Visible.InHum.Val = settings.DataVisibility.humidity.InHum;
					cumulus.GraphOptions.Visible.UV.Val = settings.DataVisibility.solar.UV;
					cumulus.GraphOptions.Visible.Solar.Val = settings.DataVisibility.solar.Solar;
					cumulus.GraphOptions.Visible.Sunshine.Val = settings.DataVisibility.solar.Sunshine;
					cumulus.GraphOptions.Visible.AvgTemp.Val = settings.DataVisibility.temperature.AvgTemp;
					cumulus.GraphOptions.Visible.MaxTemp.Val = settings.DataVisibility.temperature.MaxTemp;
					cumulus.GraphOptions.Visible.MinTemp.Val = settings.DataVisibility.temperature.MinTemp;
					cumulus.GraphOptions.Visible.TempSum0.Val = settings.DataVisibility.tempsum.TempSum0;
					cumulus.GraphOptions.Visible.TempSum1.Val = settings.DataVisibility.tempsum.TempSum1;
					cumulus.GraphOptions.Visible.TempSum2.Val = settings.DataVisibility.tempsum.TempSum2;
					cumulus.GraphOptions.Visible.GrowingDegreeDays1.Val = settings.DataVisibility.degreedays.GrowingDegreeDays1;
					cumulus.GraphOptions.Visible.GrowingDegreeDays2.Val = settings.DataVisibility.degreedays.GrowingDegreeDays2;
					cumulus.GraphOptions.Visible.ExtraTemp.Vals = settings.DataVisibility.extratemp.sensors;
					cumulus.GraphOptions.Visible.ExtraHum.Vals = settings.DataVisibility.extrahum.sensors;
					cumulus.GraphOptions.Visible.ExtraDewPoint.Vals = settings.DataVisibility.extradew.sensors;
					cumulus.GraphOptions.Visible.SoilTemp.Vals = settings.DataVisibility.soiltemp.sensors;
					cumulus.GraphOptions.Visible.SoilMoist.Vals = settings.DataVisibility.soilmoist.sensors;
					cumulus.GraphOptions.Visible.LeafWetness.Vals = settings.DataVisibility.leafwet.sensors;
					cumulus.GraphOptions.Visible.UserTemp.Vals = settings.DataVisibility.usertemp.sensors;
					for (var i = 0; i < cumulus.GraphOptions.Visible.AqSensor.Pm.Vals.Length; i++)
					{
						cumulus.GraphOptions.Visible.AqSensor.Pm.Vals[i] = settings.DataVisibility.aq.sensors[i].pm;
						cumulus.GraphOptions.Visible.AqSensor.PmAvg.Vals[i] = settings.DataVisibility.aq.sensors[i].pmavg;
						cumulus.GraphOptions.Visible.AqSensor.Temp.Vals[i] = settings.DataVisibility.aq.sensors[i].temp;
						cumulus.GraphOptions.Visible.AqSensor.Hum.Vals[i] = settings.DataVisibility.aq.sensors[i].hum;
					}
					cumulus.GraphOptions.Visible.CO2Sensor.CO2.Val = settings.DataVisibility.co2.co2;
					cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.Val = settings.DataVisibility.co2.co2avg;
					cumulus.GraphOptions.Visible.CO2Sensor.Pm25.Val = settings.DataVisibility.co2.pm25;
					cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.Val = settings.DataVisibility.co2.pm25avg;
					cumulus.GraphOptions.Visible.CO2Sensor.Pm10.Val = settings.DataVisibility.co2.pm10;
					cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.Val = settings.DataVisibility.co2.pm10avg;
					cumulus.GraphOptions.Visible.CO2Sensor.Temp.Val = settings.DataVisibility.co2.temp;
					cumulus.GraphOptions.Visible.CO2Sensor.Hum.Val = settings.DataVisibility.co2.hum;

					cumulus.GraphHours = settings.Graphs.graphhours;
					cumulus.RecentDataDays = (int) Math.Ceiling(Math.Max(7, cumulus.GraphHours / 24.0));
					cumulus.GraphDays = settings.Graphs.graphdays;

					cumulus.GraphOptions.Colour.Temp = settings.Graphs.colour.temperature.Temp;
					cumulus.GraphOptions.Colour.InTemp = settings.Graphs.colour.temperature.InTemp;
					cumulus.GraphOptions.Colour.HeatIndex = settings.Graphs.colour.temperature.HeatIndex;
					cumulus.GraphOptions.Colour.DewPoint = settings.Graphs.colour.temperature.DewPoint;
					cumulus.GraphOptions.Colour.WindChill = settings.Graphs.colour.temperature.WindChill;
					cumulus.GraphOptions.Colour.AppTemp = settings.Graphs.colour.temperature.AppTemp;
					cumulus.GraphOptions.Colour.FeelsLike = settings.Graphs.colour.temperature.FeelsLike;
					cumulus.GraphOptions.Colour.Humidex = settings.Graphs.colour.temperature.Humidex;

					cumulus.GraphOptions.Colour.OutHum = settings.Graphs.colour.humidity.Hum;
					cumulus.GraphOptions.Colour.InHum = settings.Graphs.colour.humidity.InHum;
					cumulus.GraphOptions.Colour.Press = settings.Graphs.colour.press.Press;

					cumulus.GraphOptions.Colour.WindGust = settings.Graphs.colour.wind.WindGust;
					cumulus.GraphOptions.Colour.WindAvg = settings.Graphs.colour.wind.WindAvg;
					cumulus.GraphOptions.Colour.WindRun = settings.Graphs.colour.wind.WindRun;
					cumulus.GraphOptions.Colour.WindBearing = settings.Graphs.colour.bearing.Bearing;
					cumulus.GraphOptions.Colour.WindBearingAvg = settings.Graphs.colour.bearing.BearingAvg;

					cumulus.GraphOptions.Colour.Rainfall = settings.Graphs.colour.rain.Rain;
					cumulus.GraphOptions.Colour.RainRate = settings.Graphs.colour.rain.RainRate;

					cumulus.GraphOptions.Colour.UV = settings.Graphs.colour.solar.UV;
					cumulus.GraphOptions.Colour.Solar = settings.Graphs.colour.solar.Solar;
					cumulus.GraphOptions.Colour.SolarTheoretical = settings.Graphs.colour.solar.CurrentSolarMax;
					cumulus.GraphOptions.Colour.Sunshine = settings.Graphs.colour.solar.Sunshine;

					cumulus.GraphOptions.Colour.AvgTemp = settings.Graphs.colour.dailytemp.AvgTemp;
					cumulus.GraphOptions.Colour.MaxTemp = settings.Graphs.colour.dailytemp.MaxTemp;
					cumulus.GraphOptions.Colour.MinTemp = settings.Graphs.colour.dailytemp.MinTemp;
					cumulus.GraphOptions.Colour.MinDew = settings.Graphs.colour.dailytemp.MinDewPoint;
					cumulus.GraphOptions.Colour.MaxDew = settings.Graphs.colour.dailytemp.MaxDewPoint;
					cumulus.GraphOptions.Colour.MinApp = settings.Graphs.colour.dailytemp.MinAppTemp;
					cumulus.GraphOptions.Colour.MaxApp = settings.Graphs.colour.dailytemp.MaxAppTemp;
					cumulus.GraphOptions.Colour.MinFeels = settings.Graphs.colour.dailytemp.MinFeelsLike;
					cumulus.GraphOptions.Colour.MaxFeels = settings.Graphs.colour.dailytemp.MaxFeelsLike;
					cumulus.GraphOptions.Colour.MinWindChill = settings.Graphs.colour.dailytemp.MinWindChill;
					cumulus.GraphOptions.Colour.MaxHeatIndex = settings.Graphs.colour.dailytemp.MaxHeatIndex;
					cumulus.GraphOptions.Colour.MaxHumidex = settings.Graphs.colour.dailytemp.MaxHumidex;

					cumulus.GraphOptions.Colour.MaxPress = settings.Graphs.colour.dailypress.Max;
					cumulus.GraphOptions.Colour.MinPress = settings.Graphs.colour.dailypress.Min;

					cumulus.GraphOptions.Colour.MaxOutHum = settings.Graphs.colour.dailyhum.Max;
					cumulus.GraphOptions.Colour.MinOutHum = settings.Graphs.colour.dailyhum.Min;

					cumulus.GraphOptions.Colour.Pm2p5 = settings.Graphs.colour.aq.Pm2p5;
					cumulus.GraphOptions.Colour.Pm10 = settings.Graphs.colour.aq.Pm10;

					cumulus.GraphOptions.Colour.ExtraTemp = settings.Graphs.colour.extratemp.sensors;
					cumulus.GraphOptions.Colour.ExtraHum = settings.Graphs.colour.extrahum.sensors;
					cumulus.GraphOptions.Colour.ExtraDewPoint = settings.Graphs.colour.extradew.sensors;
					cumulus.GraphOptions.Colour.SoilTemp = settings.Graphs.colour.soiltemp.sensors;
					cumulus.GraphOptions.Colour.SoilMoist = settings.Graphs.colour.soilmoist.sensors;
					cumulus.GraphOptions.Colour.LeafWetness = settings.Graphs.colour.leafwet.sensors;
					cumulus.GraphOptions.Colour.UserTemp = settings.Graphs.colour.usertemp.sensors;

					cumulus.GraphOptions.Colour.CO2Sensor.CO2 = settings.Graphs.colour.co2.co2;
					cumulus.GraphOptions.Colour.CO2Sensor.CO2Avg = settings.Graphs.colour.co2.co2avg;
					cumulus.GraphOptions.Colour.CO2Sensor.Pm25 = settings.Graphs.colour.co2.pm25;
					cumulus.GraphOptions.Colour.CO2Sensor.Pm25Avg = settings.Graphs.colour.co2.pm25avg;
					cumulus.GraphOptions.Colour.CO2Sensor.Pm10 = settings.Graphs.colour.co2.pm10;
					cumulus.GraphOptions.Colour.CO2Sensor.Pm10Avg = settings.Graphs.colour.co2.pm10avg;
					cumulus.GraphOptions.Colour.CO2Sensor.Temp = settings.Graphs.colour.co2.temp;
					cumulus.GraphOptions.Colour.CO2Sensor.Hum = settings.Graphs.colour.co2.hum;

				}
				catch (Exception ex)
				{
					var msg = "Error processing Graph settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Display Options
				try
				{
					// bug catch in case user has the old JSON config files that do not work.
					if (settings.DisplayOptions.windrosepoints == 0)
						settings.DisplayOptions.windrosepoints = 8;
					else if (settings.DisplayOptions.windrosepoints == 1)
						settings.DisplayOptions.windrosepoints = 16;

					cumulus.NumWindRosePoints = settings.DisplayOptions.windrosepoints;
					cumulus.WindRoseAngle = 360.0 / cumulus.NumWindRosePoints;
					cumulus.DisplayOptions.UseApparent = settings.DisplayOptions.useapparent;
					cumulus.DisplayOptions.ShowSolar = settings.DisplayOptions.displaysolar;
					cumulus.DisplayOptions.ShowUV = settings.DisplayOptions.displayuv;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Display Options settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}
			}
			catch (Exception ex)
			{
				var msg = "Error processing Display settings: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Display Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			// Save the settings
			cumulus.WriteIniFile();

			// Graph configs may have changed, so re-create and upload the json files - just flag everything!
			for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
			{
				cumulus.GraphDataFiles[i].CreateRequired = true;
				cumulus.GraphDataFiles[i].FtpRequired = true;
				cumulus.GraphDataFiles[i].CopyRequired = true;
			}
			for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
			{
				cumulus.GraphDataEodFiles[i].CreateRequired = true;
				cumulus.GraphDataEodFiles[i].FtpRequired = true;
				cumulus.GraphDataEodFiles[i].CopyRequired = true;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}



		private class JsonData
		{
			public bool accessible { get; set; }
			public JsonGraphs Graphs { get; set; }
			public JsonDisplayOptions DisplayOptions { get; set; }
			public JsonVisibility DataVisibility { get; set; }
		}

		private class JsonGraphs
		{
			public int graphhours { get; set; }
			public int graphdays { get; set; }

			public JsonColour colour { get; set; }
		}

		private class JsonGraphVisCo2
		{
			public int co2 { get; set; }
			public int co2avg { get; set; }
			public int pm25 { get; set; }
			public int pm25avg { get; set; }
			public int pm10 { get; set; }
			public int pm10avg { get; set; }
			public int temp { get; set; }
			public int hum { get; set; }
		}

		private class JsonGraphVisAq
		{
			public JsonGraphVisAqSensor[] sensors { get; set; } = new JsonGraphVisAqSensor[] { new JsonGraphVisAqSensor(), new JsonGraphVisAqSensor(), new JsonGraphVisAqSensor(), new JsonGraphVisAqSensor() };
		}

		private class JsonGraphVisAqSensor
		{
			public int pm { get; set; }
			public int pmavg { get; set; }
			public int temp { get; set; }
			public int hum { get; set; }
		}


		private class JsonVisibility
		{
			public JsonGraphVisTemperature temperature { get; set; }
			public JsonGraphVisHumidity humidity { get; set; }
			public JsonGraphVisSolar solar { get; set; }
			public JsonGraphVisDegreeDays degreedays { get; set; }
			public JsonGraphVisTempSum tempsum { get; set; }
			public JsonGraphVisExtraSensors extratemp { get; set; }
			public JsonGraphVisExtraSensors extrahum { get; set; }
			public JsonGraphVisExtraSensors extradew { get; set; }
			public JsonGraphVisExtraSensors soiltemp { get; set; }
			public JsonGraphVisExtraSensors soilmoist { get; set; }
			public JsonGraphVisExtraSensors leafwet { get; set; }
			public JsonGraphVisExtraSensors usertemp { get; set; }
			public JsonGraphVisAq aq { get; set; }
			public JsonGraphVisCo2 co2 { get; set; }
		}

		private class JsonGraphVisTemperature
		{
			public int Temp { get; set; }
			public int InTemp { get; set; }
			public int HeatIndex { get; set; }
			public int DewPoint { get; set; }
			public int WindChill { get; set; }
			public int AppTemp { get; set; }
			public int FeelsLike { get; set; }
			public int Humidex { get; set; }
			public int AvgTemp { get; set; }
			public int MaxTemp { get; set; }
			public int MinTemp { get; set; }
		}

		private class JsonGraphVisHumidity
		{
			public int Hum { get; set; }
			public int InHum { get; set; }
		}

		private class JsonGraphVisSolar
		{
			public int UV { get; set; }
			public int Solar { get; set; }
			public int Sunshine { get; set; }
		}

		private class JsonGraphVisDegreeDays
		{
			public int GrowingDegreeDays1 { get; set; }
			public int GrowingDegreeDays2 { get; set; }
		}

		private class JsonGraphVisTempSum
		{
			public int TempSum0 { get; set; }
			public int TempSum1 { get; set; }
			public int TempSum2 { get; set; }
		}

		private class JsonGraphVisExtraSensors
		{
			public int[] sensors { get; set; }
		}

		private class JsonColour
		{
			public JsonGraphColTemperature temperature { get; set; }
			public JsonGraphColDailyTemp dailytemp { get; set; }
			public JsonGraphColHumidity humidity { get; set; }
			public JsonGraphColDailyMaxMin dailyhum { get; set; }
			public JsonGraphColPress press { get; set; }
			public JsonGraphColDailyMaxMin dailypress { get; set; }
			public JsonGraphColWind wind { get; set; }
			public JsonGraphColWindBearing bearing { get; set; }
			public jsonGraphColRain rain { get; set; }
			public JsonGraphColSolar solar { get; set; }
			public JsonGraphColAQ aq { get; set; }
			public JsonGraphColExtraSensors extratemp { get; set; }
			public JsonGraphColExtraSensors extrahum { get; set; }
			public JsonGraphColExtraSensors extradew { get; set; }
			public JsonGraphColExtraSensors soiltemp { get; set; }
			public JsonGraphColExtraSensors soilmoist { get; set; }
			public JsonGraphColExtraSensors leafwet { get; set; }
			public JsonGraphColExtraSensors usertemp { get; set; }
			public JsonGraphColCo2 co2 { get; set; }
		}


		private class JsonGraphColTemperature
		{
			public string Temp { get; set; }
			public string InTemp { get; set; }
			public string HeatIndex { get; set; }
			public string DewPoint { get; set; }
			public string WindChill { get; set; }
			public string AppTemp { get; set; }
			public string FeelsLike { get; set; }
			public string Humidex { get; set; }
		}

		private class JsonGraphColDailyTemp
		{
			public string AvgTemp { get; set; }
			public string MaxTemp { get; set; }
			public string MinTemp { get; set; }
			public string MaxDewPoint { get; set; }
			public string MinDewPoint { get; set; }
			public string MaxHeatIndex { get; set; }
			public string MinWindChill { get; set; }
			public string MaxAppTemp { get; set; }
			public string MinAppTemp { get; set; }
			public string MaxFeelsLike { get; set; }
			public string MinFeelsLike { get; set; }
			public string MaxHumidex { get; set; }
		}

		private class JsonGraphColHumidity
		{
			public string Hum { get; set; }
			public string InHum { get; set; }
		}

		private class JsonGraphColPress
		{
			public string Press { get; set; }
		}

		private class JsonGraphColDailyMaxMin
		{
			public string Max { get; set; }
			public string Min { get; set; }
		}

		private class JsonGraphColWind
		{
			public string WindGust { get; set; }
			public string WindAvg { get; set; }
			public string WindRun { get; set; }
		}

		private class JsonGraphColWindBearing
		{
			public string Bearing { get; set; }
			public string BearingAvg { get; set; }
		}

		private class jsonGraphColRain
		{
			public string Rain { get; set; }
			public string RainRate { get; set; }
		}

		private class JsonGraphColSolar
		{
			public string UV { get; set; }
			public string Solar { get; set; }
			public string CurrentSolarMax { get; set; }
			public string Sunshine { get; set; }
		}

		private class JsonGraphColAQ
		{
			public string Pm2p5 { get; set; }
			public string Pm10 { get; set; }
		}

		private class JsonGraphColExtraSensors
		{
			public string[] sensors { get; set; }
		}

		private class JsonGraphColCo2
		{
			public string co2 { get; set; }
			public string co2avg { get; set; }
			public string pm25 { get; set; }
			public string pm25avg { get; set; }
			public string pm10 { get; set; }
			public string pm10avg { get; set; }
			public string temp { get; set; }
			public string hum { get; set; }
		}
	}
}
