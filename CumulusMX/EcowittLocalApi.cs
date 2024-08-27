using System;
using System.Threading;

using ServiceStack;



namespace CumulusMX
{
	internal sealed class EcowittLocalApi : IDisposable
	{
		private readonly Cumulus cumulus;
		private string ipAddress = null;

		public EcowittLocalApi(Cumulus cumul)
		{
			cumulus = cumul;
		}


		public liveData GetLiveData(CancellationToken token)
		{
			// http://ip-address/get_livedata_info
			//
			// Returns an almighty mess! They couldn't have made this any worse if they tried!
			// All values are returned as strings - including integers and decimals
			// Some values include the units in the value string, others have a separate field for the unit
			// The separate sensors return an arrays that only ever contain a single object
			//
			//	{
			//		"common_list": [
			//			{
			//				id: "ITEM_ID",
			//				val: "VALUE[ [UNIT]]",
			//				unit: "UNIT",
			//				[Battery: "VALUE"]
			//			},
			//			{etc}
			//		],
			//		"rain": [
			//			{
			//				id: "ITEM_ID",
			//				val: "VALUE[ [UNIT]]",
			//				unit: "UNIT",
			//				[Battery: "VALUE"]
			//			},
			//			{etc}
			//		],
			//		"piezoRain": [
			//			{
			//				id: "ITEM_ID",
			//				val: "[DECIMAL][ [UNIT]]",
			//				unit: "UNIT",
			//				[Battery: "[INT]"
			//			},
			//			{etc}
			//		],
			//		"wh25": [
			//			{
			//				"intemp": "[DECIMAL]",
			//				"unit": "C|F",
			//				"inhumi": "[INT]%",
			//				"abs": "[DECIMAL] [hPa|???]",
			//				"rel": "[DECIMAL] [hPa|???]",
			//				"battery": "[INT]"
			//			}
			//		],
			//		"lightning": [
			//			{
			//				"distance": "[INT] [km|???]",
			//				"timestamp": "MM/DD/YYYY HH:MM:SS",
			//				"count": "[INT]",
			//				"battery": "[INT]"
			//			}
			//		],
			//		"co2": [
			//			{
			//				"temp": "[DECIMAL]",
			//				"unit": "[C|F]",
			//				"humidity": "[INT]%",
			//				"PM25": "[DECIMAL]",
			//				"PM25_RealAQI": "[INT]",
			//				"PM25_24HAQI": "[INT]",
			//				"PM10": "[DECIMAL]",
			//				"PM10_RealAQI": "[INT]",
			//				"PM10_24HAQI": "[INT]",
			//				"CO2": "[INT]",
			//				"CO2_24H": "[INT]",
			//				"battery": "[INT]"
			//			}
			//		],
			//		"ch_pm25": [
			//			{
			//				"channel": "[1-4]",
			//				"PM25": "[DECIMAL]",
			//				"PM25_RealAQI": "[INT]",
			//				"PM25_24HAQI": "[INT]",
			//				"battery": "[INT]"
			//			},
			//			{etc}
			//		],
			//		"ch_aisle": [
			//			{
			//				"channel": "[1-16]",
			//				"name": "",
			//				"battery": "[INT]",
			//				"temp": "[DECIMAL]",
			//				"unit": "[C|F]",
			//				"humidity": "[[INT]%|None]"
			//			},
			//			{etc}
			//		],
			//		"ch_soil": [
			//			{
			//				"channel": "[1-16]",
			//				"name": "",
			//				"battery": "[INT",
			//				"humidity": "[INT]%"
			//			},
			//			{etc}
			//		],
			//		"ch_temp": [
			//			{
			//				"channel": "[1-16]",
			//				"name": "",
			//				"temp": "[DECIMAL]",
			//				"unit": "[C|F]",
			//				"battery": "[INT]"
			//			},
			//			{etc}
			//		],
			//		"ch_leaf": [
			//			{
			//				"channel": "[1-??]",
			//				"name": "",
			//				"humidity": "[INT]%"
			//				"battery": "[INT]",
			//			},
			//			{etc}
			//		]
			//	}
			//
			//
			// Sample:
			// {"common_list": [{"id": "0x02", "val": "23.5", "unit": "C"}, {"id": "0x07", "val": "57%"}, {"id": "3", "val": "23.5", "unit": "C"}, {"id": "0x03", "val": "14.5", "unit": "C"}, {"id": "0x0B", "val": "9.00 km/h"}, {"id": "0x0C", "val": "9.00 km/h"}, {"id": "0x19", "val": "26.64 km/h"}, {"id": "0x15", "val": "646.57 W/m2"}, {"id": "0x17", "val": "3"}, {"id": "0x0A", "val": "295"}], "rain": [{"id": "0x0D", "val": "0.0 mm"}, {"id": "0x0E", "val": "0.0 mm/Hr"}, {"id": "0x10", "val": "0.0 mm"}, {"id": "0x11", "val": "5.0 mm"}, {"id": "0x12", "val": "27.1 mm"}, {"id": "0x13", "val": "681.4 mm", "battery": "0"}], "piezoRain": [{"id": "0x0D", "val": "0.0 mm"}, {"id": "0x0E", "val": "0.0 mm/Hr"}, {"id": "0x10", "val": "0.0 mm"}, {"id": "0x11", "val": "10.7 mm"}, {"id": "0x12", "val": "32.3 mm"}, {"id": "0x13", "val": "678.3 mm", "battery": "5"}], "wh25": [{"intemp": "26.0", "unit": "C", "inhumi": "56%", "abs": "993.0 hPa", "rel": "1027.4 hPa", "battery": "0"}], "lightning": [{"distance": "12 km", "timestamp": "07/15/2024 20: 46: 42", "count": "0", "battery": "3"}], "co2": [{"temp": "24.4", "unit": "C", "humidity": "62%", "PM25": "0.9", "PM25_RealAQI": "4", "PM25_24HAQI": "7", "PM10": "0.9", "PM10_RealAQI": "1", "PM10_24HAQI": "2", "CO2": "323", "CO2_24H": "348", "battery": "6"}], "ch_pm25": [{"channel": "1", "PM25": "6.0", "PM25_RealAQI": "25", "PM25_24HAQI": "24", "battery": "5"}, {"channel": "2", "PM25": "8.0", "PM25_RealAQI": "33", "PM25_24HAQI": "32", "battery": "5"}], "ch_leak": [{"channel": "2", "name": "", "battery": "4", "status": "Normal"}], "ch_aisle": [{"channel": "1", "name": "", "battery": "0", "temp": "24.9", "unit": "C", "humidity": "61%"}, {"channel": "2", "name": "", "battery": "0", "temp": "25.7", "unit": "C", "humidity": "64%"}, {"channel": "3", "name": "", "battery": "0", "temp": "23.6", "unit": "C", "humidity": "63%"}, {"channel": "4", "name": "", "battery": "0", "temp": "34.9", "unit": "C", "humidity": "83%"}, {"channel": "5", "name": "", "battery": "0", "temp": "-14.4", "unit": "C", "humidity": "None"}, {"channel": "6", "name": "", "battery": "0", "temp": "31.5", "unit": "C", "humidity": "56%"}, {"channel": "7", "name": "", "battery": "0", "temp": "8.2", "unit": "C", "humidity": "50%"}], "ch_soil": [{"channel": "1", "name": "", "battery": "5", "humidity": "56%"}, {"channel": "2", "name": "", "battery": "4", "humidity": "47%"}, {"channel": "3", "name": "", "battery": "5", "humidity": "27%"}, {"channel": "4", "name": "", "battery": "5", "humidity": "50%"}, {"channel": "5", "name": "", "battery": "4", "humidity": "54%"}, {"channel": "6", "name": "", "battery": "4", "humidity": "47%"}], "ch_temp": [{"channel": "1", "name": "", "temp": "21.5", "unit": "C", "battery": "3"}, {"channel": "2", "name": "", "temp": "16.4", "unit": "C", "battery": "5"}], "ch_leaf": [{"channel": "1", "name": "CH1 Leaf Wetness", "humidity": "10%", "battery": "5"}]}

			string responseBody;
			int responseCode;
			int retries = 3;

			string ip;
			int retry = 1;

			ip = cumulus.DavisOptions.IPAddr;

			do
			{
				var url = $"http://{ip}/get_livedata_info?";

				// we want to do this synchronously, so .Result
				using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"LocalApi.GetLiveData: Ecowitt Local API GetLiveData Response code: {responseCode}");
					cumulus.LogDataMessage($"LocalApi.GetLiveData: Ecowitt Local API GetLiveData Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					cumulus.LogWarningMessage($"LocalApi.GetLiveData: Ecowitt Local API GetLiveData Error: {responseCode}");
					Cumulus.LogConsoleMessage($" - Error {responseCode}", ConsoleColor.Red);
					return null;
				}


				if (responseBody == "{}")
				{
					cumulus.LogMessage("LocalApi.GetLiveData: Ecowitt Local API GetLiveData: No data was returned.");
					Cumulus.LogConsoleMessage(" - No Live data available");
					return null;
				}
				else if (responseBody.StartsWith("{ \"common")) // sanity check
				{
					// Convert JSON string to an object
					liveData json = responseBody.FromJson<liveData>();
					return json;
				}
			} while (retries-- > 0);

