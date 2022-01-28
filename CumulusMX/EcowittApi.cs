using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Text;

namespace CumulusMX
{
	internal class EcowittApi
	{
		private Cumulus cumulus;
		private readonly NumberFormatInfo invNum = CultureInfo.InvariantCulture.NumberFormat;

		private static readonly HttpClientHandler httpHandler = new HttpClientHandler();
		private readonly HttpClient httpClient = new HttpClient(httpHandler);

		private static string historyUrl = "https://api.ecowitt.net/api/v3/device/history?";

		public EcowittApi(Cumulus cuml)
		{
			cumulus = cuml;

			// Configure a web proxy if required
			if (!string.IsNullOrEmpty(cumulus.HTTPProxyName))
			{
				httpHandler.Proxy = new WebProxy(cumulus.HTTPProxyName, cumulus.HTTPProxyPort);
				httpHandler.UseProxy = true;
				if (!string.IsNullOrEmpty(cumulus.HTTPProxyUser))
				{
					httpHandler.Credentials = new NetworkCredential(cumulus.HTTPProxyUser, cumulus.HTTPProxyPassword);
				}
			}
		}


		internal bool GetHistoricData(DateTime startTime, DateTime endTime, string[] callbacks, out EcowittHistoricData data)
		{
			// Let's decode the Unix ts to DateTime
			JsConfig.DateHandler = DateHandler.UnixTime;

			data = new EcowittHistoricData();

			cumulus.LogMessage("API.GetHistoricData: Get Ecowitt Historic Data");

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				return false;
			}

			var sb = new StringBuilder(historyUrl);

			sb.Append($"application_key={cumulus.EcowittApplicationKey}");
			sb.Append($"&api_key={cumulus.EcowittUserApiKey}");
			sb.Append($"&mac={cumulus.EcowittMacAddress}&");
			sb.Append($"&start_date={startTime.ToString("yyyy-MM-dd'%20'HH:mm:ss")}");
			sb.Append($"&end_date={endTime.ToString("yyyy-MM-dd'%20'HH:mm:ss")}");

			// available callbacks:
			//	outdoor, indoor, solar_and_uvi, rainfall, wind, pressure, lightning
			//	indoor_co2, co2_aqi_combo, pm25_aqi_combo, pm10_aqi_combo, temp_and_humidity_aqi_combo
			//	pm25_ch[1-4]
			//	temp_and_humidity_ch[1-8]
			//	soil_ch[1-8]
			//	temp_ch[1-8]
			//	leaf_ch[1-8]
			//	batt
			sb.Append("&call_back=");
			foreach (var cb in callbacks)
				sb.Append(cb + ",");
			sb.Length--;

			//TODO: match time to logging interval
			// available times 5min, 30min, 4hour, 1day
			sb.Append($"&cycle_type=5min");

			var url = sb.ToString();

			var logUrl = url.Replace(cumulus.EcowittApplicationKey, "<<App-Key>>").Replace(cumulus.EcowittUserApiKey, "<<API-key>>");
			cumulus.LogDebugMessage($"Ecowitt URL = {logUrl}");

			try
			{
				string responseBody;
				int responseCode;

				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = httpClient.GetAsync(url).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int)response.StatusCode;
					cumulus.LogDebugMessage($"API.GetHistoricData: Ecowitt API Historic Response code: {responseCode}");
					cumulus.LogDataMessage($"API.GetHistoricData: Ecowitt API Historic Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var historyError = responseBody.FromJson<EcowitHistErrorResp>();
					cumulus.LogMessage($"API.GetHistoricData: Ecowitt API Historic Error: {historyError.code}, {historyError.msg}");
					cumulus.LogConsoleMessage($" - Error {historyError.code}: {historyError.msg}", ConsoleColor.Red);
					cumulus.LastUpdateTime = endTime;
					return false;
				}


				if (responseBody == "{}")
				{
					cumulus.LogMessage("API.GetHistoricData: Ecowitt API Historic: No data was returned.");
					cumulus.LogConsoleMessage(" - No historic data available");
					cumulus.LastUpdateTime = endTime;
					return false;
				}
				else if (responseBody.StartsWith("{\"code\":")) // sanity check
				{
					// get the sensor data
					var histObj = responseBody.FromJson<EcowittHistoricResp>();
					data = histObj.data;
					return true;
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogMessage("API.GetHistoricData: Invalid historic message received");
					cumulus.LogDataMessage("API.GetHistoricData: Received: " + responseBody);
					cumulus.LastUpdateTime = endTime;
					return false;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("API.GetHistoricData: Exception:");
				cumulus.LastUpdateTime = endTime;
				return false;
			}

		}


