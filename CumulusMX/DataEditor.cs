using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unosquare.Labs.EmbedIO;
using ServiceStack;

namespace CumulusMX
{
	internal class DataEditor
	{
		private readonly WeatherStation station;
		private readonly Cumulus cumulus;
		private readonly WebTags webtags;

		private readonly List<LastHourRainLog> hourRainLog = new List<LastHourRainLog>();

		internal DataEditor(Cumulus cumulus, WeatherStation station, WebTags webtags)
		{
			this.station = station;
			this.cumulus = cumulus;
			this.webtags = webtags;
		}

		//internal string EditRainToday(HttpListenerContext context)
		internal string EditRainToday(IHttpContext context)
		{
			var invC = new CultureInfo("");
			var request = context.Request;
			string text;
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			var kvPair = text.Split('=');
			var raintodaystring = kvPair[1];

			if (!string.IsNullOrEmpty(raintodaystring))
			{
				try
				{
					var raintoday = double.Parse(raintodaystring, CultureInfo.InvariantCulture);
					cumulus.LogMessage("Before rain today edit, raintoday=" + station.RainToday.ToString(cumulus.RainFormat) + " Raindaystart=" + station.raindaystart.ToString(cumulus.RainFormat));
					station.RainToday = raintoday;
					station.raindaystart = station.Raincounter - (station.RainToday / cumulus.RainMult);
					cumulus.LogMessage("After rain today edit,  raintoday=" + station.RainToday.ToString(cumulus.RainFormat) + " Raindaystart=" + station.raindaystart.ToString(cumulus.RainFormat));
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Edit rain today: " + ex.Message);
				}
			}

			var json = "{\"raintoday\":\"" + station.RainToday.ToString(cumulus.RainFormat, invC) +
				"\",\"raincounter\":\"" + station.Raincounter.ToString(cumulus.RainFormat, invC) +
				"\",\"startofdayrain\":\"" + station.raindaystart.ToString(cumulus.RainFormat, invC) +
				"\",\"rainmult\":\"" + cumulus.RainMult.ToString("F3", invC) + "\"}";

			return json;
		}

		internal string GetRainTodayEditData()
		{
			var invC = new CultureInfo("");
			var step = (cumulus.RainDPlaces == 1 ? "0.1" : "0.01");
			var json = "{\"raintoday\":\"" + station.RainToday.ToString(cumulus.RainFormat, invC) +
				"\",\"raincounter\":\"" + station.Raincounter.ToString(cumulus.RainFormat, invC) +
				"\",\"startofdayrain\":\"" + station.raindaystart.ToString(cumulus.RainFormat, invC) +
				"\",\"rainmult\":\"" + cumulus.RainMult.ToString("F3", invC) +
				"\",\"step\":\"" + step + "\"}";

			return json;
		}

		internal string EditDiary(IHttpContext context)
		{
			try
			{
				var request = context.Request;
				string text;

				using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
				{
					text = reader.ReadToEnd();
				}

				var newData = text.FromJson<DiaryData>();

				// write new/updated entry to the database
				var result = cumulus.DiaryDB.InsertOrReplace(newData);

				return "{\"result\":\"" + ((result == 1) ? "Success" : "Failed") + "\"}";

			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Edit Diary: " + ex.Message);
				return "{\"result\":\"Failed\"}";
			}
		}

		internal string DeleteDiary(IHttpContext context)
		{
			try
			{
				var request = context.Request;
				string text;

				using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
				{
					text = reader.ReadToEnd();
				}

				var record = text.FromJson<DiaryData>();

				// Delete the corresponding entry from the database
				var result = cumulus.DiaryDB.Delete(record);

				return "{\"result\":\"" + ((result == 1) ? "Success" : "Failed") + "\"}";

			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Delete Diary: " + ex.Message);
				return "{\"result\":\"Failed\"}";
			}
		}

		internal string GetAllTimeRecData()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";
			// Records - Temperature values
			var json = new StringBuilder("{", 1700);
			json.Append($"\"highTempVal\":\"{station.alltimerecarray[WeatherStation.AT_HighTemp].value.ToString(cumulus.TempFormat)}\","); ;
			json.Append($"\"lowTempVal\":\"{station.alltimerecarray[WeatherStation.AT_LowTemp].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.alltimerecarray[WeatherStation.AT_HighDewPoint].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.alltimerecarray[WeatherStation.AT_LowDewpoint].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.alltimerecarray[WeatherStation.AT_HighAppTemp].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.alltimerecarray[WeatherStation.AT_LowAppTemp].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.alltimerecarray[WeatherStation.AT_HighFeelsLike].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.alltimerecarray[WeatherStation.AT_LowFeelsLike].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.alltimerecarray[WeatherStation.AT_HighHumidex].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.alltimerecarray[WeatherStation.AT_LowChill].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.alltimerecarray[WeatherStation.AT_HighHeatIndex].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.alltimerecarray[WeatherStation.AT_HighMinTemp].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.alltimerecarray[WeatherStation.AT_LowMaxTemp].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.alltimerecarray[WeatherStation.AT_HighDailyTempRange].value.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.alltimerecarray[WeatherStation.AT_LowDailyTempRange].value.ToString(cumulus.TempFormat)}\",");
			// Records - Temperature timestamps
			json.Append($"\"highTempTime\":\"{station.alltimerecarray[WeatherStation.AT_HighTemp].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.alltimerecarray[WeatherStation.AT_LowTemp].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.alltimerecarray[WeatherStation.AT_HighDewPoint].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.alltimerecarray[WeatherStation.AT_LowDewpoint].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.alltimerecarray[WeatherStation.AT_HighAppTemp].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.alltimerecarray[WeatherStation.AT_LowAppTemp].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.alltimerecarray[WeatherStation.AT_HighFeelsLike].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.alltimerecarray[WeatherStation.AT_LowFeelsLike].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.alltimerecarray[WeatherStation.AT_HighHumidex].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.alltimerecarray[WeatherStation.AT_LowChill].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.alltimerecarray[WeatherStation.AT_HighHeatIndex].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.alltimerecarray[WeatherStation.AT_HighMinTemp].timestamp.ToString(dateStampFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.alltimerecarray[WeatherStation.AT_LowMaxTemp].timestamp.ToString(dateStampFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.alltimerecarray[WeatherStation.AT_HighDailyTempRange].timestamp.ToString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.alltimerecarray[WeatherStation.AT_LowDailyTempRange].timestamp.ToString(dateStampFormat)}\",");
			// Records - Humidity values
			json.Append($"\"highHumidityVal\":\"{station.alltimerecarray[WeatherStation.AT_HighHumidity].value.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.alltimerecarray[WeatherStation.AT_lowhumidity].value.ToString(cumulus.HumFormat)}\",");
			// Records - Humidity times
			json.Append($"\"highHumidityTime\":\"{station.alltimerecarray[WeatherStation.AT_HighHumidity].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.alltimerecarray[WeatherStation.AT_lowhumidity].timestamp.ToString(timeStampFormat)}\",");
			// Records - Pressure values
			json.Append($"\"highBarometerVal\":\"{station.alltimerecarray[WeatherStation.AT_HighPress].value.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.alltimerecarray[WeatherStation.AT_LowPress].value.ToString(cumulus.PressFormat)}\",");
			// Records - Pressure times
			json.Append($"\"highBarometerTime\":\"{station.alltimerecarray[WeatherStation.AT_HighPress].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.alltimerecarray[WeatherStation.AT_LowPress].timestamp.ToString(timeStampFormat)}\",");
			// Records - Wind values
			json.Append($"\"highGustVal\":\"{station.alltimerecarray[WeatherStation.AT_HighGust].value.ToString(cumulus.WindFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.alltimerecarray[WeatherStation.AT_HighWind].value.ToString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.alltimerecarray[WeatherStation.AT_HighWindrun].value.ToString(cumulus.WindRunFormat)}\",");
			// Records - Wind times
			json.Append($"\"highGustTime\":\"{station.alltimerecarray[WeatherStation.AT_HighGust].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.alltimerecarray[WeatherStation.AT_HighWind].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.alltimerecarray[WeatherStation.AT_HighWindrun].timestamp.ToString(dateStampFormat)}\",");
			// Records - Rain values
			json.Append($"\"highRainRateVal\":\"{station.alltimerecarray[WeatherStation.AT_HighRainRate].value.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.alltimerecarray[WeatherStation.AT_HourlyRain].value.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.alltimerecarray[WeatherStation.AT_DailyRain].value.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highMonthlyRainVal\":\"{station.alltimerecarray[WeatherStation.AT_WetMonth].value.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.alltimerecarray[WeatherStation.AT_LongestDryPeriod].value:f0}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.alltimerecarray[WeatherStation.AT_LongestWetPeriod].value:f0}\",");
			// Records - Rain times
			json.Append($"\"highRainRateTime\":\"{station.alltimerecarray[WeatherStation.AT_HighRainRate].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.alltimerecarray[WeatherStation.AT_HourlyRain].timestamp.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.alltimerecarray[WeatherStation.AT_DailyRain].timestamp.ToString(dateStampFormat)}\",");
			json.Append($"\"highMonthlyRainTime\":\"{station.alltimerecarray[WeatherStation.AT_WetMonth].timestamp:yyyy/MM}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.alltimerecarray[WeatherStation.AT_LongestDryPeriod].timestamp.ToString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.alltimerecarray[WeatherStation.AT_LongestWetPeriod].timestamp.ToString(dateStampFormat)}\"");
			json.Append("}");

			return json.ToString();
		}

		internal string GetRecordsDayFile(string recordType)
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var linenum = 0;
			var highTempVal = -999.0;
			var lowTempVal = 999.0;
			var highDewPtVal = highTempVal;
			var lowDewPtVal = lowTempVal;
			var highAppTempVal = highTempVal;
			var lowAppTempVal = lowTempVal;
			var highFeelsLikeVal = highTempVal;
			var lowFeelsLikeVal = lowTempVal;
			var highHumidexVal = highTempVal;
			var lowWindChillVal = lowTempVal;
			var highHeatIndVal = highTempVal;
			var highMinTempVal = highTempVal;
			var lowMaxTempVal = lowTempVal;
			var highTempRangeVal = highTempVal;
			var lowTempRangeVal = lowTempVal;
			var highHumVal = highTempVal;
			var lowHumVal = lowTempVal;
			var highBaroVal = highTempVal;
			var lowBaroVal = 99999.0;
			var highGustVal = highTempVal;
			var highWindVal = highTempVal;
			var highWindRunVal = highTempVal;
			var highRainRateVal = highTempVal;
			var highRainHourVal = highTempVal;
			var highRainDayVal = highTempVal;
			var highRainMonthVal = highTempVal;
			var dryPeriodVal = 0;
			var wetPeriodVal = 0;
			var highTempTime = new DateTime(1900, 01, 01);
			var lowTempTime = highTempTime;
			var highDewPtTime = highTempTime;
			var lowDewPtTime = highTempTime;
			var highAppTempTime = highTempTime;
			var lowAppTempTime = highTempTime;
			var highFeelsLikeTime = highTempTime;
			var lowFeelsLikeTime = highTempTime;
			var highHumidexTime = highTempTime;
			var lowWindChillTime = highTempTime;
			var highHeatIndTime = highTempTime;
			var highMinTempTime = highTempTime;
			var lowMaxTempTime = highTempTime;
			var highTempRangeTime = highTempTime;
			var lowTempRangeTime = highTempTime;
			var highHumTime = highTempTime;
			var lowHumTime = highTempTime;
			var highBaroTime = highTempTime;
			var lowBaroTime = highTempTime;
			var highGustTime = highTempTime;
			var highWindTime = highTempTime;
			var highWindRunTime = highTempTime;
			var highRainRateTime = highTempTime;
			var highRainHourTime = highTempTime;
			var highRainDayTime = highTempTime;
			var highRainMonthTime = highTempTime;
			var dryPeriodTime = highTempTime;
			var wetPeriodTime = highTempTime;

			var thisDate = highTempTime;
			DateTime startDate;
			switch (recordType)
			{
				case "alltime":
					startDate = highTempTime;
					break;
				case "thisyear":
					var now = DateTime.Now;
					startDate = new DateTime(now.Year, 1, 1);
					break;
				case "thismonth":
					now = DateTime.Now;
					startDate = new DateTime(now.Year, now.Month, 1);
					break;
				default:
					startDate = highTempTime;
					break;
			}

			var rainThisMonth = 0.0;
			var currentDryPeriod = 0;
			var currentWetPeriod = 0;
			var isDryNow = false;
			var thisDateDry = highTempTime;
			var thisDateWet = highTempTime;
			var json = new StringBuilder("{", 2048);

			double rainThreshold = 0;
			if (cumulus.RainDayThreshold > -1)
				rainThreshold = cumulus.RainDayThreshold;

			var watch = System.Diagnostics.Stopwatch.StartNew();

			// Read the dayfile and extract the records from there
			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					var dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (var line in dayfile)
					{
						linenum++;
						var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count <= 0) continue;

						var datestr = st[0];
						var loggedDate = station.ddmmyyStrToDate(datestr);
						if (loggedDate < startDate)
						{
							continue;
						}

						double valDbl, valDbl2;

