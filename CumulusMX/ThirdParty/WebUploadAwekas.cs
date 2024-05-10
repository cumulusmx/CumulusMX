using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using ServiceStack.Text;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadAwekas : WebUploadServiceBase
	{
		public bool RateLimited;
		public int OriginalInterval;
		public string Lang;
		public bool SendLeafWetness;


		public WebUploadAwekas(Cumulus cumulus, string name) : base(cumulus, name)
		{
			IntTimer.Elapsed += TimerTick;
		}


		internal override Task DoCatchUp()
		{
			// do nothing - no cath-up for AWEKAS
			return Task.CompletedTask;
		}


		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			string pwstring;
			string url = GetURL(out pwstring, timestamp);

			string starredpwstring = "<password>";

			string logUrl = url.Replace(pwstring, starredpwstring);

			cumulus.LogDebugMessage("AWEKAS: URL = " + logUrl);

			try
			{
				using var response = await cumulus.MyHttpClient.GetAsync(url);
				var responseBodyAsText = await response.Content.ReadAsStringAsync();
				cumulus.LogDebugMessage("AWEKAS Response code = " + response.StatusCode);
				cumulus.LogDataMessage("AWEKAS: Response text = " + responseBodyAsText);

				if (response.StatusCode != HttpStatusCode.OK)
				{
					cumulus.LogWarningMessage($"AWEKAS: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
					cumulus.ThirdPartyAlarm.LastMessage = $"AWEKAS: HTTP Response code = {response.StatusCode}, body = {responseBodyAsText}";
					cumulus.ThirdPartyAlarm.Triggered = true;
				}
				else
				{
					if (string.IsNullOrEmpty(responseBodyAsText))
					{
						cumulus.ThirdPartyAlarm.Triggered = false;
						return;
					}
				}

				AwekasResponse respJson;

				try
				{
					respJson = JsonSerializer.DeserializeFromString<AwekasResponse>(responseBodyAsText);
				}
				catch (Exception ex)
				{
					cumulus.LogWarningMessage("AWEKAS: Exception deserializing response = " + ex.Message);
					cumulus.LogWarningMessage($"AWEKAS: ERROR - Response body = {responseBodyAsText}");
					cumulus.ThirdPartyAlarm.LastMessage = "AWEKAS deserializing response: " + ex.Message;
					cumulus.ThirdPartyAlarm.Triggered = true;
					Updating = false;
					return;
				}

				// Check the status response
				if (respJson.status == 2)
				{
					cumulus.LogDebugMessage("AWEKAS: Data stored OK");
				}
				else if (respJson.status == 1)
				{
					cumulus.LogMessage("AWEKAS: Data PARIALLY stored");
					// TODO: Check errors and disabled
				}
				else if (respJson.status == 0)  // Authentication error or rate limited
				{
					if (respJson.minuploadtime > 0 && respJson.authentication == 0)
					{
						cumulus.LogWarningMessage("AWEKAS: Authentication error");
						if (Interval < 300)
						{
							RateLimited = true;
							OriginalInterval = Interval;
							Interval = 300;
							Enabled = true;
							SynchronisedUpdate = true;
							cumulus.LogMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 300 seconds due to authentication error");
						}
					}
					else if (respJson.minuploadtime == 0)
					{
						cumulus.LogWarningMessage("AWEKAS: Too many requests, rate limited");
						// AWEKAS PLus allows minimum of 60 second updates, try that first
						if (!RateLimited && Interval < 60)
						{
							OriginalInterval = Interval;
							RateLimited = true;
							Interval = 60;
							SynchronisedUpdate = true;
							cumulus.LogWarningMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 60 seconds due to rate limit");
						}
						// AWEKAS normal allows minimum of 300 second updates, revert to that
						else
						{
							RateLimited = true;
							Interval = 300;
							IntTimer.Interval = Interval * 1000;
							SynchronisedUpdate = true;
							cumulus.LogWarningMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 300 seconds due to rate limit");
						}
					}
					else
					{
						cumulus.LogWarningMessage("AWEKAS: Unknown error");
						cumulus.ThirdPartyAlarm.LastMessage = "AWEKAS: Unknown error";
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
				}

				// check the min upload time is greater than our upload time
				if (respJson.status > 0 && respJson.minuploadtime > OriginalInterval)
				{
					cumulus.LogWarningMessage($"AWEKAS: The minimum upload time to AWEKAS for your station is {respJson.minuploadtime} sec, Cumulus is configured for {OriginalInterval} sec, increasing Cumulus interval to match AWEKAS");
					Interval = respJson.minuploadtime;
					cumulus.WriteIniFile();
					IntTimer.Interval = Interval * 1000;
					SynchronisedUpdate = Interval % 60 == 0;
					IntTimer.Enabled = !SynchronisedUpdate;
					// we got a successful upload, and reset the interval, so clear the rate limited values
					OriginalInterval = Interval;
					RateLimited = false;
				}
				else if (RateLimited && respJson.status > 0)
				{
					// We are currently rate limited, it could have been a transient thing because
					// we just got a valid response, and our interval is >= the minimum allowed.
					// So we just undo the limit, and resume as before
					cumulus.LogMessage($"AWEKAS: Removing temporary increase in upload interval to 60 secs, resuming uploads every {OriginalInterval} secs");
					Interval = OriginalInterval;
					IntTimer.Interval = Interval * 1000;
					SynchronisedUpdate = Interval % 60 == 0;
					IntTimer.Enabled = !SynchronisedUpdate;
					RateLimited = false;
				}
			}
			catch (Exception ex)
			{
				string msg;

				if (ex.InnerException is TimeoutException)
				{
					msg = $"AWEKAS: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
					cumulus.LogWarningMessage(msg);
				}
				else
				{
					msg = "AWEKAS: " + ex.Message;
					cumulus.LogExceptionMessage(ex, "AWEKAS: Error");
				}
				cumulus.ThirdPartyAlarm.LastMessage = msg;
				cumulus.ThirdPartyAlarm.Triggered = true;
			}
			finally
			{
				Updating = false;
			}
		}


		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			var InvC = new CultureInfo("");
			string sep = ";";

			int presstrend;

			// password is passed as a MD5 hash - not very secure, but better than plain text I guess
			pwstring = Utils.GetMd5String(PW);

			double threeHourlyPressureChangeMb = 0;

			switch (cumulus.Units.Press)
			{
				case 0:
				case 1:
					threeHourlyPressureChangeMb = station.presstrendval * 3;
					break;
				case 2:
					threeHourlyPressureChangeMb = station.presstrendval * 3 / 0.0295333727;
					break;
			}

			if (threeHourlyPressureChangeMb > 6) presstrend = 2;
			else if (threeHourlyPressureChangeMb > 3.5) presstrend = 2;
			else if (threeHourlyPressureChangeMb > 1.5) presstrend = 1;
			else if (threeHourlyPressureChangeMb > 0.1) presstrend = 1;
			else if (threeHourlyPressureChangeMb > -0.1) presstrend = 0;
			else if (threeHourlyPressureChangeMb > -1.5) presstrend = -1;
			else if (threeHourlyPressureChangeMb > -3.5) presstrend = -1;
			else if (threeHourlyPressureChangeMb > -6) presstrend = -2;
			else
				presstrend = -2;

			double AvgTemp;
			if (station.tempsamplestoday > 0)
				AvgTemp = station.TempTotalToday / station.tempsamplestoday;
			else
				AvgTemp = 0;

			StringBuilder sb = new StringBuilder("http://data.awekas.at/eingabe_pruefung.php?");

			var started = false;

			// indoor temp/humidity
			if (SendIndoor)
			{
				if (station.IndoorTemperature > Cumulus.DefaultHiVal)
					sb.Append("indoortemp=" + ConvertUnits.UserTempToC(station.IndoorTemperature).ToString("F1", InvC));

				if (station.IndoorHumidity >= 0)
					sb.Append("&indoorhumidity=" + station.IndoorHumidity);
				started = true;
			}

			if (SendSoilTemp)
			{
				if (started) sb.Append('&'); else started = true;
				for (var i = 1; i <= 4; i++)
				{
					if (station.SoilTemp[i] > Cumulus.DefaultHiVal)
						sb.Append($"soiltemp{i}={ConvertUnits.UserTempToC(station.SoilTemp[i]).ToString("F1", InvC)}");
				}
			}

			if (SendSoilMoisture)
			{
				if (started) sb.Append('&'); else started = true;
				sb.Append("soilmoisture1=" + station.SoilMoisture1);
				sb.Append("&soilmoisture2=" + station.SoilMoisture2);
				sb.Append("&soilmoisture3=" + station.SoilMoisture3);
				sb.Append("&soilmoisture4=" + station.SoilMoisture4);
			}

			if (SendLeafWetness)
			{
				if (started) sb.Append('&'); else started = true;
				sb.Append("leafwetness1=" + station.LeafWetness1.ToString(cumulus.LeafWetFormat));
				sb.Append("&leafwetness2=" + station.LeafWetness2.ToString(cumulus.LeafWetFormat));
				sb.Append("&leafwetness3=" + station.LeafWetness3.ToString(cumulus.LeafWetFormat));
				sb.Append("&leafwetness4=" + station.LeafWetness4.ToString(cumulus.LeafWetFormat));
			}

			if (SendAirQuality)
			{
				if (started) sb.Append('&'); else started = true;

				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							sb.Append($"AqPM1={cumulus.airLinkDataOut.pm1.ToString("F1", InvC)}");
							sb.Append($"&AqPM2.5={cumulus.airLinkDataOut.pm2p5.ToString("F1", InvC)}");
							sb.Append($"&AqPM10={cumulus.airLinkDataOut.pm10.ToString("F1", InvC)}");
							sb.Append($"&AqPM2.5_avg_24h={cumulus.airLinkDataOut.pm2p5_24hr.ToString("F1", InvC)}");
							sb.Append($"&AqPM10_avg_24h={cumulus.airLinkDataOut.pm10_24hr.ToString("F1", InvC)}");
						}
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt1:
						if (station.AirQuality1 > Cumulus.DefaultHiVal)
							sb.Append($"AqPM2.5={station.AirQuality1.ToString("F1", InvC)}");
						if (station.AirQualityAvg1 > Cumulus.DefaultHiVal)
							sb.Append($"&AqPM2.5_avg_24h={station.AirQualityAvg1.ToString("F1", InvC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
						if (station.AirQualityAvg2 > Cumulus.DefaultHiVal)
							sb.Append($"AqPM2.5={station.AirQuality2.ToString("F1", InvC)}");
						if (station.AirQualityAvg2 > Cumulus.DefaultHiVal)
							sb.Append($"&AqPM2.5_avg_24h={station.AirQualityAvg2.ToString("F1", InvC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
						if (station.AirQualityAvg3 > Cumulus.DefaultHiVal)
							sb.Append($"AqPM2.5={station.AirQuality3.ToString("F1", InvC)}");
						if (station.AirQualityAvg3 > Cumulus.DefaultHiVal)
							sb.Append($"&AqPM2.5_avg_24h={station.AirQualityAvg3.ToString("F1", InvC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
						if (station.AirQualityAvg4 > Cumulus.DefaultHiVal)
							sb.Append($"AqPM2.5={station.AirQuality4.ToString("F1", InvC)}");
						if (station.AirQualityAvg4 > Cumulus.DefaultHiVal)
							sb.Append($"&AqPM2.5_avg_24h={station.AirQualityAvg4.ToString("F1", InvC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
						if (station.CO2_pm2p5 >= Cumulus.DefaultHiVal)
							sb.Append($"AqPM2.5={station.CO2_pm2p5.ToString("F1", InvC)}");
						if (station.CO2_pm2p5_24h >= Cumulus.DefaultHiVal)
							sb.Append($"&AqPM2.5_avg_24h={station.CO2_pm2p5_24h.ToString("F1", InvC)}");
						if (station.CO2_pm10 >= Cumulus.DefaultHiVal)
							sb.Append($"&AqPM10={station.CO2_pm10.ToString("F1", InvC)}");
						if (station.CO2_pm10_24h >= Cumulus.DefaultHiVal)
							sb.Append($"&AqPM10_avg_24h={station.CO2_pm10_24h.ToString("F1", InvC)}");
						break;
				}
			}

			if (started) sb.Append('&');
			sb.Append("output=json&val=");

			//
			// Start of val
			//
			sb.Append(ID + sep);                                                                              // 1
			sb.Append(pwstring + sep);                                                                        // 2
			sb.Append(timestamp.ToString("dd'.'MM'.'yyyy';'HH':'mm") + sep);                                  // 3 + 4
			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				sb.Append(ConvertUnits.UserTempToC(station.OutdoorTemperature).ToString("F1", InvC) + sep);   // 5
			else
				sb.Append(sep);
			if (station.OutdoorHumidity >= 0)
				sb.Append(station.OutdoorHumidity + sep);                                                     // 6
			else
				sb.Append(sep);
			if (station.Pressure > 0)
				sb.Append(ConvertUnits.UserPressToMB(station.Pressure).ToString("F1", InvC) + sep);           // 7
			else
				sb.Append(sep);
			sb.Append(ConvertUnits.UserRainToMM(station.RainSinceMidnight).ToString("F1", InvC) + sep);       // 8   - was RainToday in v2
			if (station.WindAverage >= 0)
				sb.Append(ConvertUnits.UserWindToKPH(station.WindAverage).ToString("F1", InvC) + sep);        // 9
			else
				sb.Append(sep);
			sb.Append(station.AvgBearing + sep);                                                              // 10
			sb.Append(sep + sep + sep);                                                                       // 11/12/13 - condition and warning, snow height
			sb.Append(Lang + sep);                                                                            // 14
			sb.Append(presstrend + sep);                                                                      // 15
			if (station.RecentMaxGust >= 0)
				sb.Append(ConvertUnits.UserWindToKPH(station.RecentMaxGust).ToString("F1", InvC) + sep);      // 16
			else
				sb.Append(sep);
			if (SendSolar && station.SolarRad >= 0)
				sb.Append(station.SolarRad.ToString("F1", InvC) + sep);                                       // 17
			else
				sb.Append(sep);

			if (SendUV && station.UV >= 0)
				sb.Append(station.UV.ToString("F1", InvC) + sep);                                             // 18
			else
				sb.Append(sep);

			if (SendSolar)
			{
				if (cumulus.StationType == StationTypes.FineOffsetSolar)
					sb.Append(station.LightValue.ToString("F0", InvC) + sep);                                 // 19
				else
					sb.Append(sep);

				sb.Append(station.SunshineHours.ToString("F2", InvC) + sep);                                  // 20
			}
			else
			{
				sb.Append(sep + sep);
			}

			if (SendSoilTemp && station.SoilTemp[1] > Cumulus.DefaultHiVal)
				sb.Append(ConvertUnits.UserTempToC(station.SoilTemp[1]).ToString("F1", InvC) + sep);    // 21
			else
				sb.Append(sep);

			if (station.RainRate >= 0)
				sb.Append(ConvertUnits.UserRainToMM(station.RainRate).ToString("F1", InvC) + sep);      // 22
			else
				sb.Append(sep);
			sb.Append("Cum_" + cumulus.Version + sep);                                                          // 23
			sb.Append(sep + sep);                                                                               // 24/25 location for mobile
			if (station.HiLoToday.LowTemp < Cumulus.DefaultLoVal)
				sb.Append(ConvertUnits.UserTempToC(station.HiLoToday.LowTemp).ToString("F1", InvC));  // 26
			sb.Append(sep);

			sb.Append(ConvertUnits.UserTempToC(AvgTemp).ToString("F1", InvC) + sep);                    // 27
			if (station.HiLoToday.HighTemp > Cumulus.DefaultHiVal)
				sb.Append(ConvertUnits.UserTempToC(station.HiLoToday.HighTemp).ToString("F1", InvC)); // 28
			sb.Append(sep);

			sb.Append(ConvertUnits.UserTempToC(station.ThisMonth.LowTemp.Val).ToString("F1", InvC) + sep);  // 29
			sb.Append(sep);                                                                                         // 30 avg temp this month
			sb.Append(ConvertUnits.UserTempToC(station.ThisMonth.HighTemp.Val).ToString("F1", InvC) + sep); // 31
			sb.Append(ConvertUnits.UserTempToC(station.ThisYear.LowTemp.Val).ToString("F1", InvC) + sep);   // 32
			sb.Append(sep);                                                                                         // 33 avg temp this year
			sb.Append(ConvertUnits.UserTempToC(station.ThisYear.HighTemp.Val).ToString("F1", InvC) + sep);  // 34
			if (station.HiLoToday.LowHumidity < Cumulus.DefaultLoVal)
				sb.Append(station.HiLoToday.LowHumidity);                                                       // 35
			sb.Append(sep);

			sb.Append(sep);                                                                                     // 36 avg hum today
			if (station.HiLoToday.HighHumidity >= Cumulus.DefaultHiVal)
				sb.Append(station.HiLoToday.HighHumidity);                                                  // 37
			sb.Append(sep);

			sb.Append(station.ThisMonth.LowHumidity.Val + sep);                                                 // 38
			sb.Append(sep);                                                                                     // 39 avg hum this month
			sb.Append(station.ThisMonth.HighHumidity.Val + sep);                                                // 40
			sb.Append(station.ThisYear.LowHumidity.Val + sep);                                                  // 41
			sb.Append(sep);                                                                                     // 42 avg hum this year
			sb.Append(station.ThisYear.HighHumidity.Val + sep);                                                 // 43
			if (station.HiLoToday.LowPress < Cumulus.DefaultLoVal)
				sb.Append(ConvertUnits.UserPressToMB(station.HiLoToday.LowPress).ToString("F1", InvC));     // 44
			sb.Append(sep);

			sb.Append(sep);                                                                                             // 45 avg press today
			if (station.HiLoToday.HighPress >= Cumulus.DefaultHiVal)
				sb.Append(ConvertUnits.UserPressToMB(station.HiLoToday.HighPress).ToString("F1", InvC));        // 46
			sb.Append(sep);

			sb.Append(ConvertUnits.UserPressToMB(station.ThisMonth.LowPress.Val).ToString("F1", InvC) + sep);   // 47
			sb.Append(sep);                                                                                             // 48 avg press this month
			sb.Append(ConvertUnits.UserPressToMB(station.ThisMonth.HighPress.Val).ToString("F1", InvC) + sep);  // 49
			sb.Append(ConvertUnits.UserPressToMB(station.ThisYear.LowPress.Val).ToString("F1", InvC) + sep);    // 50
			sb.Append(sep);                                                                                             // 51 avg press this year
			sb.Append(ConvertUnits.UserPressToMB(station.ThisYear.HighPress.Val).ToString("F1", InvC) + sep);   // 52
			sb.Append(sep + sep);                                                                               // 53/54 min/avg wind today
			if (station.HiLoToday.HighWind >= Cumulus.DefaultHiVal)
				sb.Append(ConvertUnits.UserWindToKPH(station.HiLoToday.HighWind).ToString("F1", InvC));     // 55
			sb.Append(sep);

			sb.Append(sep + sep);                                                                               // 56/57 min/avg wind this month
			sb.Append(ConvertUnits.UserWindToKPH(station.ThisMonth.HighWind.Val).ToString("F1", InvC) + sep); // 58
			sb.Append(sep + sep);                                                                               // 59/60 min/avg wind this year
			sb.Append(ConvertUnits.UserWindToKPH(station.ThisYear.HighWind.Val).ToString("F1", InvC) + sep);    // 61
			sb.Append(sep + sep);                                                                               // 62/63 min/avg gust today
			if (station.HiLoToday.HighGust >= 0)
				sb.Append(ConvertUnits.UserWindToKPH(station.HiLoToday.HighGust).ToString("F1", InvC) + sep); // 64
			else
				sb.Append(sep);
			sb.Append(sep + sep);                                                           // 65/66 min/avg gust this month
			sb.Append(ConvertUnits.UserWindToKPH(station.ThisMonth.HighGust.Val).ToString("F1", InvC) + sep); // 67
			sb.Append(sep + sep);                                                           // 68/69 min/avg gust this year
			sb.Append(ConvertUnits.UserWindToKPH(station.ThisYear.HighGust.Val).ToString("F1", InvC) + sep); // 70
			sb.Append(sep + sep + sep);                                                     // 71/72/73 avg wind bearing today/month/year
			sb.Append(ConvertUnits.UserRainToMM(station.RainLast24Hour).ToString("F1", InvC) + sep);      // 74
			sb.Append(ConvertUnits.UserRainToMM(station.RainMonth).ToString("F1", InvC) + sep);           // 75
			sb.Append(ConvertUnits.UserRainToMM(station.RainYear).ToString("F1", InvC) + sep);            // 76
			sb.Append(sep);                                                                 // 77 avg rain rate today
			if (station.HiLoToday.HighRainRate >= 0)
				sb.Append(ConvertUnits.UserRainToMM(station.HiLoToday.HighRainRate).ToString("F1", InvC) + sep); // 78
			else
				sb.Append(sep);
			sb.Append(sep);                                                                 // 79 avg rain rate this month
			sb.Append(ConvertUnits.UserRainToMM(station.ThisMonth.HighRainRate.Val).ToString("F1", InvC) + sep); // 80
			sb.Append(sep);                                                                 // 81 avg rain rate this year
			sb.Append(ConvertUnits.UserRainToMM(station.ThisYear.HighRainRate.Val).ToString("F1", InvC) + sep); // 82
			sb.Append(sep);                                                                 // 83 avg solar today
			if (SendSolar && station.HiLoToday.HighSolar >= 0)
				sb.Append(station.HiLoToday.HighSolar);                               // 84
			else
				sb.Append(sep);

			sb.Append(sep + sep);                                                           // 85/86 avg/high solar this month
			sb.Append(sep + sep);                                                           // 87/88 avg/high solar this year
			sb.Append(sep);                                                                 // 89 avg uv today

			if (SendUV && station.HiLoToday.HighUv >= 0)
				sb.Append(station.HiLoToday.HighUv.ToString("F1", InvC));             // 90
			else
				sb.Append(sep);

			sb.Append(sep + sep);                                                           // 91/92 avg/high uv this month
			sb.Append(sep + sep);                                                           // 93/94 avg/high uv this year
			sb.Append(sep + sep + sep + sep + sep + sep);                                   // 95/96/97/98/99/100 avg/max lux today/month/year
			sb.Append(sep + sep);                                                           // 101/102 sun hours this month/year
			sb.Append(sep + sep + sep + sep + sep + sep + sep + sep + sep);                 // 103-111 min/avg/max Soil temp today/month/year
																							//
																							// End of val fixed structure
																							//

			return sb.ToString();
		}


		private void TimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(ID))
				_ = DoUpdate(DateTime.Now);
		}


#pragma warning disable S3459 // Unassigned members should be removed
#pragma warning disable S1144 // Unused private types or members should be removed
		private sealed class AwekasResponse
		{
			public int status { get; set; }
			public int authentication { get; set; }
			public int minuploadtime { get; set; }
			public AwekasErrors error { get; set; }
			public AwekasDisabled disabled { get; set; }
		}

		private sealed class AwekasErrors
		{
			public int count { get; set; }
			public int time { get; set; }
			public int date { get; set; }
			public int temp { get; set; }
			public int hum { get; set; }
			public int airp { get; set; }
			public int rain { get; set; }
			public int rainrate { get; set; }
			public int wind { get; set; }
			public int gust { get; set; }
			public int snow { get; set; }
			public int solar { get; set; }
			public int uv { get; set; }
			public int brightness { get; set; }
			public int suntime { get; set; }
			public int indoortemp { get; set; }
			public int indoorhumidity { get; set; }
			public int soilmoisture1 { get; set; }
			public int soilmoisture2 { get; set; }
			public int soilmoisture3 { get; set; }
			public int soilmoisture4 { get; set; }
			public int soiltemp1 { get; set; }
			public int soiltemp2 { get; set; }
			public int soiltemp3 { get; set; }
			public int soiltemp4 { get; set; }
			public int leafwetness1 { get; set; }
			public int leafwetness2 { get; set; }
			public int warning { get; set; }
		}

		private sealed class AwekasDisabled
		{
			public int temp { get; set; }
			public int hum { get; set; }
			public int airp { get; set; }
			public int rain { get; set; }
			public int rainrate { get; set; }
			public int wind { get; set; }
			public int snow { get; set; }
			public int solar { get; set; }
			public int uv { get; set; }
			public int indoortemp { get; set; }
			public int indoorhumidity { get; set; }
			public int soilmoisture1 { get; set; }
			public int soilmoisture2 { get; set; }
			public int soilmoisture3 { get; set; }
			public int soilmoisture4 { get; set; }
			public int soiltemp1 { get; set; }
			public int soiltemp2 { get; set; }
			public int soiltemp3 { get; set; }
			public int soiltemp4 { get; set; }
			public int leafwetness1 { get; set; }
			public int leafwetness2 { get; set; }
			public int report { get; set; }
		}
#pragma warning restore S3459 // Unassigned members should be removed
#pragma warning restore S1144 // Unused private types or members should be removed
	}
}
