using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unosquare.Labs.EmbedIO;

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

				var newData = JsonConvert.DeserializeObject<DiaryData>(text);

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

				var record = JsonConvert.DeserializeObject<DiaryData>(text);

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
			var json = "{\"highTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_hightemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowtemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"highDewPointVal\":\"" + station.alltimerecarray[WeatherStation.AT_highdewpoint].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowDewPointVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowdewpoint].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"highApparentTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_highapptemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowApparentTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowapptemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"highFeelsLikeVal\":\"" + station.alltimerecarray[WeatherStation.AT_highfeelslike].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowFeelsLikeVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowfeelslike].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowWindChillVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowchill].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"highHeatIndexVal\":\"" + station.alltimerecarray[WeatherStation.AT_highheatindex].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"highMinTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_highmintemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowMaxTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowmaxtemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"highDailyTempRangeVal\":\"" + station.alltimerecarray[WeatherStation.AT_highdailytemprange].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowDailyTempRangeVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowdailytemprange].value.ToString(cumulus.TempFormat) + "\",";
			// Records - Temperature timestamps
			json += "\"highTempTime\":\"" + station.alltimerecarray[WeatherStation.AT_hightemp].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"lowTempTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowtemp].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highDewPointTime\":\"" + station.alltimerecarray[WeatherStation.AT_highdewpoint].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"lowDewPointTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowdewpoint].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highApparentTempTime\":\"" + station.alltimerecarray[WeatherStation.AT_highapptemp].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"lowApparentTempTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowapptemp].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highFeelsLikeTime\":\"" + station.alltimerecarray[WeatherStation.AT_highfeelslike].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"lowFeelsLikeTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowfeelslike].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"lowWindChillTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowchill].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highHeatIndexTime\":\"" + station.alltimerecarray[WeatherStation.AT_highheatindex].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highMinTempTime\":\"" + station.alltimerecarray[WeatherStation.AT_highmintemp].timestamp.ToString(dateStampFormat) + "\",";
			json += "\"lowMaxTempTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowmaxtemp].timestamp.ToString(dateStampFormat) + "\",";
			json += "\"highDailyTempRangeTime\":\"" + station.alltimerecarray[WeatherStation.AT_highdailytemprange].timestamp.ToString(dateStampFormat) + "\",";
			json += "\"lowDailyTempRangeTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowdailytemprange].timestamp.ToString(dateStampFormat) + "\",";
			// Records - Humidity values
			json += "\"highHumidityVal\":\"" + station.alltimerecarray[WeatherStation.AT_highhumidity].value.ToString(cumulus.HumFormat) + "\",";
			json += "\"lowHumidityVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowhumidity].value.ToString(cumulus.HumFormat) + "\",";
			// Records - Humidity times
			json += "\"highHumidityTime\":\"" + station.alltimerecarray[WeatherStation.AT_highhumidity].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"lowHumidityTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowhumidity].timestamp.ToString(timeStampFormat) + "\",";
			// Records - Pressure values
			json += "\"highBarometerVal\":\"" + station.alltimerecarray[WeatherStation.AT_highpress].value.ToString(cumulus.PressFormat) + "\",";
			json += "\"lowBarometerVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowpress].value.ToString(cumulus.PressFormat) + "\",";
			// Records - Pressure times
			json += "\"highBarometerTime\":\"" + station.alltimerecarray[WeatherStation.AT_highpress].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"lowBarometerTime\":\"" + station.alltimerecarray[WeatherStation.AT_lowpress].timestamp.ToString(timeStampFormat) + "\",";
			// Records - Wind values
			json += "\"highGustVal\":\"" + station.alltimerecarray[WeatherStation.AT_highgust].value.ToString(cumulus.WindFormat) + "\",";
			json += "\"highWindVal\":\"" + station.alltimerecarray[WeatherStation.AT_highwind].value.ToString(cumulus.WindFormat) + "\",";
			json += "\"highWindRunVal\":\"" + station.alltimerecarray[WeatherStation.AT_highwindrun].value.ToString(cumulus.WindRunFormat) + "\",";
			// Records - Wind times
			json += "\"highGustTime\":\"" + station.alltimerecarray[WeatherStation.AT_highgust].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highWindTime\":\"" + station.alltimerecarray[WeatherStation.AT_highwind].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highWindRunTime\":\"" + station.alltimerecarray[WeatherStation.AT_highwindrun].timestamp.ToString(dateStampFormat) + "\",";
			// Records - Rain values
			json += "\"highRainRateVal\":\"" + station.alltimerecarray[WeatherStation.AT_highrainrate].value.ToString(cumulus.RainFormat) + "\",";
			json += "\"highHourlyRainVal\":\"" + station.alltimerecarray[WeatherStation.AT_hourlyrain].value.ToString(cumulus.RainFormat) + "\",";
			json += "\"highDailyRainVal\":\"" + station.alltimerecarray[WeatherStation.AT_dailyrain].value.ToString(cumulus.RainFormat) + "\",";
			json += "\"highMonthlyRainVal\":\"" + station.alltimerecarray[WeatherStation.AT_wetmonth].value.ToString(cumulus.RainFormat) + "\",";
			json += "\"longestDryPeriodVal\":\"" + station.alltimerecarray[WeatherStation.AT_longestdryperiod].value.ToString("f0") + "\",";
			json += "\"longestWetPeriodVal\":\"" + station.alltimerecarray[WeatherStation.AT_longestwetperiod].value.ToString("f0") + "\",";
			// Records - Rain times
			json += "\"highRainRateTime\":\"" + station.alltimerecarray[WeatherStation.AT_highrainrate].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highHourlyRainTime\":\"" + station.alltimerecarray[WeatherStation.AT_hourlyrain].timestamp.ToString(timeStampFormat) + "\",";
			json += "\"highDailyRainTime\":\"" + station.alltimerecarray[WeatherStation.AT_dailyrain].timestamp.ToString(dateStampFormat) + "\",";
			json += "\"highMonthlyRainTime\":\"" + station.alltimerecarray[WeatherStation.AT_wetmonth].timestamp.ToString("yyyy/MM") + "\",";
			json += "\"longestDryPeriodTime\":\"" + station.alltimerecarray[WeatherStation.AT_longestdryperiod].timestamp.ToString(dateStampFormat) + "\",";
			json += "\"longestWetPeriodTime\":\"" + station.alltimerecarray[WeatherStation.AT_longestwetperiod].timestamp.ToString(dateStampFormat) + "\"}";

			return json;
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
			var json = "{";

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

					json += "\"highTempValDayfile\":\"" + highTempVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"highTempTimeDayfile\":\"" + highTempTime.ToString(timeStampFormat) + "\",";
					json += "\"lowTempValDayfile\":\"" + lowTempVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"lowTempTimeDayfile\":\"" + lowTempTime.ToString(timeStampFormat) + "\",";
					json += "\"highDewPointValDayfile\":\"" + highDewPtVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"highDewPointTimeDayfile\":\"" + highDewPtTime.ToString(timeStampFormat) + "\",";
					json += "\"lowDewPointValDayfile\":\"" + lowDewPtVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"lowDewPointTimeDayfile\":\"" + lowDewPtTime.ToString(timeStampFormat) + "\",";
					json += "\"highApparentTempValDayfile\":\"" + highAppTempVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"highApparentTempTimeDayfile\":\"" + highAppTempTime.ToString(timeStampFormat) + "\",";
					json += "\"lowApparentTempValDayfile\":\"" + lowAppTempVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"lowApparentTempTimeDayfile\":\"" + lowAppTempTime.ToString(timeStampFormat) + "\",";
					json += "\"highFeelsLikeValDayfile\":\"" + highFeelsLikeVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"highFeelsLikeTimeDayfile\":\"" + highFeelsLikeTime.ToString(timeStampFormat) + "\",";
					json += "\"lowFeelsLikeValDayfile\":\"" + lowFeelsLikeVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"lowFeelsLikeTimeDayfile\":\"" + lowFeelsLikeTime.ToString(timeStampFormat) + "\",";
					json += "\"lowWindChillValDayfile\":\"" + lowWindChillVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"lowWindChillTimeDayfile\":\"" + lowWindChillTime.ToString(timeStampFormat) + "\",";
					json += "\"highHeatIndexValDayfile\":\"" + highHeatIndVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"highHeatIndexTimeDayfile\":\"" + highHeatIndTime.ToString(timeStampFormat) + "\",";
					json += "\"highMinTempValDayfile\":\"" + highMinTempVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"highMinTempTimeDayfile\":\"" + highMinTempTime.ToString(dateStampFormat) + "\",";
					json += "\"lowMaxTempValDayfile\":\"" + lowMaxTempVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"lowMaxTempTimeDayfile\":\"" + lowMaxTempTime.ToString(dateStampFormat) + "\",";
					json += "\"highDailyTempRangeValDayfile\":\"" + highTempRangeVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"highDailyTempRangeTimeDayfile\":\"" + highTempRangeTime.ToString(dateStampFormat) + "\",";
					json += "\"lowDailyTempRangeValDayfile\":\"" + lowTempRangeVal.ToString(cumulus.TempFormat) + "\",";
					json += "\"lowDailyTempRangeTimeDayfile\":\"" + lowTempRangeTime.ToString(dateStampFormat) + "\",";
					json += "\"highHumidityValDayfile\":\"" + highHumVal.ToString(cumulus.HumFormat) + "\",";
					json += "\"highHumidityTimeDayfile\":\"" + highHumTime.ToString(timeStampFormat) + "\",";
					json += "\"lowHumidityValDayfile\":\"" + lowHumVal.ToString(cumulus.HumFormat) + "\",";
					json += "\"lowHumidityTimeDayfile\":\"" + lowHumTime.ToString(timeStampFormat) + "\",";
					json += "\"highBarometerValDayfile\":\"" + highBaroVal.ToString(cumulus.PressFormat) + "\",";
					json += "\"highBarometerTimeDayfile\":\"" + highBaroTime.ToString(timeStampFormat) + "\",";
					json += "\"lowBarometerValDayfile\":\"" + lowBaroVal.ToString(cumulus.PressFormat) + "\",";
					json += "\"lowBarometerTimeDayfile\":\"" + lowBaroTime.ToString(timeStampFormat) + "\",";
					json += "\"highGustValDayfile\":\"" + highGustVal.ToString(cumulus.WindFormat) + "\",";
					json += "\"highGustTimeDayfile\":\"" + highGustTime.ToString(timeStampFormat) + "\",";
					json += "\"highWindValDayfile\":\"" + highWindVal.ToString(cumulus.WindFormat) + "\",";
					json += "\"highWindTimeDayfile\":\"" + highWindTime.ToString(timeStampFormat) + "\",";
					json += "\"highWindRunValDayfile\":\"" + highWindRunVal.ToString(cumulus.WindRunFormat) + "\",";
					json += "\"highWindRunTimeDayfile\":\"" + highWindRunTime.ToString(dateStampFormat) + "\",";
					json += "\"highRainRateValDayfile\":\"" + highRainRateVal.ToString(cumulus.RainFormat) + "\",";
					json += "\"highRainRateTimeDayfile\":\"" + highRainRateTime.ToString(timeStampFormat) + "\",";
					json += "\"highHourlyRainValDayfile\":\"" + highRainHourVal.ToString(cumulus.RainFormat) + "\",";
					json += "\"highHourlyRainTimeDayfile\":\"" + highRainHourTime.ToString(timeStampFormat) + "\",";
					json += "\"highDailyRainValDayfile\":\"" + highRainDayVal.ToString(cumulus.RainFormat) + "\",";
					json += "\"highDailyRainTimeDayfile\":\"" + highRainDayTime.ToString(dateStampFormat) + "\",";
					if (recordType != "thismonth")
					{
						json += "\"highMonthlyRainValDayfile\":\"" + highRainMonthVal.ToString(cumulus.RainFormat) + "\",";
						json += "\"highMonthlyRainTimeDayfile\":\"" + highRainMonthTime.ToString("yyyy/MM") + "\",";
					}
					json += "\"longestDryPeriodValDayfile\":\"" + dryPeriodVal.ToString() + "\",";
					json += "\"longestDryPeriodTimeDayfile\":\"" + dryPeriodTime.ToString(dateStampFormat) + "\",";
					json += "\"longestWetPeriodValDayfile\":\"" + wetPeriodVal.ToString() + "\",";
					json += "\"longestWetPeriodTimeDayfile\":\"" + wetPeriodTime.ToString(dateStampFormat) + "\"}";
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

			return json;
		}

		internal string GetRecordsLogFile(string recordType)
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = "{";
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

			json += "\"highTempValLogfile\":\"" + highTempVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"highTempTimeLogfile\":\"" + highTempTime.ToString(timeStampFormat) + "\",";
			json += "\"lowTempValLogfile\":\"" + lowTempVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowTempTimeLogfile\":\"" + lowTempTime.ToString(timeStampFormat) + "\",";
			json += "\"highDewPointValLogfile\":\"" + highDewPtVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"highDewPointTimeLogfile\":\"" + highDewPtTime.ToString(timeStampFormat) + "\",";
			json += "\"lowDewPointValLogfile\":\"" + lowDewPtVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowDewPointTimeLogfile\":\"" + lowDewPtTime.ToString(timeStampFormat) + "\",";
			json += "\"highApparentTempValLogfile\":\"" + highAppTempVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"highApparentTempTimeLogfile\":\"" + highAppTempTime.ToString(timeStampFormat) + "\",";
			json += "\"lowApparentTempValLogfile\":\"" + lowAppTempVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowApparentTempTimeLogfile\":\"" + lowAppTempTime.ToString(timeStampFormat) + "\",";
			json += "\"highFeelsLikeValLogfile\":\"" + highFeelsLikeVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"highFeelsLikeTimeLogfile\":\"" + highFeelsLikeTime.ToString(timeStampFormat) + "\",";
			json += "\"lowFeelsLikeValLogfile\":\"" + lowFeelsLikeVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowFeelsLikeTimeLogfile\":\"" + lowFeelsLikeTime.ToString(timeStampFormat) + "\",";
			json += "\"lowWindChillValLogfile\":\"" + lowWindChillVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowWindChillTimeLogfile\":\"" + lowWindChillTime.ToString(timeStampFormat) + "\",";
			json += "\"highHeatIndexValLogfile\":\"" + highHeatIndVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"highHeatIndexTimeLogfile\":\"" + highHeatIndTime.ToString(timeStampFormat) + "\",";
			json += "\"highMinTempValLogfile\":\"" + highMinTempVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"highMinTempTimeLogfile\":\"" + highMinTempTime.ToString(dateStampFormat) + "\",";
			json += "\"lowMaxTempValLogfile\":\"" + lowMaxTempVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowMaxTempTimeLogfile\":\"" + lowMaxTempTime.ToString(dateStampFormat) + "\",";
			json += "\"highDailyTempRangeValLogfile\":\"" + highTempRangeVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"highDailyTempRangeTimeLogfile\":\"" + highTempRangeTime.ToString(dateStampFormat) + "\",";
			json += "\"lowDailyTempRangeValLogfile\":\"" + lowTempRangeVal.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowDailyTempRangeTimeLogfile\":\"" + lowTempRangeTime.ToString(dateStampFormat) + "\",";
			json += "\"highHumidityValLogfile\":\"" + highHumVal.ToString(cumulus.HumFormat) + "\",";
			json += "\"highHumidityTimeLogfile\":\"" + highHumTime.ToString(timeStampFormat) + "\",";
			json += "\"lowHumidityValLogfile\":\"" + lowHumVal.ToString(cumulus.HumFormat) + "\",";
			json += "\"lowHumidityTimeLogfile\":\"" + lowHumTime.ToString(timeStampFormat) + "\",";
			json += "\"highBarometerValLogfile\":\"" + highBaroVal.ToString(cumulus.PressFormat) + "\",";
			json += "\"highBarometerTimeLogfile\":\"" + highBaroTime.ToString(timeStampFormat) + "\",";
			json += "\"lowBarometerValLogfile\":\"" + lowBaroVal.ToString(cumulus.PressFormat) + "\",";
			json += "\"lowBarometerTimeLogfile\":\"" + lowBaroTime.ToString(timeStampFormat) + "\",";
			json += "\"highGustValLogfile\":\"" + highGustVal.ToString(cumulus.WindFormat) + "\",";
			json += "\"highGustTimeLogfile\":\"" + highGustTime.ToString(timeStampFormat) + "\",";
			json += "\"highWindValLogfile\":\"" + highWindVal.ToString(cumulus.WindFormat) + "\",";
			json += "\"highWindTimeLogfile\":\"" + highWindTime.ToString(timeStampFormat) + "\",";
			json += "\"highWindRunValLogfile\":\"" + highWindRunVal.ToString(cumulus.WindRunFormat) + "\",";
			json += "\"highWindRunTimeLogfile\":\"" + highWindRunTime.ToString(dateStampFormat) + "\",";
			json += "\"highRainRateValLogfile\":\"" + highRainRateVal.ToString(cumulus.RainFormat) + "\",";
			json += "\"highRainRateTimeLogfile\":\"" + highRainRateTime.ToString(timeStampFormat) + "\",";
			json += "\"highHourlyRainValLogfile\":\"" + highRainHourVal.ToString(cumulus.RainFormat) + "\",";
			json += "\"highHourlyRainTimeLogfile\":\"" + highRainHourTime.ToString(timeStampFormat) + "\",";
			json += "\"highDailyRainValLogfile\":\"" + highRainDayVal.ToString(cumulus.RainFormat) + "\",";
			json += "\"highDailyRainTimeLogfile\":\"" + highRainDayTime.ToString(dateStampFormat) + "\",";
			json += "\"highMonthlyRainValLogfile\":\"" + highRainMonthVal.ToString(cumulus.RainFormat) + "\",";
			json += "\"highMonthlyRainTimeLogfile\":\"" + highRainMonthTime.ToString("yyyy/MM") + "\",";
			if (recordType == "alltime")
			{
				json += "\"longestDryPeriodValLogfile\":\"" + dryPeriodVal.ToString() + "\",";
				json += "\"longestDryPeriodTimeLogfile\":\"" + dryPeriodTime.ToString(dateStampFormat) + "\",";
				json += "\"longestWetPeriodValLogfile\":\"" + wetPeriodVal.ToString() + "\",";
				json += "\"longestWetPeriodTimeLogfile\":\"" + wetPeriodTime.ToString(dateStampFormat) + "\"}";
			}
			else
			{
				json += "\"longestDryPeriodValLogfile\":\"" + dryPeriodVal.ToString() + "*\",";
				json += "\"longestDryPeriodTimeLogfile\":\"" + dryPeriodTime.ToString(dateStampFormat) + "*\",";
				json += "\"longestWetPeriodValLogfile\":\"" + wetPeriodVal.ToString() + "*\",";
				json += "\"longestWetPeriodTimeLogfile\":\"" + wetPeriodTime.ToString(dateStampFormat) + "*\"}";
			}

			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"All time recs editor Logfiles load = {elapsed} ms");

			return json;
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
						station.SetAlltime(WeatherStation.AT_hightemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_hightemp].timestamp);
						break;
					case "highTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_hightemp, station.alltimerecarray[WeatherStation.AT_hightemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowTempVal":
						station.SetAlltime(WeatherStation.AT_lowtemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowtemp].timestamp);
						break;
					case "lowTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowtemp, station.alltimerecarray[WeatherStation.AT_lowtemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDewPointVal":
						station.SetAlltime(WeatherStation.AT_highdewpoint, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highdewpoint].timestamp);
						break;
					case "highDewPointTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highdewpoint, station.alltimerecarray[WeatherStation.AT_highdewpoint].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowDewPointVal":
						station.SetAlltime(WeatherStation.AT_lowdewpoint, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowdewpoint].timestamp);
						break;
					case "lowDewPointTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowdewpoint, station.alltimerecarray[WeatherStation.AT_lowdewpoint].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highApparentTempVal":
						station.SetAlltime(WeatherStation.AT_highapptemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highapptemp].timestamp);
						break;
					case "highApparentTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highapptemp, station.alltimerecarray[WeatherStation.AT_highapptemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowApparentTempVal":
						station.SetAlltime(WeatherStation.AT_lowapptemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowapptemp].timestamp);
						break;
					case "lowApparentTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowapptemp, station.alltimerecarray[WeatherStation.AT_lowapptemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highFeelsLikeVal":
						station.SetAlltime(WeatherStation.AT_highfeelslike, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highfeelslike].timestamp);
						break;
					case "highFeelsLikeTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highfeelslike, station.alltimerecarray[WeatherStation.AT_highfeelslike].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowFeelsLikeVal":
						station.SetAlltime(WeatherStation.AT_lowfeelslike, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowfeelslike].timestamp);
						break;
					case "lowFeelsLikeTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowfeelslike, station.alltimerecarray[WeatherStation.AT_lowfeelslike].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowWindChillVal":
						station.SetAlltime(WeatherStation.AT_lowchill, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowchill].timestamp);
						break;
					case "lowWindChillTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowchill, station.alltimerecarray[WeatherStation.AT_lowchill].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHeatIndexVal":
						station.SetAlltime(WeatherStation.AT_highheatindex, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highheatindex].timestamp);
						break;
					case "highHeatIndexTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highheatindex, station.alltimerecarray[WeatherStation.AT_highheatindex].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highMinTempVal":
						station.SetAlltime(WeatherStation.AT_highmintemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highmintemp].timestamp);
						break;
					case "highMinTempTime":
						station.SetAlltime(WeatherStation.AT_highmintemp, station.alltimerecarray[WeatherStation.AT_highmintemp].value, station.ddmmyyStrToDate(value));
						break;
					case "lowMaxTempVal":
						station.SetAlltime(WeatherStation.AT_lowmaxtemp, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowmaxtemp].timestamp);
						break;
					case "lowMaxTempTime":
						station.SetAlltime(WeatherStation.AT_lowmaxtemp, station.alltimerecarray[WeatherStation.AT_lowmaxtemp].value, station.ddmmyyStrToDate(value));
						break;
					case "highDailyTempRangeVal":
						station.SetAlltime(WeatherStation.AT_highdailytemprange, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highdailytemprange].timestamp);
						break;
					case "highDailyTempRangeTime":
						station.SetAlltime(WeatherStation.AT_highdailytemprange, station.alltimerecarray[WeatherStation.AT_highdailytemprange].value, station.ddmmyyStrToDate(value));
						break;
					case "lowDailyTempRangeVal":
						station.SetAlltime(WeatherStation.AT_lowdailytemprange, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowdailytemprange].timestamp);
						break;
					case "lowDailyTempRangeTime":
						station.SetAlltime(WeatherStation.AT_lowdailytemprange, station.alltimerecarray[WeatherStation.AT_lowdailytemprange].value, station.ddmmyyStrToDate(value));
						break;
					case "highHumidityVal":
						station.SetAlltime(WeatherStation.AT_highhumidity, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highhumidity].timestamp);
						break;
					case "highHumidityTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highhumidity, station.alltimerecarray[WeatherStation.AT_highhumidity].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowHumidityVal":
						station.SetAlltime(WeatherStation.AT_lowhumidity, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowhumidity].timestamp);
						break;
					case "lowHumidityTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowhumidity, station.alltimerecarray[WeatherStation.AT_lowhumidity].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highBarometerVal":
						station.SetAlltime(WeatherStation.AT_highpress, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highpress].timestamp);
						break;
					case "highBarometerTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highpress, station.alltimerecarray[WeatherStation.AT_highpress].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowBarometerVal":
						station.SetAlltime(WeatherStation.AT_lowpress, double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowpress].timestamp);
						break;
					case "lowBarometerTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowpress, station.alltimerecarray[WeatherStation.AT_lowpress].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highGustVal":
						station.SetAlltime(WeatherStation.AT_highgust, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highgust].timestamp);
						break;
					case "highGustTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highgust, station.alltimerecarray[WeatherStation.AT_highgust].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highWindVal":
						station.SetAlltime(WeatherStation.AT_highwind, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highwind].timestamp);
						break;
					case "highWindTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highwind, station.alltimerecarray[WeatherStation.AT_highwind].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highWindRunVal":
						station.SetAlltime(WeatherStation.AT_highwindrun, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highwindrun].timestamp);
						break;
					case "highWindRunTime":
						station.SetAlltime(WeatherStation.AT_highwindrun, station.alltimerecarray[WeatherStation.AT_highwindrun].value, station.ddmmyyStrToDate(value));
						break;
					case "highRainRateVal":
						station.SetAlltime(WeatherStation.AT_highrainrate, double.Parse(value), station.alltimerecarray[WeatherStation.AT_highrainrate].timestamp);
						break;
					case "highRainRateTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highrainrate, station.alltimerecarray[WeatherStation.AT_highrainrate].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHourlyRainVal":
						station.SetAlltime(WeatherStation.AT_hourlyrain, double.Parse(value), station.alltimerecarray[WeatherStation.AT_hourlyrain].timestamp);
						break;
					case "highHourlyRainTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_hourlyrain, station.alltimerecarray[WeatherStation.AT_hourlyrain].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDailyRainVal":
						station.SetAlltime(WeatherStation.AT_dailyrain, double.Parse(value), station.alltimerecarray[WeatherStation.AT_dailyrain].timestamp);
						break;
					case "highDailyRainTime":
						station.SetAlltime(WeatherStation.AT_dailyrain, station.alltimerecarray[WeatherStation.AT_dailyrain].value, station.ddmmyyStrToDate(value));
						break;
					case "highMonthlyRainVal":
						station.SetAlltime(WeatherStation.AT_wetmonth, double.Parse(value), station.alltimerecarray[WeatherStation.AT_wetmonth].timestamp);
						break;
					case "highMonthlyRainTime":
						dt = value.Split('/');
						var datstr = "01/" + dt[1] + "/" + dt[0].Substring(2, 2);
						station.SetAlltime(WeatherStation.AT_wetmonth, station.alltimerecarray[WeatherStation.AT_wetmonth].value, station.ddmmyyStrToDate(datstr));
						break;
					case "longestDryPeriodVal":
						station.SetAlltime(WeatherStation.AT_longestdryperiod, double.Parse(value), station.alltimerecarray[WeatherStation.AT_longestdryperiod].timestamp);
						break;
					case "longestDryPeriodTime":
						station.SetAlltime(WeatherStation.AT_longestdryperiod, station.alltimerecarray[WeatherStation.AT_longestdryperiod].value, station.ddmmyyStrToDate(value));
						break;
					case "longestWetPeriodVal":
						station.SetAlltime(WeatherStation.AT_longestwetperiod, double.Parse(value), station.alltimerecarray[WeatherStation.AT_longestwetperiod].timestamp);
						break;
					case "longestWetPeriodTime":
						station.SetAlltime(WeatherStation.AT_longestwetperiod, station.alltimerecarray[WeatherStation.AT_longestwetperiod].value, station.ddmmyyStrToDate(value));
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
							station.SetMonthlyAlltime(WeatherStation.AT_hightemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_hightemp, month].timestamp, month);
							break;
						case "highTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_hightemp, station.monthlyrecarray[WeatherStation.AT_hightemp, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowtemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowtemp, month].timestamp, month);
							break;
						case "lowTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_lowtemp, station.monthlyrecarray[WeatherStation.AT_lowtemp, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highDewPointVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highdewpoint, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highdewpoint, month].timestamp, month);
							break;
						case "highDewPointTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highdewpoint, station.monthlyrecarray[WeatherStation.AT_highdewpoint, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowDewPointVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowdewpoint, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowdewpoint, month].timestamp, month);
							break;
						case "lowDewPointTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_lowdewpoint, station.monthlyrecarray[WeatherStation.AT_lowdewpoint, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highApparentTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highapptemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highapptemp, month].timestamp, month);
							break;
						case "highApparentTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highapptemp, station.monthlyrecarray[WeatherStation.AT_highapptemp, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowApparentTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowapptemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowapptemp, month].timestamp, month);
							break;
						case "lowApparentTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_lowapptemp, station.monthlyrecarray[WeatherStation.AT_lowapptemp, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highFeelsLikeVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highfeelslike, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highfeelslike, month].timestamp, month);
							break;
						case "highFeelsLikeTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highfeelslike, station.monthlyrecarray[WeatherStation.AT_highfeelslike, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowFeelsLikeVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowfeelslike, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowfeelslike, month].timestamp, month);
							break;
						case "lowFeelsLikeTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_lowfeelslike, station.monthlyrecarray[WeatherStation.AT_lowfeelslike, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowWindChillVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowchill, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowchill, month].timestamp, month);
							break;
						case "lowWindChillTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_lowchill, station.monthlyrecarray[WeatherStation.AT_lowchill, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highHeatIndexVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highheatindex, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highheatindex, month].timestamp, month);
							break;
						case "highHeatIndexTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highheatindex, station.monthlyrecarray[WeatherStation.AT_highheatindex, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highMinTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highmintemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highmintemp, month].timestamp, month);
							break;
						case "highMinTempTime":
							station.SetMonthlyAlltime(WeatherStation.AT_highmintemp, station.monthlyrecarray[WeatherStation.AT_highmintemp, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "lowMaxTempVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowmaxtemp, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowmaxtemp, month].timestamp, month);
							break;
						case "lowMaxTempTime":
							station.SetMonthlyAlltime(WeatherStation.AT_lowmaxtemp, station.monthlyrecarray[WeatherStation.AT_lowmaxtemp, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "highDailyTempRangeVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highdailytemprange, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highdailytemprange, month].timestamp, month);
							break;
						case "highDailyTempRangeTime":
							station.SetMonthlyAlltime(WeatherStation.AT_highdailytemprange, station.monthlyrecarray[WeatherStation.AT_highdailytemprange, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "lowDailyTempRangeVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowdailytemprange, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowdailytemprange, month].timestamp, month);
							break;
						case "lowDailyTempRangeTime":
							station.SetMonthlyAlltime(WeatherStation.AT_lowdailytemprange, station.monthlyrecarray[WeatherStation.AT_lowdailytemprange, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "highHumidityVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highhumidity, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highhumidity, month].timestamp, month);
							break;
						case "highHumidityTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highhumidity, station.monthlyrecarray[WeatherStation.AT_highhumidity, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowHumidityVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowhumidity, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowhumidity, month].timestamp, month);
							break;
						case "lowHumidityTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_lowhumidity, station.monthlyrecarray[WeatherStation.AT_lowhumidity, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highBarometerVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highpress, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highpress, month].timestamp, month);
							break;
						case "highBarometerTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highpress, station.monthlyrecarray[WeatherStation.AT_highpress, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "lowBarometerVal":
							station.SetMonthlyAlltime(WeatherStation.AT_lowpress, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_lowpress, month].timestamp, month);
							break;
						case "lowBarometerTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_lowpress, station.monthlyrecarray[WeatherStation.AT_lowpress, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highGustVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highgust, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highgust, month].timestamp, month);
							break;
						case "highGustTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highgust, station.monthlyrecarray[WeatherStation.AT_highgust, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highWindVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highwind, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highwind, month].timestamp, month);
							break;
						case "highWindTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highwind, station.monthlyrecarray[WeatherStation.AT_highwind, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highWindRunVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highwindrun, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highwindrun, month].timestamp, month);
							break;
						case "highWindRunTime":
							station.SetMonthlyAlltime(WeatherStation.AT_highwindrun, station.monthlyrecarray[WeatherStation.AT_highwindrun, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "highRainRateVal":
							station.SetMonthlyAlltime(WeatherStation.AT_highrainrate, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_highrainrate, month].timestamp, month);
							break;
						case "highRainRateTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_highrainrate, station.monthlyrecarray[WeatherStation.AT_highrainrate, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highHourlyRainVal":
							station.SetMonthlyAlltime(WeatherStation.AT_hourlyrain, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_hourlyrain, month].timestamp, month);
							break;
						case "highHourlyRainTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(WeatherStation.AT_hourlyrain, station.monthlyrecarray[WeatherStation.AT_hourlyrain, month].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]), month);
							break;
						case "highDailyRainVal":
							station.SetMonthlyAlltime(WeatherStation.AT_dailyrain, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_dailyrain, month].timestamp, month);
							break;
						case "highDailyRainTime":
							station.SetMonthlyAlltime(WeatherStation.AT_dailyrain, station.monthlyrecarray[WeatherStation.AT_dailyrain, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "highMonthlyRainVal":
							station.SetMonthlyAlltime(WeatherStation.AT_wetmonth, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_wetmonth, month].timestamp, month);
							break;
						case "highMonthlyRainTime":
							dt = value.Split('/');
							var datstr = "01/" + dt[1] + "/" + dt[0].Substring(2, 2);
							station.SetMonthlyAlltime(WeatherStation.AT_wetmonth, station.monthlyrecarray[WeatherStation.AT_wetmonth, month].value, station.ddmmyyStrToDate(datstr), month);
							break;
						case "longestDryPeriodVal":
							station.SetMonthlyAlltime(WeatherStation.AT_longestdryperiod, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_longestdryperiod, month].timestamp, month);
							break;
						case "longestDryPeriodTime":
							station.SetMonthlyAlltime(WeatherStation.AT_longestdryperiod, station.monthlyrecarray[WeatherStation.AT_longestdryperiod, month].value, station.ddmmyyStrToDate(value), month);
							break;
						case "longestWetPeriodVal":
							station.SetMonthlyAlltime(WeatherStation.AT_longestwetperiod, double.Parse(value), station.monthlyrecarray[WeatherStation.AT_longestwetperiod, month].timestamp, month);
							break;
						case "longestWetPeriodTime":
							station.SetMonthlyAlltime(WeatherStation.AT_longestwetperiod, station.monthlyrecarray[WeatherStation.AT_longestwetperiod, month].value, station.ddmmyyStrToDate(value), month);
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

			var json = "{";
			for (var m = 1; m <= 12; m++)
			{
				// Records - Temperature values
				json += $"\"{m}-highTempVal\":\"" + station.monthlyrecarray[WeatherStation.AT_hightemp, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowTempVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowtemp, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highDewPointVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highdewpoint, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowDewPointVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowdewpoint, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highApparentTempVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highapptemp, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowApparentTempVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowapptemp, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highFeelsLikeVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highfeelslike, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowFeelsLikeVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowfeelslike, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowWindChillVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowchill, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highHeatIndexVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highheatindex, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highMinTempVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highmintemp, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowMaxTempVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowmaxtemp, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highDailyTempRangeVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highdailytemprange, m].value.ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowDailyTempRangeVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowdailytemprange, m].value.ToString(cumulus.TempFormat) + "\",";
				// Records - Temperature timestamps
				json += $"\"{m}-highTempTime\":\"" + station.monthlyrecarray[WeatherStation.AT_hightemp, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowTempTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowtemp, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highDewPointTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highdewpoint, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowDewPointTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowdewpoint, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highApparentTempTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highapptemp, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowApparentTempTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowapptemp, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highFeelsLikeTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highfeelslike, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowFeelsLikeTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowfeelslike, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowWindChillTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowchill, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highHeatIndexTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highheatindex, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highMinTempTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highmintemp, m].timestamp.ToString(dateStampFormat) + "\",";
				json += $"\"{m}-lowMaxTempTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowmaxtemp, m].timestamp.ToString(dateStampFormat) + "\",";
				json += $"\"{m}-highDailyTempRangeTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highdailytemprange, m].timestamp.ToString(dateStampFormat) + "\",";
				json += $"\"{m}-lowDailyTempRangeTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowdailytemprange, m].timestamp.ToString(dateStampFormat) + "\",";
				// Records - Humidity values
				json += $"\"{m}-highHumidityVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highhumidity, m].value.ToString(cumulus.HumFormat) + "\",";
				json += $"\"{m}-lowHumidityVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowhumidity, m].value.ToString(cumulus.HumFormat) + "\",";
				// Records - Humidity times
				json += $"\"{m}-highHumidityTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highhumidity, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowHumidityTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowhumidity, m].timestamp.ToString(timeStampFormat) + "\",";
				// Records - Pressure values
				json += $"\"{m}-highBarometerVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highpress, m].value.ToString(cumulus.PressFormat) + "\",";
				json += $"\"{m}-lowBarometerVal\":\"" + station.monthlyrecarray[WeatherStation.AT_lowpress, m].value.ToString(cumulus.PressFormat) + "\",";
				// Records - Pressure times
				json += $"\"{m}-highBarometerTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highpress, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowBarometerTime\":\"" + station.monthlyrecarray[WeatherStation.AT_lowpress, m].timestamp.ToString(timeStampFormat) + "\",";
				// Records - Wind values
				json += $"\"{m}-highGustVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highgust, m].value.ToString(cumulus.WindFormat) + "\",";
				json += $"\"{m}-highWindVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highwind, m].value.ToString(cumulus.WindFormat) + "\",";
				json += $"\"{m}-highWindRunVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highwindrun, m].value.ToString(cumulus.WindRunFormat) + "\",";
				// Records - Wind times
				json += $"\"{m}-highGustTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highgust, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highWindTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highwind, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highWindRunTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highwindrun, m].timestamp.ToString(dateStampFormat) + "\",";
				// Records - Rain values
				json += $"\"{m}-highRainRateVal\":\"" + station.monthlyrecarray[WeatherStation.AT_highrainrate, m].value.ToString(cumulus.RainFormat) + "\",";
				json += $"\"{m}-highHourlyRainVal\":\"" + station.monthlyrecarray[WeatherStation.AT_hourlyrain, m].value.ToString(cumulus.RainFormat) + "\",";
				json += $"\"{m}-highDailyRainVal\":\"" + station.monthlyrecarray[WeatherStation.AT_dailyrain, m].value.ToString(cumulus.RainFormat) + "\",";
				json += $"\"{m}-highMonthlyRainVal\":\"" + station.monthlyrecarray[WeatherStation.AT_wetmonth, m].value.ToString(cumulus.RainFormat) + "\",";
				json += $"\"{m}-longestDryPeriodVal\":\"" + station.monthlyrecarray[WeatherStation.AT_longestdryperiod, m].value.ToString("f0") + "\",";
				json += $"\"{m}-longestWetPeriodVal\":\"" + station.monthlyrecarray[WeatherStation.AT_longestwetperiod, m].value.ToString("f0") + "\",";
				// Records - Rain times
				json += $"\"{m}-highRainRateTime\":\"" + station.monthlyrecarray[WeatherStation.AT_highrainrate, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highHourlyRainTime\":\"" + station.monthlyrecarray[WeatherStation.AT_hourlyrain, m].timestamp.ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highDailyRainTime\":\"" + station.monthlyrecarray[WeatherStation.AT_dailyrain, m].timestamp.ToString(dateStampFormat) + "\",";
				json += $"\"{m}-highMonthlyRainTime\":\"" + station.monthlyrecarray[WeatherStation.AT_wetmonth, m].timestamp.ToString("yyyy/MM") + "\",";
				json += $"\"{m}-longestDryPeriodTime\":\"" + station.monthlyrecarray[WeatherStation.AT_longestdryperiod, m].timestamp.ToString(dateStampFormat) + "\",";
				json += $"\"{m}-longestWetPeriodTime\":\"" + station.monthlyrecarray[WeatherStation.AT_longestwetperiod, m].timestamp.ToString(dateStampFormat) + "\",";
			}
			json = json.Remove(json.Length - 1);
			json += "}";

			return json;
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
			var json = "{";

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
					}

					for (var i = 0; i < 12; i++)
					{
						var m = i + 1;
						json += $"\"{m}-highTempValDayfile\":\"" + highTempVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-highTempTimeDayfile\":\"" + highTempTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-lowTempValDayfile\":\"" + lowTempVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-lowTempTimeDayfile\":\"" + lowTempTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highDewPointValDayfile\":\"" + highDewPtVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-highDewPointTimeDayfile\":\"" + highDewPtTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-lowDewPointValDayfile\":\"" + lowDewPtVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-lowDewPointTimeDayfile\":\"" + lowDewPtTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highApparentTempValDayfile\":\"" + highAppTempVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-highApparentTempTimeDayfile\":\"" + highAppTempTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-lowApparentTempValDayfile\":\"" + lowAppTempVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-lowApparentTempTimeDayfile\":\"" + lowAppTempTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highFeelsLikeValDayfile\":\"" + highFeelsLikeVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-highFeelsLikeTimeDayfile\":\"" + highFeelsLikeTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-lowFeelsLikeValDayfile\":\"" + lowFeelsLikeVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-lowFeelsLikeTimeDayfile\":\"" + lowFeelsLikeTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-lowWindChillValDayfile\":\"" + lowWindChillVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-lowWindChillTimeDayfile\":\"" + lowWindChillTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highHeatIndexValDayfile\":\"" + highHeatIndVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-highHeatIndexTimeDayfile\":\"" + highHeatIndTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highMinTempValDayfile\":\"" + highMinTempVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-highMinTempTimeDayfile\":\"" + highMinTempTime[i].ToString(dateStampFormat) + "\",";
						json += $"\"{m}-lowMaxTempValDayfile\":\"" + lowMaxTempVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-lowMaxTempTimeDayfile\":\"" + lowMaxTempTime[i].ToString(dateStampFormat) + "\",";
						json += $"\"{m}-highDailyTempRangeValDayfile\":\"" + highTempRangeVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-highDailyTempRangeTimeDayfile\":\"" + highTempRangeTime[i].ToString(dateStampFormat) + "\",";
						json += $"\"{m}-lowDailyTempRangeValDayfile\":\"" + lowTempRangeVal[i].ToString(cumulus.TempFormat) + "\",";
						json += $"\"{m}-lowDailyTempRangeTimeDayfile\":\"" + lowTempRangeTime[i].ToString(dateStampFormat) + "\",";
						json += $"\"{m}-highHumidityValDayfile\":\"" + highHumVal[i].ToString(cumulus.HumFormat) + "\",";
						json += $"\"{m}-highHumidityTimeDayfile\":\"" + highHumTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-lowHumidityValDayfile\":\"" + lowHumVal[i].ToString(cumulus.HumFormat) + "\",";
						json += $"\"{m}-lowHumidityTimeDayfile\":\"" + lowHumTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highBarometerValDayfile\":\"" + highBaroVal[i].ToString(cumulus.PressFormat) + "\",";
						json += $"\"{m}-highBarometerTimeDayfile\":\"" + highBaroTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-lowBarometerValDayfile\":\"" + lowBaroVal[i].ToString(cumulus.PressFormat) + "\",";
						json += $"\"{m}-lowBarometerTimeDayfile\":\"" + lowBaroTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highGustValDayfile\":\"" + highGustVal[i].ToString(cumulus.WindFormat) + "\",";
						json += $"\"{m}-highGustTimeDayfile\":\"" + highGustTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highWindValDayfile\":\"" + highWindVal[i].ToString(cumulus.WindFormat) + "\",";
						json += $"\"{m}-highWindTimeDayfile\":\"" + highWindTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highWindRunValDayfile\":\"" + highWindRunVal[i].ToString(cumulus.WindRunFormat) + "\",";
						json += $"\"{m}-highWindRunTimeDayfile\":\"" + highWindRunTime[i].ToString(dateStampFormat) + "\",";
						json += $"\"{m}-highRainRateValDayfile\":\"" + highRainRateVal[i].ToString(cumulus.RainFormat) + "\",";
						json += $"\"{m}-highRainRateTimeDayfile\":\"" + highRainRateTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highHourlyRainValDayfile\":\"" + highRainHourVal[i].ToString(cumulus.RainFormat) + "\",";
						json += $"\"{m}-highHourlyRainTimeDayfile\":\"" + highRainHourTime[i].ToString(timeStampFormat) + "\",";
						json += $"\"{m}-highDailyRainValDayfile\":\"" + highRainDayVal[i].ToString(cumulus.RainFormat) + "\",";
						json += $"\"{m}-highDailyRainTimeDayfile\":\"" + highRainDayTime[i].ToString(dateStampFormat) + "\",";
						json += $"\"{m}-highMonthlyRainValDayfile\":\"" + highRainMonthVal[i].ToString(cumulus.RainFormat) + "\",";
						json += $"\"{m}-highMonthlyRainTimeDayfile\":\"" + highRainMonthTime[i].ToString("yyyy/MM") + "\",";
						json += $"\"{m}-longestDryPeriodValDayfile\":\"" + dryPeriodVal[i].ToString() + "\",";
						json += $"\"{m}-longestDryPeriodTimeDayfile\":\"" + dryPeriodTime[i].ToString(dateStampFormat) + "\",";
						json += $"\"{m}-longestWetPeriodValDayfile\":\"" + wetPeriodVal[i].ToString() + "\",";
						json += $"\"{m}-longestWetPeriodTimeDayfile\":\"" + wetPeriodTime[i].ToString(dateStampFormat) + "\",";
					}
					json = json.Remove(json.Length - 1);
					json += "}";
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
			cumulus.LogDebugMessage($"Monthly recs editor Dayfile load = {elapsed} ms");

			return json;
		}

		internal string GetMonthlyRecLogFile()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = "{";
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
				json += $"\"{m}-highTempValLogfile\":\"" + highTempVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highTempTimeLogfile\":\"" + highTempTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowTempValLogfile\":\"" + lowTempVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowTempTimeLogfile\":\"" + lowTempTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highDewPointValLogfile\":\"" + highDewPtVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highDewPointTimeLogfile\":\"" + highDewPtTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowDewPointValLogfile\":\"" + lowDewPtVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowDewPointTimeLogfile\":\"" + lowDewPtTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highApparentTempValLogfile\":\"" + highAppTempVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highApparentTempTimeLogfile\":\"" + highAppTempTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowApparentTempValLogfile\":\"" + lowAppTempVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowApparentTempTimeLogfile\":\"" + lowAppTempTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highFeelsLikeValLogfile\":\"" + highFeelsLikeVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highFeelsLikeTimeLogfile\":\"" + highFeelsLikeTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowFeelsLikeValLogfile\":\"" + lowFeelsLikeVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowFeelsLikeTimeLogfile\":\"" + lowFeelsLikeTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowWindChillValLogfile\":\"" + lowWindChillVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowWindChillTimeLogfile\":\"" + lowWindChillTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highHeatIndexValLogfile\":\"" + highHeatIndVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highHeatIndexTimeLogfile\":\"" + highHeatIndTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highMinTempValLogfile\":\"" + highMinTempVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highMinTempTimeLogfile\":\"" + highMinTempTime[i].ToString(dateStampFormat) + "\",";
				json += $"\"{m}-lowMaxTempValLogfile\":\"" + lowMaxTempVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowMaxTempTimeLogfile\":\"" + lowMaxTempTime[i].ToString(dateStampFormat) + "\",";
				json += $"\"{m}-highDailyTempRangeValLogfile\":\"" + highTempRangeVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-highDailyTempRangeTimeLogfile\":\"" + highTempRangeTime[i].ToString(dateStampFormat) + "\",";
				json += $"\"{m}-lowDailyTempRangeValLogfile\":\"" + lowTempRangeVal[i].ToString(cumulus.TempFormat) + "\",";
				json += $"\"{m}-lowDailyTempRangeTimeLogfile\":\"" + lowTempRangeTime[i].ToString(dateStampFormat) + "\",";
				json += $"\"{m}-highHumidityValLogfile\":\"" + highHumVal[i].ToString(cumulus.HumFormat) + "\",";
				json += $"\"{m}-highHumidityTimeLogfile\":\"" + highHumTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowHumidityValLogfile\":\"" + lowHumVal[i].ToString(cumulus.HumFormat) + "\",";
				json += $"\"{m}-lowHumidityTimeLogfile\":\"" + lowHumTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highBarometerValLogfile\":\"" + highBaroVal[i].ToString(cumulus.PressFormat) + "\",";
				json += $"\"{m}-highBarometerTimeLogfile\":\"" + highBaroTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-lowBarometerValLogfile\":\"" + lowBaroVal[i].ToString(cumulus.PressFormat) + "\",";
				json += $"\"{m}-lowBarometerTimeLogfile\":\"" + lowBaroTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highGustValLogfile\":\"" + highGustVal[i].ToString(cumulus.WindFormat) + "\",";
				json += $"\"{m}-highGustTimeLogfile\":\"" + highGustTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highWindValLogfile\":\"" + highWindVal[i].ToString(cumulus.WindFormat) + "\",";
				json += $"\"{m}-highWindTimeLogfile\":\"" + highWindTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highWindRunValLogfile\":\"" + highWindRunVal[i].ToString(cumulus.WindRunFormat) + "\",";
				json += $"\"{m}-highWindRunTimeLogfile\":\"" + highWindRunTime[i].ToString(dateStampFormat) + "\",";
				json += $"\"{m}-highRainRateValLogfile\":\"" + highRainRateVal[i].ToString(cumulus.RainFormat) + "\",";
				json += $"\"{m}-highRainRateTimeLogfile\":\"" + highRainRateTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highHourlyRainValLogfile\":\"" + highRainHourVal[i].ToString(cumulus.RainFormat) + "\",";
				json += $"\"{m}-highHourlyRainTimeLogfile\":\"" + highRainHourTime[i].ToString(timeStampFormat) + "\",";
				json += $"\"{m}-highDailyRainValLogfile\":\"" + highRainDayVal[i].ToString(cumulus.RainFormat) + "\",";
				json += $"\"{m}-highDailyRainTimeLogfile\":\"" + highRainDayTime[i].ToString(dateStampFormat) + "\",";
				json += $"\"{m}-highMonthlyRainValLogfile\":\"" + highRainMonthVal[i].ToString(cumulus.RainFormat) + "\",";
				json += $"\"{m}-highMonthlyRainTimeLogfile\":\"" + highRainMonthTime[i].ToString("yyyy/MM") + "\",";
				json += $"\"{m}-longestDryPeriodValLogfile\":\"" + dryPeriodVal[i].ToString() + "\",";
				json += $"\"{m}-longestDryPeriodTimeLogfile\":\"" + dryPeriodTime[i].ToString(dateStampFormat) + "\",";
				json += $"\"{m}-longestWetPeriodValLogfile\":\"" + wetPeriodVal[i].ToString() + "\",";
				json += $"\"{m}-longestWetPeriodTimeLogfile\":\"" + wetPeriodTime[i].ToString(dateStampFormat) + "\",";
			}

			json = json.Remove(json.Length - 1);
			json += "}";

			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"Monthly recs editor Logfiles load = {elapsed} ms");

			return json;
		}

		internal string GetThisMonthRecData()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = "{";
			// Records - Temperature
			json += $"\"highTempVal\": \"{station.HighTempThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"highTempTime\": \"{station.HighTempThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"lowTempVal\": \"{station.LowTempThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowTempTime\": \"{station.LowTempThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highDewPointVal\": \"{station.HighDewpointThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"highDewPointTime\": \"{station.HighDewpointThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"lowDewPointVal\": \"{station.LowDewpointThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowDewPointTime\": \"{station.LowDewpointThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highApparentTempVal\": \"{station.HighAppTempThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"highApparentTempTime\": \"{station.HighAppTempThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"lowApparentTempVal\": \"{station.LowAppTempThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowApparentTempTime\": \"{station.LowAppTempThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highFeelsLikeVal\": \"{station.HighFeelsLikeThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"highFeelsLikeTime\": \"{station.HighFeelsLikeThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"lowFeelsLikeVal\": \"{station.LowFeelsLikeThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowFeelsLikeTime\": \"{station.LowFeelsLikeThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"lowWindChillVal\": \"{station.LowWindChillThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowWindChillTime\": \"{station.LowWindChillThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highHeatIndexVal\": \"{station.HighHeatIndexThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"highHeatIndexTime\": \"{station.HighHeatIndexThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highMinTempVal\": \"{station.HighMinTempThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"highMinTempTime\": \"{station.HighMinTempThisMonthTS.ToString(dateStampFormat)}\",";
			json += $"\"lowMaxTempVal\": \"{station.LowMaxTempThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowMaxTempTime\": \"{station.LowMaxTempThisMonthTS.ToString(dateStampFormat)}\",";
			json += $"\"highDailyTempRangeVal\": \"{station.HighDailyTempRangeThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"highDailyTempRangeTime\": \"{station.HighDailyTempRangeThisMonthTS.ToString(dateStampFormat)}\",";
			json += $"\"lowDailyTempRangeVal\": \"{station.LowDailyTempRangeThisMonth.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowDailyTempRangeTime\": \"{station.LowDailyTempRangeThisMonthTS.ToString(dateStampFormat)}\",";
			// Records - Humidty
			json += $"\"highHumidityVal\": \"{station.HighHumidityThisMonth.ToString(cumulus.HumFormat)}\",";
			json += $"\"highHumidityTime\": \"{station.HighHumidityThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"lowHumidityVal\": \"{station.LowHumidityThisMonth.ToString(cumulus.HumFormat)}\",";
			json += $"\"lowHumidityTime\": \"{station.LowHumidityThisMonthTS.ToString(timeStampFormat)}\",";
			// Records - Pressure
			json += $"\"highBarometerVal\": \"{station.HighPressThisMonth.ToString(cumulus.PressFormat)}\",";
			json += $"\"highBarometerTime\": \"{station.HighPressThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"lowBarometerVal\": \"{station.LowPressThisMonth.ToString(cumulus.PressFormat)}\",";
			json += $"\"lowBarometerTime\": \"{station.LowPressThisMonthTS.ToString(timeStampFormat)}\",";
			// Records - Wind
			json += $"\"highGustVal\": \"{station.HighGustThisMonth.ToString(cumulus.WindFormat)}\",";
			json += $"\"highGustTime\": \"{station.HighGustThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highWindVal\": \"{station.HighWindThisMonth.ToString(cumulus.WindFormat)}\",";
			json += $"\"highWindTime\": \"{station.HighWindThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highWindRunVal\": \"{station.HighDailyWindrunThisMonth.ToString(cumulus.WindRunFormat)}\",";
			json += $"\"highWindRunTime\": \"{station.HighDailyWindrunThisMonthTS.ToString(dateStampFormat)}\",";
			// Records - Rain
			json += $"\"highRainRateVal\": \"{station.HighRainThisMonth.ToString(cumulus.RainFormat)}\",";
			json += $"\"highRainRateTime\": \"{station.HighRainThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highHourlyRainVal\": \"{station.HighHourlyRainThisMonth.ToString(cumulus.RainFormat)}\",";
			json += $"\"highHourlyRainTime\": \"{station.HighHourlyRainThisMonthTS.ToString(timeStampFormat)}\",";
			json += $"\"highDailyRainVal\": \"{station.HighDailyRainThisMonth.ToString(cumulus.RainFormat)}\",";
			json += $"\"highDailyRainTime\": \"{station.HighDailyRainThisMonthTS.ToString(dateStampFormat)}\",";
			json += $"\"longestDryPeriodVal\": \"{station.LongestDryPeriodThisMonth.ToString("D")}\",";
			json += $"\"longestDryPeriodTime\": \"{station.LongestDryPeriodThisMonthTS.ToString(dateStampFormat)}\",";
			json += $"\"longestWetPeriodVal\": \"{station.LongestWetPeriodThisMonth.ToString("D")}\",";
			json += $"\"longestWetPeriodTime\": \"{station.LongestWetPeriodThisMonthTS.ToString(dateStampFormat)}\"";

			json += "}";

			return json;
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
			return "{\"result\":\"" + ((result == 1) ? "Success" : "Failed") + "\"}";
		}

		internal string GetThisYearRecData()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

			var json = "{";
			// Records - Temperature
			json += $"\"highTempVal\": \"{station.HighTempThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"highTempTime\": \"{station.HighTempThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"lowTempVal\": \"{station.LowTempThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowTempTime\": \"{station.LowTempThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highDewPointVal\": \"{station.HighDewpointThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"highDewPointTime\": \"{station.HighDewpointThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"lowDewPointVal\": \"{station.LowDewpointThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowDewPointTime\": \"{station.LowDewpointThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highApparentTempVal\": \"{station.HighAppTempThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"highApparentTempTime\": \"{station.HighAppTempThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"lowApparentTempVal\": \"{station.LowAppTempThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowApparentTempTime\": \"{station.LowAppTempThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highFeelsLikeVal\": \"{station.HighFeelsLikeThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"highFeelsLikeTime\": \"{station.HighFeelsLikeThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"lowFeelsLikeVal\": \"{station.LowFeelsLikeThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowFeelsLikeTime\": \"{station.LowFeelsLikeThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"lowWindChillVal\": \"{station.LowWindChillThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowWindChillTime\": \"{station.LowWindChillThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highHeatIndexVal\": \"{station.HighHeatIndexThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"highHeatIndexTime\": \"{station.HighHeatIndexThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highMinTempVal\": \"{station.HighMinTempThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"highMinTempTime\": \"{station.HighMinTempThisYearTS.ToString(dateStampFormat)}\",";
			json += $"\"lowMaxTempVal\": \"{station.LowMaxTempThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowMaxTempTime\": \"{station.LowMaxTempThisYearTS.ToString(dateStampFormat)}\",";
			json += $"\"highDailyTempRangeVal\": \"{station.HighDailyTempRangeThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"highDailyTempRangeTime\": \"{station.HighDailyTempRangeThisYearTS.ToString(dateStampFormat)}\",";
			json += $"\"lowDailyTempRangeVal\": \"{station.LowDailyTempRangeThisYear.ToString(cumulus.TempFormat)}\",";
			json += $"\"lowDailyTempRangeTime\": \"{station.LowDailyTempRangeThisYearTS.ToString(dateStampFormat)}\",";
			// Records - Humidty
			json += $"\"highHumidityVal\": \"{station.HighHumidityThisYear.ToString(cumulus.HumFormat)}\",";
			json += $"\"highHumidityTime\": \"{station.HighHumidityThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"lowHumidityVal\": \"{station.LowHumidityThisYear.ToString(cumulus.HumFormat)}\",";
			json += $"\"lowHumidityTime\": \"{station.LowHumidityThisYearTS.ToString(timeStampFormat)}\",";
			// Records - Pressure
			json += $"\"highBarometerVal\": \"{station.HighPressThisYear.ToString(cumulus.PressFormat)}\",";
			json += $"\"highBarometerTime\": \"{station.HighPressThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"lowBarometerVal\": \"{station.LowPressThisYear.ToString(cumulus.PressFormat)}\",";
			json += $"\"lowBarometerTime\": \"{station.LowPressThisYearTS.ToString(timeStampFormat)}\",";
			// Records - Wind
			json += $"\"highGustVal\": \"{station.HighGustThisYear.ToString(cumulus.WindFormat)}\",";
			json += $"\"highGustTime\": \"{station.HighGustThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highWindVal\": \"{station.HighWindThisYear.ToString(cumulus.WindFormat)}\",";
			json += $"\"highWindTime\": \"{station.HighWindThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highWindRunVal\": \"{station.HighDailyWindrunThisYear.ToString(cumulus.WindRunFormat)}\",";
			json += $"\"highWindRunTime\": \"{station.HighDailyWindrunThisYearTS.ToString(dateStampFormat)}\",";
			// Records - Rain
			json += $"\"highRainRateVal\": \"{station.HighRainThisYear.ToString(cumulus.RainFormat)}\",";
			json += $"\"highRainRateTime\": \"{station.HighRainThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highHourlyRainVal\": \"{station.HighHourlyRainThisYear.ToString(cumulus.RainFormat)}\",";
			json += $"\"highHourlyRainTime\": \"{station.HighHourlyRainThisYearTS.ToString(timeStampFormat)}\",";
			json += $"\"highDailyRainVal\": \"{station.HighDailyRainThisYear.ToString(cumulus.RainFormat)}\",";
			json += $"\"highDailyRainTime\": \"{station.HighDailyRainThisYearTS.ToString(dateStampFormat)}\",";
			json += $"\"highMonthlyRainVal\": \"{station.HighMonthlyRainThisYear.ToString(cumulus.RainFormat)}\",";
			json += $"\"highMonthlyRainTime\": \"{station.HighMonthlyRainThisYearTS.ToString("yyyy/MM")}\",";
			json += $"\"longestDryPeriodVal\": \"{station.LongestDryPeriodThisYear.ToString("D")}\",";
			json += $"\"longestDryPeriodTime\": \"{station.LongestDryPeriodThisYearTS.ToString(dateStampFormat)}\",";
			json += $"\"longestWetPeriodVal\": \"{station.LongestWetPeriodThisYear.ToString("D")}\",";
			json += $"\"longestWetPeriodTime\": \"{station.LongestWetPeriodThisYearTS.ToString(dateStampFormat)}\"";

			json += "}";

			return json;
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
			return "{\"result\":\"" + ((result == 1) ? "Success" : "Failed") + "\"}";
		}

		internal string GetCurrentCond()
		{
			return "{\"data\":\"" + webtags.GetCurrCondText() + "\"}";
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

			return "{\"result\":\"" + (result ? "Success" : "Failed") + "\"}";
		}

		internal string EditDayFile(IHttpContext context)
		{
			var request = context.Request;
			string text;
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			var newData = JsonConvert.DeserializeObject<DayFileEditor>(text);

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
			return JsonConvert.SerializeObject(rec);
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

			var newData = JsonConvert.DeserializeObject<DatalogEditor>(text);

			// date will (hopefully) be in format "m-yyyy" or "mm-yyyy"
			int month = Convert.ToInt32(newData.Month.Split('-')[0]);
			int year = Convert.ToInt32(newData.Month.Split('-')[1]);

			// Get a timestamp, use 15th day to avoid wrap issues
			var ts = new DateTime(year, month, 15);

			var logfile = (newData.Extra ? cumulus.GetExtraLogFileName(ts) : cumulus.GetLogFileName(ts));
			var numFields = (newData.Extra ? Cumulus.NumExtraLogFileFields : Cumulus.NumLogFileFields);

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
			return JsonConvert.SerializeObject(rec);
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
