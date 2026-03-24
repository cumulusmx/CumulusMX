using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace CumulusMX.Stations
{
	internal sealed class EcowittLocalApi : IDisposable
	{
		private readonly Cumulus cumulus ;
		private static readonly NumberFormatInfo invNum = CultureInfo.InvariantCulture.NumberFormat;
		internal readonly string[] lineEnds = ["\r\n", "\n"];

		private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions();

		public EcowittLocalApi(Cumulus cumul)
		{
			cumulus = cumul;

			/*
			 * NOTE: Nasty 'orrible JSON from Ecowitt, they send every value as a string!
			 *
			 * We must create a custom converter for System.Text.Json so parse the strings into the required data types
			 *
			 */
			jsonOptions.Converters.Add(new JsonConverters.JsonIntConverter());
			jsonOptions.Converters.Add(new JsonConverters.JsonLongConverter());
			jsonOptions.Converters.Add(new JsonConverters.JsonDoubleConverter());
			jsonOptions.Converters.Add(new JsonConverters.JsonDecimalConverter());
			jsonOptions.Converters.Add(new JsonConverters.JsonNullIntConverter());
			jsonOptions.Converters.Add(new JsonConverters.JsonNullLongConverter());
			jsonOptions.Converters.Add(new JsonConverters.JsonNullDoubleConverter());
			jsonOptions.Converters.Add(new JsonConverters.JsonNullDecimalConverter());
			jsonOptions.Converters.Add(new JsonConverters.JsonBoolConverter());

		}

		public int SdCardInterval { get; set; }


		#region Get Methods
		public static void GetAvailbleSsids(CancellationToken token)
		{
			// http://ip-address/usr_scan_ssid_list
			// response
			//{
			//	"list": [
			//		{
			//			"ssid": "SSID_NAME",
			//			"rssi": "-45",
			//			"auth": "4"
			//		},
			//		{etc}
			//	]
			//}
		}

		public async Task<Calibration> GetCalibrationData(CancellationToken token)
		{
			// http://ip-address/get_calibration_data

			// response
			//{
			//	"SolarRadWave": "126.7",	???
			//	"solarRadGain": "1.00",		Irradiance gain
			//	"uvGain": "1.00",
			//	"windGain": "1.00",
			//	"inTempOffset": "0.0",
			//	"inHumOffset": "0.0",
			//	"absOffset": "0.3",
			//	"altitude": "72",
			//	"outTempOffset": "0.0",
			//	"outHumOffset": "0.0",
			//	"windDirsOffset": "0",
			//	"th_cli": true,				Show Multi CH T/H calibration
			//	"pm25_cli": true			Show PM2.5 calibration
			//	"soil_cli": true,			Show Soil sensor calibration
			//	"co2_cli": true				Show CO2 calibration
			//}

			if (!Utils.ValidateIPv4(cumulus.Gw1000IpAddress))
			{
				cumulus.LogErrorMessage("GetCalibrationData: Invalid station IP address: " + cumulus.Gw1000IpAddress);
				return null;
			}

			string responseBody;
			int responseCode;

			try
			{
				var url = $"http://{cumulus.Gw1000IpAddress}/get_calibration_data";

				// we want to do this synchronously, so .Result
				using (var response = await cumulus.MyHttpClient.GetAsync(url, token))
				{
					responseBody = await response.Content.ReadAsStringAsync(token);
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"LocalApi.GetCalibrationData: Ecowitt Local API Response code: {responseCode}");
					cumulus.LogDataMessage($"LocalApi.GetCalibrationData: Ecowitt Local API Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					cumulus.LogWarningMessage($"LocalApi.GetCalibrationData: Ecowitt Local API Error: {responseCode}");
					Cumulus.LogConsoleMessage($" - Error {responseCode}", ConsoleColor.Red);
					return null;
				}


				if (responseBody == "{}")
				{
					cumulus.LogMessage("LocalApi.GetLiveData: Ecowitt Local API: No data was returned.");
					Cumulus.LogConsoleMessage(" - No Calibration data available");
					return null;
				}
				else if (responseBody.StartsWith('{')) // sanity check
				{
					// Convert JSON string to an object
					var json = JsonSerializer.Deserialize<Calibration>(responseBody, jsonOptions);
					return json;
				}
			}
			catch (HttpRequestException ex)
			{
				if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
				{
					cumulus.LogErrorMessage("GetCalibrationData: Error - This Station does not support the HTTP API!");
				}
				else
				{
					cumulus.LogExceptionMessage(ex, "GetCalibrationData: HTTP Error");
				}
			}
			catch (Exception ex)
			{
				if (token.IsCancellationRequested)
				{
					cumulus.LogDebugMessage("GetCalibrationData: Operation cancelled due to shutting down");
					return null;
				}

				cumulus.LogExceptionMessage(ex, "GetCalibrationData: Error");
			}

			return null;
		}

		public async Task<DeviceInfo> GetDeviceInfo(CancellationToken token)
		{
			// http://ip-address/get_device_info

			//{
			//	"sensorType":	"1",
			//	"rf_freq":	"1",
			//	"AFC":	"0",
			//	"tz_auto":	"1",
			//	"tz_name":	"",
			//	"tz_index":	"39",
			//	"dst_stat":	"1",
			//	"radcompensation":	"0",
			//	"date":	"2024-09-06T16:36",
			//	"upgrade":	"0",
			//	"apAuto":	"1",
			//	"newVersion":	"0",
			//	"curr_msg":	"Current version:V2.3.4\r\n- Optimize RF reception performance.\r\n- Fix the issue of incorrect voltage upload for wh34/wh35/wh68 batteries.",
			//	"apName":	"GW1100A-WIFID4D3",
			//	"APpwd":	"base64-string",
			//	"time":	"20"
			//}

			string responseBody;
			int responseCode;
			var retries = 2;

			if (!Utils.ValidateIPv4(cumulus.Gw1000IpAddress))
			{
				cumulus.LogErrorMessage("LocalApi.GetDeviceInfo: Invalid station IP address: " + cumulus.Gw1000IpAddress);
				return null;
			}


			do
			{
				try
				{
					var url = $"http://{cumulus.Gw1000IpAddress}/get_device_info";

					// we want to do this synchronously, so .Result
					using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
					{
						responseBody = response.Content.ReadAsStringAsync(token).Result;
						responseCode = (int) response.StatusCode;
						cumulus.LogDebugMessage($"LocalApi.GetDeviceInfo: Ecowitt Local API GetDeviceInfo Response code: {responseCode}");
						cumulus.LogDataMessage($"LocalApi.GetDeviceInfo: Ecowitt Local API GetDeviceInfo Response: {Utils.RemoveCrTabsFromString(responseBody)}");
					}

					if (responseCode != 200)
					{
						cumulus.LogWarningMessage($"LocalApi.GetDeviceInfo: Ecowitt Local API GetDeviceInfo Error: {responseCode}");
						Cumulus.LogConsoleMessage($" - Error {responseCode}", ConsoleColor.Red);
						return null;
					}


					if (responseBody == "{}")
					{
						cumulus.LogMessage("LocalApi.GetDeviceInfo: Ecowitt Local API GetDeviceInfo: No data was returned.");
						Cumulus.LogConsoleMessage(" - No Live data available");
						return null;
					}
					else if (responseBody.StartsWith('{')) // sanity check
					{
						// Convert JSON string to an object
						var json = JsonSerializer.Deserialize<DeviceInfo>(responseBody, jsonOptions);
						return json;
					}
				}
				catch (HttpRequestException ex)
				{
					if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						cumulus.LogErrorMessage("LocalApi.GetDeviceInfo: Error - This Station does not support the HTTP API!");
					}
					else
					{
						cumulus.LogExceptionMessage(ex, "LocalApi.GetDeviceInfo: HTTP Error");
					}
				}
				catch (Exception ex)
				{
					if (token.IsCancellationRequested)
					{
						cumulus.LogDebugMessage("LocalApi.GetDeviceInfo: Operation cancelled due to shutting down");
						return null;
					}

					cumulus.LogExceptionMessage(ex, "LocalApi.GetDeviceInfo: Error");
				}
			} while (retries-- > 0);

			return null;
		}

		public static void GetIotList(CancellationToken token)
		{
			// http://ip-address/get_iot_device_list
		}

		public LiveData GetLiveData(CancellationToken token)
		{
			// http://ip-address/get_livedata_info
			//
			// Returns an almighty mess! They couldn't have made this any worse if they tried!
			// All values are returned as strings - including integers and decimals
			// Some values include the units in the value string, others have a separate field for the unit
			// The separate sensors return an array that only ever contain a single object
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
			//		],
			//		"ch_lds": [
			//			{
			//				"channel": "[1-4]",
			//				"name": "",
			//				"unit": "[mm|cm|in]",
			//				"battery": "[INT]",
			//				"air": "[DECIMAL]",
			//				"depth": "[DECIMAL]"
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
			var retries = 2;

			if (!Utils.ValidateIPv4(cumulus.Gw1000IpAddress))
			{
				cumulus.LogErrorMessage("LocalApi.GetLiveData: Invalid station IP address: " + cumulus.Gw1000IpAddress);
				return null;
			}


			do
			{
				try
				{
					var url = $"http://{cumulus.Gw1000IpAddress}/get_livedata_info";

					// we want to do this synchronously, so .Result
					using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
					{
						responseBody = response.Content.ReadAsStringAsync(token).Result;
						responseCode = (int) response.StatusCode;
						cumulus.LogDebugMessage($"LocalApi.GetLiveData: Ecowitt Local API GetLiveData Response code: {responseCode}");
						cumulus.LogDataMessage($"LocalApi.GetLiveData: Ecowitt Local API GetLiveData Response: {Utils.RemoveCrTabsFromString(responseBody)}");
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
					else if (responseBody.Contains("\"data\":[]"))
					{
						cumulus.LogMessage("LocalApi.GetLiveData: Ecowitt Local API GetLiveData: No data block was returned.");
						Cumulus.LogConsoleMessage(" - No Live data available");
						return null;
					}
					else if (responseBody.StartsWith('{')) // sanity check
					{
						// Convert JSON string to an object
						var json = JsonSerializer.Deserialize<LiveData>(responseBody, jsonOptions);
						return json;
					}
				}
				catch (HttpRequestException ex)
				{
					if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						cumulus.LogErrorMessage("LocalApi.GetLiveData: Error - This Station does not support the HTTP API!");
					}
					else
					{
						cumulus.LogExceptionMessage(ex, "LocalApi.GetLiveData: HTTP Error");
					}
				}
				catch (Exception ex)
				{
					if (token.IsCancellationRequested)
					{
						cumulus.LogDebugMessage("LocalApi.GetLiveData: Operation cancelled due to shutting down");
						return null;
					}

					cumulus.LogExceptionMessage(ex, "LocalApi.GetLiveData: Error");
				}
			} while (retries-- > 0);

			return null;
		}

		public static void GetNetworkInfo(CancellationToken token)
		{
			// http://ip-address/get_network_info
			//response
			//{
			//	"mac": "E8:DB:84:0F:15:43",
			//	"ehtIpType": "1",				0=WiFi, 1=Ethernet
			//	"ethIP": "10.10.10.106",
			//	"ethMask": "255.255.255.0",
			//	"ethGateway": "10.10.10.100",
			//	"ssid": "GW1100A-WIFI38B4",
			//	"wifi_pwd": "base64-string",
			//	"wifi_ip": "192.168.4.10",
			//	"wifi_mask": "192.168.4.10",
			//	"wifi_gateway":	"192.168.4.1"
			//}
		}

		public static void GetRainTotals(CancellationToken token)
		{
			// http://ip-address/get_rain_totals

			// response
			//{
			//	"rainFallPriority": "1",       0=No Guage 1=Traditional 2=Piezo
			//	"list":	[
			//		{
			//			"gauge": "No rain gauge",
			//			"value": "0"
			//		}, {
			//			"gauge": "Traditional rain gauge",
			//			"value": "1"
			//		}, {
			//			"gauge": "Piezoelectric rain gauge",
			//			"value": "2"
			//		}
			//	],
			//	"rainDay": "0.0",
			//	"rainWeek": "5.3",
			//	"rainMonth": "6.8",
			//	"rainYear": "572.5",
			//	"rainGain": "1.00",
			//	"rstRainDay": "0",      reset hour - 0=00:00 etc
			//	"rstRainWeek": "1",     0=Sunday 1=Monday
			//	"rstRainYear":"0"       reset month
			//}

			// response = 200 - OK
		}

		public async Task<SdCard> GetSdCardInfo(CancellationToken token)
		{
			// http://IP-address/get_sdmmc_info
			//
			// {"info":{"Name":"SZYL","Type":"SDHC/SDXC","Speed":"20 MHz","Size":"7580 MB","Interval":"5"},"file_list":[{"name":"202409A.csv","type":"1","size":"3212"},{"name":"202409Allsensors_A.csv","type":"1","size":"10075"},{"name":"log","type":"2","size":"-"},{"name":"202401A.csv","type":"1","size":"604"},{"name":"202401Allsensors_A.csv","type":"1","size":"2123"},{"name":"202409B.csv","type":"1","size":"398829"},{"name":"202409Allsensors_B.csv", "type":"1","size":"1160913"},{"name":"202410B.csv","type":"1","size":"1051061"},{"name":"202410Allsensors_B.csv","type":"1","size":"3039518"},{"name":"202411B.csv","type":"1","size":"986611"},{"name":"202411Allsensors_B.csv","type":"1","size":"2861108"},{"name":"202412B.csv","type":"1","size":"78625"},{"name":"202412Allsensors_B.csv","type":"1","size":"228437"}]}

			if (!Utils.ValidateIPv4(cumulus.Gw1000IpAddress))
			{
				cumulus.LogErrorMessage("GetSensorInfo: Invalid station IP address: " + cumulus.Gw1000IpAddress);
				return null;
			}

			string responseBody;
			int responseCode;
			var retries = 1;

			//responseBody = "{\"info\":{\"Name\":\"     \",\"Type\":\"SDHC/SDXC\",\"Speed\":\"20 MHz\",\"Size\":\"30223 MB\",\"Interval\":\"1\"},\"file_list\":[{\n\t\t\"name\":\t\"202502B.csv\",\n\t\t\"type\":\t\"file\",\n\t\t\"size\":\t\"71 KB\"\n\t}, {\n\t\t\"name\":\t\"202502Allsensors_A.csv\",\n\t\t\"type\":\t\"file\",\n\t\t\"size\":\t\"202 KB\"\n\t}]}"
			//return responseBody.FromJson<SdCard>()

			var url = $"http://{cumulus.Gw1000IpAddress}/get_sdmmc_info";
			// my test server uses port 81 for everything
			//var url = $"http://{cumulus.Gw1000IpAddress}:81/get_sdmmc_info";

			do
			{
				try
				{
					using (var response = await cumulus.MyHttpClient.GetAsync(url, token))
					{
						responseBody = response.Content.ReadAsStringAsync(token).Result;
						responseCode = (int) response.StatusCode;
						cumulus.LogDebugMessage($"LocalApi.GetSdCardInfo: Ecowitt Local API GetSdCardInfo Response code: {responseCode}");
						cumulus.LogDataMessage($"LocalApi.GetSdCardInfo: Ecowitt Local API GetSdCardInfo Response: {Utils.RemoveCrTabsFromString(responseBody)}");
					}

					if (responseCode != 200)
					{
						cumulus.LogWarningMessage($"LocalApi.GetSdCardInfo: Ecowitt Local API GetSdCardInfo Error: {responseCode}");
						Cumulus.LogConsoleMessage($" - Error {responseCode}", ConsoleColor.Red);
					}
					else if (responseBody == "{}")
					{
						cumulus.LogMessage("LocalApi.GetSdCardInfo: Ecowitt Local API GetSdCardInfo: No data was returned.");
						Cumulus.LogConsoleMessage(" - No data available");
					}
					else if (responseBody.StartsWith('{')) // sanity check
					{
						// Convert JSON string to an object
						var resp = JsonSerializer.Deserialize<SdCard>(responseBody, jsonOptions);
						SdCardInterval = resp.info.Interval;
						return resp;
					}

				}
				catch (HttpRequestException ex)
				{
					if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						cumulus.LogErrorMessage("GetSdCardInfo: Error - This Station does not support the HTTP API!");
					}
					else
					{
						cumulus.LogExceptionMessage(ex, "GetSdCardInfo: HTTP Error");
					}
				}
				catch (Exception ex)
				{
					if (token.IsCancellationRequested)
					{
						cumulus.LogDebugMessage("GetSdCardInfo: Operation cancelled due to shutting down");
						return null;
					}
					cumulus.LogExceptionMessage(ex, "GetSdCardInfo: Error");
				}

				retries--;
				Thread.Sleep(250);
			} while (retries >= 0);

			return null;
		}

		public async Task<List<string>> GetSdFileContents(string fileName, DateTime startTime, CancellationToken token)
		{
			// http://IP-address:81/filename (where filename is YYYYMM[A-Z].csv resp. YYYMMAllSensors_[A-Z].csv)
			//
			// YYYYMM[A - Z].csv
			// Time,Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃),Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),Wind(m/s),Gust(m/s),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm)
			// 2024-09-18 14:25,22.8,55,23.2,54,13.4,23.2,1.1,1.6,259,989.6,1013.1,519.34,4,5.47,4.84,1,0.0,0.0,0.0,0.0,0.0,0.0
			//
			// YYYMMAllSensors_[A-Z].csv
			// Time,CH1 Temperature(℃),CH1 Dew point(℃),CH1 HeatIndex(℃),CH1 Humidity(%),CH2 Temperature(℃),CH2 Dew point(℃),CH2 HeatIndex(℃),CH2 Humidity(%),CH3 Temperature(℃),CH3 Dew point(℃),CH3 HeatIndex(℃),CH3 Humidity(%),CH4 Temperature(℃),CH4 Dew point(℃),CH4 HeatIndex(℃),CH4 Humidity(%),CH5 Temperature(℃),CH5 Dew point(℃),CH5 HeatIndex(℃),CH5 Humidity(%),CH6 Temperature(℃),CH6 Dew point(℃),CH6 HeatIndex(℃),CH6 Humidity(%),CH7 Temperature(℃),CH7 Dew point(℃),CH7 HeatIndex(℃),CH7 Humidity(%),CH8 Temperature(℃),CH8 Dew point(℃),CH8 HeatIndex(℃),CH8 Humidity(%),WH35 CH1hum(%),WH35 CH2hum(%),WH35 CH3hum(%),WH35 CH4hum(%),WH35 CH5hum(%),WH35 CH6hum(%),WH35 CH7hum(%),WH35 CH8hum(%),Thunder count,Thunder distance(km),AQIN Temperature(℃),AQIN Humidity(%),AQIN CO2(ppm),AQIN PM2.5(ug/m3),AQIN PM10(ug/m3),AQIN PM1.0(ug/m3),AQIN PM4.0(ug/m3),SoilMoisture CH1(%),SoilMoisture CH2(%),SoilMoisture CH3(%),SoilMoisture CH4(%),SoilMoisture CH5(%),SoilMoisture CH6(%),SoilMoisture CH7(%),SoilMoisture CH8(%),SoilMoisture CH9(%),SoilMoisture CH10(%),SoilMoisture CH11(%),SoilMoisture CH12(%),SoilMoisture CH13(%),SoilMoisture CH14(%),SoilMoisture CH15(%),SoilMoisture CH16(%),Water CH1,Water CH2,Water CH3,Water CH4,Pm2.5 CH1(ug/m3),Pm2.5 CH2(ug/m3),Pm2.5 CH3(ug/m3),Pm2.5 CH4(ug/m3),WN34 CH1(℃),WN34 CH2(℃),WN34 CH3(℃),WN34 CH4(℃),WN34 CH5(℃),WN34 CH6(℃),WN34 CH7(℃),WN34 CH8(℃),LDS_Air CH1(mm),LDS_Air CH2(mm),LDS_Air CH3(mm),LDS_Air CH4(mm),
			// 2024-09-18 14:25,24.5,14.3,24.5,53,30.0,17.5,30.5,47,24.0,13.6,24.0,52,--.-,--.-,--.-,--,-15.5,--.-,--.-,--,38.1,23.8,45.6,44,6.6,-1.5,6.6,56,--.-,--.-,--.-,--,19,--,--,--,--,--,--,--,0,--.-,20.3,66,479,10.4,10.8,--.-,--.-,69,51,78,49,44,52,--,--,--,--,--,--,--,--,--,--,--,Normal,--,--,--.-,--.-,--.-,--.-,11.5,16.8,24.0,--.-,--.-,--.-,--.-,--.-
			// 2025-01-10 12:34,1.8,0.8,1.8,93,3.3,1.5,3.3,88,1.5,-0.1,1.5,89,1.6,-0.3,1.6,87,-19.3,--,--,--,3.9,2.7,3.9,92,7.0,-3.0,7.0,49,--,--,--,--,77,--,--,--,--,--,--,--,0,--,15.3,60,775,6.4,6.7,--,--,60,45,56,72,50,74,--,--,--,--,--,--,--,--,--,--,--,Normal,--,--,12.0,9.0,--,--,2.5,2.5,2.0,--,--,--,--,--,--,--,--,--

			cumulus.LogDebugMessage($"LocalApi.GetSdFileContents: Requesting file {fileName} from station");

			if (!Utils.ValidateIPv4(cumulus.Gw1000IpAddress))
			{
				cumulus.LogErrorMessage("LocalApi.GetSdFileContents: Invalid station IP address: " + cumulus.Gw1000IpAddress);
				return null;
			}

			var retries = 1;

			var url = $"http://{cumulus.Gw1000IpAddress}:81/" + fileName;

			Cumulus.LogConsoleMessage("  Extracting data");

			// Get the contents
			do
			{
				try
				{
					using var fileStream = await cumulus.MyHttpClient.GetStreamAsync(url, token);
					using var streamReader = new StreamReader(fileStream, Encoding.UTF8);

					string line;
					var count = 0;
					bool useTimeStamp = true;
					List<string> result = [];

					cumulus.LogDebugMessage($"LocalApi.GetSdFileContents: Extracting all lines from starting time {startTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");

					while ((line = await streamReader.ReadLineAsync(token)) != null)
					{
						count++;

						if (count % 50 == 0 && !Program.service)
						{
							Console.Write($"  Extracting line: {count}\r");
						}

						if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(line.Trim()))
						{
							// skip any blank lines
							continue;
						}

						// quick check if there is any data in the file!
						var fields = line.Split(',');
						if (count == 1)
						{
							if (fields.Length < 10)
							{
								cumulus.LogWarningMessage($"LocalApi.GetSdFileContents: File {fileName} header line is malformed");
								cumulus.LogMessage("Header line = " + line);
								// try again?
								if (retries >= 0)
								{
									cumulus.LogMessage("LocalApi.GetSdFileContents: Try and fetch the file again");
								}
								break;
							}

							useTimeStamp = fields[1].Equals("timestamp", StringComparison.CurrentCultureIgnoreCase);

							// always add the header line
							result.Add(line);

							cumulus.LogDebugMessage("LocalApi.GetSdFileContents: Processed file header OK");

							// skip to first data line
							continue;
						}

						if (fields.Length < 10)
						{
							cumulus.LogWarningMessage($"LocalApi.GetSdFileContents: File {fileName} line # {count} is malformed");
							cumulus.LogDataMessage($"line # {count} = " + line);
							continue;
						}

						if (useTimeStamp)
						{
							// timestamp is in the second field
							if (Utils.RoundDownUnixTimestamp(long.Parse(fields[1]), SdCardInterval).LocalFromUnixTime() >= startTime)
							{
								result.Add(line);
							}
						}
						else
						{
							if (DateTime.TryParseExact(fields[0], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) && dt >= startTime)
							{
								result.Add(line);
							}
						}

						//if (fields.Length < 10)
						//{
						//	cumulus.LogWarningMessage($"LocalApi.GetSdFileContents: File {fileName} line # {count} is malformed");
						//	cumulus.LogDataMessage($"line # {count} = " + line);
						//	continue;
						//}

					}

					if (!Program.service)
					{
						Cumulus.LogConsoleMessage("  Data extraction complete           ");
					}

					cumulus.LogDebugMessage($"LocalApi.GetSdFileContents: Extracted {result.Count} lines from {fileName}");

					if (result.Count > 0)
					{
						return result;
					}
				}
				catch (HttpRequestException ex)
				{
					if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						cumulus.LogErrorMessage("GetSdFileContents: Error - This Station does not support the HTTP API!");
						return null;
					}
					else
					{
						cumulus.LogExceptionMessage(ex, "GetSdFileContents: HTTP Error");
					}
				}
				catch (Exception ex)
				{
					if (token.IsCancellationRequested)
					{
						cumulus.LogDebugMessage("GetSdFileContents: Operation cancelled due to shutting down");
						return null;
					}
					cumulus.LogExceptionMessage(ex, "GetSdFileContents: Error");
				}

				retries--;
				Thread.Sleep(250);
			} while (retries >= 0);

			return null;
		}

		public async Task<List<string>> GetSdFileList(DateTime startTime, CancellationToken token)
		{
			// Get the full list of files on the SD card

			cumulus.LogDebugMessage("LocalApi.GetSdFileList: Getting SD card info");

			var sdCard = await GetSdCardInfo(token);
			if (sdCard == null)
			{
				cumulus.LogErrorMessage("LocalApi.GetSdFileList: Error - Unable to get SD card info");
				return null;
			}

			// Get the list of files
			if (sdCard.file_list == null)
			{
				cumulus.LogErrorMessage("LocalApi.GetSdFileList: Error - No files found on SD card");
				return null;
			}

			// Filter the list of files to those that are within the requested time frame
			var files = new List<string>();
			var startMonth = new DateTime(startTime.Year, startTime.Month, 1, 0, 0, 0, DateTimeKind.Local);
			files.AddRange(from file in sdCard.file_list
						   where file.name.EndsWith(".csv")
						   let fileDate = DateTime.ParseExact(file.name[..6], "yyyyMM", CultureInfo.InvariantCulture)
						   where fileDate >= startMonth
						   select file.name);
			cumulus.LogDebugMessage($"LocalApi.GetSdFileList: Found {files.Count} files matching start time {startTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");

			return files;
		}

		public async Task<SensorInfo[]> GetSensorInfo(CancellationToken token)
		{
			// http://ip-address/get_sensors_info?page=1..4
			// pages 3 and 4 are optional and may return 404

			if (!Utils.ValidateIPv4(cumulus.Gw1000IpAddress))
			{
				cumulus.LogErrorMessage("GetSensorInfo: Invalid station IP address: " + cumulus.Gw1000IpAddress);
				return null;
			}

			var sensors = new List<SensorInfo>();
			var lastData = string.Empty;

			for (var page = 1; page <= 4; page++)
			{
				try
				{
					var url = $"http://{cumulus.Gw1000IpAddress}/get_sensors_info?page={page}";
					var result = await cumulus.MyHttpClient.GetStringAsync(url, token);
					cumulus.LogDataMessage($"GetSensorInfo: Page {page} = " + Utils.RemoveCrTabsFromString(result));

					if (!string.IsNullOrEmpty(result))
					{
						if (result == lastData)
						{
							cumulus.LogDebugMessage($"GetSensorInfo: Page {page} the same as previous. Aborting downloads.");
							break;
						}

						lastData = result;

						var pageSensors = JsonSerializer.Deserialize<SensorInfo[]>(result, jsonOptions);
						if (pageSensors != null && pageSensors.Length > 0)
						{
							sensors.AddRange(pageSensors);
						}
					}
				}
				catch (HttpRequestException ex)
				{
					// Pages 3 and 4 may return 404 - ignore those and continue.
					if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						cumulus.LogDebugMessage($"GetSensorInfo: Page {page} not found (404). Skipping.");
						// If page 1 is missing, treat as station not supporting API
						if (page == 1)
						{
							cumulus.LogErrorMessage("GetSensorInfo: Error - This Station does not support the HTTP API!");
							return null;
						}
						break;
					}

					// Other HTTP errors are logged and abort
					cumulus.LogExceptionMessage(ex, "GetSensorInfo: HTTP Error");
					return null;
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "GetSensorInfo: Error");
					return null;
				}
			}

			if (sensors.Count == 0)
			{
				cumulus.LogMessage("GetSensorInfo: No sensors returned");
				return null;
			}

			return sensors.ToArray();
		}

		public static void GetUnits(CancellationToken token)
		{
			// http://ip-address/get_units_info

			// response
			//{
			//	"temperature": "0",      0=C 1=F
			//	"pressure": "0",         0=hPa 1=inHg 2=mmHg
			//	"wind": "2",             0=ms 1=km/h 2=mph 3=knots
			//	"rain": "0",             0=mm 1=in
			//	"light": "1"             0=kLux=? 1=W/m2 2=kfc
			//}
		}

		public async Task<string> GetVersion(CancellationToken token)
		{
			// http://ip-address/get_version

			// response
			//	{
			//		"version":	"Version: GW1100A_V2.3.4",
			//		"newVersion":	"0",
			//		"platform":	"ecowitt"
			//	}}

			string responseBody;
			int responseCode;
			var unknown = "unknown";

			try
			{
				var url = $"http://{cumulus.Gw1000IpAddress}/get_version";

				using (var response = await cumulus.MyHttpClient.GetAsync(url, token))
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"LocalApi.GetVersion: Ecowitt Local API GetVersion Response code: {responseCode}");
					cumulus.LogDataMessage($"LocalApi.GetVersion: Ecowitt Local API GetVersion Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					cumulus.LogWarningMessage($"LocalApi.GetVersion: Ecowitt Local API GetVersion Error: {responseCode}");
					Cumulus.LogConsoleMessage($" - Error {responseCode}", ConsoleColor.Red);
					return unknown;
				}


				if (responseBody == "{}")
				{
					cumulus.LogMessage("LocalApi.GetVersion: Ecowitt Local API GetVersion: No data was returned.");
					Cumulus.LogConsoleMessage(" - No Live data available");
					return unknown;
				}
				else if (responseBody.StartsWith('{')) // sanity check
				{
					// Convert JSON string to an object
					var ver = JsonSerializer.Deserialize<VersionInfo>(responseBody, jsonOptions).version.Split(':')[1].Trim().Split('_')[1];
					cumulus.LogMessage("Station firmware version is " + ver);
					return ver;
				}
			}
			catch (HttpRequestException ex)
			{
				if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
				{
					cumulus.LogErrorMessage("GetVersion: Error - This Station does not support the HTTP API!");
				}
				else
				{
					cumulus.LogExceptionMessage(ex, "GetVersion: HTTP Error");
				}
			}

			return unknown;
		}

		public async Task<WeatherServices> GetWeatherServiceSettings(bool mainStation, CancellationToken token)
		{
			// http://ip-address/get_ws_settings
			// response
			//{
			//  "platform": "ecowitt",
			//  "ost_interval": "1",
			//  "sta_mac": "E8:DB:84:0F:15:43",
			//  "wu_interval": "16",
			//  "wu_id": "",
			//  "wu_key": "",
			//  "wcl_interval": "10",
			//  "wcl_id": "",
			//  "wcl_key": "",
			//  "wow_interval": "5",
			//  "wow_id": "",
			//  "wow_key": "",
			//  "Customized": "disable",
			//  "Protocol": "ecowitt",
			//  "ecowitt_ip": "",
			//  "ecowitt_path": "/data/report/",
			//  "ecowitt_port": "80",
			//  "ecowitt_upload": "60",
			//  "usr_wu_path": "/weatherstation/updateweatherstation.php?",
			//  "usr_wu_id": "",
			//  "usr_wu_key": "",
			//  "usr_wu_port": "80",
			//  "usr_wu_upload": "60",
			//  "mqtt_name": "",
			//  "mqtt_host": "",
			//  "mqtt_transport": "0",
			//  "mqtt_port": "0",
			//  "mqtt_topic": "ecowitt/3C8A1FB32BAF",
			//  "mqtt_clientid": "",
			//  "mqtt_username": "",
			//  "mqtt_password": "",
			//  "mqtt_keepalive": "0",
			//  "mqtt_interval": "0"
			//}

			string responseBody;
			int responseCode;
			var retries = 1;
			var ip = mainStation ? cumulus.Gw1000IpAddress : cumulus.EcowittExtraGatewayAddr;

			if (!Utils.ValidateIPv4(ip))
			{
				cumulus.LogErrorMessage("LocalApi.GetWeatherServiceSettings: Invalid station IP address: " + ip);
				return null;
			}


			do
			{
				try
				{
					var url = $"http://{ip}/get_ws_settings";

					using (var response = await cumulus.MyHttpClient.GetAsync(url, token))
					{
						responseBody = await response.Content.ReadAsStringAsync(token);
						responseCode = (int) response.StatusCode;
						cumulus.LogDebugMessage($"LocalApi.GetWeatherServiceSettings: Ecowitt Local API Response code: {responseCode}");
						cumulus.LogDataMessage($"LocalApi.GetWeatherServiceSettings: Ecowitt Local API Response: {Utils.RemoveCrTabsFromString(responseBody)}");
					}

					if (responseCode != 200)
					{
						cumulus.LogWarningMessage($"LocalApi.GetWeatherServiceSettings: Ecowitt Local API Error: {responseCode}");
						return null;
					}


					if (responseBody == "{}")
					{
						cumulus.LogMessage("LocalApi.GetWeatherServiceSettings: Ecowitt Local API: No data was returned.");
						return null;
					}
					else if (responseBody.StartsWith('{')) // sanity check
					{
						// Convert JSON string to an object
						var json = JsonSerializer.Deserialize<WeatherServices>(responseBody, jsonOptions);
						return json;
					}
				}
				catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException se && se.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused)
				{
					// Handle "connection refused"
					cumulus.LogMessage("LocalApi.GetWeatherServiceSettings: This Station does not support the HTTP API!");
					return null;
				}
				catch (HttpRequestException ex)
				{
					if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						cumulus.LogMessage("LocalApi.GetWeatherServiceSettings: This Station does not support the HTTP API!");
						return null;
					}
					else
					{
						cumulus.LogExceptionMessage(ex, "LocalApi.GetWeatherServiceSettings: HTTP Error");
					}
				}
				catch (Exception ex)
				{
					if (token.IsCancellationRequested)
					{
						cumulus.LogDebugMessage("LocalApi.GetWeatherServiceSettings: Operation cancelled due to shutting down");
						return null;
					}

					cumulus.LogExceptionMessage(ex, "LocalApi.GetWeatherServiceSettings: Error");
				}

				if (retries > 0)
				{
					Thread.Sleep(500);
				}

			} while (retries-- > 0);

			return null;
		}

		#endregion

		#region Set Methods

		public static void SetCalibarationData(CancellationToken token)
		{
			// http://ip-address/set_calibration_data
			// POST
			//{
			//	"solarRadGain": "1.00",		Irradiance gain
			//	"uvGain": "1.00",
			//	"windGain": "1.00",
			//	"inTempOffset": "0.0",
			//	"inHumOffset": "0.0",
			//	"absOffset": "0.3",
			//	"altitude": "72",
			//	"outTempOffset": "0.0",
			//	"outHumOffset": "0.0",
			//	"windDirsOffset": "0"
			//}
		}

		public static void SetDeviceInfo(CancellationToken token)
		{
			// http://ip-address/set_device_info

			// POST

		}

		public static void SetLogin(string password)
		{
			// http://ip-address/set_login_info

			// POST
			//{
			//	"pwd":""
			//}

			// Response
			//{
			//	"status":	"1",
			//	"online":	"0",
			//	"msg":	"success"
			//}
		}

		public static void SetNetworkInfo(CancellationToken token)
		{
			// http://ip-address/set_network_info
			// POST
			//{
			//	ehtIpType: "0",
			//	ethGateway: "10.10.10.100",
			//	ethIP: "10.10.10.106"
			//	ethMask: "255.255.255.0"
			//}
			//or
			//{
			//	ssid: "AX88U",
			//	wifi_pwd: "string"
			//}
		}

		public static void SetRainTotals(CancellationToken token)
		{
			// http://ip-address/set_rain_totals

			// POST
			//{
			//	"rainDay": "0.0",
			//	"rainWeek": "5.3",
			//	"rainMonth": "6.8",
			//	"rainYear": "572.5",
			//	"rainGain": "1.01",
			//	"rainFallPriority": "1",
			//	"rstRainDay": "0",
			//	"rstRainWeek": "1",
			//	"rstRainYear": "0"
			//}

			// response = 200 - OK
		}

		public static void SetUnits(CancellationToken token)
		{
			// http://ip-address/set_units_info

			// POST
			//{temperature: "1", pressure: "0", wind: "2", rain: "0", light: "1"}

			// response = 200 - OK
		}

		public async Task<bool> SetWeatherServiceSettings(WeatherServices config, bool mainStation, CancellationToken token)
		{
			// http://ip-address/set_ws_settings
			// POST
			// {
			//  "platform": "ecowitt",
			//  "ost_interval": "1",
			//  "sta_mac": "E8:DB:84:0F:15:43",
			//  "wu_interval": "16",
			//  "wu_id": "",
			//  "wu_key": "",
			//  "wcl_interval": "10",
			//  "wcl_id": "",
			//  "wcl_key": "",
			//  "wow_interval": "5",
			//  "wow_id": "",
			//  "wow_key": "",
			//  "Customized": "disable",
			//  "Protocol": "ecowitt",
			//  "ecowitt_ip": "",
			//  "ecowitt_path": "/data/report/",
			//  "ecowitt_port": "80",
			//  "ecowitt_upload": "60",
			//  "usr_wu_path": "/weatherstation/updateweatherstation.php?",
			//  "usr_wu_id": "",
			//  "usr_wu_key": "",
			//  "usr_wu_port": "80",
			//  "usr_wu_upload": "60",
			//  "mqtt_name": "",
			//  "mqtt_host": "",
			//  "mqtt_transport": "0",
			//  "mqtt_port": "0",
			//  "mqtt_topic": "ecowitt/3C8A1FB32BAF",
			//  "mqtt_clientid": "",
			//  "mqtt_username": "",
			//  "mqtt_password": "",
			//  "mqtt_keepalive": "0",
			//  "mqtt_interval": "0"
			//}

			string responseBody;
			int responseCode;
			var ip = mainStation ? cumulus.Gw1000IpAddress : cumulus.EcowittExtraGatewayAddr;

			try
			{
				var url = $"http://{ip}/set_ws_settings";

				var data = new StringContent(JsonSerializer.Serialize(config), Encoding.UTF8, "application/json");
				using (var response = await cumulus.MyHttpClient.PostAsync(url, data, token))
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"LocalApi.SetWeatherServiceSettings: Ecowitt Local API Response code: {responseCode}");
					cumulus.LogDataMessage($"LocalApi.SetWeatherServiceSettings: Ecowitt Local API Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					cumulus.LogWarningMessage($"LocalApi.SetWeatherServiceSettings: Ecowitt Local API Error: {responseCode}");
					return false;
				}

				return true;
			}
			catch (HttpRequestException ex)
			{
				if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
				{
					cumulus.LogErrorMessage("SetWeatherServiceSettings: Error - This Station does not support the HTTP API!");
				}
				else
				{
					cumulus.LogExceptionMessage(ex, "SetWeatherServiceSettings: HTTP Error");
				}
			}
			catch (Exception ex)
			{
				if (token.IsCancellationRequested)
				{
					cumulus.LogDebugMessage("SetWeatherServiceSettings: Operation cancelled due to shutting down");
					return false;
				}
				cumulus.LogExceptionMessage(ex, "SetWeatherServiceSettings: Error");
			}

			return false;
		}

		#endregion

		#region Other Methods
		public async Task<bool> CheckForUpgrade(CancellationToken token)
		{
			// http://ip-address/upgrade_process

			// POST
			// {"upgrade": "check"}

			// response
			//{
			//	"is_new": false,
			//	"msg": "It's the latest version\r\nCurrent version:V2.3.4\r\n- Optimize RF reception performance.\r\n- Fix the issue of incorrect voltage upload for wh34/wh35/wh68 batteries."
			//}

			string responseBody;
			int responseCode;

			try
			{
				var url = $"http://{cumulus.Gw1000IpAddress}/upgrade_process";

				var data = new StringContent("{\"upgrade\": \"check\"}", Encoding.UTF8, "application/json");
				using (var response = await cumulus.MyHttpClient.PostAsync(url, data, token))
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"LocalApi.CheckForUpgrade: Ecowitt Local API GetVersion Response code: {responseCode}");
					cumulus.LogDataMessage($"LocalApi.CheckForUpgrade: Ecowitt Local API GetVersion Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					cumulus.LogWarningMessage($"LocalApi.CheckForUpgrade: Ecowitt Local API GetVersion Error: {responseCode}");
					Cumulus.LogConsoleMessage($" - Error {responseCode}", ConsoleColor.Red);
					return false;
				}


				if (responseBody == "{}")
				{
					cumulus.LogMessage("LocalApi.CheckForUpgrade: Ecowitt Local API GetVersion: No data was returned.");
					Cumulus.LogConsoleMessage(" - No Live data available");
					return false;
				}
				else if (responseBody.StartsWith('{')) // sanity check
				{
					// Convert JSON string to an object
					var result = JsonSerializer.Deserialize<CheckUpgrade>(responseBody, jsonOptions);
					if (result.is_new)
					{
						cumulus.LogWarningMessage("Station firmware is out of date");
						cumulus.LogMessage("New firmware: " + result.msg);
						cumulus.FirmwareAlarm.LastMessage = "New firmware version = " + result.msg;
						cumulus.FirmwareAlarm.Triggered = true;
					}
					else
					{
						cumulus.LogMessage("Station firmware is up to date");
						cumulus.FirmwareAlarm.Triggered = false;
					}

					return result.is_new;
				}
			}
			catch (HttpRequestException ex)
			{
				if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
				{
					cumulus.LogErrorMessage("CheckForUpgrade: Error - This Station does not support the HTTP API!");
				}
				else
				{
					cumulus.LogExceptionMessage(ex, "CheckForUpgrade: HTTP Error");
				}
			}
			catch (Exception ex)
			{
				if (token.IsCancellationRequested)
				{
					cumulus.LogDebugMessage("CheckForUpgrade: Operation cancelled due to shutting down");
					return false;
				}
				cumulus.LogExceptionMessage(ex, "CheckForUpgrade: Error");
			}

			return false;
		}

		public static void StartUpgrade(CancellationToken token)
		{
			// http://ip-address/upgrade_process

			// POST
			// {"upgrade": "start"}

			// response
			// object
			// status: N   1=running
			// 'over'

		}

		public static void Login(string password, CancellationToken token)
		{
			// http://ip-address/set_login_info

			// POST
			//{pwd: "base64_string"}
		}

		public static void Reboot(CancellationToken token)
		{
			// http://ip-address/set_device_info

			// POST
			// { sysreboot: 1 }

		}

		#endregion

		private static string decodePassword(string base64EncodedData)
		{
			var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
			return Encoding.UTF8.GetString(base64EncodedBytes);
		}

		private static string encodePassword(string plainText)
		{
			var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
			return Convert.ToBase64String(plainTextBytes);
		}


		public void Dispose()
		{
			try
			{
			}
			catch
			{
				// do nothing, the boy does nothing!
			}
		}

		public class CommonSensor
		{
			public string id { get; set; }
			public string val { get; set; }
			public string? unit { get; set; }
			public double? battery { get; set; }
			public double? voltage { get; set; }
			public decimal? ws90cap_volt { get; set; }

			public int? valInt
			{
				get
				{
					if (val.EndsWith('%'))
						val = val[0..^1];
					return int.TryParse(val, out var result) ? result : null;
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
					return double.TryParse(val, invNum, out var result) ? result : null;
				}
			}
		}

		public class TempHumSensor
		{
			public int channel { get; set; }
			public int? battery { get; set; }
			public double? temp { get; set; }
			public string? humidity { get; set; }
			public string? unit { get; set; }

			public int? humidityVal
			{
				get
				{
					return int.TryParse(humidity[0..^1], out var result) ? result : null;
				}
			}
		}

		public class SoilMoistEcSensor : TempHumSensor
		{
			public string? ec { get; set; }

			public int?ecVal
			{
				get
				{
					return int.TryParse(ec.Split(' ')[0], out var result) ? result : null;
				}
			}
		}

		public class Wh25Sensor
		{
			public double intemp { get; set; }
			public string unit { get; set; }
			public string inhumi { get; set; }
			public string abs { get; set; }
			public string rel { get; set; }
			public int? CO2 { get; set; }
			public int? CO2_24H { get; set; }
			public int? battery { get; set; }

			public int? inhumiInt
			{
				get
				{
					return int.TryParse(inhumi[0..^1], out var result) ? result : null;
				}
			}
		}

		public class LightningSensor
		{
			public string distance { get; set; }
			public string timestamp { get; set; }
			public int? count { get; set; }
			public int? battery { get; set; }

			public double? distanceVal
			{
				get
				{
					var temp = distance.Split(' ');
					return double.TryParse(temp[0], invNum, out var result) ? result : null;
				}
			}

			public string distanceUnit
			{
				get
				{
					return distance.Split(' ')[1];
				}
			}
		}

		public class Co2Sensor
		{
			public double? temp { get; set; }
			public string unit { get; set; }
			public string humidity { get; set; }
			public double? PM25 { get; set; }
			public double? PM25_24H { get; set; }
			//public double? PM25_RealAQI { get; set; }
			//public double? PM25_24HAQI { get; set; }
			public double? PM10 { get; set; }
			public double? PM10_24H { get; set; }
			//public double? PM10_RealAQI { get; set; }
			//public double? PM10_24HAQI { get; set; }
			public double? PM1 { get; set; }
			public double? PM1_24H { get; set; }
			//public double? PM1_RealAQI { get; set; }
			//public double? PM1_24HAQI { get; set; }
			public double? PM4 { get; set; }
			public double? PM4_24H { get; set; }
			//public double? PM4_RealAQI { get; set; }
			//public double? PM4_24HAQI { get; set; }
			public int? CO2 { get; set; }
			public int? CO2_24H { get; set; }
			public int? battery { get; set; }

			public int? humidityVal
			{
				get
				{
					return int.TryParse(humidity[0..^1], out var result) ? result : null;
				}
			}
		}

		public class ChPm25Sensor
		{
			public int? channel { get; set; }
			public double? PM25 { get; set; }
			public double? PM25_24H { get; set; }
			//public double? PM25_RealAQI { get; set; }
			//public double? PM25_24HAQI { get; set; }
			public int? battery { get; set; }
		}

		public class ChLeakSensor
		{
			public int? channel { get; set; }
			public string name { get; set; }
			public int? battery { get; set; }
			public string status { get; set; }
		}

		public class LdsSensor
		{
			public int channel { get; set; }
			public string name { get; set; }
			public string unit { get; set; }
			public int battery { get; set; }
			//public decimal? voltage { get; set; }
			public string air { get; set; }
			public string depth { get; set; }
			//public decimal? total_height { get; set; }
			//public int total_heat { get; set; }

			public decimal? airVal
			{
				get
				{
					var temp = air.Split(' ');
					return decimal.TryParse(temp[0], invNum, out var result) ? result : null;
				}
			}

			public decimal? depthVal
			{
				get
				{
					var temp = depth.Split(' ');
					return decimal.TryParse(temp[0], invNum, out var result) ? result : null;
				}
			}
		}

		public class LiveData
		{
			public CommonSensor[] common_list { get; set; }
			public CommonSensor[]? rain { get; set; }
			public CommonSensor[]? piezoRain { get; set; }
			public Wh25Sensor[]? wh25 { get; set; }
			public LightningSensor[]? lightning { get; set; }
			public Co2Sensor[]? co2 { get; set; }
			public ChPm25Sensor[]? ch_pm25 { get; set; }
			public ChLeakSensor[]? ch_leak { get; set; }
			public TempHumSensor[]? ch_aisle { get; set; }
			public TempHumSensor[]? ch_soil { get; set; }
			public TempHumSensor[]? ch_temp { get; set; }
			public TempHumSensor[]? ch_leaf { get; set; }
			public LdsSensor[]? ch_lds { get; set; }
			public SoilMoistEcSensor[] ch_ec { get; set; }
		}

		public class SensorInfo
		{
			public string img { get; set; }
			public int type { get; set; }
			public string name { get; set; }
			public string id { get; set; }
			public int batt { get; set; }
			public int? signal { get; set; }

			public int? rssi { get; set; }
			public bool idst { get; set; }
		}

		public class VersionInfo
		{
			public string version { get; set; }
			public string newVersion { get; set; }
			public string platform { get; set; }
		}

		public class SdCardInfo
		{
			public string Name { get; set; }
			public string Type { get; set; }
			public string Speed { get; set; }
			public string Size { get; set; }
			public int Interval { get; set; }
		}

		public class SdCardfile
		{
			public string name { get; set; }
			public string type { get; set; }
			public string size { get; set; }
		}

		public class SdCard
		{
			public SdCardInfo info { get; set; }

			public SdCardfile[] file_list { get; set; }
		}

		public class Calibration
		{
			public double uvGain { get; set; }
		}

		private sealed class CheckUpgrade
		{
			public bool is_new { get; set; }
			public string msg { get; set; }
		}

		public class DeviceInfo
		{
			//	"sensorType":	"1",
			//	"rf_freq":	"1",
			//	"ntp_server": "pool.ntp.org",
			//	"AFC":	"0",
			//	"tz_auto":	"1",
			//	"tz_name":	"",
			//	"tz_index":	"39",
			//	"dst_stat":	"1",
			//	"radcompensation":	"0",
			//	"date":	"2024-09-06T16:36",
			//	"upgrade":	"0",
			//	"apAuto":	"1",
			//	"newVersion":	"0",
			//	"curr_msg":	"Current version:V2.3.4\r\n- Optimize RF reception performance.\r\n- Fix the issue of incorrect voltage upload for wh34/wh35/wh68 batteries.",
			//	"apName":	"GW1100A-WIFID4D3",
			//	"APpwd":	"base64-string",
			//	"time":	"20"
			public int sensorType { get; set; }  // 0=WH24, 1=anything else
			public int rf_freq { get; set; }    // 0=, 1=868, 2=
			public bool AFC { get; set; }
			public string ntp_server { get; set; }
			public bool tz_auto { get; set; }
			public string tz_name { get; set; }
			// public int tz_index { get; set; }
			public bool dst_stat { get; set; }
			public bool radcompensation { get; set; }
			public string date { get; set; }
			public bool upgrade { get; set; }
			public bool apAuto { get; set; }
			public bool ap_auto { get; set; }
			public string newVersion { get; set; }
			public string curr_msg { get; set; }
			public string apName { get; set; }
			public string APpwd { get; set; }
			public int time { get; set; }
		}

		public class WeatherServices
		{
			//{
			//  "platform": "ecowitt",
			//  "ost_interval": "1",
			//  "sta_mac": "E8:DB:84:0F:15:43",
			//  "wu_interval": "16",
			//  "wu_id": "",
			//  "wu_key": "",
			//  "wcl_interval": "10",
			//  "wcl_id": "",
			//  "wcl_key": "",
			//  "wow_interval": "5",
			//  "wow_id": "",
			//  "wow_key": "",
			//  "Customized": "disable",
			//  "Protocol": "ecowitt",
			//  "ecowitt_ip": "",
			//  "ecowitt_path": "/data/report/",
			//  "ecowitt_port": "80",
			//  "ecowitt_upload": "60",
			//  "usr_wu_path": "/weatherstation/updateweatherstation.php?",
			//  "usr_wu_id": "",
			//  "usr_wu_key": "",
			//  "usr_wu_port": "80",
			//  "usr_wu_upload": "60",
			//  "mqtt_name": "",
			//  "mqtt_host": "",
			//  "mqtt_transport": "0",
			//  "mqtt_port": "0",
			//  "mqtt_topic": "ecowitt/3C8A1FB32BAF",
			//  "mqtt_clientid": "",
			//  "mqtt_username": "",
			//  "mqtt_password": "",
			//  "mqtt_keepalive": "0",
			//  "mqtt_interval": "0"

			public string platform { get; set; }
			public string ost_interval { get; set; }
			public string sta_mac { get; set; }
			public string wu_interval { get; set; }
			public string wu_id { get; set; }
			public string wu_key { get; set; }
			public string wcl_interval { get; set; }
			public string wcl_id { get; set; }
			public string wcl_key { get; set; }
			public string wow_interval { get; set; }
			public string wow_id { get; set; }
			public string wow_key { get; set; }

			public string Customized { get; set; }
			public string Protocol { get; set; }
			public string ecowitt_ip { get; set; }
			public string ecowitt_path { get; set; }
			public string ecowitt_port { get; set; }
			public string ecowitt_upload { get; set; }
			public string usr_wu_path { get; set; }
			public string usr_wu_id { get; set; }
			public string usr_wu_key { get; set; }
			public string usr_wu_port { get; set; }
			public string usr_wu_upload { get; set; }
			public string mqtt_name { get; set; }
			public string mqtt_host { get; set; }
			public string mqtt_transport { get; set; }
			public string mqtt_port { get; set; }
			public string mqtt_topic { get; set; }
			public string mqtt_clientid { get; set; }
			public string mqtt_username { get; set; }
			public string mqtt_password { get; set; }
			public string mqtt_keepalive { get; set; }
			public string mqtt_interval { get; set; }
		}
	}
}
