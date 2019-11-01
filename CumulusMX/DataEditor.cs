using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	internal class DataEditor
	{
		private WeatherStation station;
		private Cumulus cumulus;
		private WebTags webtags;

		private List<LastHourRainLog> HourRainLog = new List<LastHourRainLog>();

		internal DataEditor(Cumulus cumulus, WeatherStation station, WebTags webtags)
		{
			this.station = station;
			this.cumulus = cumulus;
			this.webtags = webtags;
		}

		//internal string EditRainToday(HttpListenerContext context)
		internal string EditRainToday(IHttpContext context)
		{
			var InvC = new CultureInfo("");
			var request = context.Request;
			string text;
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			string[] kvPair = text.Split('=');
			string key = kvPair[0];
			string raintodaystring = kvPair[1];

			if (!String.IsNullOrEmpty(raintodaystring))
			{
				try
				{
					double raintoday = Double.Parse(raintodaystring, CultureInfo.InvariantCulture);
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

			var json = "{\"raintoday\":\"" + station.RainToday.ToString(cumulus.RainFormat, InvC) +
				"\",\"raincounter\":\"" + station.Raincounter.ToString(cumulus.RainFormat, InvC) +
				"\",\"startofdayrain\":\"" + station.raindaystart.ToString(cumulus.RainFormat, InvC) +
				"\",\"rainmult\":\"" + cumulus.RainMult.ToString("F3", InvC) + "\"}";

			return json;
		}

		internal string GetRainTodayEditData()
		{
			var InvC = new CultureInfo("");
			string step = (cumulus.RainDPlaces == 1 ? "0.1" : "0.01");
			var json = "{\"raintoday\":\"" + station.RainToday.ToString(cumulus.RainFormat, InvC) +
				"\",\"raincounter\":\"" + station.Raincounter.ToString(cumulus.RainFormat, InvC) +
				"\",\"startofdayrain\":\"" + station.raindaystart.ToString(cumulus.RainFormat, InvC) +
				"\",\"rainmult\":\"" + cumulus.RainMult.ToString("F3", InvC) +
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
			var timeStampFormat = "dd/MM/yy HH:mm";
			var dateStampFormat = "dd/MM/yy";
			var InvC = new CultureInfo("");
			// Records - Temperature values
			var json = "{\"highTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_hightemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowtemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"highDewPointVal\":\"" + station.alltimerecarray[WeatherStation.AT_highdewpoint].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowDewPointVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowdewpoint].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"highApparentTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_highapptemp].value.ToString(cumulus.TempFormat) + "\",";
			json += "\"lowApparentTempVal\":\"" + station.alltimerecarray[WeatherStation.AT_lowapptemp].value.ToString(cumulus.TempFormat) + "\",";
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

		internal string GetAllTimeRecDayFile()
		{
			var timeStampFormat = "dd/MM/yy HH:mm";
			var dateStampFormat = "dd/MM/yy";

			int linenum = 0;
			string LogFile = cumulus.Datapath + cumulus.GetLogFileName(cumulus.LastUpdateTime);
			double highTempVal = -999;
			double lowTempVal = 999;
			double highDewPtVal = -999;
			double lowDewPtVal = 999;
			double highAppTempVal = -999;
			double lowAppTempVal = 999;
			double lowWindChillVal = 999;
			double highHeatIndVal = -999;
			double highMinTempVal = -999;
			double lowMaxTempVal = 999;
			double highTempRangeVal = -999;
			double lowTempRangeVal = 999;
			double highHumVal = -999;
			double lowHumVal = 999;
			double highBaroVal = -999;
			double lowBaroVal = 99999;
			double highGustVal = -999;
			double highWindVal = -999;
			double highWindRunVal = -999;
			double highRainRateVal = -999;
			double highRainHourVal = -999;
			double highRainDayVal = -999;
			double highRainMonthVal = -999;
			int dryPeriodVal = 0;
			int wetPeriodVal = 0;
			DateTime highTempTime = new DateTime(1900, 01, 01);
			DateTime lowTempTime = highTempTime;
			DateTime highDewPtTime = highTempTime;
			DateTime lowDewPtTime = highTempTime;
			DateTime highAppTempTime = highTempTime;
			DateTime lowAppTempTime = highTempTime;
			DateTime lowWindChillTime = highTempTime;
			DateTime highHeatIndTime = highTempTime;
			DateTime highMinTempTime = highTempTime;
			DateTime lowMaxTempTime = highTempTime;
			DateTime highTempRangeTime = highTempTime;
			DateTime lowTempRangeTime = highTempTime;
			DateTime highHumTime = highTempTime;
			DateTime lowHumTime = highTempTime;
			DateTime highBaroTime = highTempTime;
			DateTime lowBaroTime = highTempTime;
			DateTime highGustTime = highTempTime;
			DateTime highWindTime = highTempTime;
			DateTime highWindRunTime = highTempTime;
			DateTime highRainRateTime = highTempTime;
			DateTime highRainHourTime = highTempTime;
			DateTime highRainDayTime = highTempTime;
			DateTime highRainMonthTime = highTempTime;
			DateTime dryPeriodTime = highTempTime;
			DateTime wetPeriodTime = highTempTime;

			DateTime thisDate = highTempTime;
			double rainThisMonth = 0;
			int currentDryPeriod = 0;
			int currentWetPeriod = 0;
			bool isDryNow = false;
			DateTime thisDateDry = highTempTime;
			DateTime thisDateWet = highTempTime;
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
					string[] dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (string line in dayfile)
					{
						linenum++;
						var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count > 0)
						{
							string datestr = st[0];
							DateTime loggedDate = station.ddmmyyStrToDate(datestr);

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
							// hi temp
							if (Double.Parse(st[6]) > highTempVal)
							{
								highTempVal = Double.Parse(st[6]);
								highTempTime = getDateTime(loggedDate, st[7]);
							}
							// lo temp
							if (Double.Parse(st[4]) < lowTempVal)
							{
								lowTempVal = Double.Parse(st[4]);
								lowTempTime = getDateTime(loggedDate, st[5]);
							}
							// hi dewpt
							if (Double.Parse(st[35]) > highDewPtVal)
							{
								highDewPtVal = Double.Parse(st[35]);
								highDewPtTime = getDateTime(loggedDate, st[36]);
							}
							// lo dewpt
							if (Double.Parse(st[37]) < lowDewPtVal)
							{
								lowDewPtVal = Double.Parse(st[37]);
								lowDewPtTime = getDateTime(loggedDate, st[38]);
							}
							// hi app temp
							if (Double.Parse(st[27]) > highAppTempVal)
							{
								highAppTempVal = Double.Parse(st[27]);
								highAppTempTime = getDateTime(loggedDate, st[28]);
							}
							// lo app temp
							if (Double.Parse(st[29]) < lowAppTempVal)
							{
								lowAppTempVal = Double.Parse(st[29]);
								lowAppTempTime = getDateTime(loggedDate, st[30]);
							}
							// lo wind chill
							if (Double.Parse(st[33]) < lowWindChillVal)
							{
								lowWindChillVal = Double.Parse(st[33]);
								lowWindChillTime = getDateTime(loggedDate, st[34]);
							}
							// hi heat index
							if (Double.Parse(st[25]) > highHeatIndVal)
							{
								highHeatIndVal = Double.Parse(st[25]);
								highHeatIndTime = getDateTime(loggedDate, st[26]);
							}
							// hi min temp
							if (Double.Parse(st[4]) > highMinTempVal)
							{
								highMinTempVal = Double.Parse(st[4]);
								highMinTempTime = loggedDate;
							}
							// lo max temp
							if (Double.Parse(st[6]) < lowMaxTempVal)
							{
								lowMaxTempVal = Double.Parse(st[6]);
								lowMaxTempTime = loggedDate;
							}
							// hi temp range
							if (Double.Parse(st[6]) - Double.Parse(st[4]) > highTempRangeVal)
							{
								highTempRangeVal = Double.Parse(st[6]) - Double.Parse(st[4]);
								highTempRangeTime = loggedDate;
							}
							// lo temp range
							if (Double.Parse(st[6]) - Double.Parse(st[4]) < lowTempRangeVal)
							{
								lowTempRangeVal = Double.Parse(st[6]) - Double.Parse(st[4]);
								lowTempRangeTime = loggedDate;
							}
							// hi humidity
							if (Double.Parse(st[21]) > highHumVal)
							{
								highHumVal = Double.Parse(st[21]);
								highHumTime = getDateTime(loggedDate, st[22]);
							}
							// lo humidity
							if (Double.Parse(st[19]) < lowHumVal)
							{
								lowHumVal = Double.Parse(st[19]);
								lowHumTime = getDateTime(loggedDate, st[20]);
							}
							// hi baro
							if (Double.Parse(st[10]) > highBaroVal)
							{
								highBaroVal = Double.Parse(st[10]);
								highBaroTime = getDateTime(loggedDate, st[11]);
							}
							// lo baro
							if (Double.Parse(st[8]) < lowBaroVal)
							{
								lowBaroVal = Double.Parse(st[8]);
								lowBaroTime = getDateTime(loggedDate, st[9]);
							}
							// hi gust
							if (Double.Parse(st[1]) > highGustVal)
							{
								highGustVal = Double.Parse(st[1]);
								highGustTime = getDateTime(loggedDate, st[3]);
							}
							// hi wind
							if (Double.Parse(st[17]) > highWindVal)
							{
								highWindVal = Double.Parse(st[17]);
								highWindTime = getDateTime(loggedDate, st[18]);
							}
							// hi wind run
							if (Double.Parse(st[16]) > highWindRunVal)
							{
								highWindRunVal = Double.Parse(st[16]);
								highWindRunTime = loggedDate;
							}
							// hi rain rate
							if (Double.Parse(st[12]) > highRainRateVal)
							{
								highRainRateVal = Double.Parse(st[12]);
								highRainRateTime = getDateTime(loggedDate, st[13]);
							}
							// hi rain hour
							if (Double.Parse(st[31]) > highRainHourVal)
							{
								highRainHourVal = Double.Parse(st[31]);
								highRainHourTime = getDateTime(loggedDate, st[32]);
							}
							// hi rain day
							if (Double.Parse(st[14]) > highRainDayVal)
							{
								highRainDayVal = Double.Parse(st[14]);
								highRainDayTime = loggedDate;
							}
							// monthly rain
							rainThisMonth += Double.Parse(st[14]);
							// dry/wet period
							if (Double.Parse(st[14]) > rainThreshold)
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
					json += "\"highMonthlyRainValDayfile\":\"" + highRainMonthVal.ToString(cumulus.RainFormat) + "\",";
					json += "\"highMonthlyRainTimeDayfile\":\"" + highRainMonthTime.ToString("yyyy/MM") + "\",";
					json += "\"longestDryPeriodValDayfile\":\"" + dryPeriodVal.ToString() + "\",";
					json += "\"longestDryPeriodTimeDayfile\":\"" + dryPeriodTime.ToString(dateStampFormat) + "\",";
					json += "\"longestWetPeriodValDayfile\":\"" + wetPeriodVal.ToString() + "\",";
					json += "\"longestWetPeriodTimeDayfile\":\"" + wetPeriodTime.ToString(dateStampFormat) + "\"}";
				}
				catch (Exception E)
				{
					cumulus.LogMessage("Error on line " + linenum + " of " + cumulus.DayFile + ": " + E.Message);
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
		internal string GetAllTimeRecLogFile()
		{
			var timeStampFormat = "dd/MM/yy HH:mm";
			var dateStampFormat = "dd/MM/yy";

			var json = "{";
			var datefrom = DateTime.Parse(cumulus.RecordsBeganDate);
			var dateto = DateTime.Now;
			var filedate = datefrom;

			string logFile = cumulus.GetLogFileName(filedate);
			bool started = false;
			bool finished = false;
			var entrydate = datefrom;
			var lastentrydate = datefrom;
			var metoDate = datefrom;

			var currentDay = datefrom;
			double dayHighTemp = -999;
			double dayLowTemp = 999;
			double dayWindRun = 0;
			//double hourRain = 0;
			double dayRain = 0;

			bool isDryNow = false;
			int currentDryPeriod = 0;
			int currentWetPeriod = 0;

			double rainThreshold = 0;
			if (cumulus.RainDayThreshold > -1)
				rainThreshold = cumulus.RainDayThreshold;

			double highTempVal = -999;
			double lowTempVal = 999;
			double highDewPtVal = -999;
			double lowDewPtVal = 999;
			double highAppTempVal = -999;
			double lowAppTempVal = 999;
			double lowWindChillVal = 999;
			double highHeatIndVal = -999;
			double highMinTempVal = -999;
			double lowMaxTempVal = 999;
			double highTempRangeVal = -999;
			double lowTempRangeVal = 999;
			double highHumVal = -999;
			double lowHumVal = 999;
			double highBaroVal = -999;
			double lowBaroVal = 99999;
			double highGustVal = -999;
			double highWindVal = -999;
			double highWindRunVal = -999;
			double highRainRateVal = -999;
			double highRainHourVal = -999;
			double highRainDayVal = -999;
			double highRainMonthVal = -999;
			int dryPeriodVal = 0;
			int wetPeriodVal = 0;

			DateTime highTempTime = new DateTime(1900, 01, 01);
			DateTime lowTempTime = highTempTime;
			DateTime highDewPtTime = highTempTime;
			DateTime lowDewPtTime = highTempTime;
			DateTime highAppTempTime = highTempTime;
			DateTime lowAppTempTime = highTempTime;
			DateTime lowWindChillTime = highTempTime;
			DateTime highHeatIndTime = highTempTime;
			DateTime highMinTempTime = highTempTime;
			DateTime lowMaxTempTime = highTempTime;
			DateTime highTempRangeTime = highTempTime;
			DateTime lowTempRangeTime = highTempTime;
			DateTime highHumTime = highTempTime;
			DateTime lowHumTime = highTempTime;
			DateTime highBaroTime = highTempTime;
			DateTime lowBaroTime = highTempTime;
			DateTime highGustTime = highTempTime;
			DateTime highWindTime = highTempTime;
			DateTime highWindRunTime = highTempTime;
			DateTime highRainRateTime = highTempTime;
			DateTime highRainHourTime = highTempTime;
			DateTime highRainDayTime = highTempTime;
			DateTime highRainMonthTime = highTempTime;
			DateTime dryPeriodTime = highTempTime;
			DateTime wetPeriodTime = highTempTime;

			DateTime thisDateDry = highTempTime;
			DateTime thisDateWet = highTempTime;

			Double lastRainMidnight = 0;
			Double rainMidnight = 0;
			Double totalRainfall = 0;

			var watch = System.Diagnostics.Stopwatch.StartNew();

			while (!finished)
			{
				double monthlyRain = 0;

				if (File.Exists(logFile))
				{
					int linenum = 0;
					try
					{
						string[] logfile = File.ReadAllLines(logFile);

						foreach (string line in logfile)
						{
							// process each record in the file
							linenum++;
							var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
							entrydate = station.ddmmyyhhmmStrToDate(st[0], st[1]);
							// TODO: We need to work in meto dates not clock dates for hi/lows
							metoDate = entrydate.AddHours(cumulus.GetHourInc());

							if (!started)
							{
								lastentrydate = entrydate;
								currentDay = metoDate;
								started = true;
							}

							var outsidetemp = Convert.ToDouble(st[2]);
							var hum = Convert.ToInt32(st[3]);
							var dewpoint = Convert.ToDouble(st[4]);
							var speed = Convert.ToDouble(st[5]);
							var gust = Convert.ToDouble(st[6]);
							var rainrate = Convert.ToDouble(st[8]);
							var raintoday = Convert.ToDouble(st[9]);
							var pressure = Convert.ToDouble(st[10]);
							var chill = 999.0;
							if (st.Count >= 16)
							{
								chill = Convert.ToDouble(st[15]);
							}
							var heat = -999.0;
							if (st.Count >= 17)
							{
								heat = Convert.ToDouble(st[16]);
							}
							double apptemp = -999.0;
							if (st.Count >= 22)
							{
								apptemp = Convert.ToDouble(st[21]);
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
							if (apptemp > -999)
							{
								// hi appt
								if (apptemp > highAppTempVal)
								{
									highAppTempVal = apptemp;
									highAppTempTime = entrydate;
								}
								// lo appt
								if (apptemp < lowAppTempVal)
								{
									lowAppTempVal = apptemp;
									lowAppTempTime = entrydate;
								}
							}
							// low chill
							if (chill < lowWindChillVal)
							{
								lowWindChillVal = chill;
								lowWindChillTime = entrydate;
							}
							// hi heat
							if (heat > highHeatIndVal)
							{
								highHeatIndVal = heat;
								highHeatIndTime = entrydate;
							}
							// same meto day
							if (currentDay.Day == metoDate.Day && currentDay.Month == metoDate.Month && currentDay.Year == metoDate.Year)
							{
								if (outsidetemp > dayHighTemp)
									dayHighTemp = outsidetemp;

								if (outsidetemp < dayLowTemp)
									dayLowTemp = outsidetemp;

								dayWindRun += entrydate.Subtract(lastentrydate).TotalHours * speed;
								dayRain = raintoday;
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
								dayRain = 0;
								totalRainfall += raintoday;
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
							// hourly rain
							/*
							 * need to track what the rainfall has been in the last rolling hour
							 * across day rollovers where the count resets
							 */
							AddLastHourRainEntry(entrydate, totalRainfall + raintoday);
							RemoveOldRainData(entrydate);

							var rainThisHour = HourRainLog.First().raincounter - HourRainLog.Last().raincounter;
							if (rainThisHour > highRainHourVal)
							{
								highRainHourVal = rainThisHour;
								highRainHourTime = entrydate;
							}

							monthlyRain += dayRain;
							if (monthlyRain > highRainMonthVal)
							{
								highRainMonthVal = monthlyRain;
								highRainMonthTime = entrydate;
							}

							lastentrydate = entrydate;
							lastRainMidnight = rainMidnight;
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage("Error at line " + linenum + " of " + logFile + " : " + e.Message);
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				if (entrydate >= dateto || filedate > dateto.AddMonths(1))
				{
					finished = true;
				}
				else
				{
					filedate = filedate.AddMonths(1);
					logFile = cumulus.GetLogFileName(filedate);
				}

			}

			// convert Hourly rain counter to user units.
			//highRainDayVal *= cumulus.RainMult ;

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
			//json += "\"highHourlyRainValLogfile\":\"n/a\",";
			//json += "\"highHourlyRainTimeLogfile\":\"n/a\",";
			json += "\"highDailyRainValLogfile\":\"" + highRainDayVal.ToString(cumulus.RainFormat) + "\",";
			json += "\"highDailyRainTimeLogfile\":\"" + highRainDayTime.ToString(dateStampFormat) + "\",";
			json += "\"highMonthlyRainValLogfile\":\"" + highRainMonthVal.ToString(cumulus.RainFormat) + "\",";
			json += "\"highMonthlyRainTimeLogfile\":\"" + highRainMonthTime.ToString("yyyy/MM") + "\",";
			json += "\"longestDryPeriodValLogfile\":\"" + dryPeriodVal.ToString() + "\",";
			json += "\"longestDryPeriodTimeLogfile\":\"" + dryPeriodTime.ToString(dateStampFormat) + "\",";
			json += "\"longestWetPeriodValLogfile\":\"" + wetPeriodVal.ToString() + "\",";
			json += "\"longestWetPeriodTimeLogfile\":\"" + wetPeriodTime.ToString(dateStampFormat) + "\"}";

			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"All time recs editor Logfiles load = {elapsed} ms");

			return json;
		}

		private DateTime getDateTime(DateTime date, string time)
		{
			string[] tim = time.Split(CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator.ToCharArray()[0]);
			return new DateTime(date.Year, date.Month, date.Day, int.Parse(tim[0]), int.Parse(tim[1]), 0);
		}

		internal string EditAllTimeRecs(IHttpContext context)
		{
			var request = context.Request;
			string text;
			int result;
			string[] dt;

			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = Uri.UnescapeDataString(reader.ReadToEnd());
			}
			// Eg "name=highTempValvalue=134.6&pk=1"
			var newData = text.Split('&');
			var field = newData[0].Split('=')[1];
			var value = newData[1].Split('=')[1];
			result = 1;
			try
			{
				switch (field)
				{
					case "highTempVal":
						station.SetAlltime(WeatherStation.AT_hightemp, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_hightemp].timestamp);
						break;
					case "highTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_hightemp, station.alltimerecarray[WeatherStation.AT_hightemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowTempVal":
						station.SetAlltime(WeatherStation.AT_lowtemp, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowtemp].timestamp);
						break;
					case "lowTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowtemp, station.alltimerecarray[WeatherStation.AT_lowtemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDewPointVal":
						station.SetAlltime(WeatherStation.AT_highdewpoint, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highdewpoint].timestamp);
						break;
					case "highDewPointTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highdewpoint, station.alltimerecarray[WeatherStation.AT_highdewpoint].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowDewPointVal":
						station.SetAlltime(WeatherStation.AT_lowdewpoint, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowdewpoint].timestamp);
						break;
					case "lowDewPointTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowdewpoint, station.alltimerecarray[WeatherStation.AT_lowdewpoint].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highApparentTempVal":
						station.SetAlltime(WeatherStation.AT_highapptemp, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highapptemp].timestamp);
						break;
					case "highApparentTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highapptemp, station.alltimerecarray[WeatherStation.AT_highapptemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowApparentTempVal":
						station.SetAlltime(WeatherStation.AT_lowapptemp, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowapptemp].timestamp);
						break;
					case "lowApparentTempTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowapptemp, station.alltimerecarray[WeatherStation.AT_lowapptemp].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowWindChillVal":
						station.SetAlltime(WeatherStation.AT_lowchill, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowchill].timestamp);
						break;
					case "lowWindChillTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowchill, station.alltimerecarray[WeatherStation.AT_lowchill].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHeatIndexVal":
						station.SetAlltime(WeatherStation.AT_highheatindex, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highheatindex].timestamp);
						break;
					case "highHeatIndexTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highheatindex, station.alltimerecarray[WeatherStation.AT_highheatindex].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highMinTempVal":
						station.SetAlltime(WeatherStation.AT_highmintemp, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highmintemp].timestamp);
						break;
					case "highMinTempTime":
						station.SetAlltime(WeatherStation.AT_highmintemp, station.alltimerecarray[WeatherStation.AT_highmintemp].value, station.ddmmyyStrToDate(value));
						break;
					case "lowMaxTempVal":
						station.SetAlltime(WeatherStation.AT_lowmaxtemp, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowmaxtemp].timestamp);
						break;
					case "lowMaxTempTime":
						station.SetAlltime(WeatherStation.AT_lowmaxtemp, station.alltimerecarray[WeatherStation.AT_lowmaxtemp].value, station.ddmmyyStrToDate(value));
						break;
					case "highDailyTempRangeVal":
						station.SetAlltime(WeatherStation.AT_highdailytemprange, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highdailytemprange].timestamp);
						break;
					case "highDailyTempRangeTime":
						station.SetAlltime(WeatherStation.AT_highdailytemprange, station.alltimerecarray[WeatherStation.AT_highdailytemprange].value, station.ddmmyyStrToDate(value));
						break;
					case "lowDailyTempRangeVal":
						station.SetAlltime(WeatherStation.AT_lowdailytemprange, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowdailytemprange].timestamp);
						break;
					case "lowDailyTempRangeTime":
						station.SetAlltime(WeatherStation.AT_lowdailytemprange, station.alltimerecarray[WeatherStation.AT_lowdailytemprange].value, station.ddmmyyStrToDate(value));
						break;
					case "highHumidityVal":
						station.SetAlltime(WeatherStation.AT_highhumidity, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highhumidity].timestamp);
						break;
					case "highHumidityTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highhumidity, station.alltimerecarray[WeatherStation.AT_highhumidity].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowHumidityVal":
						station.SetAlltime(WeatherStation.AT_lowhumidity, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowhumidity].timestamp);
						break;
					case "lowHumidityTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowhumidity, station.alltimerecarray[WeatherStation.AT_lowhumidity].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highBarometerVal":
						station.SetAlltime(WeatherStation.AT_highpress, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highpress].timestamp);
						break;
					case "highBarometerTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highpress, station.alltimerecarray[WeatherStation.AT_highpress].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowBarometerVal":
						station.SetAlltime(WeatherStation.AT_lowpress, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_lowpress].timestamp);
						break;
					case "lowBarometerTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_lowpress, station.alltimerecarray[WeatherStation.AT_lowpress].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highGustVal":
						station.SetAlltime(WeatherStation.AT_highgust, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highgust].timestamp);
						break;
					case "highGustTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highgust, station.alltimerecarray[WeatherStation.AT_highgust].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highWindVal":
						station.SetAlltime(WeatherStation.AT_highwind, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highwind].timestamp);
						break;
					case "highWindTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highwind, station.alltimerecarray[WeatherStation.AT_highwind].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highWindRunVal":
						station.SetAlltime(WeatherStation.AT_highwindrun, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highwindrun].timestamp);
						break;
					case "highWindRunTime":
						station.SetAlltime(WeatherStation.AT_highwindrun, station.alltimerecarray[WeatherStation.AT_highwindrun].value, station.ddmmyyStrToDate(value));
						break;
					case "highRainRateVal":
						station.SetAlltime(WeatherStation.AT_highrainrate, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_highrainrate].timestamp);
						break;
					case "highRainRateTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_highrainrate, station.alltimerecarray[WeatherStation.AT_highrainrate].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHourlyRainVal":
						station.SetAlltime(WeatherStation.AT_hourlyrain, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_hourlyrain].timestamp);
						break;
					case "highHourlyRainTime":
						dt = value.Split('+');
						station.SetAlltime(WeatherStation.AT_hourlyrain, station.alltimerecarray[WeatherStation.AT_hourlyrain].value, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDailyRainVal":
						station.SetAlltime(WeatherStation.AT_dailyrain, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_dailyrain].timestamp);
						break;
					case "highDailyRainTime":
						station.SetAlltime(WeatherStation.AT_dailyrain, station.alltimerecarray[WeatherStation.AT_dailyrain].value, station.ddmmyyStrToDate(value));
						break;
					case "highMonthlyRainVal":
						station.SetAlltime(WeatherStation.AT_wetmonth, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_wetmonth].timestamp);
						break;
					case "highMonthlyRainTime":
						dt = value.Split('/');
						var datstr = "01/" + dt[1] + "/" + dt[0].Substring(2, 2);
						station.SetAlltime(WeatherStation.AT_wetmonth, station.alltimerecarray[WeatherStation.AT_wetmonth].value, station.ddmmyyStrToDate(datstr));
						break;
					case "longestDryPeriodVal":
						station.SetAlltime(WeatherStation.AT_longestdryperiod, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_longestdryperiod].timestamp);
						break;
					case "longestDryPeriodTime":
						station.SetAlltime(WeatherStation.AT_longestdryperiod, station.alltimerecarray[WeatherStation.AT_longestdryperiod].value, station.ddmmyyStrToDate(value));
						break;
					case "longestWetPeriodVal":
						station.SetAlltime(WeatherStation.AT_longestwetperiod, Double.Parse(value), station.alltimerecarray[WeatherStation.AT_longestwetperiod].timestamp);
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

		internal string GetCurrentCond()
		{
			return "{\"data\":\"" + webtags.GetCurrCondText() + "\"}";
		}

		internal string EditCurrentCond(IHttpContext context)
		{
			var request = context.Request;
			string text;
			bool result = true;
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			result = SetCurrCondText(text);

			return "{\"result\":\"" + (result ? "Success" : "Failed") + "\"}";
		}

		private bool SetCurrCondText(string currCondText)
		{
			string fileName = cumulus.AppDir + "currentconditions.txt";
			try
			{
				cumulus.LogMessage("Writing current conditions to file...");

				System.IO.File.WriteAllText(fileName, currCondText);
				return true;
			}
			catch (Exception e)
			{
				cumulus.LogMessage("Error writing current conditions to file - " + e.Message);
				return false;
			}
		}

		internal class JsonEditRainData
		{
			public double raintoday { get; set; }
			public double raincounter { get; set; }
			public double startofdayrain { get; set; }
			public double rainmult { get; set; }
		}

		private void AddLastHourRainEntry(DateTime ts, double rain)
		{
			LastHourRainLog lasthourrain = new LastHourRainLog(ts, rain);

			HourRainLog.Add(lasthourrain);
		}

		private class LastHourRainLog
		{
			public DateTime timestamp;
			public double raincounter;

			public LastHourRainLog(DateTime ts, double rain)
			{
				timestamp = ts;
				raincounter = rain;
			}
		}

		private void RemoveOldRainData(DateTime ts)
		{
			DateTime onehourago = ts.AddHours(-1);

			if (HourRainLog.Count > 0)
			{
				// there are entries to consider
				while ((HourRainLog.Count > 0) && (HourRainLog.First().timestamp < onehourago))
				{
					// the oldest entry is older than 1 hour ago, delete it
					HourRainLog.RemoveAt(0);
				}
			}
		}
	}
}