		private string ErrorCode(int code)
		{
			switch (code)
			{
				case -1: return "System is busy";
				case 0: return "Success!";
				case 40000: return "Illegal parameter";
				case 40010: return "Illegal Application_Key Parameter";
				case 40011: return "Illegal Api_Key Parameter";
				case 40012: return "Illegal MAC/IMEI Parameter";
				case 40013: return "Illegal start_date Parameter";
				case 40014: return "Illegal end_date Parameter";
				case 40015: return "Illegal cycle_type Parameter";
				case 40016: return "Illegal call_back Parameter";
				case 40017: return "Missing Application_Key Parameter";
				case 40018: return "Missing Api_Key Parameter";
				case 40019: return "Missing MAC Parameter";
				case 40020: return "Missing start_date Parameter";
				case 40021: return "Missing end_date Parameter";
				case 40022: return "Illegal Voucher type";
				case 43001: return "Needs other service support";
				case 44001: return "Media file or data packet is null";
				case 45001: return "Over the limit or other error";
				case 46001: return "No existing request";
				case 47001: return "Parse JSON/XML contents error";
				case 48001: return "Privilege Problem";
				default: return "Unknown error code";
			}
		}

		private class EcowitHistErrorResp
		{
			public int code { get; set; }
			public string msg { get; set; }
			public DateTime time { get; set; }
			public object data { get; set; }
		}

		internal class EcowittHistoricResp
		{
			public int code { get; set; }
			public string msg { get; set; }
			public DateTime time { get; set; }
			public EcowittHistoricData data { get; set; }
		}

		internal class EcowittHistoricData
		{
			public EcowittHistoricTempHum indoor { get; set; }
			public EcowittHistoricDataPressure pressure { get; set; }
			public EcowittHistoricOutdoor outdoor { get; set; }
			public EcowittHistoricDataWind wind { get; set; }
			public EcowittHistoricDataSolar solar_and_uvi { get; set; }
			public EcowittHistoricDataRainfall rainfall { get; set; }
			public EcowittHistoricTempHum temp_and_humidity_ch1 { get; set; }
			public EcowittHistoricTempHum temp_and_humidity_ch2 { get; set; }
			public EcowittHistoricTempHum temp_and_humidity_ch3 { get; set; }
			public EcowittHistoricTempHum temp_and_humidity_ch4 { get; set; }
			public EcowittHistoricTempHum temp_and_humidity_ch5 { get; set; }
			public EcowittHistoricTempHum temp_and_humidity_ch6 { get; set; }
			public EcowittHistoricTempHum temp_and_humidity_ch7 { get; set; }
			public EcowittHistoricTempHum temp_and_humidity_ch8 { get; set; }
			public EcowittHistoricDataSoil soil_ch1 { get; set; }
			public EcowittHistoricDataSoil soil_ch2 { get; set; }
			public EcowittHistoricDataSoil soil_ch3 { get; set; }
			public EcowittHistoricDataSoil soil_ch4 { get; set; }
			public EcowittHistoricDataSoil soil_ch5 { get; set; }
			public EcowittHistoricDataSoil soil_ch6 { get; set; }
			public EcowittHistoricDataSoil soil_ch7 { get; set; }
			public EcowittHistoricDataSoil soil_ch8 { get; set; }
			public EcowittHistoricDataTemp temp_ch1 { get; set; }
			public EcowittHistoricDataTemp temp_ch2 { get; set; }
			public EcowittHistoricDataTemp temp_ch3 { get; set; }
			public EcowittHistoricDataTemp temp_ch4 { get; set; }
			public EcowittHistoricDataTemp temp_ch5 { get; set; }
			public EcowittHistoricDataTemp temp_ch6 { get; set; }
			public EcowittHistoricDataTemp temp_ch7 { get; set; }
			public EcowittHistoricDataTemp temp_ch8 { get; set; }
			public EcowittHistoricDataLeaf leaf_ch1 { get; set; }
			public EcowittHistoricDataLeaf leaf_ch2 { get; set; }
			public EcowittHistoricDataLeaf leaf_ch3 { get; set; }
			public EcowittHistoricDataLeaf leaf_ch4 { get; set; }
			public EcowittHistoricDataLeaf leaf_ch5 { get; set; }
			public EcowittHistoricDataLeaf leaf_ch6 { get; set; }
			public EcowittHistoricDataLeaf leaf_ch7 { get; set; }
			public EcowittHistoricDataLeaf leaf_ch8 { get; set; }
			public EcowittHistoricDataLightning lightning { get; set; }
			public EcowittHistoricDataCo2 indoor_co2 { get; set; }

		}