			return null;
		}


		public void GetSensorInfo()
		{
			// http://ip-address/get_sensors_info?page=1
			// http://ip-address/get_sensors_info?page=2

		}


		public void Dispose()
		{
			try
			{
			}
			catch
			{
				// do nothing
			}
		}

		private enum commonSensorTypes
		{
			indoorTemp = 1,
			temp = 2,
			dewpoint = 3,
			windchill = 4,
			heatindex = 5,
			indoorHum = 6,
			hum = 7,
			absPressure = 8,
			relPressure = 9,
			windDir = 10,
			windSpeed = 11,
			windGust = 12,
			rainEvent = 13,
			rainRate = 14,
			rainGain = 15,
			rainDay = 16,
			rainWeek = 17,
			rainMonth = 18,
			rainYear = 19,
			rainTotals = 20,
			light = 21,
			uv = 22,
			uvindex = 23,
			time = 24,
			dayWindMax = 25
		}

		public class commonSensor
		{
			public int id { get; set; }
			public string val { get; set; }
			public string? unit { get; set; }
			public double? battery { get; set; }

			public int? valInt
			{
				get
				{
					if (val.EndsWith('%'))
						val = val[0..^1];
					return int.TryParse(val, out int result) ? result : null;
				}
			}

			public double? valDbl
			{
				get
				{
					if (val.Contains(' '))
					{
						var temp = val.Split(' ');
						unit = temp[1];
						val = temp[0];
					}
					return double.TryParse(val, out double result) ? result : null;
				}
			}
		}

