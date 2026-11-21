using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadNeverin : WebUploadServiceBase
	{
		public new decimal Interval;
		public new bool[] SendSoilTemp = new bool[8];
		public new bool[] SendSoilMoisture = new bool[8];
		public bool[] SendLeafWetness = new bool[8];
		public bool[] SendExtraTemp = new bool[8];
		public bool[] SendExtraHum = new bool[8];
		public bool SendLightning;

		private static string url = "http://ws.neverin.hr/v2/";


		internal WebUploadNeverin(Cumulus cumulus, string name) : base(cumulus, name)
		{
			IntTimer.Elapsed += TimerTick;
		}

		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage("Neverin: Not uploading, " + reason);
				return;
			}

			if (string.IsNullOrEmpty(PW))
			{
				cumulus.LogWarningMessage("Neverin: Not uploading, no valid sender key");
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000), Program.ExitSystemToken);

			string apistring;
			string data = GetURL(out apistring, timestamp);
			string logData = data.Replace(apistring, "<API_KEY>");

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					cumulus.LogDebugMessage("Neverin: Sending Data = " + logData);

					var content = new StringContent(data, Encoding.UTF8, "application/json");

					using var response = await cumulus.MyHttpClient.PostAsync(url, content, Program.ExitSystemToken);
					var responseBodyAsText = await response.Content.ReadAsStringAsync(Program.ExitSystemToken);
					var msg = "Unknown";

					cumulus.LogDebugMessage("Neverin: Response Code = " + response.StatusCode);
					cumulus.LogDataMessage("Neverin: Response Body = " + responseBodyAsText);

					if (response.StatusCode == HttpStatusCode.OK)
					{
						try
						{
							var stored = false;
							var resp = JsonSerializer.Deserialize<GoodResponse>(responseBodyAsText);

							if (resp != null)
							{
								stored = resp.data.stored;
							}


							if (resp.data.stored)
							{
								cumulus.LogDebugMessage("Neverin: Data stored OK");
								cumulus.LogDataMessage("Neverin: Stored fields = " + string.Join(",", resp.data.fields) + " - Extra fields = " + string.Join(",", resp.data.extra));
							}
							else
							{

							}
						}
						catch
						{
							cumulus.LogMessage($"Neverin: Failed to decode the success response - '{responseBodyAsText}'");
						}

						cumulus.ThirdPartyAlarm.Triggered = false;
						Updating = false;
						return;
					}
					else
					{
						try
						{
							var resp = JsonSerializer.Deserialize<ErrorResponse>(responseBodyAsText);

							if (resp != null)
							{
								msg = resp.error.message;
							}
						}
						catch
						{
							cumulus.LogMessage($"Neverin: Failed to decode the error response - '{responseBodyAsText}'");
						}

						if (response.StatusCode == HttpStatusCode.Unauthorized)
						{
							cumulus.LogWarningMessage("Neverin: Bad Sender Code used - " + msg);
							cumulus.ThirdPartyAlarm.LastMessage = "Neverin: Bad Sender Code used - " + msg;
							cumulus.ThirdPartyAlarm.Triggered = true;
						}
						else if (response.StatusCode == HttpStatusCode.Forbidden)
						{
							cumulus.LogWarningMessage("Neverin: Station Invalid - " + msg);
							cumulus.ThirdPartyAlarm.LastMessage = "Neverin: Station Invalid - " + msg;
							cumulus.ThirdPartyAlarm.Triggered = true;
						}
						else if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
						{
							cumulus.LogWarningMessage("Neverin: Service unavailable");
							cumulus.ThirdPartyAlarm.LastMessage = "Neverin: Service unavailable";
							cumulus.ThirdPartyAlarm.Triggered = true;
						}
						else
						{
							cumulus.ThirdPartyAlarm.LastMessage = "Neverin: unexpected error - " + response.StatusCode;
							cumulus.ThirdPartyAlarm.Triggered = true;
						}

						Updating = false;
						return;
					}
				}
				catch (Exception ex)
				{
					string msg;

					if (retryCount == 0)
					{
						if (ex.InnerException is TimeoutException)
						{
							msg = $"Neverin: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "Neverin: " + ex.Message;
							cumulus.LogExceptionMessage(ex, "Windy: Error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"Neverin: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("Neverin: Error - " + ex.Message);
						}

						cumulus.LogMessage($"Neverin: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount), Program.ExitSystemToken);
					}
				}
			}

			Updating = false;
		}


		// Documentation on the API can be found here...
		// http://ws.neverin.hr/v2/<senderKey>?datetime=<#year>-<#month>-<#day> <#timehhmmss>&temp=<#temp rc=y unit=C>&rh=<#hum>&baro_rel=<#press rc=y unit=hPa>&wspeed=<#wlatest rc=y unit=ms>&wavg=<#wspeed rc=y unit=ms>&wgust=<#wgust rc=y unit=ms>&wdir=<#bearing>&precip_year=<#ryear rc=y unit=mm>
		//
		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			pwstring = PW;

			string dateStr = timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture);
			StringBuilder body = new StringBuilder(1024);

			body.Append($"{{\"key\":\"{PW}\"");

			body.Append($",\"time\":\"{dateStr}\"");

			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				body.Append($",\"temp\":{WeatherStation.TempCstr(station.OutdoorTemperature)}");

			if (station.OutdoorHumidity >= 0)
				body.Append($",\"rh\":{station.OutdoorHumidity}");

			if (station.Pressure > 0)
				body.Append($",\"baro_rel\":{WeatherStation.PresshPastr(station.Pressure)}");

			if (station.StationPressure > 0)
				body.Append($",\"baro_abs\":{ WeatherStation.PresshPastr(station.StationPressure)}");

			if (station.WindLatest >= 0)
				body.Append($",\"wspeed\":{station.WindMSStr(station.WindLatest)}");

			if (station.WindAverage >= 0)
				body.Append($",\"wavg\":{station.WindMSStr(station.WindAverage)}");

			if (station.RecentMaxGust >= 0)
				body.Append($",\"wgust\":{station.WindMSStr(station.RecentMaxGust)}");

			body.Append($",\"winddir\":{station.AvgBearing}");

			body.Append($",\"precip_total\":{WeatherStation.RainMMstr(station.RainAccumulator.RainCounter)}");

			if (SendUV && station.UV.HasValue)
				body.Append($",\"uv\":{station.UV.Value.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture)}");

			if (SendSolar && station.SolarRad.HasValue)
				body.Append($",\"solar\":{station.SolarRad}");

			if (SendAirQuality)
			{
				body.Append(GetAirQualityStr());
			}

			if (SendLightning)
			{
				body.Append($",\"lightning_count\":{station.LightningStrikesToday}");
				if (station.LightningDistance >= 0)
					body.Append($",\"lightning_dist\":{ConvertUnits.UserWindToKPH(station.LightningDistance)}");
			}

			for (var i = 0; i < SendSoilTemp.Length; i++)
			{
				if (SendSoilTemp[i] && station.SoilTemp[i + 1].HasValue)
					body.Append($",\"soil_temp_{i + 1}\":{WeatherStation.TempCstr(station.SoilTemp[i + 1].Value)}");
			}

			for (var i = 0; i < SendSoilMoisture.Length; i++)
			{
				if (SendSoilMoisture[i] && station.SoilMoisture[i + 1].HasValue) 
				{
					if (cumulus.Units.SoilMoistureUnitText[i + 1] == "%")
					{
						body.Append($",\"soil_vwc_{i + 1}\":{station.SoilMoisture[i + 1]}");
					}
					else
					{
						body.Append($",\"soil_moist_{i + 1}\":{station.SoilMoisture[i + 1]}");
					}
				}
			}

			for (var i = 0; i < SendExtraTemp.Length; i++)
			{
				if (SendExtraTemp[i] && station.ExtraTemp[i + 1].HasValue)
					body.Append($",\"temp_ch{i + 1}\":{WeatherStation.TempCstr(station.ExtraTemp[i + 1].Value)}");
			}

			for (var i = 0; i < SendExtraHum.Length; i++)
			{
				if (SendExtraHum[i] && station.ExtraHum[i + 1].HasValue)
					body.Append($",\"rh_ch{i + 1}\":{station.ExtraHum[i + 1].Value}");
			}

			for (var i = 0; i < SendLeafWetness.Length; i++)
			{
				if (SendLeafWetness[i] && station.LeafWetness[i + 1].HasValue)
					body.Append($",\"leaf_wet_{i + 1}\":{station.LeafWetness[i + 1]:cumulus.LeafWetFormat}");
			}

			body.Append($",\"software\":\"CumulusMX+v{cumulus.Version}\"}}");

			return body.ToString();
		}

		private string GetAirQualityStr()
		{
			var sb = new StringBuilder();
			var InvC = new CultureInfo("");

			switch (cumulus.StationOptions.PrimaryAqSensor)
			{
				case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
					if (cumulus.airLinkDataOut != null)
					{
						sb.Append($",\"pm1\":{cumulus.airLinkDataOut.pm1.ToString("F1", InvC)}");
						sb.Append($",\"pm2p5\":{cumulus.airLinkDataOut.pm2p5.ToString("F1", InvC)}");
						sb.Append($",\"pm10\":{cumulus.airLinkDataOut.pm10.ToString("F1", InvC)}");
					}
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor1:
				case (int) Cumulus.PrimaryAqSensor.Sensor2:
				case (int) Cumulus.PrimaryAqSensor.Sensor3:
				case (int) Cumulus.PrimaryAqSensor.Sensor4:
					{
						int idx = cumulus.StationOptions.PrimaryAqSensor;
						if (station.AirQuality[idx].HasValue)
							sb.Append($",\"pm2p5\":{station.AirQuality[idx].Value.ToString("F1", InvC)}");
						if (station.AirQuality10[idx].HasValue)
							sb.Append($",\"pm10\":{station.AirQuality10[idx].Value.ToString("F1", InvC)}");
						break;
					}
				case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
					if (station.CO2_pm2p5.HasValue)
						sb.Append($",\"pm2p5\":{station.CO2_pm2p5.Value.ToString("F1", InvC)}");
					if (station.CO2_pm10.HasValue)
						sb.Append($",\"pm10\":{station.CO2_pm10.Value.ToString("F1", InvC)}");
					break;
			}

			return sb.ToString();
		}

		private void TimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(PW))
			{
				_ = DoUpdate(DateTime.Now);
			}
		}

		private class GoodResponse
		{
			public GoodResponseMeta meta { get; set; }
			public GoodResponseData data { get; set; }
		}

		private class GoodResponseMeta
		{
			public string api_version { get; set; }
		}

		private class GoodResponseData
		{
			public int station_id { get; set; }
			public bool stored { get; set; }
			public string[] fields { get; set; }
			public string[] extra { get; set; }
			public int request_id { get; set; }
			public 
		}

		private class ErrorResponse
		{
			public ErrorResponseError error { get; set; }
			public int statusCode { get; set; }
		}

		private class ErrorResponseError
		{
			public string code { get; set; }
			public string message { get; set; }
		}

		private class ErrorResponseMeta
		{
			public string request_id { get; set; }
		}

	}
}