		internal class EcowittHistoricDataTypeInt
		{
			public string unit { get; set; }
			public Dictionary<DateTime, int?> list { get; set; }
		}

		internal class EcowittHistoricDataTypeDbl
		{
			public string unit { get; set; }
			public Dictionary<DateTime, decimal?> list { get; set; }
		}

		internal class EcowittHistoricTempHum
		{
			public EcowittHistoricDataTypeDbl temperature { get; set; }
			public EcowittHistoricDataTypeInt humidity { get; set; }
		}

		internal class EcowittHistoricOutdoor : EcowittHistoricTempHum
		{
			public EcowittHistoricDataTypeDbl dew_point { get; set; }
		}

		internal class EcowittHistoricDataPressure
		{
			public EcowittHistoricDataTypeDbl relative { get; set; }
		}

		internal class EcowittHistoricDataWind
		{
			public EcowittHistoricDataTypeInt wind_direction { get; set; }
			public EcowittHistoricDataTypeDbl wind_speed { get; set; }
			public EcowittHistoricDataTypeDbl wind_gust { get; set; }
		}

		internal class EcowittHistoricDataSolar
		{
			public EcowittHistoricDataTypeDbl solar { get; set; }
			public EcowittHistoricDataTypeDbl uvi { get; set; }
		}

		internal class EcowittHistoricDataRainfall
		{
			public EcowittHistoricDataTypeDbl rain_rate { get; set; }
			public EcowittHistoricDataTypeDbl yearly { get; set; }
		}

		internal class EcowittHistoricDataSoil
		{
			public EcowittHistoricDataTypeInt soilmoisture { get; set; }
		}
		internal class EcowittHistoricDataTemp
		{
			public EcowittHistoricDataTypeDbl temperature { get; set; }
		}

		internal class EcowittHistoricDataLeaf
		{
			public EcowittHistoricDataTypeInt leaf_wetness { get; set; }
		}

		internal class EcowittHistoricDataLightning
		{
			public EcowittHistoricDataTypeDbl distance { get; set; }
			public EcowittHistoricDataTypeInt count	{ get; set; }	
		}

		[DataContract]
		internal class EcowittHistoricDataCo2
		{
			public EcowittHistoricDataTypeInt co2 { get; set; }
			[DataMember(Name= "24_hours_average")]
			public EcowittHistoricDataTypeInt average24h { get; set; }
		}

		internal class HistoricData
		{
			public decimal? IndoorTemp { get; set; }
			public int? IndoorHum { get; set; }
			public decimal? Temp { get; set; }
			public decimal? DewPoint { get; set; }
			public decimal? FeelsLike { get; set; }
			public int? Humidity { get; set; }
			public decimal? RainRate { get; set; }
			public decimal? RainYear { get; set; }
			public decimal? WindSpd { get; set; }
			public decimal? WindGust { get; set; }
			public int? WindDir { get; set; }
			public decimal? Pressure { get; set; }
			public int? Solar { get; set; }
			public decimal? UVI { get; set; }
			public decimal? LightningDist { get; set; }
			public int? LightningCount { get; set; }
			public decimal?[] ExtraTemp { get; set; }
			public int?[] ExtraHumidity { get; set; }
			public int?[] SoilMoist { get; set; }
			public decimal?[] UserTemp { get; set; }
			public int?[] LeafWetness { get; set; }
			public decimal? pm25 { get; set; }
			public decimal? AqiComboPm25 { get; set; }
			public decimal? AqiComboPm10 { get; set; }
			public decimal? AqiComboTemp { get; set; }
			public int? AqiComboHum { get; set; }
			public int? Co2 { get; set; }
			public int? Co2hr24 { get; set; }
			public int? IndoorCo2 { get; set; }
			public int? IndoorCo2hr24 { get; set; }

			public HistoricData()
			{
				ExtraTemp = new decimal?[8];
				ExtraHumidity = new int?[8];
				SoilMoist = new int?[8];
				UserTemp = new decimal?[8];
				LeafWetness = new int?[8];
			}
		}

	}
}