		public class tempHumSensor
		{
			public int channel { get; set; }
			public int battery { get; set; }
			public double? temp { get; set; }
			public string? humidity { get; set; }
			public string? unit { get; set; }

			public int? humidityVal
			{
				get
				{
					if (humidity.EndsWith('%'))
						humidity = humidity[0..^1];
					return int.TryParse(humidity, out int result) ? result : null;
				}
			}
		}


		public class wh25Sensor
		{
			public double intemp { get; set; }
			public string unit { get; set; }
			public string inhumi { get; set; }
			public string abs { get; set; }
			public string rel { get; set; }
			public int battery { get; set; }

			public int? inhumiInt
			{
				get
				{
					return int.TryParse(inhumi[0..^1], out int result) ? result : null;
				}
			}
		}

		public class lightningSensor
		{
			public string distance { get; set; }
			public string timestamp { get; set; }
			public int count { get; set; }
			public int battery { get; set; }

			public double? distanceVal
			{
				get
				{
					var temp = distance.Split(' ');
					return double.TryParse(temp[0], out double result) ? result : null;
				}
			}

			public string? distanceUnit
			{
				get
				{
					var temp = distance.Split(' ');
					return temp[1];
				}
			}
		}

		public class co2Sensor
		{
			public double temp { get; set; }
			public string unit { get; set; }
			public string humidity { get; set; }
			public double PM25 { get; set; }
			public double PM25_RealAQI { get; set; }
			public double PM25_24HAQI { get; set; }
			public double PM10 { get; set; }
			public double PM10_RealAQI { get; set; }
			public double PM10_24HAQI { get; set; }
			public int CO2 { get; set; }
			public int CO2_24H { get; set; }
			public int battery { get; set; }
		}

		public class ch_pm25Sensor
		{
			public int channel { get; set; }
			public double PM25 { get; set; }
			public double PM25_RealAQI { get; set; }
			public double PM25_24HAQI { get; set; }
			public int battery { get; set; }
		}

		public class ch_leakSensor
		{
			public int channel { get; set; }
			public string name { get; set; }
			public int battery { get; set; }
			public string status { get; set; }
		}

		public class liveData
		{
			public commonSensor[] common_list { get; set; }
			public commonSensor[]? rain { get; set; }
			public commonSensor[]? piezoRain { get; set; }
			public wh25Sensor[]? wh25 { get; set; }
			public lightningSensor[]? lightning { get; set; }
			public co2Sensor[]? co2 { get; set; }
			public ch_pm25Sensor[]? ch_pm25 { get; set; }
			public ch_leakSensor[]? ch_leak { get; set; }
			public tempHumSensor[]? ch_aisle { get; set; }
			public tempHumSensor[]? ch_soil { get; set; }
			public tempHumSensor[]? ch_temp { get; set; }
			public tempHumSensor[]? ch_leaf { get; set; }
		}


	}

}