						// This assumes the day file is in date order!
						if (thisDate.Month != loggedDate.Month)
						{
							// monthly rain
							if (rainThisMonth > highRainMonthVal)
							{
								highRainMonthVal = rainThisMonth;
								highRainMonthTime = thisDate;
							}
							// reset the date and counter for a new month
							thisDate = loggedDate;
							rainThisMonth = 0;
						}
						// hi gust
						if (double.TryParse(st[1], out valDbl) && valDbl > highGustVal)
						{
							highGustVal = valDbl;
							highGustTime = GetDateTime(loggedDate, st[3]);
						}
						// hi temp
						if (double.TryParse(st[6], out valDbl) && valDbl > highTempVal)
						{
							highTempVal = valDbl;
							highTempTime = GetDateTime(loggedDate, st[7]);
						}
						// lo temp
						if (double.TryParse(st[4], out valDbl) && valDbl < lowTempVal)
						{
							lowTempVal = valDbl;
							lowTempTime = GetDateTime(loggedDate, st[5]);
						}
						// hi min temp
						if (double.TryParse(st[4], out valDbl) && valDbl > highMinTempVal)
						{
							highMinTempVal = valDbl;
							highMinTempTime = loggedDate;
						}
						// lo max temp
						if (double.TryParse(st[6], out valDbl) && valDbl < lowMaxTempVal)
						{
							lowMaxTempVal = valDbl;
							lowMaxTempTime = loggedDate;
						}
						// temp ranges
						if (double.TryParse(st[6], out valDbl) && double.TryParse(st[4], out valDbl2))
						{
							// hi temp range
							if ((valDbl - valDbl2) > highTempRangeVal)
							{
								highTempRangeVal = valDbl - valDbl2;
								highTempRangeTime = loggedDate;
							}
							// lo temp range
							if ((valDbl - valDbl2) < lowTempRangeVal)
							{
								lowTempRangeVal = valDbl - valDbl2;
								lowTempRangeTime = loggedDate;
							}
						}
						// lo baro
						if (double.TryParse(st[8], out valDbl) && valDbl < lowBaroVal)
						{
							lowBaroVal = valDbl;
							lowBaroTime = GetDateTime(loggedDate, st[9]);
						}
						// hi baro
						if (double.TryParse(st[10], out valDbl) && valDbl > highBaroVal)
						{
							highBaroVal = valDbl;
							highBaroTime = GetDateTime(loggedDate, st[11]);
						}
						// hi rain rate
						if (double.TryParse(st[12], out valDbl) && valDbl > highRainRateVal)
						{
							highRainRateVal = valDbl;
							highRainRateTime = GetDateTime(loggedDate, st[13]);
						}
						if (double.TryParse(st[14], out valDbl))
						{
							// hi rain day
							if (valDbl > highRainDayVal)
							{
								highRainDayVal = valDbl;
								highRainDayTime = loggedDate;
							}

							// monthly rain
							rainThisMonth += valDbl;

							// dry/wet period
							if (valDbl > rainThreshold)
							{
								if (isDryNow)
								{
									currentWetPeriod = 1;
									isDryNow = false;
									if (currentDryPeriod > dryPeriodVal)
									{
										dryPeriodVal = currentDryPeriod;
										dryPeriodTime = thisDateDry;
									}
									currentDryPeriod = 0;
								}
								else
								{
									currentWetPeriod++;
									thisDateWet = loggedDate;
								}
							}
							else
							{
								if (isDryNow)
								{
									currentDryPeriod++;
									thisDateDry = loggedDate;
								}
								else
								{
									currentDryPeriod = 1;
									isDryNow = true;
									if (currentWetPeriod > wetPeriodVal)
									{
										wetPeriodVal = currentWetPeriod;
										wetPeriodTime = thisDateWet;
									}
									currentWetPeriod = 0;
								}
							}
						}
						// Extended for ???
						if (st.Count > 15)
						{
							// hi wind run
							if (double.TryParse(st[16], out valDbl) && valDbl > highWindRunVal)
							{
								highWindRunVal = valDbl;
								highWindRunTime = loggedDate;
							}
						}
						// Extended for v1.8.9
						if (st.Count > 17)
						{
							// hi wind
							if (double.TryParse(st[17], out valDbl) && valDbl > highWindVal)
							{
								highWindVal = valDbl;
								highWindTime = GetDateTime(loggedDate, st[18]);
							}
						}
						// Extended for v1.9.0
						if (st.Count > 18)
						{
							// lo humidity
							if (double.TryParse(st[19], out valDbl) && valDbl < lowHumVal)
							{
								lowHumVal = valDbl;
								lowHumTime = GetDateTime(loggedDate, st[20]);
							}
							// hi humidity
							if (double.TryParse(st[21], out valDbl) && valDbl > highHumVal)
							{
								highHumVal = valDbl;
								highHumTime = GetDateTime(loggedDate, st[22]);
							}
							// hi heat index
							if (double.TryParse(st[25], out valDbl) && valDbl > highHeatIndVal)
							{
								highHeatIndVal = valDbl;
								highHeatIndTime = GetDateTime(loggedDate, st[26]);
							}
							// hi app temp
							if (double.TryParse(st[27], out valDbl) && valDbl > highAppTempVal)
							{
								highAppTempVal = valDbl;
								highAppTempTime = GetDateTime(loggedDate, st[28]);
							}
							// lo app temp
							if (double.TryParse(st[29], out valDbl) && valDbl < lowAppTempVal)
							{
								lowAppTempVal = valDbl;
								lowAppTempTime = GetDateTime(loggedDate, st[30]);
							}
							// hi rain hour
							if (double.TryParse(st[31], out valDbl) && valDbl > highRainHourVal)
							{
								highRainHourVal = valDbl;
								highRainHourTime = GetDateTime(loggedDate, st[32]);
							}
							// lo wind chill
							if (double.TryParse(st[33], out valDbl) && valDbl < lowWindChillVal)
							{
								lowWindChillVal = valDbl;
								lowWindChillTime = GetDateTime(loggedDate, st[34]);
							}
						}
						// extended for v1.9.1
						if (st.Count > 35)
						{
							// hi dewpt
							if (double.TryParse(st[35], out valDbl) && valDbl > highDewPtVal)
							{
								highDewPtVal = valDbl;
								highDewPtTime = GetDateTime(loggedDate, st[36]);
							}
							// lo dewpt
							if (double.TryParse(st[37], out valDbl) && valDbl < lowDewPtVal)
							{
								lowDewPtVal = valDbl;
								lowDewPtTime = GetDateTime(loggedDate, st[38]);
							}
						}
						// extended for v3.6.0
						if (st.Count > 46)
						{
							// hi feels like
							if (double.TryParse(st[46], out valDbl) && valDbl > highFeelsLikeVal)
							{
								highFeelsLikeVal = valDbl;
								highFeelsLikeTime = GetDateTime(loggedDate, st[47]);
							}
							// lo feels like
							if (double.TryParse(st[48], out valDbl) && valDbl < lowFeelsLikeVal)
							{
								lowFeelsLikeVal = valDbl;
								lowFeelsLikeTime = GetDateTime(loggedDate, st[49]);
							}
						}
						// extended for v3.7.0
						if (st.Count > 50)
						{
							// hi humidex
							if (double.TryParse(st[50], out valDbl) && valDbl > highHumidexVal)
							{
								highHumidexVal = valDbl;
								highHumidexTime = GetDateTime(loggedDate, st[51]);
							}
						}
					}

					// We need to check if the run or wet/dry days at the end of logs exceeds any records
					if (currentWetPeriod > wetPeriodVal)
					{
						wetPeriodVal = currentWetPeriod;
						wetPeriodTime = thisDateWet;
					}
					if (currentDryPeriod > dryPeriodVal)
					{
						dryPeriodVal = currentDryPeriod;
						dryPeriodTime = thisDateDry;
					}

					json.Append($"\"highTempValDayfile\":\"{highTempVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"highTempTimeDayfile\":\"{highTempTime.ToString(timeStampFormat)}\",");
					json.Append($"\"lowTempValDayfile\":\"{lowTempVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"lowTempTimeDayfile\":\"{lowTempTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highDewPointValDayfile\":\"{highDewPtVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"highDewPointTimeDayfile\":\"{highDewPtTime.ToString(timeStampFormat)}\",");
					json.Append($"\"lowDewPointValDayfile\":\"{lowDewPtVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"lowDewPointTimeDayfile\":\"{lowDewPtTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highApparentTempValDayfile\":\"{highAppTempVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"highApparentTempTimeDayfile\":\"{highAppTempTime.ToString(timeStampFormat)}\",");
					json.Append($"\"lowApparentTempValDayfile\":\"{lowAppTempVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"lowApparentTempTimeDayfile\":\"{lowAppTempTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highFeelsLikeValDayfile\":\"{highFeelsLikeVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"highFeelsLikeTimeDayfile\":\"{highFeelsLikeTime.ToString(timeStampFormat)}\",");
					json.Append($"\"lowFeelsLikeValDayfile\":\"{lowFeelsLikeVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"lowFeelsLikeTimeDayfile\":\"{lowFeelsLikeTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highHumidexValDayfile\":\"{highHumidexVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"highHumidexTimeDayfile\":\"{highHumidexTime.ToString(timeStampFormat)}\",");
					json.Append($"\"lowWindChillValDayfile\":\"{lowWindChillVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"lowWindChillTimeDayfile\":\"{lowWindChillTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highHeatIndexValDayfile\":\"{highHeatIndVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"highHeatIndexTimeDayfile\":\"{highHeatIndTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highMinTempValDayfile\":\"{highMinTempVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"highMinTempTimeDayfile\":\"{highMinTempTime.ToString(dateStampFormat)}\",");
					json.Append($"\"lowMaxTempValDayfile\":\"{lowMaxTempVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"lowMaxTempTimeDayfile\":\"{lowMaxTempTime.ToString(dateStampFormat)}\",");
					json.Append($"\"highDailyTempRangeValDayfile\":\"{highTempRangeVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"highDailyTempRangeTimeDayfile\":\"{highTempRangeTime.ToString(dateStampFormat)}\",");
					json.Append($"\"lowDailyTempRangeValDayfile\":\"{lowTempRangeVal.ToString(cumulus.TempFormat)}\",");
					json.Append($"\"lowDailyTempRangeTimeDayfile\":\"{lowTempRangeTime.ToString(dateStampFormat)}\",");
					json.Append($"\"highHumidityValDayfile\":\"{highHumVal.ToString(cumulus.HumFormat)}\",");
					json.Append($"\"highHumidityTimeDayfile\":\"{highHumTime.ToString(timeStampFormat)}\",");
					json.Append($"\"lowHumidityValDayfile\":\"{lowHumVal.ToString(cumulus.HumFormat)}\",");
					json.Append($"\"lowHumidityTimeDayfile\":\"{lowHumTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highBarometerValDayfile\":\"{highBaroVal.ToString(cumulus.PressFormat)}\",");
					json.Append($"\"highBarometerTimeDayfile\":\"{highBaroTime.ToString(timeStampFormat)}\",");
					json.Append($"\"lowBarometerValDayfile\":\"{lowBaroVal.ToString(cumulus.PressFormat)}\",");
					json.Append($"\"lowBarometerTimeDayfile\":\"{lowBaroTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highGustValDayfile\":\"{highGustVal.ToString(cumulus.WindFormat)}\",");
					json.Append($"\"highGustTimeDayfile\":\"{highGustTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highWindValDayfile\":\"{highWindVal.ToString(cumulus.WindAvgFormat)}\",");
					json.Append($"\"highWindTimeDayfile\":\"{highWindTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highWindRunValDayfile\":\"{highWindRunVal.ToString(cumulus.WindRunFormat)}\",");
					json.Append($"\"highWindRunTimeDayfile\":\"{highWindRunTime.ToString(dateStampFormat)}\",");
					json.Append($"\"highRainRateValDayfile\":\"{highRainRateVal.ToString(cumulus.RainFormat)}\",");
					json.Append($"\"highRainRateTimeDayfile\":\"{highRainRateTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highHourlyRainValDayfile\":\"{highRainHourVal.ToString(cumulus.RainFormat)}\",");
					json.Append($"\"highHourlyRainTimeDayfile\":\"{highRainHourTime.ToString(timeStampFormat)}\",");
					json.Append($"\"highDailyRainValDayfile\":\"{highRainDayVal.ToString(cumulus.RainFormat)}\",");
					json.Append($"\"highDailyRainTimeDayfile\":\"{highRainDayTime.ToString(dateStampFormat)}\",");
					if (recordType != "thismonth")
					{
						json.Append($"\"highMonthlyRainValDayfile\":\"{highRainMonthVal.ToString(cumulus.RainFormat)}\",");
						json.Append($"\"highMonthlyRainTimeDayfile\":\"{highRainMonthTime:yyyy/MM}\",");
					}
					json.Append($"\"longestDryPeriodValDayfile\":\"{dryPeriodVal}\",");
					json.Append($"\"longestDryPeriodTimeDayfile\":\"{dryPeriodTime.ToString(dateStampFormat)}\",");
					json.Append($"\"longestWetPeriodValDayfile\":\"{wetPeriodVal}\",");
					json.Append($"\"longestWetPeriodTimeDayfile\":\"{wetPeriodTime.ToString(dateStampFormat)}\"");
					json.Append("}");
				}
				catch (Exception e)
				{
					cumulus.LogMessage("Error on line " + linenum + " of " + cumulus.DayFile + ": " + e.Message);
				}
			}
			else
			{
				cumulus.LogMessage("Error failed to find day file: " + cumulus.DayFile);
			}

			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"All time recs editor Dayfile load = {elapsed} ms");

			return json.ToString();
		}

		internal string GetRecordsLogFile(string recordType)
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = new StringBuilder("{", 2048);
			DateTime datefrom;
			switch (recordType)
			{
				case "alltime":
					datefrom = DateTime.Parse(cumulus.RecordsBeganDate);
					break;
				case "thisyear":
					var now = DateTime.Now;
					datefrom = new DateTime(now.Year, 1, 2);
					break;
				case "thismonth":
					now = DateTime.Now;
					datefrom = new DateTime(now.Year, now.Month, 2);
					break;
				default:
					datefrom = DateTime.Parse(cumulus.RecordsBeganDate);
					break;
			}
			datefrom = new DateTime(datefrom.Year, datefrom.Month, datefrom.Day, 0, 0, 0);
			var dateto = DateTime.Now;
			dateto = new DateTime(dateto.Year, dateto.Month, 2, 0, 0, 0);
			var filedate = datefrom;

			var logFile = cumulus.GetLogFileName(filedate);
			var started = false;
			var finished = false;
			var lastentrydate = datefrom;

			var currentDay = datefrom;
			double dayHighTemp = -999;
			double dayLowTemp = 999;
			double dayWindRun = 0;
			double dayRain = 0;

			var isDryNow = false;
			var currentDryPeriod = 0;
			var currentWetPeriod = 0;

			var rainThreshold = 0.0;
			if (cumulus.RainDayThreshold > -1)
				rainThreshold = cumulus.RainDayThreshold;

			var highTempVal = -999.0;
			var lowTempVal = 999.0;
			var highDewPtVal = highTempVal;
			var lowDewPtVal = lowTempVal;
			var highAppTempVal = highTempVal;
			var lowAppTempVal = lowTempVal;
			var highFeelsLikeVal = highTempVal;
			var lowFeelsLikeVal = lowTempVal;
			var highHumidexVal = highTempVal;
			var lowWindChillVal = lowTempVal;
			var highHeatIndVal = highTempVal;
			var highMinTempVal = highTempVal;
			var lowMaxTempVal = lowTempVal;
			var highTempRangeVal = highTempVal;
			var lowTempRangeVal = lowTempVal;
			var highHumVal = highTempVal;
			var lowHumVal = lowTempVal;
			var highBaroVal = highTempVal;
			var lowBaroVal = 99999.0;
			var highGustVal = highTempVal;
			var highWindVal = highTempVal;
			var highWindRunVal = highTempVal;
			var highRainRateVal = highTempVal;
			var highRainHourVal = highTempVal;
			var highRainDayVal = highTempVal;
			var highRainMonthVal = highTempVal;
			var dryPeriodVal = 0;
			var wetPeriodVal = 0;

			var highTempTime = new DateTime(1900, 01, 01);
			var lowTempTime = highTempTime;
			var highDewPtTime = highTempTime;
			var lowDewPtTime = highTempTime;
			var highAppTempTime = highTempTime;
			var lowAppTempTime = highTempTime;
			var highFeelsLikeTime = highTempTime;
			var lowFeelsLikeTime = highTempTime;
			var highHumidexTime = highTempTime;
			var lowWindChillTime = highTempTime;
			var highHeatIndTime = highTempTime;
			var highMinTempTime = highTempTime;
			var lowMaxTempTime = highTempTime;
			var highTempRangeTime = highTempTime;
			var lowTempRangeTime = highTempTime;
			var highHumTime = highTempTime;
			var lowHumTime = highTempTime;
			var highBaroTime = highTempTime;
			var lowBaroTime = highTempTime;
			var highGustTime = highTempTime;
			var highWindTime = highTempTime;
			var highWindRunTime = highTempTime;
			var highRainRateTime = highTempTime;
			var highRainHourTime = highTempTime;
			var highRainDayTime = highTempTime;
			var highRainMonthTime = highTempTime;
			var dryPeriodTime = highTempTime;
			var wetPeriodTime = highTempTime;

			var thisDateDry = highTempTime;
			var thisDateWet = highTempTime;

			var totalRainfall = 0.0;

			var watch = System.Diagnostics.Stopwatch.StartNew();

			hourRainLog.Clear();

			while (!finished)
			{
				double monthlyRain = 0;

				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetAllTimeRecLogFile: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);
						double valDbl;

						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;
							//var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
							// Regex is very expensive, let's assume the separator is always a single character
							var st = new List<string>(line.Split((CultureInfo.CurrentCulture.TextInfo.ListSeparator)[0]));
							var entrydate = station.ddmmyyhhmmStrToDate(st[0], st[1]);
							// We need to work in meto dates not clock dates for day hi/lows
							var metoDate = entrydate.AddHours(cumulus.GetHourInc());

							if (!started)
							{
								lastentrydate = entrydate;
								currentDay = metoDate;
								started = true;
							}

							var outsidetemp = double.Parse(st[2]);
							var hum = int.Parse(st[3]);
							var dewpoint = double.Parse(st[4]);
							var speed = double.Parse(st[5]);
							var gust = double.Parse(st[6]);
							var rainrate = double.Parse(st[8]);
							var raintoday = double.Parse(st[9]);
							var pressure = double.Parse(st[10]);

							// low chill
							if (double.TryParse(st[15], out valDbl) && valDbl < lowWindChillVal)
							{
								lowWindChillVal = valDbl;
								lowWindChillTime = entrydate;
							}
							// hi heat
							if (double.TryParse(st[16], out valDbl) && valDbl > highHeatIndVal)
							{
								highHeatIndVal = valDbl;
								highHeatIndTime = entrydate;
							}
							// hi/low appt
							if (st.Count > 21 && double.TryParse(st[21], out valDbl))
							{
								if (valDbl > highAppTempVal)
								{
									highAppTempVal = valDbl;
									highAppTempTime = entrydate;
								}
								if (valDbl < lowAppTempVal)
								{
									lowAppTempVal = valDbl;
									lowAppTempTime = entrydate;
								}
							}
							// hi/low feels like
							if (st.Count > 27 && double.TryParse(st[27], out valDbl))
							{
								if (valDbl > highFeelsLikeVal)
								{
									highFeelsLikeVal = valDbl;
									highFeelsLikeTime = entrydate;
								}
								if (valDbl < lowFeelsLikeVal)
								{
									lowFeelsLikeVal = valDbl;
									lowFeelsLikeTime = entrydate;
								}
							}

							// hi/low humidex
							if (st.Count > 28 && double.TryParse(st[28], out valDbl))
							{
								if (valDbl > highHumidexVal)
								{
									highHumidexVal = valDbl;
									highHumidexTime = entrydate;
								}
							}

							// hi temp
							if (outsidetemp > highTempVal)
							{
								highTempVal = outsidetemp;
								highTempTime = entrydate;
							}
							// lo temp
							if (outsidetemp < lowTempVal)
							{
								lowTempVal = outsidetemp;
								lowTempTime = entrydate;
							}
							// hi dewpoint
							if (dewpoint > highDewPtVal)
							{
								highDewPtVal = dewpoint;
								highDewPtTime = entrydate;
							}
							// low dewpoint
							if (dewpoint < lowDewPtVal)
							{
								lowDewPtVal = dewpoint;
								lowDewPtTime = entrydate;
							}
							// hi hum
							if (hum > highHumVal)
							{
								highHumVal = hum;
								highHumTime = entrydate;
							}
							// lo hum
							if (hum < lowHumVal)
							{
								lowHumVal = hum;
								lowHumTime = entrydate;
							}
							// hi baro
							if (pressure > highBaroVal)
							{
								highBaroVal = pressure;
								highBaroTime = entrydate;
							}
							// lo hum
							if (pressure < lowBaroVal)
							{
								lowBaroVal = pressure;
								lowBaroTime = entrydate;
							}
							// hi gust
							if (gust > highGustVal)
							{
								highGustVal = gust;
								highGustTime = entrydate;
							}
							// hi wind
							if (speed > highWindVal)
							{
								highWindVal = speed;
								highWindTime = entrydate;
							}
							// hi rain rate
							if (rainrate > highRainRateVal)
							{
								highRainRateVal = rainrate;
								highRainRateTime = entrydate;
							}

							if (monthlyRain > highRainMonthVal)
							{
								highRainMonthVal = monthlyRain;
								highRainMonthTime = entrydate;
							}

							// same meto day
							if (currentDay.Day == metoDate.Day && currentDay.Month == metoDate.Month && currentDay.Year == metoDate.Year)
							{
								if (outsidetemp > dayHighTemp)
									dayHighTemp = outsidetemp;

								if (outsidetemp < dayLowTemp)
									dayLowTemp = outsidetemp;

								if (dayRain < raintoday)
									dayRain = raintoday;

								dayWindRun += entrydate.Subtract(lastentrydate).TotalHours * speed;
							}
							else // new meto day
							{
								if (dayHighTemp < lowMaxTempVal)
								{
									lowMaxTempVal = dayHighTemp;
									lowMaxTempTime = currentDay;
								}
								if (dayLowTemp > highMinTempVal)
								{
									highMinTempVal = dayLowTemp;
									highMinTempTime = currentDay;
								}
								if (dayHighTemp - dayLowTemp > highTempRangeVal)
								{
									highTempRangeVal = dayHighTemp - dayLowTemp;
									highTempRangeTime = currentDay;
								}
								if (dayHighTemp - dayLowTemp < lowTempRangeVal)
								{
									lowTempRangeVal = dayHighTemp - dayLowTemp;
									lowTempRangeTime = currentDay;
								}
								if (dayWindRun > highWindRunVal)
								{
									highWindRunVal = dayWindRun;
									highWindRunTime = currentDay;
								}
								if (dayRain > highRainDayVal)
								{
									highRainDayVal = dayRain;
									highRainDayTime = currentDay;
								}
								monthlyRain += dayRain;

								// dry/wet period
								if (dayRain > rainThreshold)
								{
									if (isDryNow)
									{
										currentWetPeriod = 1;
										isDryNow = false;
										if (currentDryPeriod > dryPeriodVal)
										{
											dryPeriodVal = currentDryPeriod;
											dryPeriodTime = thisDateDry;
										}
										currentDryPeriod = 0;
									}
									else
									{
										currentWetPeriod++;
										thisDateWet = currentDay;
									}
								}
								else
								{
									if (isDryNow)
									{
										currentDryPeriod++;
										thisDateDry = currentDay;
									}
									else
									{
										currentDryPeriod = 1;
										isDryNow = true;
										if (currentWetPeriod > wetPeriodVal)
										{
											wetPeriodVal = currentWetPeriod;
											wetPeriodTime = thisDateWet;
										}
										currentWetPeriod = 0;
									}
								}

								currentDay = metoDate;
								dayHighTemp = outsidetemp;
								dayLowTemp = outsidetemp;
								dayWindRun = 0;
								totalRainfall += dayRain;
								dayRain = 0;
							}

							// hourly rain
							/*
							 * need to track what the rainfall has been in the last rolling hour
							 * across day rollovers where the count resets
							 */
							AddLastHourRainEntry(entrydate, totalRainfall + dayRain);
							RemoveOldRainData(entrydate);

							var rainThisHour = hourRainLog.Last().Raincounter - hourRainLog.First().Raincounter;
							if (rainThisHour > highRainHourVal)
							{
								highRainHourVal = rainThisHour;
								highRainHourTime = entrydate;
							}

							lastentrydate = entrydate;
							//lastRainMidnight = rainMidnight;
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetAllTimeRecLogFile: Log file  not found - {logFile}");
				}
				if (filedate >= dateto)
				{
					finished = true;
					cumulus.LogDebugMessage("GetAllTimeRecLogFile: Finished processing the log files");
				}
				else
				{
					cumulus.LogDebugMessage($"GetAllTimeRecLogFile: Finished processing log file - {logFile}");
					filedate = filedate.AddMonths(1);
					logFile = cumulus.GetLogFileName(filedate);
				}
			}

			// We need to check if the run or wet/dry days at the end of logs exceeds any records
			if (currentWetPeriod > wetPeriodVal)
			{
				wetPeriodVal = currentWetPeriod;
				wetPeriodTime = thisDateWet;
			}
			if (currentDryPeriod > dryPeriodVal)
			{
				dryPeriodVal = currentDryPeriod;
				dryPeriodTime = thisDateDry;
			}

			json.Append($"\"highTempValLogfile\":\"{highTempVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTimeLogfile\":\"{highTempTime.ToString(timeStampFormat)}\",");
			json.Append($"\"lowTempValLogfile\":\"{lowTempVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTimeLogfile\":\"{lowTempTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highDewPointValLogfile\":\"{highDewPtVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTimeLogfile\":\"{highDewPtTime.ToString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointValLogfile\":\"{lowDewPtVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTimeLogfile\":\"{lowDewPtTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempValLogfile\":\"{highAppTempVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTimeLogfile\":\"{highAppTempTime.ToString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempValLogfile\":\"{lowAppTempVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTimeLogfile\":\"{lowAppTempTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeValLogfile\":\"{highFeelsLikeVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTimeLogfile\":\"{highFeelsLikeTime.ToString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeValLogfile\":\"{lowFeelsLikeVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTimeLogfile\":\"{lowFeelsLikeTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highHumidexValLogfile\":\"{highHumidexVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTimeLogfile\":\"{highHumidexTime.ToString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillValLogfile\":\"{lowWindChillVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTimeLogfile\":\"{lowWindChillTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexValLogfile\":\"{highHeatIndVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTimeLogfile\":\"{highHeatIndTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highMinTempValLogfile\":\"{highMinTempVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTimeLogfile\":\"{highMinTempTime.ToString(dateStampFormat)}\",");
			json.Append($"\"lowMaxTempValLogfile\":\"{lowMaxTempVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTimeLogfile\":\"{lowMaxTempTime.ToString(dateStampFormat)}\",");
			json.Append($"\"highDailyTempRangeValLogfile\":\"{highTempRangeVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTimeLogfile\":\"{highTempRangeTime.ToString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeValLogfile\":\"{lowTempRangeVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTimeLogfile\":\"{lowTempRangeTime.ToString(dateStampFormat)}\",");
			json.Append($"\"highHumidityValLogfile\":\"{highHumVal.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTimeLogfile\":\"{highHumTime.ToString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityValLogfile\":\"{lowHumVal.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTimeLogfile\":\"{lowHumTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highBarometerValLogfile\":\"{highBaroVal.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTimeLogfile\":\"{highBaroTime.ToString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerValLogfile\":\"{lowBaroVal.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTimeLogfile\":\"{lowBaroTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highGustValLogfile\":\"{highGustVal.ToString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTimeLogfile\":\"{highGustTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindValLogfile\":\"{highWindVal.ToString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTimeLogfile\":\"{highWindTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindRunValLogfile\":\"{highWindRunVal.ToString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTimeLogfile\":\"{highWindRunTime.ToString(dateStampFormat)}\",");
			json.Append($"\"highRainRateValLogfile\":\"{highRainRateVal.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTimeLogfile\":\"{highRainRateTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainValLogfile\":\"{highRainHourVal.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTimeLogfile\":\"{highRainHourTime.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainValLogfile\":\"{highRainDayVal.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTimeLogfile\":\"{highRainDayTime.ToString(dateStampFormat)}\",");
			json.Append($"\"highMonthlyRainValLogfile\":\"{highRainMonthVal.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highMonthlyRainTimeLogfile\":\"{highRainMonthTime.ToString($"yyyy/MM")}\",");
			if (recordType == "alltime")
			{
				json.Append($"\"longestDryPeriodValLogfile\":\"{dryPeriodVal}\",");
				json.Append($"\"longestDryPeriodTimeLogfile\":\"{dryPeriodTime.ToString(dateStampFormat)}\",");
				json.Append($"\"longestWetPeriodValLogfile\":\"{wetPeriodVal}\",");
				json.Append($"\"longestWetPeriodTimeLogfile\":\"{wetPeriodTime.ToString(dateStampFormat)}\"");
			}
			else
			{
				json.Append($"\"longestDryPeriodValLogfile\":\"{dryPeriodVal}*\",");
				json.Append($"\"longestDryPeriodTimeLogfile\":\"{dryPeriodTime.ToString(dateStampFormat)}*\",");
				json.Append($"\"longestWetPeriodValLogfile\":\"{wetPeriodVal}*\",");
				json.Append($"\"longestWetPeriodTimeLogfile\":\"{wetPeriodTime.ToString(dateStampFormat)}*\"");
			}
			json.Append("}");
			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"All time recs editor Logfiles load = {elapsed} ms");

			return json.ToString();
		}

		private static DateTime GetDateTime(DateTime date, string time)
		{
			var tim = time.Split(CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator.ToCharArray()[0]);
			return new DateTime(date.Year, date.Month, date.Day, int.Parse(tim[0]), int.Parse(tim[1]), 0);
		}

		internal string EditAllTimeRecs(IHttpContext context)
		{
			var request = context.Request;
			string text;

			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = Uri.UnescapeDataString(reader.ReadToEnd());
			}
			// Eg "name=highTempValvalue=134.6&pk=1"
			var newData = text.Split('&');
			var field = newData[0].Split('=')[1];
			var value = newData[1].Split('=')[1];
			var result = 1;
			try
			{
				string[] dt;
				switch (field)
				{
					case "highTempVal":
						station.SetAlltime(WeatherStation.AT_HighTemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighTemp].timestamp);
						break;
					case "highTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighTemp, station.alltimerecarray[WeatherStation.AT_HighTemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowTempVal":
						station.SetAlltime(WeatherStation.AT_LowTemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LowTemp].timestamp);
						break;
					case "lowTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_LowTemp, station.alltimerecarray[WeatherStation.AT_LowTemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDewPointVal":
						station.SetAlltime(WeatherStation.AT_HighDewPoint, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighDewPoint].timestamp);
						break;
					case "highDewPointTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighDewPoint, station.alltimerecarray[WeatherStation.AT_HighDewPoint].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowDewPointVal":
						station.SetAlltime(WeatherStation.AT_LowDewpoint, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LowDewpoint].timestamp);
						break;
					case "lowDewPointTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_LowDewpoint, station.alltimerecarray[WeatherStation.AT_LowDewpoint].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highApparentTempVal":
						station.SetAlltime(WeatherStation.AT_HighAppTemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighAppTemp].timestamp);
						break;
					case "highApparentTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighAppTemp, station.alltimerecarray[WeatherStation.AT_HighAppTemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowApparentTempVal":
						station.SetAlltime(WeatherStation.AT_LowAppTemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LowAppTemp].timestamp);
						break;
					case "lowApparentTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_LowAppTemp, station.alltimerecarray[WeatherStation.AT_LowAppTemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highFeelsLikeVal":
						station.SetAlltime(WeatherStation.AT_HighFeelsLike, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighFeelsLike].timestamp);
						break;
					case "highFeelsLikeTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighFeelsLike, station.alltimerecarray[WeatherStation.AT_HighFeelsLike].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowFeelsLikeVal":
						station.SetAlltime(WeatherStation.AT_LowFeelsLike, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LowFeelsLike].timestamp);
						break;
					case "lowFeelsLikeTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_LowFeelsLike, station.alltimerecarray[WeatherStation.AT_LowFeelsLike].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHumidexVal":
						station.SetAlltime(WeatherStation.AT_HighHumidex, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighHumidex].timestamp);
						break;
					case "highHumidexTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighHumidex, station.alltimerecarray[WeatherStation.AT_HighHumidex].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowWindChillVal":
						station.SetAlltime(WeatherStation.AT_LowChill, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LowChill].timestamp);
						break;
					case "lowWindChillTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_LowChill, station.alltimerecarray[WeatherStation.AT_LowChill].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHeatIndexVal":
						station.SetAlltime(WeatherStation.AT_HighHeatIndex, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighHeatIndex].timestamp);
						break;
					case "highHeatIndexTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighHeatIndex, station.alltimerecarray[WeatherStation.AT_HighHeatIndex].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highMinTempVal":
						station.SetAlltime(WeatherStation.AT_HighMinTemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighMinTemp].timestamp);
						break;
					case "highMinTempTime":
						station.SetAlltime(WeatherStation.AT_HighMinTemp, station.alltimerecarray[WeatherStation.AT_HighMinTemp].value, station.ddmmyyStrToDate(value));
						break;
					case "lowMaxTempVal":
						station.SetAlltime(WeatherStation.AT_LowMaxTemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LowMaxTemp].timestamp);
						break;
					case "lowMaxTempTime":
						station.SetAlltime(WeatherStation.AT_LowMaxTemp, station.alltimerecarray[WeatherStation.AT_LowMaxTemp].value, station.ddmmyyStrToDate(value));
						break;
					case "highDailyTempRangeVal":
						station.SetAlltime(WeatherStation.AT_HighDailyTempRange, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighDailyTempRange].timestamp);
						break;
					case "highDailyTempRangeTime":
						station.SetAlltime(WeatherStation.AT_HighDailyTempRange, station.alltimerecarray[WeatherStation.AT_HighDailyTempRange].value, station.ddmmyyStrToDate(value));
						break;
					case "lowDailyTempRangeVal":
						station.SetAlltime(WeatherStation.AT_LowDailyTempRange, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LowDailyTempRange].timestamp);
						break;
					case "lowDailyTempRangeTime":
						station.SetAlltime(WeatherStation.AT_LowDailyTempRange, station.alltimerecarray[WeatherStation.AT_LowDailyTempRange].value, station.ddmmyyStrToDate(value));
						break;
					case "highHumidityVal":
						station.SetAlltime(WeatherStation.AT_HighHumidity, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighHumidity].timestamp);
						break;
					case "highHumidityTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighHumidity, station.alltimerecarray[WeatherStation.AT_HighHumidity].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowHumidityVal":
						station.SetAlltime(WeatherStation.AT_lowhumidity, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowhumidity].timestamp);
						break;
					case "lowHumidityTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowhumidity, station.alltimerecarray[WeatherStation.AT_lowhumidity].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highBarometerVal":
						station.SetAlltime(WeatherStation.AT_HighPress, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighPress].timestamp);
						break;
					case "highBarometerTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighPress, station.alltimerecarray[WeatherStation.AT_HighPress].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowBarometerVal":
						station.SetAlltime(WeatherStation.AT_LowPress, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LowPress].timestamp);
						break;
					case "lowBarometerTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_LowPress, station.alltimerecarray[WeatherStation.AT_LowPress].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highGustVal":
						station.SetAlltime(WeatherStation.AT_HighGust, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighGust].timestamp);
						break;
					case "highGustTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighGust, station.alltimerecarray[WeatherStation.AT_HighGust].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highWindVal":
						station.SetAlltime(WeatherStation.AT_HighWind, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighWind].timestamp);
						break;
					case "highWindTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighWind, station.alltimerecarray[WeatherStation.AT_HighWind].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highWindRunVal":
						station.SetAlltime(WeatherStation.AT_HighWindrun, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighWindrun].timestamp);
						break;
					case "highWindRunTime":
						station.SetAlltime(WeatherStation.AT_HighWindrun, station.alltimerecarray[WeatherStation.AT_HighWindrun].value, station.ddmmyyStrToDate(value));
						break;
					case "highRainRateVal":
						station.SetAlltime(WeatherStation.AT_HighRainRate, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HighRainRate].timestamp);
						break;
					case "highRainRateTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HighRainRate, station.alltimerecarray[WeatherStation.AT_HighRainRate].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHourlyRainVal":
						station.SetAlltime(WeatherStation.AT_HourlyRain, double.Parse(value), station.alltimerecarray[WeatherStation.AT_HourlyRain].timestamp);
						break;
					case "highHourlyRainTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_HourlyRain, station.alltimerecarray[WeatherStation.AT_HourlyRain].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDailyRainVal":
						station.SetAlltime(WeatherStation.AT_DailyRain, double.Parse(value), station.alltimerecarray[WeatherStation.AT_DailyRain].timestamp);
						break;
					case "highDailyRainTime":
						station.SetAlltime(WeatherStation.AT_DailyRain, station.alltimerecarray[WeatherStation.AT_DailyRain].value, station.ddmmyyStrToDate(value));
						break;
					case "highMonthlyRainVal":
						station.SetAlltime(WeatherStation.AT_WetMonth, double.Parse(value), station.alltimerecarray[WeatherStation.AT_WetMonth].timestamp);
						break;
					case "highMonthlyRainTime":
						dt = value.Split('/');
						var datstr = "01/" + dt[1] + "/" + dt[0].Substring(2, 2);
						station.SetAlltime(WeatherStation.AT_WetMonth, station.alltimerecarray[WeatherStation.AT_WetMonth].value, station.ddmmyyStrToDate(datstr));
						break;
					case "longestDryPeriodVal":
						station.SetAlltime(WeatherStation.AT_LongestDryPeriod, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LongestDryPeriod].timestamp);
						break;
					case "longestDryPeriodTime":
						station.SetAlltime(WeatherStation.AT_LongestDryPeriod, station.alltimerecarray[WeatherStation.AT_LongestDryPeriod].value, station.ddmmyyStrToDate(value));
						break;
					case "longestWetPeriodVal":
						station.SetAlltime(WeatherStation.AT_LongestWetPeriod, double.Parse(value), station.alltimerecarray[WeatherStation.AT_LongestWetPeriod].timestamp);
						break;
					case "longestWetPeriodTime":
						station.SetAlltime(WeatherStation.AT_LongestWetPeriod, station.alltimerecarray[WeatherStation.AT_LongestWetPeriod].value, station.ddmmyyStrToDate(value));
						break;
					default:
						result = 0;
						break;
				}
			}
			catch
			{
				result = 0;
			}
			return "{\"result\":\"" + ((result == 1) ? "Success" : "Failed") + "\"}";
		}

		internal string EditMonthlyRecs(IHttpContext context)
		{
			var request = context.Request;
			string text;

			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = Uri.UnescapeDataString(reader.ReadToEnd());
			}
			// Eg "name=2-highTempValvalue=134.6&pk=1"
			var newData = text.Split('&');
			var monthField = newData[0].Split('=')[1].Split('-');
			var month = int.Parse(monthField[0]);
			var field = monthField[1];
			var value = newData[1].Split('=')[1];
			var result = 1;
			try
			{
				lock (station.monthlyalltimeIniThreadLock)
				{
					string[] dt;
					switch (field)
					{
						case "highTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighTemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighTemp, month].timestamp, month);
							break;
						case "highTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighTemp, station.monthlyrecarray[WeatherStation.AT_HighTemp, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LowTemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LowTemp, month].timestamp, month);
							break;
						case "lowTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_LowTemp, station.monthlyrecarray[WeatherStation.AT_LowTemp, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highDewPointVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighDewPoint, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighDewPoint, month].timestamp, month);
							break;
						case "highDewPointTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighDewPoint, station.monthlyrecarray[WeatherStation.AT_HighDewPoint, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowDewPointVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LowDewpoint, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LowDewpoint, month].timestamp, month);
							break;
						case "lowDewPointTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_LowDewpoint, station.monthlyrecarray[WeatherStation.AT_LowDewpoint, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highApparentTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighAppTemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighAppTemp, month].timestamp, month);
							break;
						case "highApparentTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighAppTemp, station.monthlyrecarray[WeatherStation.AT_HighAppTemp, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowApparentTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LowAppTemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LowAppTemp, month].timestamp, month);
							break;
						case "lowApparentTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_LowAppTemp, station.monthlyrecarray[WeatherStation.AT_LowAppTemp, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highFeelsLikeVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighFeelsLike, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighFeelsLike, month].timestamp, month);
							break;
						case "highFeelsLikeTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighFeelsLike, station.monthlyrecarray[WeatherStation.AT_HighFeelsLike, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowFeelsLikeVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LowFeelsLike, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LowFeelsLike, month].timestamp, month);
							break;
						case "lowFeelsLikeTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_LowFeelsLike, station.monthlyrecarray[WeatherStation.AT_LowFeelsLike, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highHumidexVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighHumidex, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighHumidex, month].timestamp, month);
							break;
						case "highHumidexTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighHumidex, station.monthlyrecarray[WeatherStation.AT_HighHumidex, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowWindChillVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LowChill, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LowChill, month].timestamp, month);
							break;
						case "lowWindChillTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_LowChill, station.monthlyrecarray[WeatherStation.AT_LowChill, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highHeatIndexVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighHeatIndex, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighHeatIndex, month].timestamp, month);
							break;
						case "highHeatIndexTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighHeatIndex, station.monthlyrecarray[WeatherStation.AT_HighHeatIndex, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highMinTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighMinTemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighMinTemp, month].timestamp, month);
							break;
						case "highMinTempTime":
							station.SetMonthlyAlltime(WeatherStation.AT_HighMinTemp, station.monthlyrecarray[WeatherStation.AT_HighMinTemp, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "lowMaxTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LowMaxTemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LowMaxTemp, month].timestamp, month);
							break;
						case "lowMaxTempTime":
							station.SetMonthlyAlltime(WeatherStation.AT_LowMaxTemp, station.monthlyrecarray[WeatherStation.AT_LowMaxTemp, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "highDailyTempRangeVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighDailyTempRange, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighDailyTempRange, month].timestamp, month);
							break;
						case "highDailyTempRangeTime":
							station.SetMonthlyAlltime(WeatherStation.AT_HighDailyTempRange, station.monthlyrecarray[WeatherStation.AT_HighDailyTempRange, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "lowDailyTempRangeVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LowDailyTempRange, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LowDailyTempRange, month].timestamp, month);
							break;
						case "lowDailyTempRangeTime":
							station.SetMonthlyAlltime(WeatherStation.AT_LowDailyTempRange, station.monthlyrecarray[WeatherStation.AT_LowDailyTempRange, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "highHumidityVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighHumidity, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighHumidity, month].timestamp, month);
							break;
						case "highHumidityTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighHumidity, station.monthlyrecarray[WeatherStation.AT_HighHumidity, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowHumidityVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowhumidity, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowhumidity, month].timestamp, month);
							break;
						case "lowHumidityTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_lowhumidity, station.monthlyrecarray[WeatherStation.AT_lowhumidity, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highBarometerVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighPress, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighPress, month].timestamp, month);
							break;
						case "highBarometerTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighPress, station.monthlyrecarray[WeatherStation.AT_HighPress, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowBarometerVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LowPress, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LowPress, month].timestamp, month);
							break;
						case "lowBarometerTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_LowPress, station.monthlyrecarray[WeatherStation.AT_LowPress, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highGustVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighGust, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighGust, month].timestamp, month);
							break;
						case "highGustTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighGust, station.monthlyrecarray[WeatherStation.AT_HighGust, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highWindVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighWind, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighWind, month].timestamp, month);
							break;
						case "highWindTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighWind, station.monthlyrecarray[WeatherStation.AT_HighWind, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highWindRunVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighWindrun, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighWindrun, month].timestamp, month);
							break;
						case "highWindRunTime":
							station.SetMonthlyAlltime(WeatherStation.AT_HighWindrun, station.monthlyrecarray[WeatherStation.AT_HighWindrun, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "highRainRateVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HighRainRate, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HighRainRate, month].timestamp, month);
							break;
						case "highRainRateTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HighRainRate, station.monthlyrecarray[WeatherStation.AT_HighRainRate, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highHourlyRainVal":
							station.SetMonthlyAlltime(WeatherStation.AT_HourlyRain, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_HourlyRain, month].timestamp, month);
							break;
						case "highHourlyRainTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_HourlyRain, station.monthlyrecarray[WeatherStation.AT_HourlyRain, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highDailyRainVal":
							station.SetMonthlyAlltime(WeatherStation.AT_DailyRain, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_DailyRain, month].timestamp, month);
							break;
						case "highDailyRainTime":
							station.SetMonthlyAlltime(WeatherStation.AT_DailyRain, station.monthlyrecarray[WeatherStation.AT_DailyRain, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "highMonthlyRainVal":
							station.SetMonthlyAlltime(WeatherStation.AT_WetMonth, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_WetMonth, month].timestamp, month);
							break;
						case "highMonthlyRainTime":
							dt = value.Split('/');
							var datstr = "01/" + dt[1] + "/" + dt[0].Substring(2, 2);
							station.SetMonthlyAlltime(WeatherStation.AT_WetMonth, station.monthlyrecarray[WeatherStation.AT_WetMonth, month].value, station.ddmmyyStrToDate(datstr), month);
							break;
						case "longestDryPeriodVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LongestDryPeriod, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LongestDryPeriod, month].timestamp, month);
							break;
						case "longestDryPeriodTime":
							station.SetMonthlyAlltime(WeatherStation.AT_LongestDryPeriod, station.monthlyrecarray[WeatherStation.AT_LongestDryPeriod, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "longestWetPeriodVal":
							station.SetMonthlyAlltime(WeatherStation.AT_LongestWetPeriod, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_LongestWetPeriod, month].timestamp, month);
							break;
						case "longestWetPeriodTime":
							station.SetMonthlyAlltime(WeatherStation.AT_LongestWetPeriod, station.monthlyrecarray[WeatherStation.AT_LongestWetPeriod, month].value, station.ddmmyyStrToDate(value), month);
							break;
						default:
							result = 0;
							break;
					}
				}
			}
			catch
			{
				result = 0;
			}
			return "{\"result\":\"" + ((result == 1) ? "Success" : "Failed") + "\"}";
		}

		internal string GetMonthlyRecData()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = new StringBuilder("{", 21000);
			for (var m = 1; m <= 12; m++)
			{
				// Records - Temperature values
				json.Append($"\"{m}-highTempVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighTemp, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowTempVal\":\"{station.monthlyrecarray[WeatherStation.AT_LowTemp, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDewPointVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighDewPoint, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDewPointVal\":\"{station.monthlyrecarray[WeatherStation.AT_LowDewpoint, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highApparentTempVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighAppTemp, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowApparentTempVal\":\"{station.monthlyrecarray[WeatherStation.AT_LowAppTemp, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighFeelsLike, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeVal\":\"{station.monthlyrecarray[WeatherStation.AT_LowFeelsLike, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHumidexVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighHumidex, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowWindChillVal\":\"{station.monthlyrecarray[WeatherStation.AT_LowChill, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHeatIndexVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighHeatIndex, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highMinTempVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighMinTemp, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowMaxTempVal\":\"{station.monthlyrecarray[WeatherStation.AT_LowMaxTemp, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighDailyTempRange, m].value.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeVal\":\"{station.monthlyrecarray[WeatherStation.AT_LowDailyTempRange, m].value.ToString(cumulus.TempFormat)}\",");
				// Records - Temperature timestamps
				json.Append($"\"{m}-highTempTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighTemp, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowTempTime\":\"{station.monthlyrecarray[WeatherStation.AT_LowTemp, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDewPointTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighDewPoint, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowDewPointTime\":\"{station.monthlyrecarray[WeatherStation.AT_LowDewpoint, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highApparentTempTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighAppTemp, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowApparentTempTime\":\"{station.monthlyrecarray[WeatherStation.AT_LowAppTemp, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighFeelsLike, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeTime\":\"{station.monthlyrecarray[WeatherStation.AT_LowFeelsLike, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHumidexTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighHumidex, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowWindChillTime\":\"{station.monthlyrecarray[WeatherStation.AT_LowChill, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHeatIndexTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighHeatIndex, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMinTempTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighMinTemp, m].timestamp.ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-lowMaxTempTime\":\"{station.monthlyrecarray[WeatherStation.AT_LowMaxTemp, m].timestamp.ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighDailyTempRange, m].timestamp.ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeTime\":\"{station.monthlyrecarray[WeatherStation.AT_LowDailyTempRange, m].timestamp.ToString(dateStampFormat)}\",");
				// Records - Humidity values
				json.Append($"\"{m}-highHumidityVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighHumidity, m].value.ToString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-lowHumidityVal\":\"{station.monthlyrecarray[WeatherStation.AT_lowhumidity, m].value.ToString(cumulus.HumFormat)}\",");
				// Records - Humidity times
				json.Append($"\"{m}-highHumidityTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighHumidity, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowHumidityTime\":\"{station.monthlyrecarray[WeatherStation.AT_lowhumidity, m].timestamp.ToString(timeStampFormat)}\",");
				// Records - Pressure values
				json.Append($"\"{m}-highBarometerVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighPress, m].value.ToString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-lowBarometerVal\":\"{station.monthlyrecarray[WeatherStation.AT_LowPress, m].value.ToString(cumulus.PressFormat)}\",");
				// Records - Pressure times
				json.Append($"\"{m}-highBarometerTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighPress, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowBarometerTime\":\"{station.monthlyrecarray[WeatherStation.AT_LowPress, m].timestamp.ToString(timeStampFormat)}\",");
				// Records - Wind values
				json.Append($"\"{m}-highGustVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighGust, m].value.ToString(cumulus.WindFormat)}\",");
				json.Append($"\"{m}-highWindVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighWind, m].value.ToString(cumulus.WindAvgFormat)}\",");
				json.Append($"\"{m}-highWindRunVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighWindrun, m].value.ToString(cumulus.WindRunFormat)}\",");
				// Records - Wind times
				json.Append($"\"{m}-highGustTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighGust, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighWind, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindRunTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighWindrun, m].timestamp.ToString(dateStampFormat)}\",");
				// Records - Rain values
				json.Append($"\"{m}-highRainRateVal\":\"{station.monthlyrecarray[WeatherStation.AT_HighRainRate, m].value.ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highHourlyRainVal\":\"{station.monthlyrecarray[WeatherStation.AT_HourlyRain, m].value.ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highDailyRainVal\":\"{station.monthlyrecarray[WeatherStation.AT_DailyRain, m].value.ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainVal\":\"{station.monthlyrecarray[WeatherStation.AT_WetMonth, m].value.ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-longestDryPeriodVal\":\"{station.monthlyrecarray[WeatherStation.AT_LongestDryPeriod, m].value:f0}\",");
				json.Append($"\"{m}-longestWetPeriodVal\":\"{station.monthlyrecarray[WeatherStation.AT_LongestWetPeriod, m].value:f0}\",");
				// Records - Rain times
				json.Append($"\"{m}-highRainRateTime\":\"{station.monthlyrecarray[WeatherStation.AT_HighRainRate, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHourlyRainTime\":\"{station.monthlyrecarray[WeatherStation.AT_HourlyRain, m].timestamp.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyRainTime\":\"{station.monthlyrecarray[WeatherStation.AT_DailyRain, m].timestamp.ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainTime\":\"{station.monthlyrecarray[WeatherStation.AT_WetMonth, m].timestamp:yyyy/MM}\",");
				json.Append($"\"{m}-longestDryPeriodTime\":\"{station.monthlyrecarray[WeatherStation.AT_LongestDryPeriod, m].timestamp.ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-longestWetPeriodTime\":\"{station.monthlyrecarray[WeatherStation.AT_LongestWetPeriod, m].timestamp.ToString(dateStampFormat)}\",");
			}
			json.Remove(json.Length - 1, 1);
			json.Append("}");

			return json.ToString();
		}

		internal string GetMonthlyRecDayFile()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var linenum = 0;
			var highTempVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowTempVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highDewPtVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowDewPtVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highAppTempVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowAppTempVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highFeelsLikeVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowFeelsLikeVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highHumidexVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowWindChillVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highHeatIndVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highMinTempVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowMaxTempVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highTempRangeVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowTempRangeVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highHumVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowHumVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highBaroVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowBaroVal = new double[] { 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999 };
			var highGustVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highWindVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highWindRunVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highRainRateVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highRainHourVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highRainDayVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highRainMonthVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var dryPeriodVal = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			var wetPeriodVal = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

			var thisDate = new DateTime(1900, 01, 01);
			var highTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highDewPtTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowDewPtTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highAppTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowAppTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highFeelsLikeTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowFeelsLikeTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highHumidexTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowWindChillTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highHeatIndTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highMinTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowMaxTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highTempRangeTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowTempRangeTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highHumTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowHumTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highBaroTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowBaroTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highGustTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highWindTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highWindRunTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highRainRateTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highRainHourTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highRainDayTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highRainMonthTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var dryPeriodTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var wetPeriodTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };

			var rainThisMonth = 0.0;
			var currentDryPeriod = 0;
			var currentWetPeriod = 0;
			var isDryNow = false;
			var thisDateDry = thisDate;
			var thisDateWet = thisDate;
			var firstEntry = true;
			var json = new StringBuilder("{", 25500);

			var rainThreshold = 0.0;
			if (cumulus.RainDayThreshold > -1)
				rainThreshold = cumulus.RainDayThreshold;

			var watch = System.Diagnostics.Stopwatch.StartNew();

			// Read the dayfile and extract the records from there
			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					var dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (var line in dayfile)
					{
						linenum++;
						var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count <= 0) continue;

						var datestr = st[0];
						var loggedDate = station.ddmmyyStrToDate(datestr);
						var monthOffset = loggedDate.Month - 1;
						double valDbl, valDbl2;

						// for the very first record we need to record the date
						if (firstEntry)
						{
							thisDate = loggedDate;
							firstEntry = false;
						}

						// This assumes the day file is in date order!
						if (thisDate.Month != loggedDate.Month)
						{
							var offset = thisDate.Month - 1;
							// monthly rain
							if (rainThisMonth > highRainMonthVal[offset])
							{
								highRainMonthVal[offset] = rainThisMonth;
								highRainMonthTime[offset] = thisDate;
							}
							// reset the date and counter for a new month
							thisDate = loggedDate;
							rainThisMonth = 0;
						}
						// hi gust
						if (double.TryParse(st[1], out valDbl) && valDbl > highGustVal[monthOffset])
						{
							highGustVal[monthOffset] = valDbl;
							highGustTime[monthOffset] = GetDateTime(loggedDate, st[3]);
						}
						// lo temp
						if (double.TryParse(st[4], out valDbl))
						{
							if (valDbl < lowTempVal[monthOffset])
							{
								lowTempVal[monthOffset] = valDbl;
								lowTempTime[monthOffset] = GetDateTime(loggedDate, st[5]);
							}
							// hi min temp
							if (valDbl > highMinTempVal[monthOffset])
							{
								highMinTempVal[monthOffset] = valDbl;
								highMinTempTime[monthOffset] = loggedDate;
							}
						}
						// hi temp
						if (double.TryParse(st[6], out valDbl))
						{
							if (valDbl > highTempVal[monthOffset])
							{
								highTempVal[monthOffset] = valDbl;
								highTempTime[monthOffset] = GetDateTime(loggedDate, st[7]);
							}
							// lo max temp
							if (valDbl < lowMaxTempVal[monthOffset])
							{
								lowMaxTempVal[monthOffset] = valDbl;
								lowMaxTempTime[monthOffset] = loggedDate;
							}
						}

						// temp ranges
						if (double.TryParse(st[6], out valDbl) && double.TryParse(st[4], out valDbl2))
						{
							// hi temp range
							if ((valDbl - valDbl2) > highTempRangeVal[monthOffset])
							{
								highTempRangeVal[monthOffset] = valDbl - valDbl2;
								highTempRangeTime[monthOffset] = loggedDate;
							}
							// lo temp range
							if ((valDbl - valDbl2) < lowTempRangeVal[monthOffset])
							{
								lowTempRangeVal[monthOffset] = valDbl - valDbl2;
								lowTempRangeTime[monthOffset] = loggedDate;
							}
						}
						// lo baro
						if (double.TryParse(st[8], out valDbl) && valDbl < lowBaroVal[monthOffset])
						{
							lowBaroVal[monthOffset] = valDbl;
							lowBaroTime[monthOffset] = GetDateTime(loggedDate, st[9]);
						}
						// hi baro
						if (double.TryParse(st[10], out valDbl) && valDbl > highBaroVal[monthOffset])
						{
							highBaroVal[monthOffset] = valDbl;
							highBaroTime[monthOffset] = GetDateTime(loggedDate, st[11]);
						}
						// hi rain rate
						if (double.TryParse(st[12], out valDbl) && valDbl > highRainRateVal[monthOffset])
						{
							highRainRateVal[monthOffset] = valDbl;
							highRainRateTime[monthOffset] = GetDateTime(loggedDate, st[13]);
						}
						if (double.TryParse(st[14], out valDbl))
						{
							// hi rain day
							if (valDbl > highRainDayVal[monthOffset])
							{
								highRainDayVal[monthOffset] = valDbl;
								highRainDayTime[monthOffset] = loggedDate;
							}

							// monthly rain
							rainThisMonth += valDbl;

							// dry/wet period
							if (valDbl > rainThreshold)
							{
								if (isDryNow)
								{
									currentWetPeriod = 1;
									isDryNow = false;
									var dryMonthOffset = thisDateWet.Month - 1;
									if (currentDryPeriod > dryPeriodVal[dryMonthOffset])
									{
										dryPeriodVal[dryMonthOffset] = currentDryPeriod;
										dryPeriodTime[dryMonthOffset] = thisDateDry;
									}
									currentDryPeriod = 0;
								}
								else
								{
									currentWetPeriod++;
									thisDateWet = loggedDate;
								}
							}
							else
							{
								if (isDryNow)
								{
									currentDryPeriod++;
									thisDateDry = loggedDate;
								}
								else
								{
									currentDryPeriod = 1;
									isDryNow = true;
									var wetMonthOffset = thisDateWet.Month - 1;
									if (currentWetPeriod > wetPeriodVal[wetMonthOffset])
									{
										wetPeriodVal[wetMonthOffset] = currentWetPeriod;
										wetPeriodTime[wetMonthOffset] = thisDateWet;
									}
									currentWetPeriod = 0;
								}
							}
						}
						// extended v????
						if (st.Count > 15)
						{
							// hi wind run
							if (double.TryParse(st[16], out valDbl) && valDbl > highWindRunVal[monthOffset])
							{
								highWindRunVal[monthOffset] = valDbl;
								highWindRunTime[monthOffset] = loggedDate;
							}
						}
						// extended v1.8.9
						if (st.Count > 17)
						{
							// hi wind
							if (double.TryParse(st[17], out valDbl) && valDbl > highWindVal[monthOffset])
							{
								highWindVal[monthOffset] = valDbl;
								highWindTime[monthOffset] = GetDateTime(loggedDate, st[18]);
							}
						}
						//extended v1.9.0
						if (st.Count > 19)
						{
							// lo humidity
							if (double.TryParse(st[19], out valDbl) && valDbl < lowHumVal[monthOffset])
							{
								lowHumVal[monthOffset] = valDbl;
								lowHumTime[monthOffset] = GetDateTime(loggedDate, st[20]);
							}
							// hi humidity
							if (double.TryParse(st[21], out valDbl) && valDbl > highHumVal[monthOffset])
							{
								highHumVal[monthOffset] = valDbl;
								highHumTime[monthOffset] = GetDateTime(loggedDate, st[22]);
							}
							// hi heat index
							if (double.TryParse(st[25], out valDbl) && valDbl > highHeatIndVal[monthOffset])
							{
								highHeatIndVal[monthOffset] = valDbl;
								highHeatIndTime[monthOffset] = GetDateTime(loggedDate, st[26]);
							}
							// hi app temp
							if (double.TryParse(st[27], out valDbl) && valDbl > highAppTempVal[monthOffset])
							{
								highAppTempVal[monthOffset] = valDbl;
								highAppTempTime[monthOffset] = GetDateTime(loggedDate, st[28]);
							}
							// lo app temp
							if (double.TryParse(st[29], out valDbl) && valDbl < lowAppTempVal[monthOffset])
							{
								lowAppTempVal[monthOffset] = valDbl;
								lowAppTempTime[monthOffset] = GetDateTime(loggedDate, st[30]);
							}
							// hi rain hour
							if (double.TryParse(st[31], out valDbl) && valDbl > highRainHourVal[monthOffset])
							{
								highRainHourVal[monthOffset] = valDbl;
								highRainHourTime[monthOffset] = GetDateTime(loggedDate, st[32]);
							}
							// lo wind chill
							if (double.TryParse(st[33], out valDbl) && valDbl < lowWindChillVal[monthOffset])
							{
								lowWindChillVal[monthOffset] = valDbl;
								lowWindChillTime[monthOffset] = GetDateTime(loggedDate, st[34]);
							}
						}
						// extended v1.9.1
						if (st.Count > 35)
						{
							// hi dewpt
							if (double.TryParse(st[35], out valDbl) && valDbl > highDewPtVal[monthOffset])
							{
								highDewPtVal[monthOffset] = valDbl;
								highDewPtTime[monthOffset] = GetDateTime(loggedDate, st[36]);
							}
							// lo dewpt
							if (double.TryParse(st[37], out valDbl) && valDbl < lowDewPtVal[monthOffset])
							{
								lowDewPtVal[monthOffset] = valDbl;
								lowDewPtTime[monthOffset] = GetDateTime(loggedDate, st[38]);
							}
						}
						// extended v3.6.0
						if (st.Count > 46)
						{
							// hi feels like
							if (double.TryParse(st[46], out valDbl) && valDbl > highFeelsLikeVal[monthOffset])
							{
								highFeelsLikeVal[monthOffset] = valDbl;
								highFeelsLikeTime[monthOffset] = GetDateTime(loggedDate, st[47]);
							}
							// lo feels like
							if (double.TryParse(st[48], out valDbl) && valDbl < lowFeelsLikeVal[monthOffset])
							{
								lowFeelsLikeVal[monthOffset] = valDbl;
								lowFeelsLikeTime[monthOffset] = GetDateTime(loggedDate, st[49]);
							}
						}
						// extended v3.7.0
						if (st.Count > 50)
						{
							// hi humidex
							if (double.TryParse(st[50], out valDbl) && valDbl > highHumidexVal[monthOffset])
							{
								highHumidexVal[monthOffset] = valDbl;
								highHumidexTime[monthOffset] = GetDateTime(loggedDate, st[51]);
							}
						}
					}

					for (var i = 0; i < 12; i++)
					{
						var m = i + 1;
						json.Append($"\"{m}-highTempValDayfile\":\"{highTempVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-highTempTimeDayfile\":\"{highTempTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-lowTempValDayfile\":\"{lowTempVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-lowTempTimeDayfile\":\"{lowTempTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highDewPointValDayfile\":\"{highDewPtVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-highDewPointTimeDayfile\":\"{highDewPtTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-lowDewPointValDayfile\":\"{lowDewPtVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-lowDewPointTimeDayfile\":\"{lowDewPtTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highApparentTempValDayfile\":\"{highAppTempVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-highApparentTempTimeDayfile\":\"{highAppTempTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-lowApparentTempValDayfile\":\"{lowAppTempVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-lowApparentTempTimeDayfile\":\"{lowAppTempTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highFeelsLikeValDayfile\":\"{highFeelsLikeVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-highFeelsLikeTimeDayfile\":\"{highFeelsLikeTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-lowFeelsLikeValDayfile\":\"{lowFeelsLikeVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-lowFeelsLikeTimeDayfile\":\"{lowFeelsLikeTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highHumidexValDayfile\":\"{highHumidexVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-highHumidexTimeDayfile\":\"{highHumidexTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-lowWindChillValDayfile\":\"{lowWindChillVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-lowWindChillTimeDayfile\":\"{lowWindChillTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highHeatIndexValDayfile\":\"{highHeatIndVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-highHeatIndexTimeDayfile\":\"{highHeatIndTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highMinTempValDayfile\":\"{highMinTempVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-highMinTempTimeDayfile\":\"{highMinTempTime[i].ToString(dateStampFormat)}\",");
						json.Append($"\"{m}-lowMaxTempValDayfile\":\"{lowMaxTempVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-lowMaxTempTimeDayfile\":\"{lowMaxTempTime[i].ToString(dateStampFormat)}\",");
						json.Append($"\"{m}-highDailyTempRangeValDayfile\":\"{highTempRangeVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-highDailyTempRangeTimeDayfile\":\"{highTempRangeTime[i].ToString(dateStampFormat)}\",");
						json.Append($"\"{m}-lowDailyTempRangeValDayfile\":\"{lowTempRangeVal[i].ToString(cumulus.TempFormat)}\",");
						json.Append($"\"{m}-lowDailyTempRangeTimeDayfile\":\"{lowTempRangeTime[i].ToString(dateStampFormat)}\",");
						json.Append($"\"{m}-highHumidityValDayfile\":\"{highHumVal[i].ToString(cumulus.HumFormat)}\",");
						json.Append($"\"{m}-highHumidityTimeDayfile\":\"{highHumTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-lowHumidityValDayfile\":\"{lowHumVal[i].ToString(cumulus.HumFormat)}\",");
						json.Append($"\"{m}-lowHumidityTimeDayfile\":\"{lowHumTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highBarometerValDayfile\":\"{highBaroVal[i].ToString(cumulus.PressFormat)}\",");
						json.Append($"\"{m}-highBarometerTimeDayfile\":\"{highBaroTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-lowBarometerValDayfile\":\"{lowBaroVal[i].ToString(cumulus.PressFormat)}\",");
						json.Append($"\"{m}-lowBarometerTimeDayfile\":\"{lowBaroTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highGustValDayfile\":\"{highGustVal[i].ToString(cumulus.WindFormat)}\",");
						json.Append($"\"{m}-highGustTimeDayfile\":\"{highGustTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highWindValDayfile\":\"{highWindVal[i].ToString(cumulus.WindAvgFormat)}\",");
						json.Append($"\"{m}-highWindTimeDayfile\":\"{highWindTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highWindRunValDayfile\":\"{highWindRunVal[i].ToString(cumulus.WindRunFormat)}\",");
						json.Append($"\"{m}-highWindRunTimeDayfile\":\"{highWindRunTime[i].ToString(dateStampFormat)}\",");
						json.Append($"\"{m}-highRainRateValDayfile\":\"{highRainRateVal[i].ToString(cumulus.RainFormat)}\",");
						json.Append($"\"{m}-highRainRateTimeDayfile\":\"{highRainRateTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highHourlyRainValDayfile\":\"{highRainHourVal[i].ToString(cumulus.RainFormat)}\",");
						json.Append($"\"{m}-highHourlyRainTimeDayfile\":\"{highRainHourTime[i].ToString(timeStampFormat)}\",");
						json.Append($"\"{m}-highDailyRainValDayfile\":\"{highRainDayVal[i].ToString(cumulus.RainFormat)}\",");
						json.Append($"\"{m}-highDailyRainTimeDayfile\":\"{highRainDayTime[i].ToString(dateStampFormat)}\",");
						json.Append($"\"{m}-highMonthlyRainValDayfile\":\"{highRainMonthVal[i].ToString(cumulus.RainFormat)}\",");
						json.Append($"\"{m}-highMonthlyRainTimeDayfile\":\"{highRainMonthTime[i]:yyyy/MM}\",");
						json.Append($"\"{m}-longestDryPeriodValDayfile\":\"{dryPeriodVal[i]}\",");
						json.Append($"\"{m}-longestDryPeriodTimeDayfile\":\"{dryPeriodTime[i].ToString(dateStampFormat)}\",");
						json.Append($"\"{m}-longestWetPeriodValDayfile\":\"{wetPeriodVal[i]}\",");
						json.Append($"\"{m}-longestWetPeriodTimeDayfile\":\"{wetPeriodTime[i].ToString(dateStampFormat)}\",");
					}
					json.Remove(json.Length - 1, 1);
					json.Append("}");
				}
				catch (Exception e)
				{
					cumulus.LogMessage($"Error on line {linenum } of {cumulus.DayFile}: {e.Message}");
				}
			}
			else
			{
				cumulus.LogMessage($"Error failed to find day file: {cumulus.DayFile}");
			}

			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"Monthly recs editor Dayfile load = {elapsed} ms");

			return json.ToString();
		}

		internal string GetMonthlyRecLogFile()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = new StringBuilder("{", 25500);
			var datefrom = DateTime.Parse(cumulus.RecordsBeganDate);
			datefrom = new DateTime(datefrom.Year, datefrom.Month, 1, 0, 0, 0);
			var dateto = DateTime.Now;
			dateto = new DateTime(dateto.Year, dateto.Month, 1, 0, 0, 0);
			var filedate = datefrom;

			var logFile = cumulus.GetLogFileName(filedate);
			var started = false;
			var finished = false;
			var lastentrydate = datefrom;

			var currentDay = datefrom;
			double dayHighTemp = -999;
			double dayLowTemp = 999;
			double dayWindRun = 0;
			double dayRain = 0;

			var isDryNow = false;
			var currentDryPeriod = 0;
			var currentWetPeriod = 0;

			var rainThreshold = 0.0;
			if (cumulus.RainDayThreshold > -1)
				rainThreshold = cumulus.RainDayThreshold;

			var highTempVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowTempVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highDewPtVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowDewPtVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highAppTempVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowAppTempVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highFeelsLikeVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowFeelsLikeVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highHumidexVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowWindChillVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highHeatIndVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highMinTempVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowMaxTempVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highTempRangeVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowTempRangeVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highHumVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowHumVal = new double[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };
			var highBaroVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var lowBaroVal = new double[] { 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999 };
			var highGustVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highWindVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highWindRunVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highRainRateVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highRainHourVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highRainDayVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var highRainMonthVal = new double[] { -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999, -999 };
			var dryPeriodVal = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			var wetPeriodVal = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

			var thisDate = new DateTime(1900, 01, 01);
			var highTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highDewPtTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowDewPtTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highAppTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowAppTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highFeelsLikeTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowFeelsLikeTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highHumidexTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowWindChillTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highHeatIndTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highMinTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowMaxTempTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highTempRangeTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowTempRangeTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highHumTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowHumTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highBaroTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var lowBaroTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highGustTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highWindTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highWindRunTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highRainRateTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highRainHourTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highRainDayTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var highRainMonthTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var dryPeriodTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };
			var wetPeriodTime = new[] { thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate, thisDate };

			var thisDateDry = thisDate;
			var thisDateWet = thisDate;

			var monthlyRain = 0.0;

			var totalRainfall = 0.0;

			hourRainLog.Clear();

			var watch = System.Diagnostics.Stopwatch.StartNew();

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetMonthlyTimeRecLogFile: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);
						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;
							//var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
							// Regex is very expensive, let's assume the separator is always a single character
							var st = new List<string>(line.Split((CultureInfo.CurrentCulture.TextInfo.ListSeparator)[0]));
							var entrydate = station.ddmmyyhhmmStrToDate(st[0], st[1]);
							// We need to work in meto dates not clock dates for day hi/lows
							var metoDate = entrydate.AddHours(cumulus.GetHourInc());
							var monthOffset = metoDate.Month - 1;
							double valDbl;

							if (!started)
							{
								lastentrydate = entrydate;
								currentDay = metoDate;
								started = true;
							}

							var outsidetemp = double.Parse(st[2]);
							var hum = double.Parse(st[3]);
							var dewpoint = double.Parse(st[4]);
							var speed = double.Parse(st[5]);
							var gust = double.Parse(st[6]);
							var rainrate = double.Parse(st[8]);
							var raintoday = double.Parse(st[9]);
							var pressure = double.Parse(st[10]);

							// extended v1.9.1
							if (st.Count > 19)
							{
								// low chill
								if (double.TryParse(st[15], out valDbl) && valDbl < lowWindChillVal[monthOffset])
								{
									lowWindChillVal[monthOffset] = valDbl;
									lowWindChillTime[monthOffset] = entrydate;
								}
								// hi heat
								if (double.TryParse(st[16], out valDbl) && valDbl > highHeatIndVal[monthOffset])
								{
									highHeatIndVal[monthOffset] = valDbl;
									highHeatIndTime[monthOffset] = entrydate;
								}
								if (double.TryParse(st[21], out valDbl))
								{
									// hi appt
									if (valDbl > highAppTempVal[monthOffset])
									{
										highAppTempVal[monthOffset] = valDbl;
										highAppTempTime[monthOffset] = entrydate;
									}
									// lo appt
									if (valDbl < lowAppTempVal[monthOffset])
									{
										lowAppTempVal[monthOffset] = valDbl;
										lowAppTempTime[monthOffset] = entrydate;
									}
								}
							}
							// extended v3.6.0
							if (st.Count > 27)
							{
								if (double.TryParse(st[27], out valDbl))
								{
									// hi feels like
									if (valDbl > highFeelsLikeVal[monthOffset])
									{
										highFeelsLikeVal[monthOffset] = valDbl;
										highFeelsLikeTime[monthOffset] = entrydate;
									}
									// lo feels like
									if (valDbl < lowFeelsLikeVal[monthOffset])
									{
										lowFeelsLikeVal[monthOffset] = valDbl;
										lowFeelsLikeTime[monthOffset] = entrydate;
									}
								}
							}
							// extended v3.7.0
							if (st.Count > 28)
							{
								if (double.TryParse(st[28], out valDbl))
								{
									// hi humidex
									if (valDbl > highHumidexVal[monthOffset])
									{
										highHumidexVal[monthOffset] = valDbl;
										highHumidexTime[monthOffset] = entrydate;
									}
								}
							}

							// hi temp
							if (outsidetemp > highTempVal[monthOffset])
							{
								highTempVal[monthOffset] = outsidetemp;
								highTempTime[monthOffset] = entrydate;
							}
							// lo temp
							if (outsidetemp < lowTempVal[monthOffset])
							{
								lowTempVal[monthOffset] = outsidetemp;
								lowTempTime[monthOffset] = entrydate;
							}
							// hi dewpoint
							if (dewpoint > highDewPtVal[monthOffset])
							{
								highDewPtVal[monthOffset] = dewpoint;
								highDewPtTime[monthOffset] = entrydate;
							}
							// low dewpoint
							if (dewpoint < lowDewPtVal[monthOffset])
							{
								lowDewPtVal[monthOffset] = dewpoint;
								lowDewPtTime[monthOffset] = entrydate;
							}
							// hi hum
							if (hum > highHumVal[monthOffset])
							{
								highHumVal[monthOffset] = hum;
								highHumTime[monthOffset] = entrydate;
							}
							// lo hum
							if (hum < lowHumVal[monthOffset])
							{
								lowHumVal[monthOffset] = hum;
								lowHumTime[monthOffset] = entrydate;
							}
							// hi baro
							if (pressure > highBaroVal[monthOffset])
							{
								highBaroVal[monthOffset] = pressure;
								highBaroTime[monthOffset] = entrydate;
							}
							// lo hum
							if (pressure < lowBaroVal[monthOffset])
							{
								lowBaroVal[monthOffset] = pressure;
								lowBaroTime[monthOffset] = entrydate;
							}
							// hi gust
							if (gust > highGustVal[monthOffset])
							{
								highGustVal[monthOffset] = gust;
								highGustTime[monthOffset] = entrydate;
							}
							// hi wind
							if (speed > highWindVal[monthOffset])
							{
								highWindVal[monthOffset] = speed;
								highWindTime[monthOffset] = entrydate;
							}
							// hi rain rate
							if (rainrate > highRainRateVal[monthOffset])
							{
								highRainRateVal[monthOffset] = rainrate;
								highRainRateTime[monthOffset] = entrydate;
							}

							// same meto day
							if (currentDay.Day == metoDate.Day && currentDay.Month == metoDate.Month && currentDay.Year == metoDate.Year)
							{
								if (outsidetemp > dayHighTemp)
									dayHighTemp = outsidetemp;

								if (outsidetemp < dayLowTemp)
									dayLowTemp = outsidetemp;

								if (dayRain < raintoday)
									dayRain = raintoday;

								dayWindRun += entrydate.Subtract(lastentrydate).TotalHours * speed;
							}
							else // new meto day
							{
								var lastEntryMonthOffset = currentDay.Month - 1;
								if (dayHighTemp < lowMaxTempVal[lastEntryMonthOffset])
								{
									lowMaxTempVal[lastEntryMonthOffset] = dayHighTemp;
									lowMaxTempTime[lastEntryMonthOffset] = currentDay;
								}
								if (dayLowTemp > highMinTempVal[lastEntryMonthOffset])
								{
									highMinTempVal[lastEntryMonthOffset] = dayLowTemp;
									highMinTempTime[lastEntryMonthOffset] = currentDay;
								}
								if (dayHighTemp - dayLowTemp > highTempRangeVal[lastEntryMonthOffset])
								{
									highTempRangeVal[lastEntryMonthOffset] = dayHighTemp - dayLowTemp;
									highTempRangeTime[lastEntryMonthOffset] = currentDay;
								}
								if (dayHighTemp - dayLowTemp < lowTempRangeVal[lastEntryMonthOffset])
								{
									lowTempRangeVal[lastEntryMonthOffset] = dayHighTemp - dayLowTemp;
									lowTempRangeTime[lastEntryMonthOffset] = currentDay;
								}
								if (dayWindRun > highWindRunVal[lastEntryMonthOffset])
								{
									highWindRunVal[lastEntryMonthOffset] = dayWindRun;
									highWindRunTime[lastEntryMonthOffset] = currentDay;
								}
								if (dayRain > highRainDayVal[lastEntryMonthOffset])
								{
									highRainDayVal[lastEntryMonthOffset] = dayRain;
									highRainDayTime[lastEntryMonthOffset] = currentDay;
								}

								// dry/wet period
								if (dayRain > rainThreshold)
								{
									if (isDryNow)
									{
										currentWetPeriod = 1;
										isDryNow = false;
										if (currentDryPeriod > dryPeriodVal[monthOffset])
										{
											dryPeriodVal[monthOffset] = currentDryPeriod;
											dryPeriodTime[monthOffset] = thisDateDry;
										}
										currentDryPeriod = 0;
									}
									else
									{
										currentWetPeriod++;
										thisDateWet = currentDay;
									}
								}
								else
								{
									if (isDryNow)
									{
										currentDryPeriod++;
										thisDateDry = currentDay;
									}
									else
									{
										currentDryPeriod = 1;
										isDryNow = true;
										if (currentWetPeriod > wetPeriodVal[monthOffset])
										{
											wetPeriodVal[monthOffset] = currentWetPeriod;
											wetPeriodTime[monthOffset] = thisDateWet;
										}
										currentWetPeriod = 0;
									}
								}

								// new month ?
								if (currentDay.Month != metoDate.Month)
								{
									monthlyRain += dayRain;
									var offset = currentDay.Month - 1;
									if (monthlyRain > highRainMonthVal[offset])
									{
										highRainMonthVal[offset] = monthlyRain;
										highRainMonthTime[offset] = currentDay;
									}
									monthlyRain = 0.0;
								}
								else
								{
									monthlyRain += dayRain;
								}

								currentDay = metoDate;
								dayHighTemp = outsidetemp;
								dayLowTemp = outsidetemp;
								dayWindRun = 0.0;
								totalRainfall += dayRain;
								dayRain = 0.0;
							}

							// hourly rain
							/*
								* need to track what the rainfall has been in the last rolling hour
								* across day rollovers where the count resets
								*/
							AddLastHourRainEntry(entrydate, totalRainfall + dayRain);
							RemoveOldRainData(entrydate);

							var rainThisHour = hourRainLog.Last().Raincounter - hourRainLog.First().Raincounter;
							if (rainThisHour > highRainHourVal[monthOffset])
							{
								highRainHourVal[monthOffset] = rainThisHour;
								highRainHourTime[monthOffset] = entrydate;
							}

							lastentrydate = entrydate;
							//lastRainMidnight = rainMidnight;
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetMonthlyRecLogFile: Log file  not found - {logFile}");
				}
				if (filedate >= dateto)
				{
					finished = true;
					cumulus.LogDebugMessage("GetMonthlyRecLogFile: Finished processing the log files");
				}
				else
				{
					cumulus.LogDebugMessage($"GetMonthlyRecLogFile: Finished processing log file - {logFile}");
					filedate = filedate.AddMonths(1);
					logFile = cumulus.GetLogFileName(filedate);
				}
			}
			for (var i = 0; i < 12; i++)
			{
				var m = i + 1;
				json.Append($"\"{m}-highTempValLogfile\":\"{highTempVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highTempTimeLogfile\":\"{highTempTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowTempValLogfile\":\"{lowTempVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowTempTimeLogfile\":\"{lowTempTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDewPointValLogfile\":\"{highDewPtVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDewPointTimeLogfile\":\"{highDewPtTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowDewPointValLogfile\":\"{lowDewPtVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDewPointTimeLogfile\":\"{lowDewPtTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highApparentTempValLogfile\":\"{highAppTempVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highApparentTempTimeLogfile\":\"{highAppTempTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowApparentTempValLogfile\":\"{lowAppTempVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowApparentTempTimeLogfile\":\"{lowAppTempTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeValLogfile\":\"{highFeelsLikeVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeTimeLogfile\":\"{highFeelsLikeTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeValLogfile\":\"{lowFeelsLikeVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeTimeLogfile\":\"{lowFeelsLikeTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHumidexValLogfile\":\"{highHumidexVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHumidexTimeLogfile\":\"{highHumidexTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowWindChillValLogfile\":\"{lowWindChillVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowWindChillTimeLogfile\":\"{lowWindChillTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHeatIndexValLogfile\":\"{highHeatIndVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHeatIndexTimeLogfile\":\"{highHeatIndTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMinTempValLogfile\":\"{highMinTempVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highMinTempTimeLogfile\":\"{highMinTempTime[i].ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-lowMaxTempValLogfile\":\"{lowMaxTempVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowMaxTempTimeLogfile\":\"{lowMaxTempTime[i].ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeValLogfile\":\"{highTempRangeVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeTimeLogfile\":\"{highTempRangeTime[i].ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeValLogfile\":\"{lowTempRangeVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeTimeLogfile\":\"{lowTempRangeTime[i].ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-highHumidityValLogfile\":\"{highHumVal[i].ToString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-highHumidityTimeLogfile\":\"{highHumTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowHumidityValLogfile\":\"{lowHumVal[i].ToString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-lowHumidityTimeLogfile\":\"{lowHumTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highBarometerValLogfile\":\"{highBaroVal[i].ToString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-highBarometerTimeLogfile\":\"{highBaroTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowBarometerValLogfile\":\"{lowBaroVal[i].ToString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-lowBarometerTimeLogfile\":\"{lowBaroTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highGustValLogfile\":\"{highGustVal[i].ToString(cumulus.WindFormat)}\",");
				json.Append($"\"{m}-highGustTimeLogfile\":\"{highGustTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindValLogfile\":\"{highWindVal[i].ToString(cumulus.WindAvgFormat)}\",");
				json.Append($"\"{m}-highWindTimeLogfile\":\"{highWindTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindRunValLogfile\":\"{highWindRunVal[i].ToString(cumulus.WindRunFormat)}\",");
				json.Append($"\"{m}-highWindRunTimeLogfile\":\"{highWindRunTime[i].ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-highRainRateValLogfile\":\"{highRainRateVal[i].ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highRainRateTimeLogfile\":\"{highRainRateTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHourlyRainValLogfile\":\"{highRainHourVal[i].ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highHourlyRainTimeLogfile\":\"{highRainHourTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyRainValLogfile\":\"{highRainDayVal[i].ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highDailyRainTimeLogfile\":\"{highRainDayTime[i].ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainValLogfile\":\"{highRainMonthVal[i].ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainTimeLogfile\":\"{highRainMonthTime[i]:yyyy/MM}\",");
				json.Append($"\"{m}-longestDryPeriodValLogfile\":\"{dryPeriodVal[i]}\",");
				json.Append($"\"{m}-longestDryPeriodTimeLogfile\":\"{dryPeriodTime[i].ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-longestWetPeriodValLogfile\":\"{wetPeriodVal[i]}\",");
				json.Append($"\"{m}-longestWetPeriodTimeLogfile\":\"{wetPeriodTime[i].ToString(dateStampFormat)}\",");
			}

			json.Remove(json.Length - 1, 1);
			json.Append("}");

			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"Monthly recs editor Logfiles load = {elapsed} ms");

			return json.ToString();
		}

		internal string GetThisMonthRecData()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = new StringBuilder("{", 1700);
			// Records - Temperature
			json.Append($"\"highTempVal\":\"{station.HighTempThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTime\":\"{station.HighTempThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowTempVal\":\"{station.LowTempThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.LowTempThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.HighDewpointThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.HighDewpointThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.LowDewpointThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.LowDewpointThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.HighAppTempThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.HighAppTempThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.LowAppTempThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.LowAppTempThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.HighFeelsLikeThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.HighFeelsLikeThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.LowFeelsLikeThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.LowFeelsLikeThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.HighHumidexThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.HighHumidexThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.LowWindChillThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.LowWindChillThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.HighHeatIndexThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.HighHeatIndexThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.HighMinTempThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.HighMinTempThisMonthTS.ToString(dateStampFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.LowMaxTempThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.LowMaxTempThisMonthTS.ToString(dateStampFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.HighDailyTempRangeThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.HighDailyTempRangeThisMonthTS.ToString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.LowDailyTempRangeThisMonth.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.LowDailyTempRangeThisMonthTS.ToString(dateStampFormat)}\",");
			// Records - Humidty
			json.Append($"\"highHumidityVal\":\"{station.HighHumidityThisMonth.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTime\":\"{station.HighHumidityThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.LowHumidityThisMonth.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.LowHumidityThisMonthTS.ToString(timeStampFormat)}\",");
			// Records - Pressure
			json.Append($"\"highBarometerVal\":\"{station.HighPressThisMonth.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTime\":\"{station.HighPressThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.LowPressThisMonth.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.LowPressThisMonthTS.ToString(timeStampFormat)}\",");
			// Records - Wind
			json.Append($"\"highGustVal\":\"{station.HighGustThisMonth.ToString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTime\":\"{station.HighGustThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.HighWindThisMonth.ToString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.HighWindThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.HighDailyWindrunThisMonth.ToString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.HighDailyWindrunThisMonthTS.ToString(dateStampFormat)}\",");
			// Records - Rain
			json.Append($"\"highRainRateVal\":\"{station.HighRainThisMonth.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTime\":\"{station.HighRainThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.HighHourlyRainThisMonth.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.HighHourlyRainThisMonthTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.HighDailyRainThisMonth.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.HighDailyRainThisMonthTS.ToString(dateStampFormat)}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.LongestDryPeriodThisMonth:D}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.LongestDryPeriodThisMonthTS.ToString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.LongestWetPeriodThisMonth:D}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.LongestWetPeriodThisMonthTS.ToString(dateStampFormat)}\"");

			json.Append("}");

			return json.ToString();
		}

		internal string EditThisMonthRecs(IHttpContext context)
		{
			var request = context.Request;
			string text;

			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = Uri.UnescapeDataString(reader.ReadToEnd());
			}
			// Eg "name=highTempValvalue=134.6&pk=1"
			var newData = text.Split('&');
			var field = newData[0].Split('=')[1];
			var value = newData[1].Split('=')[1];
			var result = 1;
			try
			{
				string[] dt;
				switch (field)
				{
					case "highTempVal":
						station.HighTempThisMonth = double.Parse(value);
						break;
					case "highTempTime":
						dt = value.Split('+');
						station.HighTempThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowTempVal":
						station.LowTempThisMonth = double.Parse(value);
						break;
					case "lowTempTime":
						dt = value.Split('+');
						station.LowTempThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDewPointVal":
						station.HighDewpointThisMonth = double.Parse(value);
						break;
					case "highDewPointTime":
						dt = value.Split('+');
						station.HighDewpointThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowDewPointVal":
						station.LowDewpointThisMonth = double.Parse(value);
						break;
					case "lowDewPointTime":
						dt = value.Split('+');
						station.LowDewpointThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highApparentTempVal":
						station.HighAppTempThisMonth = double.Parse(value);
						break;
					case "highApparentTempTime":
						dt = value.Split('+');
						station.HighAppTempThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowApparentTempVal":
						station.LowAppTempThisMonth = double.Parse(value);
						break;
					case "lowApparentTempTime":
						dt = value.Split('+');
						station.LowAppTempThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highFeelsLikeVal":
						station.HighFeelsLikeThisMonth = double.Parse(value);
						break;
					case "highFeelsLikeTime":
						dt = value.Split('+');
						station.HighFeelsLikeThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowFeelsLikeVal":
						station.LowFeelsLikeThisMonth = double.Parse(value);
						break;
					case "lowFeelsLikeTime":
						dt = value.Split('+');
						station.LowFeelsLikeThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHumidexVal":
						station.HighHumidexThisMonth = double.Parse(value);
						break;
					case "highHumidexTime":
						dt = value.Split('+');
						station.HighHumidexThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowWindChillVal":
						station.LowWindChillThisMonth = double.Parse(value);
						break;
					case "lowWindChillTime":
						dt = value.Split('+');
						station.LowWindChillThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHeatIndexVal":
						station.HighHeatIndexThisMonth = double.Parse(value);
						break;
					case "highHeatIndexTime":
						dt = value.Split('+');
						station.HighHeatIndexThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highMinTempVal":
						station.HighMinTempThisMonth = double.Parse(value);
						break;
					case "highMinTempTime":
						station.HighMinTempThisMonthTS = station.ddmmyyStrToDate(value);
						break;
					case "lowMaxTempVal":
						station.LowMaxTempThisMonth = double.Parse(value);
						break;
					case "lowMaxTempTime":
						station.LowMaxTempThisMonthTS = station.ddmmyyStrToDate(value);
						break;
					case "highDailyTempRangeVal":
						station.HighDailyTempRangeThisMonth = double.Parse(value);
						break;
					case "highDailyTempRangeTime":
						station.HighDailyTempRangeThisMonthTS = station.ddmmyyStrToDate(value);
						break;
					case "lowDailyTempRangeVal":
						station.LowDailyTempRangeThisMonth = double.Parse(value);
						break;
					case "lowDailyTempRangeTime":
						station.LowDailyTempRangeThisMonthTS = station.ddmmyyStrToDate(value);
						break;
					case "highHumidityVal":
						station.HighHumidityThisMonth = int.Parse(value);
						break;
					case "highHumidityTime":
						dt = value.Split('+');
						station.HighHumidityThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowHumidityVal":
						station.LowHumidityThisMonth = int.Parse(value);
						break;
					case "lowHumidityTime":
						dt = value.Split('+');
						station.LowHumidityThisMonthTS =  station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highBarometerVal":
						station.HighPressThisMonth = double.Parse(value);
						break;
					case "highBarometerTime":
						dt = value.Split('+');
						station.HighPressThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowBarometerVal":
						station.LowPressThisMonth = double.Parse(value);
						break;
					case "lowBarometerTime":
						dt = value.Split('+');
						station.LowPressThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highGustVal":
						station.HighGustThisMonth = double.Parse(value);
						break;
					case "highGustTime":
						dt = value.Split('+');
						station.HighGustThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highWindVal":
						station.HighWindThisMonth = double.Parse(value);
						break;
					case "highWindTime":
						dt = value.Split('+');
						station.HighWindThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highWindRunVal":
						station.HighDailyWindrunThisMonth = double.Parse(value);
						break;
					case "highWindRunTime":
						station.HighDailyWindrunThisMonthTS = station.ddmmyyStrToDate(value);
						break;
					case "highRainRateVal":
						station.HighRainThisMonth = double.Parse(value);
						break;
					case "highRainRateTime":
						dt = value.Split('+');
						station.HighRainThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHourlyRainVal":
						station.HighHourlyRainThisMonth = double.Parse(value);
						break;
					case "highHourlyRainTime":
						dt = value.Split('+');
						station.HighHourlyRainThisMonthTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDailyRainVal":
						station.HighDailyRainThisMonth = double.Parse(value);
						break;
					case "highDailyRainTime":
						station.HighDailyRainThisMonthTS = station.ddmmyyStrToDate(value);
						break;
					case "longestDryPeriodVal":
						station.LongestDryPeriodThisMonth = int.Parse(value);
						break;
					case "longestDryPeriodTime":
						station.LongestDryPeriodThisMonthTS = station.ddmmyyStrToDate(value);
						break;
					case "longestWetPeriodVal":
						station.LongestWetPeriodThisMonth = int.Parse(value);
						break;
					case "longestWetPeriodTime":
						station.LongestWetPeriodThisMonthTS = station.ddmmyyStrToDate(value);
						break;
					default:
						result = 0;
						break;
				}
				station.WriteMonthIniFile();
			}
			catch
			{
				result = 0;
			}
			return $"{{\"result\":\"{((result == 1) ? "Success" : "Failed")}\"}}";
		}

		internal string GetThisYearRecData()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = new StringBuilder("{", 1800);
			// Records - Temperature
			json.Append($"\"highTempVal\":\"{station.HighTempThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTime\":\"{station.HighTempThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowTempVal\":\"{station.LowTempThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.LowTempThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.HighDewpointThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.HighDewpointThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.LowDewpointThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.LowDewpointThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.HighAppTempThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.HighAppTempThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.LowAppTempThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.LowAppTempThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.HighFeelsLikeThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.HighFeelsLikeThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.LowFeelsLikeThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.LowFeelsLikeThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.HighHumidexThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.HighHumidexThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.LowWindChillThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.LowWindChillThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.HighHeatIndexThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.HighHeatIndexThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.HighMinTempThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.HighMinTempThisYearTS.ToString(dateStampFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.LowMaxTempThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.LowMaxTempThisYearTS.ToString(dateStampFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.HighDailyTempRangeThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.HighDailyTempRangeThisYearTS.ToString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.LowDailyTempRangeThisYear.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.LowDailyTempRangeThisYearTS.ToString(dateStampFormat)}\",");
			// Records - Humidty
			json.Append($"\"highHumidityVal\":\"{station.HighHumidityThisYear.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTime\":\"{station.HighHumidityThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.LowHumidityThisYear.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.LowHumidityThisYearTS.ToString(timeStampFormat)}\",");
			// Records - Pressure
			json.Append($"\"highBarometerVal\":\"{station.HighPressThisYear.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTime\":\"{station.HighPressThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.LowPressThisYear.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.LowPressThisYearTS.ToString(timeStampFormat)}\",");
			// Records - Wind
			json.Append($"\"highGustVal\":\"{station.HighGustThisYear.ToString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTime\":\"{station.HighGustThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.HighWindThisYear.ToString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.HighWindThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.HighDailyWindrunThisYear.ToString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.HighDailyWindrunThisYearTS.ToString(dateStampFormat)}\",");
			// Records - Rain
			json.Append($"\"highRainRateVal\":\"{station.HighRainThisYear.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTime\":\"{station.HighRainThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.HighHourlyRainThisYear.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.HighHourlyRainThisYearTS.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.HighDailyRainThisYear.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.HighDailyRainThisYearTS.ToString(dateStampFormat)}\",");
			json.Append($"\"highMonthlyRainVal\":\"{station.HighMonthlyRainThisYear.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highMonthlyRainTime\":\"{station.HighMonthlyRainThisYearTS:yyyy/MM}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.LongestDryPeriodThisYear:D}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.LongestDryPeriodThisYearTS.ToString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.LongestWetPeriodThisYear:D}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.LongestWetPeriodThisYearTS.ToString(dateStampFormat)}\"");

			json.Append("}");

			return json.ToString();
		}

		internal string EditThisYearRecs(IHttpContext context)
		{
			var request = context.Request;
			string text;

			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = Uri.UnescapeDataString(reader.ReadToEnd());
			}
			// Eg "name=highTempValvalue=134.6&pk=1"
			var newData = text.Split('&');
			var field = newData[0].Split('=')[1];
			var value = newData[1].Split('=')[1];
			var result = 1;
			try
			{
				string[] dt;
				switch (field)
				{
					case "highTempVal":
						station.HighTempThisYear = double.Parse(value);
						break;
					case "highTempTime":
						dt = value.Split('+');
						station.HighTempThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowTempVal":
						station.LowTempThisYear = double.Parse(value);
						break;
					case "lowTempTime":
						dt = value.Split('+');
						station.LowTempThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDewPointVal":
						station.HighDewpointThisYear = double.Parse(value);
						break;
					case "highDewPointTime":
						dt = value.Split('+');
						station.HighDewpointThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowDewPointVal":
						station.LowDewpointThisYear = double.Parse(value);
						break;
					case "lowDewPointTime":
						dt = value.Split('+');
						station.LowDewpointThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highApparentTempVal":
						station.HighAppTempThisYear = double.Parse(value);
						break;
					case "highApparentTempTime":
						dt = value.Split('+');
						station.HighAppTempThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowApparentTempVal":
						station.LowAppTempThisYear = double.Parse(value);
						break;
					case "lowApparentTempTime":
						dt = value.Split('+');
						station.LowAppTempThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highFeelsLikeVal":
						station.HighFeelsLikeThisYear = double.Parse(value);
						break;
					case "highFeelsLikeTime":
						dt = value.Split('+');
						station.HighFeelsLikeThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowFeelsLikeVal":
						station.LowFeelsLikeThisYear = double.Parse(value);
						break;
					case "lowFeelsLikeTime":
						dt = value.Split('+');
						station.LowFeelsLikeThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHumidexVal":
						station.HighHumidexThisYear = double.Parse(value);
						break;
					case "highHumidexTime":
						dt = value.Split('+');
						station.HighHumidexThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowWindChillVal":
						station.LowWindChillThisYear = double.Parse(value);
						break;
					case "lowWindChillTime":
						dt = value.Split('+');
						station.LowWindChillThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHeatIndexVal":
						station.HighHeatIndexThisYear = double.Parse(value);
						break;
					case "highHeatIndexTime":
						dt = value.Split('+');
						station.HighHeatIndexThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highMinTempVal":
						station.HighMinTempThisYear = double.Parse(value);
						break;
					case "highMinTempTime":
						station.HighMinTempThisYearTS = station.ddmmyyStrToDate(value);
						break;
					case "lowMaxTempVal":
						station.LowMaxTempThisYear = double.Parse(value);
						break;
					case "lowMaxTempTime":
						station.LowMaxTempThisYearTS = station.ddmmyyStrToDate(value);
						break;
					case "highDailyTempRangeVal":
						station.HighDailyTempRangeThisYear = double.Parse(value);
						break;
					case "highDailyTempRangeTime":
						station.HighDailyTempRangeThisYearTS = station.ddmmyyStrToDate(value);
						break;
					case "lowDailyTempRangeVal":
						station.LowDailyTempRangeThisYear = double.Parse(value);
						break;
					case "lowDailyTempRangeTime":
						station.LowDailyTempRangeThisYearTS = station.ddmmyyStrToDate(value);
						break;
					case "highHumidityVal":
						station.HighHumidityThisYear = int.Parse(value);
						break;
					case "highHumidityTime":
						dt = value.Split('+');
						station.HighHumidityThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowHumidityVal":
						station.LowHumidityThisYear = int.Parse(value);
						break;
					case "lowHumidityTime":
						dt = value.Split('+');
						station.LowHumidityThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highBarometerVal":
						station.HighPressThisYear = double.Parse(value);
						break;
					case "highBarometerTime":
						dt = value.Split('+');
						station.HighPressThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowBarometerVal":
						station.LowPressThisYear = double.Parse(value);
						break;
					case "lowBarometerTime":
						dt = value.Split('+');
						station.LowPressThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highGustVal":
						station.HighGustThisYear = double.Parse(value);
						break;
					case "highGustTime":
						dt = value.Split('+');
						station.HighGustThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highWindVal":
						station.HighWindThisYear = double.Parse(value);
						break;
					case "highWindTime":
						dt = value.Split('+');
						station.HighWindThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highWindRunVal":
						station.HighDailyWindrunThisYear = double.Parse(value);
						break;
					case "highWindRunTime":
						station.HighDailyWindrunThisYearTS = station.ddmmyyStrToDate(value);
						break;
					case "highRainRateVal":
						station.HighRainThisYear = double.Parse(value);
						break;
					case "highRainRateTime":
						dt = value.Split('+');
						station.HighRainThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHourlyRainVal":
						station.HighHourlyRainThisYear = double.Parse(value);
						break;
					case "highHourlyRainTime":
						dt = value.Split('+');
						station.HighHourlyRainThisYearTS = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDailyRainVal":
						station.HighDailyRainThisYear = double.Parse(value);
						break;
					case "highDailyRainTime":
						station.HighDailyRainThisYearTS = station.ddmmyyStrToDate(value);
						break;
					case "highMonthlyRainVal":
						station.HighMonthlyRainThisYear = double.Parse(value);
						break;
					case "highMonthlyRainTime":
						var dat = value.Split('/');  // yyyy/MM
						station.HighMonthlyRainThisYearTS = new DateTime(int.Parse(dat[0]), int.Parse(dat[1]), 1);
						break;
					case "longestDryPeriodVal":
						station.LongestDryPeriodThisYear = int.Parse(value);
						break;
					case "longestDryPeriodTime":
						station.LongestDryPeriodThisYearTS = station.ddmmyyStrToDate(value);
						break;
					case "longestWetPeriodVal":
						station.LongestWetPeriodThisYear = int.Parse(value);
						break;
					case "longestWetPeriodTime":
						station.LongestWetPeriodThisYearTS = station.ddmmyyStrToDate(value);
						break;
					default:
						result = 0;
						break;
				}
				station.WriteYearIniFile();
			}
			catch
			{
				result = 0;
			}
			return $"{{\"result\":\"{((result == 1) ? "Success" : "Failed")}\"}}";
		}

		internal string GetCurrentCond()
		{
			return $"{{\"data\":\"{webtags.GetCurrCondText()}\"}}";
		}

		internal string EditCurrentCond(IHttpContext context)
		{
			var request = context.Request;
			string text;
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			var result = SetCurrCondText(text);

			return $"{{\"result\":\"{(result ? "Success" : "Failed")}\"}}";
		}

		internal string EditDayFile(IHttpContext context)
		{
			var request = context.Request;
			string text;
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			var newData = text.FromJson<DayFileEditor>();

			// read dayfile into a List
			var lines = File.ReadAllLines(cumulus.DayFile).ToList();

			var lineNum = newData.LineNum - 1; // our List is zero relative

			if (newData.Action == "Edit")
			{
				// replace the edited line
				var newLine = String.Join(cumulus.ListSeparator, newData.Data);

				lines[lineNum] = newLine;
			}
			else if (newData.Action == "Delete")
			{
				// Just double check we are deleting the correct line - see if the dates match
				var lineData = lines[lineNum].Split(cumulus.ListSeparator.ToCharArray()[0]);
				if (lineData[0] == newData.Data[0])
				{
					lines.RemoveAt(lineNum);
				}
				else
				{
					//throw("Failed, line to delete does not match the file contents");
					return "{\"result\":\"Failed, line to delete does not match the file contents\"}";
				}
			}
			else
			{
				//throw("Failed, unrecognised action");
				return "{\"result\":\"Failed, unrecognised action\"}";
			}
			// write dayfile back again
			File.WriteAllLines(cumulus.DayFile, lines);

			// return the updated record
			var rec = new List<string>(newData.Data);
			rec.Insert(0, newData.LineNum.ToString());
			return rec.ToJson();
		}

		private class DayFileEditor
		{
			public readonly string Action;
			public readonly int LineNum;
			public readonly string[] Data;

			public DayFileEditor(string action, int line, string[] data)
			{
				Action = action;
				LineNum = line;
				Data = data;
			}
		}

		internal string EditDatalog(IHttpContext context)
		{
			var request = context.Request;
			string text;
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			var newData = text.FromJson<DatalogEditor>();

			// date will (hopefully) be in format "m-yyyy" or "mm-yyyy"
			int month = Convert.ToInt32(newData.Month.Split('-')[0]);
			int year = Convert.ToInt32(newData.Month.Split('-')[1]);

			// Get a timestamp, use 15th day to avoid wrap issues
			var ts = new DateTime(year, month, 15);

			var logfile = (newData.Extra ? cumulus.GetExtraLogFileName(ts) : cumulus.GetLogFileName(ts));

			// read the log file into a List
			var lines = File.ReadAllLines(logfile).ToList();

			var lineNum = newData.LineNum - 1; // our List is zero relative

			if (newData.Action == "Edit")
			{
				// replace the edited line
				var newLine = String.Join(cumulus.ListSeparator, newData.Data);

				lines[lineNum] = newLine;
			}
			else if (newData.Action == "Delete")
			{
				// Just double check we are deleting the correct line - see if the dates match
				var lineData = lines[lineNum].Split(cumulus.ListSeparator.ToCharArray()[0]);
				if (lineData[0] == newData.Data[0])
				{
					lines.RemoveAt(lineNum);
				}
				else
				{
					//throw("Failed, line to delete does not match the file contents");
					return "{\"result\":\"Failed, line to delete does not match the file contents\"}";
				}
			}


			// write logfile back again
			File.WriteAllLines(logfile, lines);

			// return the updated record
			var rec = new List<string>(newData.Data);
			rec.Insert(0, newData.LineNum.ToString());
			return rec.ToJson();
		}

		private class DatalogEditor
		{
			public readonly string Action;
			public readonly int LineNum;
			public readonly string Month;
			public readonly bool Extra;
			public readonly string[] Data;

			public DatalogEditor(string action, int line, string month, bool extra, string[] data)
			{
				Action = action;
				LineNum = line;
				Month = month;
				Extra = extra;
				Data = data;
			}
		}


		private bool SetCurrCondText(string currCondText)
		{
			var fileName = cumulus.AppDir + "currentconditions.txt";
			try
			{
				cumulus.LogMessage("Writing current conditions to file...");

				File.WriteAllText(fileName, currCondText);
				return true;
			}
			catch (Exception e)
			{
				cumulus.LogMessage("Error writing current conditions to file - " + e.Message);
				return false;
			}
		}

		/*
		internal class JsonEditRainData
		{
			public double raintoday { get; set; }
			public double raincounter { get; set; }
			public double startofdayrain { get; set; }
			public double rainmult { get; set; }
		}
		*/

		private void AddLastHourRainEntry(DateTime ts, double rain)
		{
			var lasthourrain = new LastHourRainLog(ts, rain);

			hourRainLog.Add(lasthourrain);
		}

		private class LastHourRainLog
		{
			public readonly DateTime Timestamp;
			public readonly double Raincounter;

			public LastHourRainLog(DateTime ts, double rain)
			{
				Timestamp = ts;
				Raincounter = rain;
			}
		}

		private void RemoveOldRainData(DateTime ts)
		{
			var onehourago = ts.AddHours(-1);

			if (hourRainLog.Count <= 0) return;

			// there are entries to consider
			while ((hourRainLog.Count > 0) && (hourRainLog.First().Timestamp < onehourago))
			{
				// the oldest entry is older than 1 hour ago, delete it
				hourRainLog.RemoveAt(0);
			}
		}
	}
}
