using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.UI;
using EmbedIO;
using Renci.SshNet.Common;
using ServiceStack;

namespace CumulusMX
{
	internal class DataEditor
	{
		private WeatherStation station;
		private readonly Cumulus cumulus;
		private WebTags webtags;


		internal DataEditor(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		internal void SetStation(WeatherStation station)
		{
			this.station = station;
		}

		internal void SetWebTags(WebTags webtags)
		{
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
					station.raindaystart = station.Raincounter - (station.RainToday / cumulus.Calib.Rain.Mult);
					cumulus.LogMessage("After rain today edit,  raintoday=" + station.RainToday.ToString(cumulus.RainFormat) + " Raindaystart=" + station.raindaystart.ToString(cumulus.RainFormat));
					// force the rainthismonth/rainthisyear values to be recalculated
					station.UpdateYearMonthRainfall();
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Edit rain today: " + ex.Message);
				}
			}

			var json = "{\"raintoday\":\"" + station.RainToday.ToString(cumulus.RainFormat, invC) +
				"\",\"raincounter\":\"" + station.Raincounter.ToString(cumulus.RainFormat, invC) +
				"\",\"startofdayrain\":\"" + station.raindaystart.ToString(cumulus.RainFormat, invC) +
				"\",\"rainmult\":\"" + cumulus.Calib.Rain.Mult.ToString("F3", invC) + "\"}";

			return json;
		}

		internal string GetRainTodayEditData()
		{
			var invC = new CultureInfo("");
			var step = (cumulus.RainDPlaces == 1 ? "0.1" : "0.01");
			var json = "{\"raintoday\":\"" + station.RainToday.ToString(cumulus.RainFormat, invC) +
				"\",\"raincounter\":\"" + station.Raincounter.ToString(cumulus.RainFormat, invC) +
				"\",\"startofdayrain\":\"" + station.raindaystart.ToString(cumulus.RainFormat, invC) +
				"\",\"rainmult\":\"" + cumulus.Calib.Rain.Mult.ToString("F3", invC) +
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


				ServiceStack.Text.JsConfig<DateTime>.DeSerializeFn = datetimeStr =>
				{
					if (string.IsNullOrWhiteSpace(datetimeStr))
					{
						return DateTime.MinValue;
					}

					DateTime.TryParseExact(datetimeStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime resultLocal);

					return resultLocal;
				};


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

				ServiceStack.Text.JsConfig<DateTime>.DeSerializeFn = datetimeStr =>
				{
					if (string.IsNullOrWhiteSpace(datetimeStr))
					{
						return DateTime.MinValue;
					}

					DateTime.TryParseExact(datetimeStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime resultLocal);
					return resultLocal;
				};

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
			const string timeStampFormat = "g";
			const string dateStampFormat = "d";
			const string monthFormat = "MMM yyyy";

			// Records - Temperature values
			var json = new StringBuilder("{", 1700);
			json.Append($"\"highTempVal\":\"{station.AllTime.HighTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempVal\":\"{station.AllTime.LowTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.AllTime.HighDewPoint.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.AllTime.LowDewPoint.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.AllTime.HighAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.AllTime.LowAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.AllTime.HighFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.AllTime.LowFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.AllTime.HighHumidex.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.AllTime.LowChill.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.AllTime.HighHeatIndex.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.AllTime.HighMinTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.AllTime.LowMaxTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.AllTime.HighDailyTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.AllTime.LowDailyTempRange.GetValString(cumulus.TempFormat)}\",");
			// Records - Temperature timestamps
			json.Append($"\"highTempTime\":\"{station.AllTime.HighTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.AllTime.LowTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.AllTime.HighDewPoint.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.AllTime.LowDewPoint.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.AllTime.HighAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.AllTime.LowAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.AllTime.HighFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.AllTime.LowFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.AllTime.HighHumidex.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.AllTime.LowChill.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.AllTime.HighHeatIndex.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.AllTime.HighMinTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.AllTime.LowMaxTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.AllTime.HighDailyTempRange.GetTsString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.AllTime.LowDailyTempRange.GetTsString(dateStampFormat)}\",");
			// Records - Humidity values
			json.Append($"\"highHumidityVal\":\"{station.AllTime.HighHumidity.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.AllTime.LowHumidity.GetValString(cumulus.HumFormat)}\",");
			// Records - Humidity times
			json.Append($"\"highHumidityTime\":\"{station.AllTime.HighHumidity.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.AllTime.LowHumidity.GetTsString(timeStampFormat)}\",");
			// Records - Pressure values
			json.Append($"\"highBarometerVal\":\"{station.AllTime.HighPress.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.AllTime.LowPress.GetValString(cumulus.PressFormat)}\",");
			// Records - Pressure times
			json.Append($"\"highBarometerTime\":\"{station.AllTime.HighPress.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.AllTime.LowPress.GetTsString(timeStampFormat)}\",");
			// Records - Wind values
			json.Append($"\"highGustVal\":\"{station.AllTime.HighGust.GetValString(cumulus.WindFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.AllTime.HighWind.GetValString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.AllTime.HighWindRun.GetValString(cumulus.WindRunFormat)}\",");
			// Records - Wind times
			json.Append($"\"highGustTime\":\"{station.AllTime.HighGust.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.AllTime.HighWind.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.AllTime.HighWindRun.GetTsString(dateStampFormat)}\",");
			// Records - Rain values
			json.Append($"\"highRainRateVal\":\"{station.AllTime.HighRainRate.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.AllTime.HourlyRain.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.AllTime.DailyRain.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRain24hVal\":\"{station.AllTime.HighRain24Hours.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highMonthlyRainVal\":\"{station.AllTime.MonthlyRain.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.AllTime.LongestDryPeriod.GetValString("f0")}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.AllTime.LongestWetPeriod.GetValString("f0")}\",");
			// Records - Rain times
			json.Append($"\"highRainRateTime\":\"{station.AllTime.HighRainRate.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.AllTime.HourlyRain.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.AllTime.DailyRain.GetTsString(dateStampFormat)}\",");
			json.Append($"\"highRain24hTime\":\"{station.AllTime.HighRain24Hours.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highMonthlyRainTime\":\"{station.AllTime.MonthlyRain.GetTsString(monthFormat)}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.AllTime.LongestDryPeriod.GetTsString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.AllTime.LongestWetPeriod.GetTsString(dateStampFormat)}\"");
			json.Append('}');

			return json.ToString();
		}

		internal string GetRecordsDayFile(string recordType, DateTime? start = null, DateTime? end = null)
		{
			var timeStampFormat = "g";
			var dateStampFormat = "d";
			var monthFormat = "MMM yyyy";

			var highTemp = new LocalRec(true);
			var lowTemp = new LocalRec(false);
			var highDewPt = new LocalRec(true);
			var lowDewPt = new LocalRec(false);
			var highAppTemp = new LocalRec(true);
			var lowAppTemp = new LocalRec(false);
			var highFeelsLike = new LocalRec(true);
			var lowFeelsLike = new LocalRec(false);
			var highHumidex = new LocalRec(true);
			var lowWindChill = new LocalRec(false);
			var highHeatInd = new LocalRec(true);
			var highMinTemp = new LocalRec(true);
			var lowMaxTemp = new LocalRec(false);
			var highTempRange = new LocalRec(true);
			var lowTempRange = new LocalRec(false);
			var highHum = new LocalRec(true);
			var lowHum = new LocalRec(false);
			var highBaro = new LocalRec(true);
			var lowBaro = new LocalRec(false);
			var highGust = new LocalRec(true);
			var highWind = new LocalRec(true);
			var highWindRun = new LocalRec(true);
			var highRainRate = new LocalRec(true);
			var highRainHour = new LocalRec(true);
			var highRainDay = new LocalRec(true);
			var highRainMonth = new LocalRec(true);
			var highRain24h = new LocalRec(true);
			var dryPeriod = new LocalRec(true);
			var wetPeriod = new LocalRec(true);

			var thisDate = DateTime.MinValue;
			DateTime startDate;
			DateTime endDate = DateTime.Now;

			switch (recordType)
			{
				case "alltime":
					startDate = DateTime.MinValue;
					break;
				case "thisyear":
					var now = DateTime.Now;
					startDate = new DateTime(now.Year, 1, 1);
					break;
				case "thismonth":
					now = DateTime.Now;
					startDate = new DateTime(now.Year, now.Month, 1);
					break;
				case "thisperiod":
					startDate = start.Value;
					endDate = end.Value;
					timeStampFormat = "f";
					dateStampFormat = "D";
					break;
				default:
					startDate = DateTime.MinValue;
					break;
			}

			var rainThisMonth = 0.0;
			var currentDryPeriod = 0;
			var currentWetPeriod = 0;
			var isDryNow = false;
			var thisDateDry = DateTime.MinValue;
			var thisDateWet = DateTime.MinValue;
			var firstRec = true;

			var json = new StringBuilder("{", 2048);

			int rainThreshold;
			if (cumulus.RainDayThreshold > 0)
			{
				rainThreshold = Convert.ToInt32(cumulus.RainDayThreshold * 1000);
			}
			else
			{
				// default
				if (cumulus.Units.Rain == 0)
				{
					rainThreshold = 200; // 0.2mm *1000
				}
				else
				{
					rainThreshold = 10;  // 0.01in *1000
				}
			}

			// Read the dayfile list extract the records from there
			if (station.DayFile.Count() > 0)
			{
				var data = station.DayFile.Where(r => r.Date >= startDate && r.Date <= endDate).ToList();
				foreach (var rec in data)
				{
					if (firstRec)
					{
						thisDate = rec.Date.Date;
						firstRec = false;
					}

					// This assumes the day file is in date order!
					if (thisDate.Month != rec.Date.Month)
					{
						// monthly rain
						if (rainThisMonth > highRainMonth.Value)
						{
							highRainMonth.Value = rainThisMonth;
							highRainMonth.Ts = thisDate;
						}
						// reset the date and counter for a new month
						thisDate = rec.Date.Date;
						rainThisMonth = 0;
					}
					// hi gust
					if (rec.HighGust > highGust.Value)
					{
						highGust.Value = rec.HighGust;
						highGust.Ts = rec.HighGustTime;
					}
					// hi temp
					if (rec.HighTemp > highTemp.Value)
					{
						highTemp.Value = rec.HighTemp;
						highTemp.Ts = rec.HighTempTime;
					}
					// lo temp
					if (rec.LowTemp < lowTemp.Value)
					{
						lowTemp.Value = rec.LowTemp;
						lowTemp.Ts = rec.LowTempTime;
					}
					// hi min temp
					if (rec.LowTemp > highMinTemp.Value)
					{
						highMinTemp.Value = rec.LowTemp;
						highMinTemp.Ts = rec.LowTempTime;
					}
					// lo max temp
					if (rec.HighTemp < lowMaxTemp.Value)
					{
						lowMaxTemp.Value = rec.HighTemp;
						lowMaxTemp.Ts = rec.HighTempTime;
					}
					// hi temp range
					if ((rec.HighTemp - rec.LowTemp) > highTempRange.Value)
					{
						highTempRange.Value = rec.HighTemp - rec.LowTemp;
						highTempRange.Ts = rec.Date.Date;
					}
					// lo temp range
					if ((rec.HighTemp - rec.LowTemp) < lowTempRange.Value)
					{
						lowTempRange.Value = rec.HighTemp - rec.LowTemp;
						lowTempRange.Ts = rec.Date.Date;
					}
					// lo baro
					if (rec.LowPress < lowBaro.Value)
					{
						lowBaro.Value = rec.LowPress;
						lowBaro.Ts = rec.LowPressTime;
					}
					// hi baro
					if (rec.HighPress > highBaro.Value)
					{
						highBaro.Value = rec.HighPress;
						highBaro.Ts = rec.HighPressTime;
					}
					// hi rain rate
					if (rec.HighRainRate > highRainRate.Value)
					{
						highRainRate.Value = rec.HighRainRate;
						highRainRate.Ts = rec.HighRainRateTime;
					}
					// hi rain day
					if (rec.TotalRain > highRainDay.Value)
					{
						highRainDay.Value = rec.TotalRain;
						highRainDay.Ts = rec.Date.Date;
					}

					// monthly rain
					rainThisMonth += rec.TotalRain;

					// 24h rain
					if (rec.HighRain24h > highRain24h.Value)
					{
						highRain24h.Value = rec.HighRain24h;
						highRain24h.Ts = rec.HighRain24hTime;
					}

					// dry/wet period
					if (Convert.ToInt32(rec.TotalRain * 1000) >= rainThreshold)
					{
						if (isDryNow)
						{
							currentWetPeriod = 1;
							isDryNow = false;
							if (!(dryPeriod.Value == Cumulus.DefaultHiVal && currentDryPeriod == 0) && currentDryPeriod > dryPeriod.Value)
							{
								dryPeriod.Value = currentDryPeriod;
								dryPeriod.Ts = thisDateDry;
							}
							currentDryPeriod = 0;
						}
						else
						{
							currentWetPeriod++;
							thisDateWet = rec.Date.Date;
						}
					}
					else
					{
						if (isDryNow)
						{
							currentDryPeriod++;
							thisDateDry = rec.Date.Date;
						}
						else
						{
							currentDryPeriod = 1;
							isDryNow = true;
							if (!(wetPeriod.Value == Cumulus.DefaultHiVal && currentWetPeriod == 0) && currentWetPeriod > wetPeriod.Value)
							{
								wetPeriod.Value = currentWetPeriod;
								wetPeriod.Ts = thisDateWet;
							}
							currentWetPeriod = 0;
						}
					}
					// hi wind run
					if (rec.WindRun > highWindRun.Value)
					{
						highWindRun.Value = rec.WindRun;
						highWindRun.Ts = rec.Date.Date;
					}
					// hi wind
					if (rec.HighAvgWind > highWind.Value)
					{
						highWind.Value = rec.HighAvgWind;
						highWind.Ts = rec.HighAvgWindTime;
					}
					// lo humidity
					if (rec.LowHumidity < lowHum.Value)
					{
						lowHum.Value = rec.LowHumidity;
						lowHum.Ts = rec.LowHumidityTime;
					}
					// hi humidity
					if (rec.HighHumidity > highHum.Value)
					{
						highHum.Value = rec.HighHumidity;
						highHum.Ts = rec.HighHumidityTime;
					}
					// hi heat index
					if (rec.HighHeatIndex > highHeatInd.Value)
					{
						highHeatInd.Value = rec.HighHeatIndex;
						highHeatInd.Ts = rec.HighHeatIndexTime;
					}
					// hi app temp
					if (rec.HighAppTemp > highAppTemp.Value)
					{
						highAppTemp.Value = rec.HighAppTemp;
						highAppTemp.Ts = rec.HighAppTempTime;
					}
					// lo app temp
					if (rec.LowAppTemp < lowAppTemp.Value)
					{
						lowAppTemp.Value = rec.LowAppTemp;
						lowAppTemp.Ts = rec.LowAppTempTime;
					}
					// hi rain hour
					if (rec.HighHourlyRain > highRainHour.Value)
					{
						highRainHour.Value = rec.HighHourlyRain;
						highRainHour.Ts = rec.HighHourlyRainTime;
					}
					// lo wind chill
					if (rec.LowWindChill < lowWindChill.Value)
					{
						lowWindChill.Value = rec.LowWindChill;
						lowWindChill.Ts = rec.LowWindChillTime;
					}
					// hi dewpt
					if (rec.HighDewPoint > highDewPt.Value)
					{
						highDewPt.Value = rec.HighDewPoint;
						highDewPt.Ts = rec.HighDewPointTime;
					}
					// lo dewpt
					if (rec.LowDewPoint < lowDewPt.Value)
					{
						lowDewPt.Value = rec.LowDewPoint;
						lowDewPt.Ts = rec.LowDewPointTime;
					}
					// hi feels like
					if (rec.HighFeelsLike > highFeelsLike.Value)
					{
						highFeelsLike.Value = rec.HighFeelsLike;
						highFeelsLike.Ts = rec.HighFeelsLikeTime;
					}
					// lo feels like
					if (rec.LowFeelsLike < lowFeelsLike.Value)
					{
						lowFeelsLike.Value = rec.LowFeelsLike;
						lowFeelsLike.Ts = rec.LowFeelsLikeTime;
					}
					// hi humidex
					if (rec.HighHumidex > highHumidex.Value)
					{
						highHumidex.Value = rec.HighHumidex;
						highHumidex.Ts = rec.HighHumidexTime;
					}
				}

				// We need to check if the run or wet/dry days at the end of logs exceeds any records
				if (!(wetPeriod.Value == Cumulus.DefaultHiVal && currentWetPeriod == 0) && currentWetPeriod > wetPeriod.Value)
				{
					wetPeriod.Value = currentWetPeriod;
					wetPeriod.Ts = thisDateWet;
				}
				if (!(dryPeriod.Value == Cumulus.DefaultHiVal && currentDryPeriod == 0) && currentDryPeriod > dryPeriod.Value)
				{
					dryPeriod.Value = currentDryPeriod;
					dryPeriod.Ts = thisDateDry;
				}

				// need to do the final monthly rainfall
				if (rainThisMonth > highRainMonth.Value)
				{
					highRainMonth.Value = rainThisMonth;
					highRainMonth.Ts = thisDate;
				}
			}
			else
			{
				cumulus.LogMessage("GetRecordsDayFile: Error no day file records found");
			}

			json.Append($"\"highTempValDayfile\":\"{highTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTimeDayfile\":\"{highTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowTempValDayfile\":\"{lowTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTimeDayfile\":\"{lowTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDewPointValDayfile\":\"{highDewPt.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTimeDayfile\":\"{highDewPt.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointValDayfile\":\"{lowDewPt.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTimeDayfile\":\"{lowDewPt.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempValDayfile\":\"{highAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTimeDayfile\":\"{highAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempValDayfile\":\"{lowAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTimeDayfile\":\"{lowAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeValDayfile\":\"{highFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTimeDayfile\":\"{highFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeValDayfile\":\"{lowFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTimeDayfile\":\"{lowFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHumidexValDayfile\":\"{highHumidex.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTimeDayfile\":\"{highHumidex.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillValDayfile\":\"{lowWindChill.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTimeDayfile\":\"{lowWindChill.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexValDayfile\":\"{highHeatInd.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTimeDayfile\":\"{highHeatInd.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highMinTempValDayfile\":\"{highMinTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTimeDayfile\":\"{highMinTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempValDayfile\":\"{lowMaxTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTimeDayfile\":\"{lowMaxTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyTempRangeValDayfile\":\"{highTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTimeDayfile\":\"{highTempRange.GetTsString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeValDayfile\":\"{lowTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTimeDayfile\":\"{lowTempRange.GetTsString(dateStampFormat)}\",");
			json.Append($"\"highHumidityValDayfile\":\"{highHum.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTimeDayfile\":\"{highHum.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityValDayfile\":\"{lowHum.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTimeDayfile\":\"{lowHum.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highBarometerValDayfile\":\"{highBaro.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTimeDayfile\":\"{highBaro.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerValDayfile\":\"{lowBaro.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTimeDayfile\":\"{lowBaro.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highGustValDayfile\":\"{highGust.GetValString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTimeDayfile\":\"{highGust.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindValDayfile\":\"{highWind.GetValString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTimeDayfile\":\"{highWind.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindRunValDayfile\":\"{highWindRun.GetValString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTimeDayfile\":\"{highWindRun.GetTsString(dateStampFormat)}\",");
			json.Append($"\"highRainRateValDayfile\":\"{highRainRate.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTimeDayfile\":\"{highRainRate.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainValDayfile\":\"{highRainHour.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTimeDayfile\":\"{highRainHour.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainValDayfile\":\"{highRainDay.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTimeDayfile\":\"{highRainDay.GetTsString(dateStampFormat)}\",");
			if (recordType != "thismonth")
			{
				json.Append($"\"highMonthlyRainValDayfile\":\"{highRainMonth.GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"highMonthlyRainTimeDayfile\":\"{highRainMonth.GetTsString(monthFormat)}\",");
			}
			json.Append($"\"highRain24hValDayfile\":\"{highRain24h.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRain24hTimeDayfile\":\"{highRain24h.GetTsString(timeStampFormat)}\",");
			json.Append($"\"longestDryPeriodValDayfile\":\"{dryPeriod.GetValString()}\",");
			json.Append($"\"longestDryPeriodTimeDayfile\":\"{dryPeriod.GetTsString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodValDayfile\":\"{wetPeriod.GetValString()}\",");
			json.Append($"\"longestWetPeriodTimeDayfile\":\"{wetPeriod.GetTsString(dateStampFormat)}\"");
			json.Append('}');

			return json.ToString();
		}

		internal string GetRecordsLogFile(string recordType)
		{
			const string timeStampFormat = "g";
			const string dateStampFormat = "d";
			const string monthFormat = "MMM yyyy";

			var json = new StringBuilder("{", 2048);
			DateTime filedate, datefrom;

			switch (recordType)
			{
				case "alltime":
					datefrom = cumulus.RecordsBeganDateTime;
					break;
				case "thisyear":
					var now = DateTime.Now;
					filedate = new DateTime(now.Year, 1, 1).Date;
					datefrom = filedate.AddDays(-1);
					break;
				case "thismonth":
					now = DateTime.Now;
					filedate = new DateTime(now.Year, now.Month, 1).Date;
					datefrom = filedate.AddDays(-1);
					break;
				default:
					datefrom = cumulus.RecordsBeganDateTime;
					break;
			}
			var dateto = DateTime.Now.Date;

			// we have to go back 24 hour to calculate rain in 24h value
			filedate = datefrom.AddDays(-1);

			var logFile = cumulus.GetLogFileName(filedate);
			var started = false;
			var finished = false;
			var lastentrydate = datefrom;
			var lastentryrain = 0.0;
			var lastentrycounter = 0.0;

			var isDryNow = false;
			var currentDryPeriod = 0;
			var currentWetPeriod = 0;

			int rainThreshold;
			if (cumulus.RainDayThreshold > 0)
			{
				rainThreshold = Convert.ToInt32(cumulus.RainDayThreshold * 1000);
			}
			else
			{
				// default
				if (cumulus.Units.Rain == 0)
				{
					rainThreshold = 200; // 0.2mm *1000
				}
				else
				{
					rainThreshold = 10;  // 0.01in *1000
				}
			}

			// what do we deem to be too large a jump in the rainfall counter to be true? use 20 mm or 0.8 inches
			var counterJumpTooBig = cumulus.Units.Rain == 0 ? 20 : 0.8;

			var highTemp = new LocalRec(true);
			var lowTemp = new LocalRec(false);
			var highDewPt = new LocalRec(true);
			var lowDewPt = new LocalRec(false);
			var highAppTemp = new LocalRec(true);
			var lowAppTemp = new LocalRec(false);
			var highFeelsLike = new LocalRec(true);
			var lowFeelsLike = new LocalRec(false);
			var highHumidex = new LocalRec(true);
			var lowWindChill = new LocalRec(false);
			var highHeatInd = new LocalRec(true);
			var highMinTemp = new LocalRec(true);
			var lowMaxTemp = new LocalRec(false);
			var highTempRange = new LocalRec(true);
			var lowTempRange = new LocalRec(false);
			var highHum = new LocalRec(true);
			var lowHum = new LocalRec(false);
			var highBaro = new LocalRec(true);
			var lowBaro = new LocalRec(false);
			var highGust = new LocalRec(true);
			var highWind = new LocalRec(true);
			var highWindRun = new LocalRec(true);
			var highRainRate = new LocalRec(true);
			var highRainHour = new LocalRec(true);
			var highRainDay = new LocalRec(true);
			var highRain24h = new LocalRec(true);
			var highRainMonth = new LocalRec(true);
			var dryPeriod = new LocalRec(true);
			var wetPeriod = new LocalRec(true);

			var currentDay = datefrom;
			var dayHighTemp = new LocalRec(true);
			var dayLowTemp = new LocalRec(false);
			double dayWindRun = 0;
			double dayRain = 0;

			highRainHour.Value = 0;
			highRain24h.Value = 0;

			var thisDateDry = DateTime.MinValue;
			var thisDateWet = DateTime.MinValue;

			var rain1hLog = new Queue<LastHourRainLog>();
			var rain24hLog = new Queue<LastHourRainLog>();

			var totalRainfall = 0.0;
			var monthlyRain = 0.0;

			var watch = System.Diagnostics.Stopwatch.StartNew();

			double _day24h = 0;
			DateTime _dayTs;

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetAllTimeRecLogFile: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);

						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;

							var rec = station.ParseLogFileRec(line, true);

							// We need to work in meteo dates not clock dates for day hi/lows
							var metoDate = rec.Date.AddHours(cumulus.GetHourInc());

							if (!started)
							{
								if (metoDate >= datefrom)
								{
									lastentrydate = rec.Date;
									currentDay = metoDate;
									started = true;
									totalRainfall = lastentryrain;
								}
								else if (metoDate < filedate)
								{
									continue;
								}
								else
								{
									// OK we are within 24 hours of the start date, so record rain values
									AddLastHoursRainEntry(rec.Date, totalRainfall + rec.RainToday, ref rain1hLog, ref rain24hLog);
									lastentryrain = rec.RainToday;
									lastentrycounter = rec.Raincounter;
									continue;
								}
							}

							// low chill
							if (rec.WindChill < lowWindChill.Value)
							{
								lowWindChill.Value = rec.WindChill;
								lowWindChill.Ts = rec.Date;
							}
							// hi heat
							if (rec.HeatIndex > highHeatInd.Value)
							{
								highHeatInd.Value = rec.HeatIndex;
								highHeatInd.Ts = rec.Date;
							}
							// hi/low appt
							if (rec.ApparentTemperature > Cumulus.DefaultHiVal)
							{
								if (rec.ApparentTemperature > highAppTemp.Value)
								{
									highAppTemp.Value = rec.ApparentTemperature;
									highAppTemp.Ts = rec.Date;
								}
								if (rec.ApparentTemperature < lowAppTemp.Value)
								{
									lowAppTemp.Value = rec.ApparentTemperature;
									lowAppTemp.Ts = rec.Date;
								}
							}
							// hi/low feels like
							if (rec.FeelsLike > Cumulus.DefaultHiVal)
							{
								if (rec.FeelsLike > highFeelsLike.Value)
								{
									highFeelsLike.Value = rec.FeelsLike;
									highFeelsLike.Ts = rec.Date;
								}
								if (rec.FeelsLike < lowFeelsLike.Value)
								{
									lowFeelsLike.Value = rec.FeelsLike;
									lowFeelsLike.Ts = rec.Date;
								}
							}

							// hi/low humidex
							if (rec.Humidex > highHumidex.Value)
							{
								highHumidex.Value = rec.Humidex;
								highHumidex.Ts = rec.Date;
							}

							// hi temp
							if (rec.OutdoorTemperature > highTemp.Value)
							{
								highTemp.Value = rec.OutdoorTemperature;
								highTemp.Ts = rec.Date;
							}
							// lo temp
							if (rec.OutdoorTemperature < lowTemp.Value)
							{
								lowTemp.Value = rec.OutdoorTemperature;
								lowTemp.Ts = rec.Date;
							}
							// hi dewpoint
							if (rec.OutdoorDewpoint > highDewPt.Value)
							{
								highDewPt.Value = rec.OutdoorDewpoint;
								highDewPt.Ts = rec.Date;
							}
							// low dewpoint
							if (rec.OutdoorDewpoint < lowDewPt.Value)
							{
								lowDewPt.Value = rec.OutdoorDewpoint;
								lowDewPt.Ts = rec.Date;
							}
							// hi hum
							if (rec.OutdoorHumidity > highHum.Value)
							{
								highHum.Value = rec.OutdoorHumidity;
								highHum.Ts = rec.Date;
							}
							// lo hum
							if (rec.OutdoorHumidity < lowHum.Value)
							{
								lowHum.Value = rec.OutdoorHumidity;
								lowHum.Ts = rec.Date;
							}
							// hi baro
							if (rec.Pressure > highBaro.Value)
							{
								highBaro.Value = rec.Pressure;
								highBaro.Ts = rec.Date;
							}
							// lo hum
							if (rec.Pressure < lowBaro.Value)
							{
								lowBaro.Value = rec.Pressure;
								lowBaro.Ts = rec.Date;
							}
							// hi gust
							if (rec.RecentMaxGust > highGust.Value)
							{
								highGust.Value = rec.RecentMaxGust;
								highGust.Ts = rec.Date;
							}
							// hi wind
							if (rec.WindAverage > highWind.Value)
							{
								highWind.Value = rec.WindAverage;
								highWind.Ts = rec.Date;
							}
							// hi rain rate
							if (rec.RainRate > highRainRate.Value)
							{
								highRainRate.Value = rec.RainRate;
								highRainRate.Ts = rec.Date;
							}

							dayWindRun += rec.Date.Subtract(lastentrydate).TotalHours * rec.WindAverage;

							if (dayWindRun > highWindRun.Value)
							{
								highWindRun.Value = dayWindRun;
								highWindRun.Ts = currentDay;
							}

							// new meteo day
							if (currentDay.Date != metoDate.Date)
							{
								if (dayHighTemp.Value < lowMaxTemp.Value)
								{
									lowMaxTemp.Value = dayHighTemp.Value;
									lowMaxTemp.Ts = dayHighTemp.Ts;
								}
								if (dayLowTemp.Value > highMinTemp.Value)
								{
									highMinTemp.Value = dayLowTemp.Value;
									highMinTemp.Ts = dayLowTemp.Ts;
								}
								if (dayHighTemp.Value - dayLowTemp.Value > highTempRange.Value)
								{
									highTempRange.Value = dayHighTemp.Value - dayLowTemp.Value;
									highTempRange.Ts = currentDay;
								}
								if (dayHighTemp.Value - dayLowTemp.Value < lowTempRange.Value)
								{
									lowTempRange.Value = dayHighTemp.Value - dayLowTemp.Value;
									lowTempRange.Ts = currentDay;
								}

								// logging format changed on with C1 v1.9.3 b1055 in Dec 2012
								// before that date the 00:00 log entry contained the rain total for the day before and the next log entry was reset to zero
								// after that build the total was reset to zero in the entry
								// messy!
								// no final rainfall entry after this date (approx). The best we can do is add in the increase in rain counter during this preiod
								var rollovertime = new TimeSpan(-cumulus.GetHourInc(), 0, 0);
								if (rec.RainToday > 0 && rec.Date.TimeOfDay == rollovertime)
								{
									dayRain = rec.RainToday;
								}
								else if ((rec.Raincounter - lastentrycounter > 0) && (rec.Raincounter - lastentrycounter < counterJumpTooBig))
								{
									dayRain += (rec.Raincounter - lastentrycounter) * cumulus.Calib.Rain.Mult;
								}

								if (dayRain > highRainDay.Value)
								{
									highRainDay.Value = dayRain;
									highRainDay.Ts = currentDay;
								}

								monthlyRain += dayRain;

								if (monthlyRain > highRainMonth.Value)
								{
									highRainMonth.Value = monthlyRain;
									highRainMonth.Ts = currentDay.Date;
								}

								if (currentDay.Month != metoDate.Month)
								{
									monthlyRain = 0;
								}

								// dry/wet period
								if (Convert.ToInt32(dayRain * 1000) >= rainThreshold)
								{
									if (isDryNow)
									{
										currentWetPeriod = 1;
										isDryNow = false;
										if (!(dryPeriod.Value == Cumulus.DefaultHiVal && currentDryPeriod == 0) && currentDryPeriod > dryPeriod.Value)
										{
											dryPeriod.Value = currentDryPeriod;
											dryPeriod.Ts = thisDateDry;
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
										if (!(wetPeriod.Value == Cumulus.DefaultHiVal && currentWetPeriod == 0) && currentWetPeriod > wetPeriod.Value)
										{
											wetPeriod.Value = currentWetPeriod;
											wetPeriod.Ts = thisDateWet;
										}
										currentWetPeriod = 0;
									}
								}

							}
							else
							{
								dayRain = rec.RainToday;
							}

							if (rec.OutdoorTemperature > dayHighTemp.Value)
							{
								dayHighTemp.Value = rec.OutdoorTemperature;
								dayHighTemp.Ts = rec.Date;
							}

							if (rec.OutdoorTemperature < dayLowTemp.Value)
							{
								dayLowTemp.Value = rec.OutdoorTemperature;
								dayLowTemp.Ts = rec.Date;
							}

							// hourly rain
							/*
							 * need to track what the rainfall has been in the last rolling hour and 24 hours
							 * across day rollovers where the count resets
							 */
							AddLastHoursRainEntry(rec.Date, totalRainfall + dayRain, ref rain1hLog, ref rain24hLog);

							var rainThisHour = rain1hLog.Last().Raincounter - rain1hLog.Peek().Raincounter;
							if (rainThisHour > highRainHour.Value)
							{
								highRainHour.Value = rainThisHour;
								highRainHour.Ts = rec.Date;
							}

							var rain24h = rain24hLog.Last().Raincounter - rain24hLog.Peek().Raincounter;
							if (rain24h > highRain24h.Value)
							{
								highRain24h.Value = rain24h;
								highRain24h.Ts = rec.Date;
							}
							if (rain24h > _day24h)
							{
								_day24h = rain24h;
								_dayTs = rec.Date;
								//System.Diagnostics.Debugger.Break();
							}

							// new meteo day, part 2
							if (currentDay.Date != metoDate.Date)
							{
								currentDay = metoDate;
								dayHighTemp.Value = rec.OutdoorTemperature;
								dayLowTemp.Value = rec.OutdoorTemperature;
								dayWindRun = 0;
								totalRainfall += dayRain;

								_day24h = rain24h;
								_dayTs = rec.Date;
							}

							lastentrydate = rec.Date;
							lastentrycounter = rec.Raincounter;
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"GetRecordsLogFile: Error at line {linenum} of {logFile} : {e.Message}");
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

			rain1hLog.Clear();
			rain24hLog.Clear();

			// We need to check if the run or wet/dry days at the end of logs exceeds any records
			if (!(wetPeriod.Value == Cumulus.DefaultHiVal && currentWetPeriod == 0) && currentWetPeriod > wetPeriod.Value)
			{
				wetPeriod.Value = currentWetPeriod;
				wetPeriod.Ts = thisDateWet;
			}
			if (!(dryPeriod.Value == Cumulus.DefaultHiVal && currentDryPeriod == 0) && currentDryPeriod > dryPeriod.Value)
			{
				dryPeriod.Value = currentDryPeriod;
				dryPeriod.Ts = thisDateDry;
			}

			json.Append($"\"highTempValLogfile\":\"{highTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTimeLogfile\":\"{highTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowTempValLogfile\":\"{lowTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTimeLogfile\":\"{lowTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDewPointValLogfile\":\"{highDewPt.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTimeLogfile\":\"{highDewPt.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointValLogfile\":\"{lowDewPt.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTimeLogfile\":\"{lowDewPt.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempValLogfile\":\"{highAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTimeLogfile\":\"{highAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempValLogfile\":\"{lowAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTimeLogfile\":\"{lowAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeValLogfile\":\"{highFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTimeLogfile\":\"{highFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeValLogfile\":\"{lowFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTimeLogfile\":\"{lowFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHumidexValLogfile\":\"{highHumidex.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTimeLogfile\":\"{highHumidex.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillValLogfile\":\"{lowWindChill.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTimeLogfile\":\"{lowWindChill.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexValLogfile\":\"{highHeatInd.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTimeLogfile\":\"{highHeatInd.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highMinTempValLogfile\":\"{highMinTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTimeLogfile\":\"{highMinTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempValLogfile\":\"{lowMaxTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTimeLogfile\":\"{lowMaxTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyTempRangeValLogfile\":\"{highTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTimeLogfile\":\"{highTempRange.GetTsString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeValLogfile\":\"{lowTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTimeLogfile\":\"{lowTempRange.GetTsString(dateStampFormat)}\",");
			json.Append($"\"highHumidityValLogfile\":\"{highHum.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTimeLogfile\":\"{highHum.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityValLogfile\":\"{lowHum.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTimeLogfile\":\"{lowHum.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highBarometerValLogfile\":\"{highBaro.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTimeLogfile\":\"{highBaro.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerValLogfile\":\"{lowBaro.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTimeLogfile\":\"{lowBaro.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highGustValLogfile\":\"{highGust.GetValString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTimeLogfile\":\"{highGust.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindValLogfile\":\"{highWind.GetValString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTimeLogfile\":\"{highWind.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindRunValLogfile\":\"{highWindRun.GetValString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTimeLogfile\":\"{highWindRun.GetTsString(dateStampFormat)}\",");
			json.Append($"\"highRainRateValLogfile\":\"{highRainRate.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTimeLogfile\":\"{highRainRate.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainValLogfile\":\"{highRainHour.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTimeLogfile\":\"{highRainHour.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainValLogfile\":\"{highRainDay.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTimeLogfile\":\"{highRainDay.GetTsString(dateStampFormat)}\",");
			json.Append($"\"highRain24hValLogfile\":\"{highRain24h.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRain24hTimeLogfile\":\"{highRain24h.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highMonthlyRainValLogfile\":\"{highRainMonth.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highMonthlyRainTimeLogfile\":\"{highRainMonth.GetTsString(monthFormat)}\",");
			if (recordType == "alltime")
			{
				json.Append($"\"longestDryPeriodValLogfile\":\"{dryPeriod.GetValString()}\",");
				json.Append($"\"longestDryPeriodTimeLogfile\":\"{dryPeriod.GetTsString(dateStampFormat)}\",");
				json.Append($"\"longestWetPeriodValLogfile\":\"{wetPeriod.GetValString()}\",");
				json.Append($"\"longestWetPeriodTimeLogfile\":\"{wetPeriod.GetTsString(dateStampFormat)}\"");
			}
			else
			{
				json.Append($"\"longestDryPeriodValLogfile\":\"{dryPeriod.GetValString()}\",");
				json.Append($"\"longestDryPeriodTimeLogfile\":\"{dryPeriod.GetTsString(dateStampFormat)}\",");
				json.Append($"\"longestWetPeriodValLogfile\":\"{wetPeriod.GetValString()}\",");
				json.Append($"\"longestWetPeriodTimeLogfile\":\"{wetPeriod.GetTsString(dateStampFormat)}\"");
			}
			json.Append('}');

			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"GetRecordsLogFile: Logfiles parse = {elapsed} ms");

			return json.ToString();
		}

		/*
		private static DateTime GetDateTime(DateTime date, string time)
		{
			var tim = time.Split(CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator.ToCharArray()[0]);
			return new DateTime(date.Year, date.Month, date.Day, int.Parse(tim[0]), int.Parse(tim[1]), 0);
		}
		*/

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
			try
			{
				switch (field)
				{
					case "highTempVal":
						station.SetAlltime(station.AllTime.HighTemp, double.Parse(value), station.AllTime.HighTemp.Ts);
						break;
					case "highTempTime":
						station.SetAlltime(station.AllTime.HighTemp, station.AllTime.HighTemp.Val, localeDateTimeStrToDate(value));
						break;
					case "lowTempVal":
						station.SetAlltime(station.AllTime.LowTemp, double.Parse(value), station.AllTime.LowTemp.Ts);
						break;
					case "lowTempTime":
						station.SetAlltime(station.AllTime.LowTemp, station.AllTime.LowTemp.Val, localeDateTimeStrToDate(value));
						break;
					case "highDewPointVal":
						station.SetAlltime(station.AllTime.HighDewPoint, double.Parse(value), station.AllTime.HighDewPoint.Ts);
						break;
					case "highDewPointTime":
						station.SetAlltime(station.AllTime.HighDewPoint, station.AllTime.HighDewPoint.Val, localeDateTimeStrToDate(value));
						break;
					case "lowDewPointVal":
						station.SetAlltime(station.AllTime.LowDewPoint, double.Parse(value), station.AllTime.LowDewPoint.Ts);
						break;
					case "lowDewPointTime":
						station.SetAlltime(station.AllTime.LowDewPoint, station.AllTime.LowDewPoint.Val, localeDateTimeStrToDate(value));
						break;
					case "highApparentTempVal":
						station.SetAlltime(station.AllTime.HighAppTemp, double.Parse(value), station.AllTime.HighAppTemp.Ts);
						break;
					case "highApparentTempTime":
						station.SetAlltime(station.AllTime.HighAppTemp, station.AllTime.HighAppTemp.Val, localeDateTimeStrToDate(value));
						break;
					case "lowApparentTempVal":
						station.SetAlltime(station.AllTime.LowAppTemp, double.Parse(value), station.AllTime.LowAppTemp.Ts);
						break;
					case "lowApparentTempTime":
						station.SetAlltime(station.AllTime.LowAppTemp, station.AllTime.LowAppTemp.Val, localeDateTimeStrToDate(value));
						break;
					case "highFeelsLikeVal":
						station.SetAlltime(station.AllTime.HighFeelsLike, double.Parse(value), station.AllTime.HighFeelsLike.Ts);
						break;
					case "highFeelsLikeTime":
						station.SetAlltime(station.AllTime.HighFeelsLike, station.AllTime.HighFeelsLike.Val, localeDateTimeStrToDate(value));
						break;
					case "lowFeelsLikeVal":
						station.SetAlltime(station.AllTime.LowFeelsLike, double.Parse(value), station.AllTime.LowFeelsLike.Ts);
						break;
					case "lowFeelsLikeTime":
						station.SetAlltime(station.AllTime.LowFeelsLike, station.AllTime.LowFeelsLike.Val, localeDateTimeStrToDate(value));
						break;
					case "highHumidexVal":
						station.SetAlltime(station.AllTime.HighHumidex, double.Parse(value), station.AllTime.HighHumidex.Ts);
						break;
					case "highHumidexTime":
						station.SetAlltime(station.AllTime.HighHumidex, station.AllTime.HighHumidex.Val, localeDateTimeStrToDate(value));
						break;
					case "lowWindChillVal":
						station.SetAlltime(station.AllTime.LowChill, double.Parse(value), station.AllTime.LowChill.Ts);
						break;
					case "lowWindChillTime":
						station.SetAlltime(station.AllTime.LowChill, station.AllTime.LowChill.Val, localeDateTimeStrToDate(value));
						break;
					case "highHeatIndexVal":
						station.SetAlltime(station.AllTime.HighHeatIndex, double.Parse(value), station.AllTime.HighHeatIndex.Ts);
						break;
					case "highHeatIndexTime":
						station.SetAlltime(station.AllTime.HighHeatIndex, station.AllTime.HighHeatIndex.Val, localeDateTimeStrToDate(value));
						break;
					case "highMinTempVal":
						station.SetAlltime(station.AllTime.HighMinTemp, double.Parse(value), station.AllTime.HighMinTemp.Ts);
						break;
					case "highMinTempTime":
						station.SetAlltime(station.AllTime.HighMinTemp, station.AllTime.HighMinTemp.Val, localeDateTimeStrToDate(value));
						break;
					case "lowMaxTempVal":
						station.SetAlltime(station.AllTime.LowMaxTemp, double.Parse(value), station.AllTime.LowMaxTemp.Ts);
						break;
					case "lowMaxTempTime":
						station.SetAlltime(station.AllTime.LowMaxTemp, station.AllTime.LowMaxTemp.Val, localeDateTimeStrToDate(value));
						break;
					case "highDailyTempRangeVal":
						station.SetAlltime(station.AllTime.HighDailyTempRange, double.Parse(value), station.AllTime.HighDailyTempRange.Ts);
						break;
					case "highDailyTempRangeTime":
						station.SetAlltime(station.AllTime.HighDailyTempRange, station.AllTime.HighDailyTempRange.Val, localeDateTimeStrToDate(value));
						break;
					case "lowDailyTempRangeVal":
						station.SetAlltime(station.AllTime.LowDailyTempRange, double.Parse(value), station.AllTime.LowDailyTempRange.Ts);
						break;
					case "lowDailyTempRangeTime":
						station.SetAlltime(station.AllTime.LowDailyTempRange, station.AllTime.LowDailyTempRange.Val, localeDateTimeStrToDate(value));
						break;
					case "highHumidityVal":
						station.SetAlltime(station.AllTime.HighHumidity, double.Parse(value), station.AllTime.HighHumidity.Ts);
						break;
					case "highHumidityTime":
						station.SetAlltime(station.AllTime.HighHumidity, station.AllTime.HighHumidity.Val, localeDateTimeStrToDate(value));
						break;
					case "lowHumidityVal":
						station.SetAlltime(station.AllTime.LowHumidity, double.Parse(value), station.AllTime.LowHumidity.Ts);
						break;
					case "lowHumidityTime":
						station.SetAlltime(station.AllTime.LowHumidity, station.AllTime.LowHumidity.Val, localeDateTimeStrToDate(value));
						break;
					case "highBarometerVal":
						station.SetAlltime(station.AllTime.HighPress, double.Parse(value), station.AllTime.HighPress.Ts);
						break;
					case "highBarometerTime":
						station.SetAlltime(station.AllTime.HighPress, station.AllTime.HighPress.Val, localeDateTimeStrToDate(value));
						break;
					case "lowBarometerVal":
						station.SetAlltime(station.AllTime.LowPress, double.Parse(value), station.AllTime.LowPress.Ts);
						break;
					case "lowBarometerTime":
						station.SetAlltime(station.AllTime.LowPress, station.AllTime.LowPress.Val, localeDateTimeStrToDate(value));
						break;
					case "highGustVal":
						station.SetAlltime(station.AllTime.HighGust, double.Parse(value), station.AllTime.HighGust.Ts);
						break;
					case "highGustTime":
						station.SetAlltime(station.AllTime.HighGust, station.AllTime.HighGust.Val, localeDateTimeStrToDate(value));
						break;
					case "highWindVal":
						station.SetAlltime(station.AllTime.HighWind, double.Parse(value), station.AllTime.HighWind.Ts);
						break;
					case "highWindTime":
						station.SetAlltime(station.AllTime.HighWind, station.AllTime.HighWind.Val, localeDateTimeStrToDate(value));
						break;
					case "highWindRunVal":
						station.SetAlltime(station.AllTime.HighWindRun, double.Parse(value), station.AllTime.HighWindRun.Ts);
						break;
					case "highWindRunTime":
						station.SetAlltime(station.AllTime.HighWindRun, station.AllTime.HighWindRun.Val, localeDateTimeStrToDate(value));
						break;
					case "highRainRateVal":
						station.SetAlltime(station.AllTime.HighRainRate, double.Parse(value), station.AllTime.HighRainRate.Ts);
						break;
					case "highRainRateTime":
						station.SetAlltime(station.AllTime.HighRainRate, station.AllTime.HighRainRate.Val, localeDateTimeStrToDate(value));
						break;
					case "highHourlyRainVal":
						station.SetAlltime(station.AllTime.HourlyRain, double.Parse(value), station.AllTime.HourlyRain.Ts);
						break;
					case "highHourlyRainTime":
						station.SetAlltime(station.AllTime.HourlyRain, station.AllTime.HourlyRain.Val, localeDateTimeStrToDate(value));
						break;
					case "highDailyRainVal":
						station.SetAlltime(station.AllTime.DailyRain, double.Parse(value), station.AllTime.DailyRain.Ts);
						break;
					case "highDailyRainTime":
						station.SetAlltime(station.AllTime.DailyRain, station.AllTime.DailyRain.Val, localeDateTimeStrToDate(value));
						break;
					case "highRain24hVal":
						station.SetAlltime(station.AllTime.HighRain24Hours, double.Parse(value), station.AllTime.HighRain24Hours.Ts);
						break;
					case "highRain24hTime":
						station.SetAlltime(station.AllTime.HighRain24Hours, station.AllTime.HighRain24Hours.Val, localeDateTimeStrToDate(value));
						break;
					case "highMonthlyRainVal":
						station.SetAlltime(station.AllTime.MonthlyRain, double.Parse(value), station.AllTime.MonthlyRain.Ts);
						break;
					case "highMonthlyRainTime":
						// MM/yyyy
						station.SetAlltime(station.AllTime.MonthlyRain, station.AllTime.MonthlyRain.Val, localeMonthYearStrToDate(value));
						break;
					case "longestDryPeriodVal":
						station.SetAlltime(station.AllTime.LongestDryPeriod, double.Parse(value), station.AllTime.LongestDryPeriod.Ts);
						break;
					case "longestDryPeriodTime":
						station.SetAlltime(station.AllTime.LongestDryPeriod, station.AllTime.LongestDryPeriod.Val, localeDateTimeStrToDate(value));
						break;
					case "longestWetPeriodVal":
						station.SetAlltime(station.AllTime.LongestWetPeriod, double.Parse(value), station.AllTime.LongestWetPeriod.Ts);
						break;
					case "longestWetPeriodTime":
						station.SetAlltime(station.AllTime.LongestWetPeriod, station.AllTime.LongestWetPeriod.Val, localeDateTimeStrToDate(value));
						break;
					default:
						return "Data index not recognised";
				}
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
			return "Success";
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
			try
			{
				lock (station.monthlyalltimeIniThreadLock)
				{
					string[] dt;
					switch (field)
					{
						case "highTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighTemp, double.Parse(value), station.MonthlyRecs[month].HighTemp.Ts);
							break;
						case "highTempTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighTemp, station.MonthlyRecs[month].HighTemp.Val, localeDateTimeStrToDate(value));
							break;
						case "lowTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowTemp, double.Parse(value), station.MonthlyRecs[month].LowTemp.Ts);
							break;
						case "lowTempTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowTemp, station.MonthlyRecs[month].LowTemp.Val, localeDateTimeStrToDate(value));
							break;
						case "highDewPointVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighDewPoint, double.Parse(value), station.MonthlyRecs[month].HighDewPoint.Ts);
							break;
						case "highDewPointTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighDewPoint, station.MonthlyRecs[month].HighDewPoint.Val, localeDateTimeStrToDate(value));
							break;
						case "lowDewPointVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowDewPoint, double.Parse(value), station.MonthlyRecs[month].LowDewPoint.Ts);
							break;
						case "lowDewPointTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowDewPoint, station.MonthlyRecs[month].LowDewPoint.Val, localeDateTimeStrToDate(value));
							break;
						case "highApparentTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighAppTemp, double.Parse(value), station.MonthlyRecs[month].HighAppTemp.Ts);
							break;
						case "highApparentTempTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighAppTemp, station.MonthlyRecs[month].HighAppTemp.Val, localeDateTimeStrToDate(value));
							break;
						case "lowApparentTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowAppTemp, double.Parse(value), station.MonthlyRecs[month].LowAppTemp.Ts);
							break;
						case "lowApparentTempTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowAppTemp, station.MonthlyRecs[month].LowAppTemp.Val, localeDateTimeStrToDate(value));
							break;
						case "highFeelsLikeVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighFeelsLike, double.Parse(value), station.MonthlyRecs[month].HighFeelsLike.Ts);
							break;
						case "highFeelsLikeTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighFeelsLike, station.MonthlyRecs[month].HighFeelsLike.Val, localeDateTimeStrToDate(value));
							break;
						case "lowFeelsLikeVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowFeelsLike, double.Parse(value), station.MonthlyRecs[month].LowFeelsLike.Ts);
							break;
						case "lowFeelsLikeTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowFeelsLike, station.MonthlyRecs[month].LowFeelsLike.Val, localeDateTimeStrToDate(value));
							break;
						case "highHumidexVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHumidex, double.Parse(value), station.MonthlyRecs[month].HighHumidex.Ts);
							break;
						case "highHumidexTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHumidex, station.MonthlyRecs[month].HighHumidex.Val, localeDateTimeStrToDate(value));
							break;
						case "lowWindChillVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowChill, double.Parse(value), station.MonthlyRecs[month].LowChill.Ts);
							break;
						case "lowWindChillTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowChill, station.MonthlyRecs[month].LowChill.Val, localeDateTimeStrToDate(value));
							break;
						case "highHeatIndexVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHeatIndex, double.Parse(value), station.MonthlyRecs[month].HighHeatIndex.Ts);
							break;
						case "highHeatIndexTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHeatIndex, station.MonthlyRecs[month].HighHeatIndex.Val, localeDateTimeStrToDate(value));
							break;
						case "highMinTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighMinTemp, double.Parse(value), station.MonthlyRecs[month].HighMinTemp.Ts);
							break;
						case "highMinTempTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighMinTemp, station.MonthlyRecs[month].HighMinTemp.Val, localeDateTimeStrToDate(value));
							break;
						case "lowMaxTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowMaxTemp, double.Parse(value), station.MonthlyRecs[month].LowMaxTemp.Ts);
							break;
						case "lowMaxTempTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowMaxTemp, station.MonthlyRecs[month].LowMaxTemp.Val, localeDateTimeStrToDate(value));
							break;
						case "highDailyTempRangeVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighDailyTempRange, double.Parse(value), station.MonthlyRecs[month].HighDailyTempRange.Ts);
							break;
						case "highDailyTempRangeTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighDailyTempRange, station.MonthlyRecs[month].HighDailyTempRange.Val, localeDateTimeStrToDate(value));
							break;
						case "lowDailyTempRangeVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowDailyTempRange, double.Parse(value), station.MonthlyRecs[month].LowDailyTempRange.Ts);
							break;
						case "lowDailyTempRangeTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowDailyTempRange, station.MonthlyRecs[month].LowDailyTempRange.Val, localeDateTimeStrToDate(value));
							break;
						case "highHumidityVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHumidity, double.Parse(value), station.MonthlyRecs[month].HighHumidity.Ts);
							break;
						case "highHumidityTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHumidity, station.MonthlyRecs[month].HighHumidity.Val, localeDateTimeStrToDate(value));
							break;
						case "lowHumidityVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowHumidity, double.Parse(value), station.MonthlyRecs[month].LowHumidity.Ts);
							break;
						case "lowHumidityTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowHumidity, station.MonthlyRecs[month].LowHumidity.Val, localeDateTimeStrToDate(value));
							break;
						case "highBarometerVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighPress, double.Parse(value), station.MonthlyRecs[month].HighPress.Ts);
							break;
						case "highBarometerTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighPress, station.MonthlyRecs[month].HighPress.Val, localeDateTimeStrToDate(value));
							break;
						case "lowBarometerVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowPress, double.Parse(value), station.MonthlyRecs[month].LowPress.Ts);
							break;
						case "lowBarometerTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowPress, station.MonthlyRecs[month].LowPress.Val, localeDateTimeStrToDate(value));
							break;
						case "highGustVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighGust, double.Parse(value), station.MonthlyRecs[month].HighGust.Ts);
							break;
						case "highGustTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighGust, station.MonthlyRecs[month].HighGust.Val, localeDateTimeStrToDate(value));
							break;
						case "highWindVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighWind, double.Parse(value), station.MonthlyRecs[month].HighWind.Ts);
							break;
						case "highWindTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighWind, station.MonthlyRecs[month].HighWind.Val, localeDateTimeStrToDate(value));
							break;
						case "highWindRunVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighWindRun, double.Parse(value), station.MonthlyRecs[month].HighWindRun.Ts);
							break;
						case "highWindRunTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighWindRun, station.MonthlyRecs[month].HighWindRun.Val, localeDateTimeStrToDate(value));
							break;
						case "highRainRateVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighRainRate, double.Parse(value), station.MonthlyRecs[month].HighRainRate.Ts);
							break;
						case "highRainRateTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighRainRate, station.MonthlyRecs[month].HighRainRate.Val, localeDateTimeStrToDate(value));
							break;
						case "highHourlyRainVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HourlyRain, double.Parse(value), station.MonthlyRecs[month].HourlyRain.Ts);
							break;
						case "highHourlyRainTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HourlyRain, station.MonthlyRecs[month].HourlyRain.Val, localeDateTimeStrToDate(value));
							break;
						case "highDailyRainVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].DailyRain, double.Parse(value), station.MonthlyRecs[month].DailyRain.Ts);
							break;
						case "highDailyRainTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].DailyRain, station.MonthlyRecs[month].DailyRain.Val, localeDateTimeStrToDate(value));
							break;
						case "highRain24hVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighRain24Hours, double.Parse(value), station.MonthlyRecs[month].HighRain24Hours.Ts);
							break;
						case "highRain24hTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighRain24Hours, station.MonthlyRecs[month].HighRain24Hours.Val, localeDateTimeStrToDate(value));
							break;
						case "highMonthlyRainVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].MonthlyRain, double.Parse(value), station.MonthlyRecs[month].MonthlyRain.Ts);
							break;
						case "highMonthlyRainTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].MonthlyRain, station.MonthlyRecs[month].MonthlyRain.Val, localeMonthYearStrToDate(value));
							break;
						case "longestDryPeriodVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LongestDryPeriod, double.Parse(value), station.MonthlyRecs[month].LongestDryPeriod.Ts);
							break;
						case "longestDryPeriodTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LongestDryPeriod, station.MonthlyRecs[month].LongestDryPeriod.Val, localeDateTimeStrToDate(value));
							break;
						case "longestWetPeriodVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LongestWetPeriod, double.Parse(value), station.MonthlyRecs[month].LongestWetPeriod.Ts);
							break;
						case "longestWetPeriodTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LongestWetPeriod, station.MonthlyRecs[month].LongestWetPeriod.Val, localeDateTimeStrToDate(value));
							break;
						default:
							return "Data index not recognised";
					}
				}
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
			return "Success";
		}

		internal string GetMonthlyRecData()
		{
			const string timeStampFormat = "g";
			const string dateStampFormat = "d";
			const string monthFormat = "MMM yyyy";

			var json = new StringBuilder("{", 21000);
			for (var m = 1; m <= 12; m++)
			{
				// Records - Temperature values
				json.Append($"\"{m}-highTempVal\":\"{station.MonthlyRecs[m].HighTemp.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowTempVal\":\"{station.MonthlyRecs[m].LowTemp.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDewPointVal\":\"{station.MonthlyRecs[m].HighDewPoint.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDewPointVal\":\"{station.MonthlyRecs[m].LowDewPoint.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highApparentTempVal\":\"{station.MonthlyRecs[m].HighAppTemp.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowApparentTempVal\":\"{station.MonthlyRecs[m].LowAppTemp.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeVal\":\"{station.MonthlyRecs[m].HighFeelsLike.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeVal\":\"{station.MonthlyRecs[m].LowFeelsLike.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHumidexVal\":\"{station.MonthlyRecs[m].HighHumidex.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowWindChillVal\":\"{station.MonthlyRecs[m].LowChill.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHeatIndexVal\":\"{station.MonthlyRecs[m].HighHeatIndex.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highMinTempVal\":\"{station.MonthlyRecs[m].HighMinTemp.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowMaxTempVal\":\"{station.MonthlyRecs[m].LowMaxTemp.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeVal\":\"{station.MonthlyRecs[m].HighDailyTempRange.GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeVal\":\"{station.MonthlyRecs[m].LowDailyTempRange.GetValString(cumulus.TempFormat)}\",");
				// Records - Temperature timestamps
				json.Append($"\"{m}-highTempTime\":\"{station.MonthlyRecs[m].HighTemp.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowTempTime\":\"{station.MonthlyRecs[m].LowTemp.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDewPointTime\":\"{station.MonthlyRecs[m].HighDewPoint.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowDewPointTime\":\"{station.MonthlyRecs[m].LowDewPoint.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highApparentTempTime\":\"{station.MonthlyRecs[m].HighAppTemp.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowApparentTempTime\":\"{station.MonthlyRecs[m].LowAppTemp.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeTime\":\"{station.MonthlyRecs[m].HighFeelsLike.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeTime\":\"{station.MonthlyRecs[m].LowFeelsLike.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHumidexTime\":\"{station.MonthlyRecs[m].HighHumidex.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowWindChillTime\":\"{station.MonthlyRecs[m].LowChill.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHeatIndexTime\":\"{station.MonthlyRecs[m].HighHeatIndex.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMinTempTime\":\"{station.MonthlyRecs[m].HighMinTemp.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowMaxTempTime\":\"{station.MonthlyRecs[m].LowMaxTemp.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeTime\":\"{station.MonthlyRecs[m].HighDailyTempRange.GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeTime\":\"{station.MonthlyRecs[m].LowDailyTempRange.GetTsString(dateStampFormat)}\",");
				// Records - Humidity values
				json.Append($"\"{m}-highHumidityVal\":\"{station.MonthlyRecs[m].HighHumidity.GetValString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-lowHumidityVal\":\"{station.MonthlyRecs[m].LowHumidity.GetValString(cumulus.HumFormat)}\",");
				// Records - Humidity times
				json.Append($"\"{m}-highHumidityTime\":\"{station.MonthlyRecs[m].HighHumidity.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowHumidityTime\":\"{station.MonthlyRecs[m].LowHumidity.GetTsString(timeStampFormat)}\",");
				// Records - Pressure values
				json.Append($"\"{m}-highBarometerVal\":\"{station.MonthlyRecs[m].HighPress.GetValString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-lowBarometerVal\":\"{station.MonthlyRecs[m].LowPress.GetValString(cumulus.PressFormat)}\",");
				// Records - Pressure times
				json.Append($"\"{m}-highBarometerTime\":\"{station.MonthlyRecs[m].HighPress.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowBarometerTime\":\"{station.MonthlyRecs[m].LowPress.GetTsString(timeStampFormat)}\",");
				// Records - Wind values
				json.Append($"\"{m}-highGustVal\":\"{station.MonthlyRecs[m].HighGust.GetValString(cumulus.WindFormat)}\",");
				json.Append($"\"{m}-highWindVal\":\"{station.MonthlyRecs[m].HighWind.GetValString(cumulus.WindAvgFormat)}\",");
				json.Append($"\"{m}-highWindRunVal\":\"{station.MonthlyRecs[m].HighWindRun.GetValString(cumulus.WindRunFormat)}\",");
				// Records - Wind times
				json.Append($"\"{m}-highGustTime\":\"{station.MonthlyRecs[m].HighGust.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindTime\":\"{station.MonthlyRecs[m].HighWind.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindRunTime\":\"{station.MonthlyRecs[m].HighWindRun.GetTsString(dateStampFormat)}\",");
				// Records - Rain values
				json.Append($"\"{m}-highRainRateVal\":\"{station.MonthlyRecs[m].HighRainRate.GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highHourlyRainVal\":\"{station.MonthlyRecs[m].HourlyRain.GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highDailyRainVal\":\"{station.MonthlyRecs[m].DailyRain.GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highRain24hVal\":\"{station.MonthlyRecs[m].HighRain24Hours.GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainVal\":\"{station.MonthlyRecs[m].MonthlyRain.GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-longestDryPeriodVal\":\"{station.MonthlyRecs[m].LongestDryPeriod.GetValString("f0")}\",");
				json.Append($"\"{m}-longestWetPeriodVal\":\"{station.MonthlyRecs[m].LongestWetPeriod.GetValString("f0")}\",");
				// Records - Rain times
				json.Append($"\"{m}-highRainRateTime\":\"{station.MonthlyRecs[m].HighRainRate.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHourlyRainTime\":\"{station.MonthlyRecs[m].HourlyRain.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyRainTime\":\"{station.MonthlyRecs[m].DailyRain.GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-highRain24hTime\":\"{station.MonthlyRecs[m].HighRain24Hours.GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainTime\":\"{station.MonthlyRecs[m].MonthlyRain.GetTsString(monthFormat)}\",");
				json.Append($"\"{m}-longestDryPeriodTime\":\"{station.MonthlyRecs[m].LongestDryPeriod.GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-longestWetPeriodTime\":\"{station.MonthlyRecs[m].LongestWetPeriod.GetTsString(dateStampFormat)}\",");
			}
			json.Length--;
			json.Append('}');

			return json.ToString();
		}

		internal string GetMonthlyRecDayFile()
		{
			const string timeStampFormat = "g";
			const string dateStampFormat = "d";
			const string monthFormat = "MMM yyyy";

			var highTemp = new LocalRec[12];
			var lowTemp = new LocalRec[12];
			var highDewPt = new LocalRec[12];
			var lowDewPt = new LocalRec[12];
			var highAppTemp = new LocalRec[12];
			var lowAppTemp = new LocalRec[12];
			var highFeelsLike = new LocalRec[12];
			var lowFeelsLike = new LocalRec[12];
			var highHumidex = new LocalRec[12];
			var lowWindChill = new LocalRec[12];
			var highHeatInd = new LocalRec[12];
			var highMinTemp = new LocalRec[12];
			var lowMaxTemp = new LocalRec[12];
			var highTempRange = new LocalRec[12];
			var lowTempRange = new LocalRec[12];
			var highHum = new LocalRec[12];
			var lowHum = new LocalRec[12];
			var highBaro = new LocalRec[12];
			var lowBaro = new LocalRec[12];
			var highGust = new LocalRec[12];
			var highWind = new LocalRec[12];
			var highWindRun = new LocalRec[12];
			var highRainRate = new LocalRec[12];
			var highRainHour = new LocalRec[12];
			var highRainDay = new LocalRec[12];
			var highRain24h = new LocalRec[12];
			var highRainMonth = new LocalRec[12];
			var dryPeriod = new LocalRec[12];
			var wetPeriod = new LocalRec[12];

			for (var i = 0; i < 12; i++)
			{
				highTemp[i] = new LocalRec(true);
				lowTemp[i] = new LocalRec(false);
				highDewPt[i] = new LocalRec(true);
				lowDewPt[i] = new LocalRec(false);
				highAppTemp[i] = new LocalRec(true);
				lowAppTemp[i] = new LocalRec(false);
				highFeelsLike[i] = new LocalRec(true);
				lowFeelsLike[i] = new LocalRec(false);
				highHumidex[i] = new LocalRec(true);
				lowWindChill[i] = new LocalRec(false);
				highHeatInd[i] = new LocalRec(true);
				highMinTemp[i] = new LocalRec(true);
				lowMaxTemp[i] = new LocalRec(false);
				highTempRange[i] = new LocalRec(true);
				lowTempRange[i] = new LocalRec(false);
				highHum[i] = new LocalRec(true);
				lowHum[i] = new LocalRec(false);
				highBaro[i] = new LocalRec(true);
				lowBaro[i] = new LocalRec(false);
				highGust[i] = new LocalRec(true);
				highWind[i] = new LocalRec(true);
				highWindRun[i] = new LocalRec(true);
				highRainRate[i] = new LocalRec(true);
				highRainHour[i] = new LocalRec(true);
				highRainDay[i] = new LocalRec(true);
				highRain24h[i] = new LocalRec(true);
				highRainMonth[i] = new LocalRec(true);
				dryPeriod[i] = new LocalRec(true);
				wetPeriod[i] = new LocalRec(true);
			}

			var thisDate = DateTime.MinValue;
			var rainThisMonth = 0.0;
			var currentDryPeriod = 0;
			var currentWetPeriod = 0;
			var isDryNow = false;
			var thisDateDry = DateTime.MinValue;
			var thisDateWet = DateTime.MinValue;
			int monthOffset;
			var firstEntry = true;

			var json = new StringBuilder("{", 25500);

			int rainThreshold;
			if (cumulus.RainDayThreshold > 0)
			{
				rainThreshold = Convert.ToInt32(cumulus.RainDayThreshold * 1000);
			}
			else
			{
				// default
				if (cumulus.Units.Rain == 0)
				{
					rainThreshold = 200; // 0.2mm *1000
				}
				else
				{
					rainThreshold = 10;  // 0.01in *1000
				}
			}


			// Read the day file list and extract the records from there
			if (station.DayFile.Count > 0)
			{
				for (var i = 0; i < station.DayFile.Count; i++)
				{
					var loggedDate = station.DayFile[i].Date.Date;
					monthOffset = loggedDate.Month - 1;

					// for the very first record we need to record the date
					if (firstEntry)
					{
						thisDate = loggedDate;
						firstEntry = false;
						thisDateDry = loggedDate;
						thisDateWet = loggedDate;
					}
					else
					{
						// This assumes the day file is in date order!
						if (thisDate.Month != loggedDate.Month)
						{
							// reset the date and counter for a new month
							rainThisMonth = 0.0;
							thisDate = loggedDate;
						}
					}


					// hi gust
					if (station.DayFile[i].HighGust > highGust[monthOffset].Value)
					{
						highGust[monthOffset].Value = station.DayFile[i].HighGust;
						highGust[monthOffset].Ts = station.DayFile[i].HighGustTime;
					}
					// lo temp
					if (station.DayFile[i].LowTemp < lowTemp[monthOffset].Value)
					{
						lowTemp[monthOffset].Value = station.DayFile[i].LowTemp;
						lowTemp[monthOffset].Ts = station.DayFile[i].LowTempTime;
					}
					// hi min temp
					if (station.DayFile[i].LowTemp > highMinTemp[monthOffset].Value)
					{
						highMinTemp[monthOffset].Value = station.DayFile[i].LowTemp;
						highMinTemp[monthOffset].Ts = station.DayFile[i].LowTempTime;
					}
					// hi temp
					if (station.DayFile[i].HighTemp > highTemp[monthOffset].Value)
					{
						highTemp[monthOffset].Value = station.DayFile[i].HighTemp;
						highTemp[monthOffset].Ts = station.DayFile[i].HighTempTime;
					}
					// lo max temp
					if (station.DayFile[i].HighTemp < lowMaxTemp[monthOffset].Value)
					{
						lowMaxTemp[monthOffset].Value = station.DayFile[i].HighTemp;
						lowMaxTemp[monthOffset].Ts = station.DayFile[i].HighTempTime;
					}

					// temp ranges
					// hi temp range
					if ((station.DayFile[i].HighTemp - station.DayFile[i].LowTemp) > highTempRange[monthOffset].Value)
					{
						highTempRange[monthOffset].Value = station.DayFile[i].HighTemp - station.DayFile[i].LowTemp;
						highTempRange[monthOffset].Ts = loggedDate;
					}
					// lo temp range
					if ((station.DayFile[i].HighTemp - station.DayFile[i].LowTemp) < lowTempRange[monthOffset].Value)
					{
						lowTempRange[monthOffset].Value = station.DayFile[i].HighTemp - station.DayFile[i].LowTemp;
						lowTempRange[monthOffset].Ts = loggedDate;
					}

					// lo baro
					if (station.DayFile[i].LowPress < lowBaro[monthOffset].Value)
					{
						lowBaro[monthOffset].Value = station.DayFile[i].LowPress;
						lowBaro[monthOffset].Ts = station.DayFile[i].LowPressTime;
					}
					// hi baro
					if (station.DayFile[i].HighPress > highBaro[monthOffset].Value)
					{
						highBaro[monthOffset].Value = station.DayFile[i].HighPress;
						highBaro[monthOffset].Ts = station.DayFile[i].HighPressTime;
					}
					// hi rain rate
					if (station.DayFile[i].HighRainRate > highRainRate[monthOffset].Value)
					{
						highRainRate[monthOffset].Value = station.DayFile[i].HighRainRate;
						highRainRate[monthOffset].Ts = station.DayFile[i].HighRainRateTime;
					}
					// hi rain day
					if (station.DayFile[i].TotalRain > highRainDay[monthOffset].Value)
					{
						highRainDay[monthOffset].Value = station.DayFile[i].TotalRain;
						highRainDay[monthOffset].Ts = loggedDate;
					}
					// hi 24h rain
					if (station.DayFile[i].HighRain24h > highRain24h[monthOffset].Value)
					{
						highRain24h[monthOffset].Value = station.DayFile[i].HighRain24h;
						highRain24h[monthOffset].Ts = station.DayFile[i].HighRain24hTime;
					}

					// monthly rain
					rainThisMonth += station.DayFile[i].TotalRain;

					if (rainThisMonth > highRainMonth[monthOffset].Value)
					{
						highRainMonth[monthOffset].Value = rainThisMonth;
						highRainMonth[monthOffset].Ts = thisDate;
					}


					// dry/wet period
					if (Convert.ToInt32(station.DayFile[i].TotalRain * 1000) >= rainThreshold)
					{
						if (isDryNow)
						{
							currentWetPeriod = 1;
							isDryNow = false;
							var dryMonthOffset = thisDateDry.Month - 1;
							if (!(dryPeriod[dryMonthOffset].Value == Cumulus.DefaultHiVal && currentDryPeriod == 0) && currentDryPeriod > dryPeriod[dryMonthOffset].Value)
							{
								dryPeriod[dryMonthOffset].Value = currentDryPeriod;
								dryPeriod[dryMonthOffset].Ts = thisDateDry;
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
							if (!(wetPeriod[wetMonthOffset].Value == Cumulus.DefaultHiVal && currentWetPeriod == 0) && currentWetPeriod > wetPeriod[wetMonthOffset].Value)
							{
								wetPeriod[wetMonthOffset].Value = currentWetPeriod;
								wetPeriod[wetMonthOffset].Ts = thisDateWet;
							}
							currentWetPeriod = 0;
						}
					}

					// hi wind run
					if (station.DayFile[i].WindRun > highWindRun[monthOffset].Value)
					{
						highWindRun[monthOffset].Value = station.DayFile[i].WindRun;
						highWindRun[monthOffset].Ts = loggedDate;
					}
					// hi wind
					if (station.DayFile[i].HighAvgWind > highWind[monthOffset].Value)
					{
						highWind[monthOffset].Value = station.DayFile[i].HighAvgWind;
						highWind[monthOffset].Ts = station.DayFile[i].HighAvgWindTime;
					}

					// lo humidity
					if (station.DayFile[i].LowHumidity < lowHum[monthOffset].Value)
					{
						lowHum[monthOffset].Value = station.DayFile[i].LowHumidity;
						lowHum[monthOffset].Ts = station.DayFile[i].LowHumidityTime;
					}
					// hi humidity
					if (station.DayFile[i].HighHumidity > highHum[monthOffset].Value)
					{
						highHum[monthOffset].Value = station.DayFile[i].HighHumidity;
						highHum[monthOffset].Ts = station.DayFile[i].HighHumidityTime;
					}

					// hi heat index
					if (station.DayFile[i].HighHeatIndex > highHeatInd[monthOffset].Value)
					{
						highHeatInd[monthOffset].Value = station.DayFile[i].HighHeatIndex;
						highHeatInd[monthOffset].Ts = station.DayFile[i].HighHeatIndexTime;
					}
					// hi app temp
					if (station.DayFile[i].HighAppTemp > highAppTemp[monthOffset].Value)
					{
						highAppTemp[monthOffset].Value = station.DayFile[i].HighAppTemp;
						highAppTemp[monthOffset].Ts = station.DayFile[i].HighAppTempTime;
					}
					// lo app temp
					if (station.DayFile[i].LowAppTemp < lowAppTemp[monthOffset].Value)
					{
						lowAppTemp[monthOffset].Value = station.DayFile[i].LowAppTemp;
						lowAppTemp[monthOffset].Ts = station.DayFile[i].LowAppTempTime;
					}

					// hi rain hour
					if (station.DayFile[i].HighHourlyRain > highRainHour[monthOffset].Value)
					{
						highRainHour[monthOffset].Value = station.DayFile[i].HighHourlyRain;
						highRainHour[monthOffset].Ts = station.DayFile[i].HighHourlyRainTime;
					}

					// lo wind chill
					if (station.DayFile[i].LowWindChill < lowWindChill[monthOffset].Value)
					{
						lowWindChill[monthOffset].Value = station.DayFile[i].LowWindChill;
						lowWindChill[monthOffset].Ts = station.DayFile[i].LowWindChillTime;
					}

					// hi dewpt
					if (station.DayFile[i].HighDewPoint > highDewPt[monthOffset].Value)
					{
						highDewPt[monthOffset].Value = station.DayFile[i].HighDewPoint;
						highDewPt[monthOffset].Ts = station.DayFile[i].HighDewPointTime;
					}
					// lo dewpt
					if (station.DayFile[i].LowDewPoint < lowDewPt[monthOffset].Value)
					{
						lowDewPt[monthOffset].Value = station.DayFile[i].LowDewPoint;
						lowDewPt[monthOffset].Ts = station.DayFile[i].LowDewPointTime;
					}

					// hi feels like
					if (station.DayFile[i].HighFeelsLike > highFeelsLike[monthOffset].Value)
					{
						highFeelsLike[monthOffset].Value = station.DayFile[i].HighFeelsLike;
						highFeelsLike[monthOffset].Ts = station.DayFile[i].HighFeelsLikeTime;
					}
					// lo feels like
					if (station.DayFile[i].LowFeelsLike < lowFeelsLike[monthOffset].Value)
					{
						lowFeelsLike[monthOffset].Value = station.DayFile[i].LowFeelsLike;
						lowFeelsLike[monthOffset].Ts = station.DayFile[i].LowFeelsLikeTime;
					}

					// hi humidex
					if (station.DayFile[i].HighHumidex > highHumidex[monthOffset].Value)
					{
						highHumidex[monthOffset].Value = station.DayFile[i].HighHumidex;
						highHumidex[monthOffset].Ts = station.DayFile[i].HighHumidexTime;
					}
				}

				// We need to check if the run or wet/dry days at the end of log exceeds any records
				if (!(wetPeriod[thisDateWet.Month - 1].Value == Cumulus.DefaultHiVal && currentWetPeriod == 0) && currentWetPeriod > wetPeriod[thisDateWet.Month - 1].Value)
				{
					wetPeriod[thisDateWet.Month - 1].Value = currentWetPeriod;
					wetPeriod[thisDateWet.Month - 1].Ts = thisDateWet;
				}
				if (!(dryPeriod[thisDateDry.Month - 1].Value == Cumulus.DefaultHiVal && currentDryPeriod == 0) && currentDryPeriod > dryPeriod[thisDateDry.Month - 1].Value)
				{
					dryPeriod[thisDateDry.Month - 1].Value = currentDryPeriod;
					dryPeriod[thisDateDry.Month - 1].Ts = thisDateDry;
				}

			}
			else
			{
				cumulus.LogMessage("Error failed to find day records");
			}

			for (var i = 0; i < 12; i++)
			{
				var m = i + 1;
				json.Append($"\"{m}-highTempValDayfile\":\"{highTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highTempTimeDayfile\":\"{highTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowTempValDayfile\":\"{lowTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowTempTimeDayfile\":\"{lowTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDewPointValDayfile\":\"{highDewPt[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDewPointTimeDayfile\":\"{highDewPt[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowDewPointValDayfile\":\"{lowDewPt[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDewPointTimeDayfile\":\"{lowDewPt[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highApparentTempValDayfile\":\"{highAppTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highApparentTempTimeDayfile\":\"{highAppTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowApparentTempValDayfile\":\"{lowAppTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowApparentTempTimeDayfile\":\"{lowAppTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeValDayfile\":\"{highFeelsLike[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeTimeDayfile\":\"{highFeelsLike[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeValDayfile\":\"{lowFeelsLike[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeTimeDayfile\":\"{lowFeelsLike[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHumidexValDayfile\":\"{highHumidex[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHumidexTimeDayfile\":\"{highHumidex[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowWindChillValDayfile\":\"{lowWindChill[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowWindChillTimeDayfile\":\"{lowWindChill[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHeatIndexValDayfile\":\"{highHeatInd[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHeatIndexTimeDayfile\":\"{highHeatInd[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMinTempValDayfile\":\"{highMinTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highMinTempTimeDayfile\":\"{highMinTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowMaxTempValDayfile\":\"{lowMaxTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowMaxTempTimeDayfile\":\"{lowMaxTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeValDayfile\":\"{highTempRange[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeTimeDayfile\":\"{highTempRange[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeValDayfile\":\"{lowTempRange[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeTimeDayfile\":\"{lowTempRange[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-highHumidityValDayfile\":\"{highHum[i].GetValString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-highHumidityTimeDayfile\":\"{highHum[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowHumidityValDayfile\":\"{lowHum[i].GetValString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-lowHumidityTimeDayfile\":\"{lowHum[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highBarometerValDayfile\":\"{highBaro[i].GetValString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-highBarometerTimeDayfile\":\"{highBaro[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowBarometerValDayfile\":\"{lowBaro[i].GetValString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-lowBarometerTimeDayfile\":\"{lowBaro[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highGustValDayfile\":\"{highGust[i].GetValString(cumulus.WindFormat)}\",");
				json.Append($"\"{m}-highGustTimeDayfile\":\"{highGust[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindValDayfile\":\"{highWind[i].GetValString(cumulus.WindAvgFormat)}\",");
				json.Append($"\"{m}-highWindTimeDayfile\":\"{highWind[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindRunValDayfile\":\"{highWindRun[i].GetValString(cumulus.WindRunFormat)}\",");
				json.Append($"\"{m}-highWindRunTimeDayfile\":\"{highWindRun[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-highRainRateValDayfile\":\"{highRainRate[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highRainRateTimeDayfile\":\"{highRainRate[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHourlyRainValDayfile\":\"{highRainHour[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highHourlyRainTimeDayfile\":\"{highRainHour[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyRainValDayfile\":\"{highRainDay[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highDailyRainTimeDayfile\":\"{highRainDay[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-highRain24hValDayfile\":\"{highRain24h[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highRain24hTimeDayfile\":\"{highRain24h[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainValDayfile\":\"{highRainMonth[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainTimeDayfile\":\"{highRainMonth[i].GetTsString(monthFormat)}\",");
				json.Append($"\"{m}-longestDryPeriodValDayfile\":\"{dryPeriod[i].GetValString()}\",");
				json.Append($"\"{m}-longestDryPeriodTimeDayfile\":\"{dryPeriod[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-longestWetPeriodValDayfile\":\"{wetPeriod[i].GetValString()}\",");
				json.Append($"\"{m}-longestWetPeriodTimeDayfile\":\"{wetPeriod[i].GetTsString(dateStampFormat)}\",");
			}
			json.Length--;
			json.Append('}');

			return json.ToString();
		}

		internal string GetMonthlyRecLogFile()
		{
			const string timeStampFormat = "g";
			const string dateStampFormat = "d";
			const string monthFormat = "MMM yyyy";

			var json = new StringBuilder("{", 25500);
			var datefrom = cumulus.RecordsBeganDateTime;
			datefrom = new DateTime(datefrom.Year, datefrom.Month, 1, 0, 0, 0);
			var dateto = DateTime.Now;
			dateto = new DateTime(dateto.Year, dateto.Month, 1, 0, 0, 0);
			var filedate = datefrom;

			var logFile = cumulus.GetLogFileName(filedate);
			var started = false;
			var finished = false;
			var lastentrydate = datefrom;
			var lastentryrain = 0.0;
			var lastentrycounter = 0.0;

			var isDryNow = false;
			var currentDryPeriod = 0;
			var currentWetPeriod = 0;

			int rainThreshold;
			if (cumulus.RainDayThreshold > 0)
			{
				rainThreshold = Convert.ToInt32(cumulus.RainDayThreshold * 1000);
			}
			else
			{
				// default
				if (cumulus.Units.Rain == 0)
				{
					rainThreshold = 200; // 0.2mm *1000
				}
				else
				{
					rainThreshold = 10;  // 0.01in *1000
				}
			}

			// what do we deem to be too large a jump in the rainfall counter to be true? use 20 mm or 0.8 inches
			var counterJumpTooBig = cumulus.Units.Rain == 0 ? 20 : 0.8;

			var highTemp = new LocalRec[12];
			var lowTemp = new LocalRec[12];
			var highDewPt = new LocalRec[12];
			var lowDewPt = new LocalRec[12];
			var highAppTemp = new LocalRec[12];
			var lowAppTemp = new LocalRec[12];
			var highFeelsLike = new LocalRec[12];
			var lowFeelsLike = new LocalRec[12];
			var highHumidex = new LocalRec[12];
			var lowWindChill = new LocalRec[12];
			var highHeatInd = new LocalRec[12];
			var highMinTemp = new LocalRec[12];
			var lowMaxTemp = new LocalRec[12];
			var highTempRange = new LocalRec[12];
			var lowTempRange = new LocalRec[12];
			var highHum = new LocalRec[12];
			var lowHum = new LocalRec[12];
			var highBaro = new LocalRec[12];
			var lowBaro = new LocalRec[12];
			var highGust = new LocalRec[12];
			var highWind = new LocalRec[12];
			var highWindRun = new LocalRec[12];
			var highRainRate = new LocalRec[12];
			var highRainHour = new LocalRec[12];
			var highRainDay = new LocalRec[12];
			var highRain24h = new LocalRec[12];
			var highRainMonth = new LocalRec[12];
			var dryPeriod = new LocalRec[12];
			var wetPeriod = new LocalRec[12];

			for (var i = 0; i < 12; i++)
			{
				highTemp[i] = new LocalRec(true);
				lowTemp[i] = new LocalRec(false);
				highDewPt[i] = new LocalRec(true);
				lowDewPt[i] = new LocalRec(false);
				highAppTemp[i] = new LocalRec(true);
				lowAppTemp[i] = new LocalRec(false);
				highFeelsLike[i] = new LocalRec(true);
				lowFeelsLike[i] = new LocalRec(false);
				highHumidex[i] = new LocalRec(true);
				lowWindChill[i] = new LocalRec(false);
				highHeatInd[i] = new LocalRec(true);
				highMinTemp[i] = new LocalRec(true);
				lowMaxTemp[i] = new LocalRec(false);
				highTempRange[i] = new LocalRec(true);
				lowTempRange[i] = new LocalRec(false);
				highHum[i] = new LocalRec(true);
				lowHum[i] = new LocalRec(false);
				highBaro[i] = new LocalRec(true);
				lowBaro[i] = new LocalRec(false);
				highGust[i] = new LocalRec(true);
				highWind[i] = new LocalRec(true);
				highWindRun[i] = new LocalRec(true);
				highRainRate[i] = new LocalRec(true);
				highRainHour[i] = new LocalRec(true);
				highRainDay[i] = new LocalRec(true);
				highRain24h[i] = new LocalRec(true);
				highRainMonth[i] = new LocalRec(true);
				dryPeriod[i] = new LocalRec(true);
				wetPeriod[i] = new LocalRec(true);
			}

			var thisDateDry = DateTime.MinValue;
			var thisDateWet = DateTime.MinValue;

			var currentDay = datefrom;
			var dayHighTemp = new LocalRec(true);
			var dayLowTemp = new LocalRec(false);
			double dayWindRun = 0;
			double dayRain = 0;

			var monthlyRain = 0.0;

			var totalRainfall = 0.0;

			var hourRainLog = new Queue<LastHourRainLog>();
			var rain24hLog = new Queue<LastHourRainLog>();

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

							var rec = station.ParseLogFileRec(line, true);

							// We need to work in meteo dates not clock dates for day hi/lows
							var metoDate = rec.Date.AddHours(cumulus.GetHourInc());
							var monthOffset = metoDate.Month - 1;

							if (!started)
							{
								if (metoDate >= datefrom)
								{
									lastentrydate = rec.Date;
									currentDay = metoDate;
									started = true;
									totalRainfall = lastentryrain;
								}
								else if (metoDate < filedate)
								{
									continue;
								}
								else
								{
									// OK we are within 24 hours of the start date, so record rain values
									Add24HourRainEntry(rec.Date, totalRainfall + rec.RainToday, ref rain24hLog);
									lastentryrain = rec.RainToday;
									lastentrycounter = rec.Raincounter;
									continue;
								}
							}

							// low chill
							if (rec.WindChill < lowWindChill[monthOffset].Value)
							{
								lowWindChill[monthOffset].Value = rec.WindChill;
								lowWindChill[monthOffset].Ts = rec.Date;
							}
							// hi heat
							if (rec.HeatIndex > highHeatInd[monthOffset].Value)
							{
								highHeatInd[monthOffset].Value = rec.HeatIndex;
								highHeatInd[monthOffset].Ts = rec.Date;
							}

							if (rec.ApparentTemperature > Cumulus.DefaultHiVal)
							{
								// hi appt
								if (rec.ApparentTemperature > highAppTemp[monthOffset].Value)
								{
									highAppTemp[monthOffset].Value = rec.ApparentTemperature;
									highAppTemp[monthOffset].Ts = rec.Date;
								}
								// lo appt
								if (rec.ApparentTemperature < lowAppTemp[monthOffset].Value)
								{
									lowAppTemp[monthOffset].Value = rec.ApparentTemperature;
									lowAppTemp[monthOffset].Ts = rec.Date;
								}
							}

							if (rec.FeelsLike > Cumulus.DefaultHiVal)
							{
								// hi feels like
								if (rec.FeelsLike > highFeelsLike[monthOffset].Value)
								{
									highFeelsLike[monthOffset].Value = rec.FeelsLike;
									highFeelsLike[monthOffset].Ts = rec.Date;
								}
								// lo feels like
								if (rec.FeelsLike < lowFeelsLike[monthOffset].Value)
								{
									lowFeelsLike[monthOffset].Value = rec.FeelsLike;
									lowFeelsLike[monthOffset].Ts = rec.Date;
								}
							}

							// hi humidex
							if (rec.Humidex > highHumidex[monthOffset].Value)
							{
								highHumidex[monthOffset].Value = rec.Humidex;
								highHumidex[monthOffset].Ts = rec.Date;
							}

							// hi temp
							if (rec.OutdoorTemperature > highTemp[monthOffset].Value)
							{
								highTemp[monthOffset].Value = rec.OutdoorTemperature;
								highTemp[monthOffset].Ts = rec.Date;
							}
							// lo temp
							if (rec.OutdoorTemperature < lowTemp[monthOffset].Value)
							{
								lowTemp[monthOffset].Value = rec.OutdoorTemperature;
								lowTemp[monthOffset].Ts = rec.Date;
							}
							// hi dewpoint
							if (rec.OutdoorDewpoint > highDewPt[monthOffset].Value)
							{
								highDewPt[monthOffset].Value = rec.OutdoorDewpoint;
								highDewPt[monthOffset].Ts = rec.Date;
							}
							// low dewpoint
							if (rec.OutdoorDewpoint < lowDewPt[monthOffset].Value)
							{
								lowDewPt[monthOffset].Value = rec.OutdoorDewpoint;
								lowDewPt[monthOffset].Ts = rec.Date;
							}
							// hi hum
							if (rec.OutdoorHumidity > highHum[monthOffset].Value)
							{
								highHum[monthOffset].Value = rec.OutdoorHumidity;
								highHum[monthOffset].Ts = rec.Date;
							}
							// lo hum
							if (rec.OutdoorHumidity < lowHum[monthOffset].Value)
							{
								lowHum[monthOffset].Value = rec.OutdoorHumidity;
								lowHum[monthOffset].Ts = rec.Date;
							}
							// hi baro
							if (rec.Pressure > highBaro[monthOffset].Value)
							{
								highBaro[monthOffset].Value = rec.Pressure;
								highBaro[monthOffset].Ts = rec.Date;
							}
							// lo hum
							if (rec.Pressure < lowBaro[monthOffset].Value)
							{
								lowBaro[monthOffset].Value = rec.Pressure;
								lowBaro[monthOffset].Ts = rec.Date;
							}
							// hi gust
							if (rec.RecentMaxGust > highGust[monthOffset].Value)
							{
								highGust[monthOffset].Value = rec.RecentMaxGust;
								highGust[monthOffset].Ts = rec.Date;
							}
							// hi wind
							if (rec.WindAverage > highWind[monthOffset].Value)
							{
								highWind[monthOffset].Value = rec.WindAverage;
								highWind[monthOffset].Ts = rec.Date;
							}
							// hi rain rate
							if (rec.RainRate > highRainRate[monthOffset].Value)
							{
								highRainRate[monthOffset].Value = rec.RainRate;
								highRainRate[monthOffset].Ts = rec.Date;
							}

							dayWindRun += rec.Date.Subtract(lastentrydate).TotalHours * rec.WindAverage;

							if (dayWindRun > highWindRun[monthOffset].Value)
							{
								highWindRun[monthOffset].Value = dayWindRun;
								highWindRun[monthOffset].Ts = currentDay;
							}

							// new meteo day
							if (currentDay.Date != metoDate.Date)
							{
								var lastEntryMonthOffset = metoDate.Month - 1;
								if (dayHighTemp.Value < lowMaxTemp[lastEntryMonthOffset].Value)
								{
									lowMaxTemp[lastEntryMonthOffset].Value = dayHighTemp.Value;
									lowMaxTemp[lastEntryMonthOffset].Ts = dayHighTemp.Ts;
								}
								if (dayLowTemp.Value > highMinTemp[lastEntryMonthOffset].Value)
								{
									highMinTemp[lastEntryMonthOffset].Value = dayLowTemp.Value;
									highMinTemp[lastEntryMonthOffset].Ts = dayLowTemp.Ts;
								}
								if (dayHighTemp.Value - dayLowTemp.Value > highTempRange[lastEntryMonthOffset].Value)
								{
									highTempRange[lastEntryMonthOffset].Value = dayHighTemp.Value - dayLowTemp.Value;
									highTempRange[lastEntryMonthOffset].Ts = currentDay;
								}
								if (dayHighTemp.Value - dayLowTemp.Value < lowTempRange[lastEntryMonthOffset].Value)
								{
									lowTempRange[lastEntryMonthOffset].Value = dayHighTemp.Value - dayLowTemp.Value;
									lowTempRange[lastEntryMonthOffset].Ts = currentDay;
								}

								// logging format changed on with C1 v1.9.3 b1055 in Dec 2012
								// before that date the 00:00 log entry contained the rain total for the day before and the next log entry was reset to zero
								// after that build the total was reset to zero in the entry
								// messy!
								// no final rainfall entry after this date (approx). The best we can do is add in the increase in rain counter during this period
								var rollovertime = new TimeSpan(-cumulus.GetHourInc(), 0, 0);
								if (rec.RainToday > 0 && rec.Date.TimeOfDay == rollovertime)
								{
									dayRain = rec.RainToday;
								}
								else if ((rec.Raincounter - lastentrycounter > 0) && (rec.Raincounter - lastentrycounter < counterJumpTooBig))
								{
									dayRain += (rec.Raincounter - lastentrycounter) * cumulus.Calib.Rain.Mult;
								}

								if (dayRain > highRainDay[lastEntryMonthOffset].Value)
								{
									highRainDay[lastEntryMonthOffset].Value = dayRain;
									highRainDay[lastEntryMonthOffset].Ts = currentDay;
								}

								monthlyRain += dayRain;

								if (monthlyRain > highRainMonth[monthOffset].Value)
								{
									highRainMonth[monthOffset].Value = monthlyRain;
									highRainMonth[monthOffset].Ts = currentDay;
								}

								if (currentDay.Month != metoDate.Month)
								{
									monthlyRain = 0;
								}


								// dry/wet period
								if (Convert.ToInt32(dayRain * 1000) >= rainThreshold)
								{
									if (isDryNow)
									{
										currentWetPeriod = 1;
										isDryNow = false;
										if (currentDryPeriod > dryPeriod[monthOffset].Value)
										{
											dryPeriod[monthOffset].Value = currentDryPeriod;
											dryPeriod[monthOffset].Ts = thisDateDry;
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
										if (currentWetPeriod > wetPeriod[monthOffset].Value)
										{
											wetPeriod[monthOffset].Value = currentWetPeriod;
											wetPeriod[monthOffset].Ts = thisDateWet;
										}
										currentWetPeriod = 0;
									}
								}
							}
							else
							{
								dayRain = rec.RainToday;
							}

							if (rec.OutdoorTemperature > dayHighTemp.Value)
							{
								dayHighTemp.Value = rec.OutdoorTemperature;
								dayHighTemp.Ts = rec.Date.Date;
							}

							if (rec.OutdoorTemperature < dayLowTemp.Value)
							{
								dayLowTemp.Value = rec.OutdoorTemperature;
								dayLowTemp.Ts = rec.Date.Date;
							}


							// hourly rain
							/*
							* need to track what the rainfall has been in the last rolling hour
							* across day rollovers where the count resets
							*/
							AddLastHoursRainEntry(rec.Date, totalRainfall + dayRain, ref hourRainLog, ref rain24hLog);

							var rainThisHour = hourRainLog.Last().Raincounter - hourRainLog.Peek().Raincounter;
							if (rainThisHour > highRainHour[monthOffset].Value)
							{
								highRainHour[monthOffset].Value = rainThisHour;
								highRainHour[monthOffset].Ts = rec.Date;
							}

							var rain24h = rain24hLog.Last().Raincounter - rain24hLog.Peek().Raincounter;
							if (rain24h > highRain24h[monthOffset].Value)
							{
								highRain24h[monthOffset].Value = rain24h;
								highRain24h[monthOffset].Ts = rec.Date;
							}

							// new meteo day, part 2
							if (currentDay.Date != metoDate.Date)
							{
								currentDay = metoDate;
								dayHighTemp.Value = rec.OutdoorTemperature;
								dayLowTemp.Value = rec.OutdoorTemperature;
								dayWindRun = 0;
								totalRainfall += dayRain;
							}

							lastentrydate = rec.Date;
							lastentrycounter = rec.Raincounter;
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

			hourRainLog.Clear();
			rain24hLog.Clear();

			for (var i = 0; i < 12; i++)
			{
				var m = i + 1;
				json.Append($"\"{m}-highTempValLogfile\":\"{highTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highTempTimeLogfile\":\"{highTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowTempValLogfile\":\"{lowTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowTempTimeLogfile\":\"{lowTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDewPointValLogfile\":\"{highDewPt[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDewPointTimeLogfile\":\"{highDewPt[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowDewPointValLogfile\":\"{lowDewPt[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDewPointTimeLogfile\":\"{lowDewPt[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highApparentTempValLogfile\":\"{highAppTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highApparentTempTimeLogfile\":\"{highAppTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowApparentTempValLogfile\":\"{lowAppTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowApparentTempTimeLogfile\":\"{lowAppTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeValLogfile\":\"{highFeelsLike[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeTimeLogfile\":\"{highFeelsLike[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeValLogfile\":\"{lowFeelsLike[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeTimeLogfile\":\"{lowFeelsLike[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHumidexValLogfile\":\"{highHumidex[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHumidexTimeLogfile\":\"{highHumidex[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowWindChillValLogfile\":\"{lowWindChill[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowWindChillTimeLogfile\":\"{lowWindChill[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHeatIndexValLogfile\":\"{highHeatInd[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHeatIndexTimeLogfile\":\"{highHeatInd[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMinTempValLogfile\":\"{highMinTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highMinTempTimeLogfile\":\"{highMinTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowMaxTempValLogfile\":\"{lowMaxTemp[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowMaxTempTimeLogfile\":\"{lowMaxTemp[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeValLogfile\":\"{highTempRange[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeTimeLogfile\":\"{highTempRange[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeValLogfile\":\"{lowTempRange[i].GetValString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeTimeLogfile\":\"{lowTempRange[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-highHumidityValLogfile\":\"{highHum[i].GetValString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-highHumidityTimeLogfile\":\"{highHum[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowHumidityValLogfile\":\"{lowHum[i].GetValString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-lowHumidityTimeLogfile\":\"{lowHum[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highBarometerValLogfile\":\"{highBaro[i].GetValString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-highBarometerTimeLogfile\":\"{highBaro[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowBarometerValLogfile\":\"{lowBaro[i].GetValString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-lowBarometerTimeLogfile\":\"{lowBaro[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highGustValLogfile\":\"{highGust[i].GetValString(cumulus.WindFormat)}\",");
				json.Append($"\"{m}-highGustTimeLogfile\":\"{highGust[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindValLogfile\":\"{highWind[i].GetValString(cumulus.WindAvgFormat)}\",");
				json.Append($"\"{m}-highWindTimeLogfile\":\"{highWind[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindRunValLogfile\":\"{highWindRun[i].GetValString(cumulus.WindRunFormat)}\",");
				json.Append($"\"{m}-highWindRunTimeLogfile\":\"{highWindRun[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-highRainRateValLogfile\":\"{highRainRate[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highRainRateTimeLogfile\":\"{highRainRate[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHourlyRainValLogfile\":\"{highRainHour[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highHourlyRainTimeLogfile\":\"{highRainHour[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyRainValLogfile\":\"{highRainDay[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highDailyRainTimeLogfile\":\"{highRainDay[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-highRain24hValLogfile\":\"{highRain24h[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highRain24hTimeLogfile\":\"{highRain24h[i].GetTsString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainValLogfile\":\"{highRainMonth[i].GetValString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainTimeLogfile\":\"{highRainMonth[i].GetTsString(monthFormat)}\",");
				json.Append($"\"{m}-longestDryPeriodValLogfile\":\"{dryPeriod[i].GetValString()}\",");
				json.Append($"\"{m}-longestDryPeriodTimeLogfile\":\"{dryPeriod[i].GetTsString(dateStampFormat)}\",");
				json.Append($"\"{m}-longestWetPeriodValLogfile\":\"{wetPeriod[i].GetValString()}\",");
				json.Append($"\"{m}-longestWetPeriodTimeLogfile\":\"{wetPeriod[i].GetTsString(dateStampFormat)}\",");
			}

			json.Length--;
			json.Append('}');

			watch.Stop();
			var elapsed = watch.ElapsedMilliseconds;
			cumulus.LogDebugMessage($"Monthly recs editor Logfiles load = {elapsed} ms");

			return json.ToString();
		}

		internal string GetThisMonthRecData()
		{
			const string timeStampFormat = "g";
			const string dateStampFormat = "d";

			var json = new StringBuilder("{", 1700);
			// Records - Temperature
			json.Append($"\"highTempVal\":\"{station.ThisMonth.HighTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTime\":\"{station.ThisMonth.HighTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowTempVal\":\"{station.ThisMonth.LowTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.ThisMonth.LowTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.ThisMonth.HighDewPoint.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.ThisMonth.HighDewPoint.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.ThisMonth.LowDewPoint.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.ThisMonth.LowDewPoint.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.ThisMonth.HighAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.ThisMonth.HighAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.ThisMonth.LowAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.ThisMonth.LowAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.ThisMonth.HighFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.ThisMonth.HighFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.ThisMonth.LowFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.ThisMonth.LowFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.ThisMonth.HighHumidex.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.ThisMonth.HighHumidex.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.ThisMonth.LowChill.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.ThisMonth.LowChill.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.ThisMonth.HighHeatIndex.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.ThisMonth.HighHeatIndex.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.ThisMonth.HighMinTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.ThisMonth.HighMinTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.ThisMonth.LowMaxTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.ThisMonth.LowMaxTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.ThisMonth.HighDailyTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.ThisMonth.HighDailyTempRange.GetTsString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.ThisMonth.LowDailyTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.ThisMonth.LowDailyTempRange.GetTsString(dateStampFormat)}\",");
			// Records - Humidity
			json.Append($"\"highHumidityVal\":\"{station.ThisMonth.HighHumidity.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTime\":\"{station.ThisMonth.HighHumidity.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.ThisMonth.LowHumidity.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.ThisMonth.LowHumidity.GetTsString(timeStampFormat)}\",");
			// Records - Pressure
			json.Append($"\"highBarometerVal\":\"{station.ThisMonth.HighPress.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTime\":\"{station.ThisMonth.HighPress.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.ThisMonth.LowPress.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.ThisMonth.LowPress.GetTsString(timeStampFormat)}\",");
			// Records - Wind
			json.Append($"\"highGustVal\":\"{station.ThisMonth.HighGust.GetValString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTime\":\"{station.ThisMonth.HighGust.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.ThisMonth.HighWind.GetValString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.ThisMonth.HighWind.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.ThisMonth.HighWindRun.GetValString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.ThisMonth.HighWindRun.GetTsString(dateStampFormat)}\",");
			// Records - Rain
			json.Append($"\"highRainRateVal\":\"{station.ThisMonth.HighRainRate.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTime\":\"{station.ThisMonth.HighRainRate.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.ThisMonth.HourlyRain.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.ThisMonth.HourlyRain.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.ThisMonth.DailyRain.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.ThisMonth.DailyRain.GetTsString(dateStampFormat)}\",");
			json.Append($"\"highRain24hVal\":\"{station.ThisMonth.HighRain24Hours.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRain24hTime\":\"{station.ThisMonth.HighRain24Hours.GetTsString(timeStampFormat)}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.ThisMonth.LongestDryPeriod.GetValString("F0")}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.ThisMonth.LongestDryPeriod.GetTsString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.ThisMonth.LongestWetPeriod.GetValString("F0")}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.ThisMonth.LongestWetPeriod.GetTsString(dateStampFormat)}\"");

			json.Append('}');

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
			try
			{
				switch (field)
				{
					case "highTempVal":
						station.ThisMonth.HighTemp.Val = double.Parse(value);
						break;
					case "highTempTime":
						station.ThisMonth.HighTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowTempVal":
						station.ThisMonth.LowTemp.Val = double.Parse(value);
						break;
					case "lowTempTime":
						station.ThisMonth.LowTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "highDewPointVal":
						station.ThisMonth.HighDewPoint.Val = double.Parse(value);
						break;
					case "highDewPointTime":
						station.ThisMonth.HighDewPoint.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowDewPointVal":
						station.ThisMonth.LowDewPoint.Val = double.Parse(value);
						break;
					case "lowDewPointTime":
						station.ThisMonth.LowDewPoint.Ts = localeDateTimeStrToDate(value);
						break;
					case "highApparentTempVal":
						station.ThisMonth.HighAppTemp.Val = double.Parse(value);
						break;
					case "highApparentTempTime":
						station.ThisMonth.HighAppTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowApparentTempVal":
						station.ThisMonth.LowAppTemp.Val = double.Parse(value);
						break;
					case "lowApparentTempTime":
						station.ThisMonth.LowAppTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "highFeelsLikeVal":
						station.ThisMonth.HighFeelsLike.Val = double.Parse(value);
						break;
					case "highFeelsLikeTime":
						station.ThisMonth.HighFeelsLike.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowFeelsLikeVal":
						station.ThisMonth.LowFeelsLike.Val = double.Parse(value);
						break;
					case "lowFeelsLikeTime":
						station.ThisMonth.LowFeelsLike.Ts = localeDateTimeStrToDate(value);
						break;
					case "highHumidexVal":
						station.ThisMonth.HighHumidex.Val = double.Parse(value);
						break;
					case "highHumidexTime":
						station.ThisMonth.HighHumidex.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowWindChillVal":
						station.ThisMonth.LowChill.Val = double.Parse(value);
						break;
					case "lowWindChillTime":
						station.ThisMonth.LowChill.Ts = localeDateTimeStrToDate(value);
						break;
					case "highHeatIndexVal":
						station.ThisMonth.HighHeatIndex.Val = double.Parse(value);
						break;
					case "highHeatIndexTime":
						station.ThisMonth.HighHeatIndex.Ts = localeDateTimeStrToDate(value);
						break;
					case "highMinTempVal":
						station.ThisMonth.HighMinTemp.Val = double.Parse(value);
						break;
					case "highMinTempTime":
						station.ThisMonth.HighMinTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowMaxTempVal":
						station.ThisMonth.LowMaxTemp.Val = double.Parse(value);
						break;
					case "lowMaxTempTime":
						station.ThisMonth.LowMaxTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "highDailyTempRangeVal":
						station.ThisMonth.HighDailyTempRange.Val = double.Parse(value);
						break;
					case "highDailyTempRangeTime":
						station.ThisMonth.HighDailyTempRange.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowDailyTempRangeVal":
						station.ThisMonth.LowDailyTempRange.Val = double.Parse(value);
						break;
					case "lowDailyTempRangeTime":
						station.ThisMonth.LowDailyTempRange.Ts = localeDateTimeStrToDate(value);
						break;
					case "highHumidityVal":
						station.ThisMonth.HighHumidity.Val = int.Parse(value);
						break;
					case "highHumidityTime":
						station.ThisMonth.HighHumidity.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowHumidityVal":
						station.ThisMonth.LowHumidity.Val = int.Parse(value);
						break;
					case "lowHumidityTime":
						station.ThisMonth.LowHumidity.Ts = localeDateTimeStrToDate(value);
						break;
					case "highBarometerVal":
						station.ThisMonth.HighPress.Val = double.Parse(value);
						break;
					case "highBarometerTime":
						station.ThisMonth.HighPress.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowBarometerVal":
						station.ThisMonth.LowPress.Val = double.Parse(value);
						break;
					case "lowBarometerTime":
						station.ThisMonth.LowPress.Ts = localeDateTimeStrToDate(value);
						break;
					case "highGustVal":
						station.ThisMonth.HighGust.Val = double.Parse(value);
						break;
					case "highGustTime":
						station.ThisMonth.HighGust.Ts = localeDateTimeStrToDate(value);
						break;
					case "highWindVal":
						station.ThisMonth.HighWind.Val = double.Parse(value);
						break;
					case "highWindTime":
						station.ThisMonth.HighWind.Ts = localeDateTimeStrToDate(value);
						break;
					case "highWindRunVal":
						station.ThisMonth.HighWindRun.Val = double.Parse(value);
						break;
					case "highWindRunTime":
						station.ThisMonth.HighWindRun.Ts = localeDateTimeStrToDate(value);
						break;
					case "highRainRateVal":
						station.ThisMonth.HighRainRate.Val = double.Parse(value);
						break;
					case "highRainRateTime":
						station.ThisMonth.HighRainRate.Ts = localeDateTimeStrToDate(value);
						break;
					case "highHourlyRainVal":
						station.ThisMonth.HourlyRain.Val = double.Parse(value);
						break;
					case "highHourlyRainTime":
						station.ThisMonth.HourlyRain.Ts = localeDateTimeStrToDate(value);
						break;
					case "highDailyRainVal":
						station.ThisMonth.DailyRain.Val = double.Parse(value);
						break;
					case "highDailyRainTime":
						station.ThisMonth.DailyRain.Ts = localeDateTimeStrToDate(value);
						break;
					case "highRain24hVal":
						station.ThisMonth.HighRain24Hours.Val = double.Parse(value);
						break;
					case "highRain24hTime":
						station.ThisMonth.HighRain24Hours.Ts = localeDateTimeStrToDate(value);
						break;
					case "longestDryPeriodVal":
						station.ThisMonth.LongestDryPeriod.Val = int.Parse(value);
						break;
					case "longestDryPeriodTime":
						station.ThisMonth.LongestDryPeriod.Ts = localeDateTimeStrToDate(value);
						break;
					case "longestWetPeriodVal":
						station.ThisMonth.LongestWetPeriod.Val = int.Parse(value);
						break;
					case "longestWetPeriodTime":
						station.ThisMonth.LongestWetPeriod.Ts = localeDateTimeStrToDate(value);
						break;
					default:
						return "Data index not recognised";
				}
				station.WriteMonthIniFile();
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
			return "Success";
		}

		internal string GetThisYearRecData()
		{
			const string timeStampFormat = "g";
			const string dateStampFormat = "d";
			const string monthFormat = "MMM yyyy";

			var json = new StringBuilder("{", 1800);
			// Records - Temperature
			json.Append($"\"highTempVal\":\"{station.ThisYear.HighTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTime\":\"{station.ThisYear.HighTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowTempVal\":\"{station.ThisYear.LowTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.ThisYear.LowTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.ThisYear.HighDewPoint.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.ThisYear.HighDewPoint.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.ThisYear.LowDewPoint.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.ThisYear.LowDewPoint.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.ThisYear.HighAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.ThisYear.HighAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.ThisYear.LowAppTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.ThisYear.LowAppTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.ThisYear.HighFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.ThisYear.HighFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.ThisYear.LowFeelsLike.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.ThisYear.LowFeelsLike.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.ThisYear.HighHumidex.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.ThisYear.HighHumidex.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.ThisYear.LowChill.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.ThisYear.LowChill.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.ThisYear.HighHeatIndex.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.ThisYear.HighHeatIndex.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.ThisYear.HighMinTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.ThisYear.HighMinTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.ThisYear.LowMaxTemp.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.ThisYear.LowMaxTemp.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.ThisYear.HighDailyTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.ThisYear.HighDailyTempRange.GetTsString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.ThisYear.LowDailyTempRange.GetValString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.ThisYear.LowDailyTempRange.GetTsString(dateStampFormat)}\",");
			// Records - Humidity
			json.Append($"\"highHumidityVal\":\"{station.ThisYear.HighHumidity.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTime\":\"{station.ThisYear.HighHumidity.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.ThisYear.LowHumidity.GetValString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.ThisYear.LowHumidity.GetTsString(timeStampFormat)}\",");
			// Records - Pressure
			json.Append($"\"highBarometerVal\":\"{station.ThisYear.HighPress.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTime\":\"{station.ThisYear.HighPress.GetTsString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.ThisYear.LowPress.GetValString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.ThisYear.LowPress.GetTsString(timeStampFormat)}\",");
			// Records - Wind
			json.Append($"\"highGustVal\":\"{station.ThisYear.HighGust.GetValString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTime\":\"{station.ThisYear.HighGust.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.ThisYear.HighWind.GetValString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.ThisYear.HighWind.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.ThisYear.HighWindRun.GetValString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.ThisYear.HighWindRun.GetTsString(dateStampFormat)}\",");
			// Records - Rain
			json.Append($"\"highRainRateVal\":\"{station.ThisYear.HighRainRate.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTime\":\"{station.ThisYear.HighRainRate.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.ThisYear.HourlyRain.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.ThisYear.HourlyRain.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.ThisYear.DailyRain.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.ThisYear.DailyRain.GetTsString(dateStampFormat)}\",");
			json.Append($"\"highRain24hVal\":\"{station.ThisYear.HighRain24Hours.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highRain24hTime\":\"{station.ThisYear.HighRain24Hours.GetTsString(timeStampFormat)}\",");
			json.Append($"\"highMonthlyRainVal\":\"{station.ThisYear.MonthlyRain.GetValString(cumulus.RainFormat)}\",");
			json.Append($"\"highMonthlyRainTime\":\"{station.ThisYear.MonthlyRain.GetTsString(monthFormat)}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.ThisYear.LongestDryPeriod.GetValString("F0")}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.ThisYear.LongestDryPeriod.GetTsString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.ThisYear.LongestWetPeriod.GetValString("F0")}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.ThisYear.LongestWetPeriod.GetTsString(dateStampFormat)}\"");

			json.Append('}');

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
			try
			{
				switch (field)
				{
					case "highTempVal":
						station.ThisYear.HighTemp.Val = double.Parse(value);
						break;
					case "highTempTime":
						station.ThisYear.HighTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowTempVal":
						station.ThisYear.LowTemp.Val = double.Parse(value);
						break;
					case "lowTempTime":
						station.ThisYear.LowTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "highDewPointVal":
						station.ThisYear.HighDewPoint.Val = double.Parse(value);
						break;
					case "highDewPointTime":
						station.ThisYear.HighDewPoint.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowDewPointVal":
						station.ThisYear.LowDewPoint.Val = double.Parse(value);
						break;
					case "lowDewPointTime":
						station.ThisYear.LowDewPoint.Ts = localeDateTimeStrToDate(value);
						break;
					case "highApparentTempVal":
						station.ThisYear.HighAppTemp.Val = double.Parse(value);
						break;
					case "highApparentTempTime":
						station.ThisYear.HighAppTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowApparentTempVal":
						station.ThisYear.LowAppTemp.Val = double.Parse(value);
						break;
					case "lowApparentTempTime":
						station.ThisYear.LowAppTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "highFeelsLikeVal":
						station.ThisYear.HighFeelsLike.Val = double.Parse(value);
						break;
					case "highFeelsLikeTime":
						station.ThisYear.HighFeelsLike.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowFeelsLikeVal":
						station.ThisYear.LowFeelsLike.Val = double.Parse(value);
						break;
					case "lowFeelsLikeTime":
						station.ThisYear.LowFeelsLike.Ts = localeDateTimeStrToDate(value);
						break;
					case "highHumidexVal":
						station.ThisYear.HighHumidex.Val = double.Parse(value);
						break;
					case "highHumidexTime":
						station.ThisYear.HighHumidex.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowWindChillVal":
						station.ThisYear.LowChill.Val = double.Parse(value);
						break;
					case "lowWindChillTime":
						station.ThisYear.LowChill.Ts = localeDateTimeStrToDate(value);
						break;
					case "highHeatIndexVal":
						station.ThisYear.HighHeatIndex.Val = double.Parse(value);
						break;
					case "highHeatIndexTime":
						station.ThisYear.HighHeatIndex.Ts = localeDateTimeStrToDate(value);
						break;
					case "highMinTempVal":
						station.ThisYear.HighMinTemp.Val = double.Parse(value);
						break;
					case "highMinTempTime":
						station.ThisYear.HighMinTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowMaxTempVal":
						station.ThisYear.LowMaxTemp.Val = double.Parse(value);
						break;
					case "lowMaxTempTime":
						station.ThisYear.LowMaxTemp.Ts = localeDateTimeStrToDate(value);
						break;
					case "highDailyTempRangeVal":
						station.ThisYear.HighDailyTempRange.Val = double.Parse(value);
						break;
					case "highDailyTempRangeTime":
						station.ThisYear.HighDailyTempRange.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowDailyTempRangeVal":
						station.ThisYear.LowDailyTempRange.Val = double.Parse(value);
						break;
					case "lowDailyTempRangeTime":
						station.ThisYear.LowDailyTempRange.Ts = localeDateTimeStrToDate(value);
						break;
					case "highHumidityVal":
						station.ThisYear.HighHumidity.Val = int.Parse(value);
						break;
					case "highHumidityTime":
						station.ThisYear.HighHumidity.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowHumidityVal":
						station.ThisYear.LowHumidity.Val = int.Parse(value);
						break;
					case "lowHumidityTime":
						station.ThisYear.LowHumidity.Ts = localeDateTimeStrToDate(value);
						break;
					case "highBarometerVal":
						station.ThisYear.HighPress.Val = double.Parse(value);
						break;
					case "highBarometerTime":
						station.ThisYear.HighPress.Ts = localeDateTimeStrToDate(value);
						break;
					case "lowBarometerVal":
						station.ThisYear.LowPress.Val = double.Parse(value);
						break;
					case "lowBarometerTime":
						station.ThisYear.LowPress.Ts = localeDateTimeStrToDate(value);
						break;
					case "highGustVal":
						station.ThisYear.HighGust.Val = double.Parse(value);
						break;
					case "highGustTime":
						station.ThisYear.HighGust.Ts = localeDateTimeStrToDate(value);
						break;
					case "highWindVal":
						station.ThisYear.HighWind.Val = double.Parse(value);
						break;
					case "highWindTime":
						station.ThisYear.HighWind.Ts = localeDateTimeStrToDate(value);
						break;
					case "highWindRunVal":
						station.ThisYear.HighWindRun.Val = double.Parse(value);
						break;
					case "highWindRunTime":
						station.ThisYear.HighWindRun.Ts = localeDateTimeStrToDate(value);
						break;
					case "highRainRateVal":
						station.ThisYear.HighRainRate.Val = double.Parse(value);
						break;
					case "highRainRateTime":
						station.ThisYear.HighRainRate.Ts = localeDateTimeStrToDate(value);
						break;
					case "highHourlyRainVal":
						station.ThisYear.HourlyRain.Val = double.Parse(value);
						break;
					case "highHourlyRainTime":
						station.ThisYear.HourlyRain.Ts = localeDateTimeStrToDate(value);
						break;
					case "highDailyRainVal":
						station.ThisYear.DailyRain.Val = double.Parse(value);
						break;
					case "highDailyRainTime":
						station.ThisYear.DailyRain.Ts = localeDateTimeStrToDate(value);
						break;
					case "highRain24hVal":
						station.ThisYear.HighRain24Hours.Val = double.Parse(value);
						break;
					case "highRain24hTime":
						station.ThisYear.HighRain24Hours.Ts = localeDateTimeStrToDate(value);
						break;
					case "highMonthlyRainVal":
						station.ThisYear.MonthlyRain.Val = double.Parse(value);
						break;
					case "highMonthlyRainTime":
						// MM/yyyy
						station.ThisYear.MonthlyRain.Ts = localeMonthYearStrToDate(value);
						break;
					case "longestDryPeriodVal":
						station.ThisYear.LongestDryPeriod.Val = int.Parse(value);
						break;
					case "longestDryPeriodTime":
						station.ThisYear.LongestDryPeriod.Ts = localeDateTimeStrToDate(value);
						break;
					case "longestWetPeriodVal":
						station.ThisYear.LongestWetPeriod.Val = int.Parse(value);
						break;
					case "longestWetPeriodTime":
						station.ThisYear.LongestWetPeriod.Ts = localeDateTimeStrToDate(value);
						break;
					default:
						return "Data index not recognised";
				}
				station.WriteYearIniFile();
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
			return "Success";
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
			var lines = File.ReadAllLines(cumulus.DayFileName).ToList();
			string newLine;

			if (newData.action == "Edit")
			{
				try
				{
					var lineNum = newData.lines[0] - 1; // we want a zero relative index

					// replace the edited line
					var orgLine = lines[lineNum];
					newLine = string.Join(cumulus.ListSeparator, newData.data[0]);

					var sep = Utils.GetLogFileSeparator(orgLine, cumulus.ListSeparator);

					// check if the dates match
					if (orgLine.Split(sep[0])[0] == newData.data[0][0])
					{
						lines[lineNum] = newLine;

						// Update the in memory record
						try
						{
							station.DayFile[lineNum] = station.ParseDayFileRec(newLine);

							// write dayfile back again
							File.WriteAllLines(cumulus.DayFileName, lines);
							cumulus.LogMessage($"EditDayFile: Changed dayfile line {lineNum + 1}, original = {orgLine}");
							cumulus.LogMessage($"EditDayFile: Changed dayfile line {lineNum + 1},      new = {newLine}");
						}
						catch
						{
							cumulus.LogMessage("EditDayFile: Failed, new data does not match required values");
							cumulus.LogMessage("EditDayFile: Data received - " + newLine);
							context.Response.StatusCode = 500;

							return "{\"errors\":{\"Logfile\":[\"<br>Failed, new data does not match required values\"]}}";
						}

						// Update the MySQL record
						if (!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Server) &&
							!string.IsNullOrEmpty(cumulus.MySqlConnSettings.UserID) &&
							!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Password) &&
							!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Database) &&
							cumulus.MySqlSettings.UpdateOnEdit
							)
						{
							var updateStr = "";

							try
							{
								var InvC = new CultureInfo("");
								var updt = new StringBuilder(1024);

								updt.Append($"UPDATE {cumulus.MySqlSettings.Dayfile.TableName} SET ");
								updt.Append($"HighWindGust={station.DayFile[lineNum].HighGust.ToString(cumulus.WindFormat, InvC)},");
								updt.Append($"HWindGBear={station.DayFile[lineNum].HighGustBearing},");
								updt.Append($"THWindG={station.DayFile[lineNum].HighGustTime:\\'HH:mm\\'},");
								updt.Append($"MinTemp={station.DayFile[lineNum].LowTemp.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"TMinTemp={station.DayFile[lineNum].LowTempTime:\\'HH:mm\\'},");
								updt.Append($"MaxTemp={station.DayFile[lineNum].HighTemp.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"TMaxTemp={station.DayFile[lineNum].HighTempTime:\\'HH:mm\\'},");
								updt.Append($"MinPress={station.DayFile[lineNum].LowPress.ToString(cumulus.PressFormat, InvC)},");
								updt.Append($"TMinPress={station.DayFile[lineNum].LowPressTime:\\'HH:mm\\'},");
								updt.Append($"MaxPress={station.DayFile[lineNum].HighPress.ToString(cumulus.PressFormat, InvC)},");
								updt.Append($"TMaxPress={station.DayFile[lineNum].HighPressTime:\\'HH:mm\\'},");
								updt.Append($"MaxRainRate={station.DayFile[lineNum].HighRainRate.ToString(cumulus.RainFormat, InvC)},");
								updt.Append($"TMaxRR={station.DayFile[lineNum].HighRainRateTime:\\'HH:mm\\'},");
								updt.Append($"TotRainFall={station.DayFile[lineNum].TotalRain.ToString(cumulus.RainFormat, InvC)},");
								updt.Append($"AvgTemp={station.DayFile[lineNum].AvgTemp.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"TotWindRun={station.DayFile[lineNum].WindRun.ToString("F1", InvC)},");
								updt.Append($"HighAvgWSpeed={station.DayFile[lineNum].HighAvgWind.ToString(cumulus.WindAvgFormat, InvC)},");
								updt.Append($"THAvgWSpeed={station.DayFile[lineNum].HighAvgWindTime:\\'HH:mm\\'},");
								updt.Append($"LowHum={(station.DayFile[lineNum].LowHumidity < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowHumidity.ToString() : "NULL")},");
								updt.Append($"TLowHum={(station.DayFile[lineNum].LowHumidity < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowHumidityTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"HighHum={(station.DayFile[lineNum].HighHumidity > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighHumidity.ToString() : "NULL")},");
								updt.Append($"THighHum={(station.DayFile[lineNum].HighHumidity > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighHumidityTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"TotalEvap={station.DayFile[lineNum].ET.ToString(cumulus.ETFormat, InvC)},");
								updt.Append($"HoursSun={station.DayFile[lineNum].SunShineHours.ToString(cumulus.SunFormat, InvC)},");
								updt.Append($"HighHeatInd={(station.DayFile[lineNum].HighHeatIndex > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighHeatIndex.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"THighHeatInd={(station.DayFile[lineNum].HighHeatIndex > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighHeatIndexTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"HighAppTemp={(station.DayFile[lineNum].HighAppTemp > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighAppTemp.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"THighAppTemp={(station.DayFile[lineNum].HighAppTemp > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighAppTempTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"LowAppTemp={(station.DayFile[lineNum].LowAppTemp < Cumulus.DefaultLoVal ?  station.DayFile[lineNum].LowAppTemp.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"TLowAppTemp={(station.DayFile[lineNum].LowAppTemp < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowAppTempTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"HighHourRain={station.DayFile[lineNum].HighHourlyRain.ToString(cumulus.RainFormat, InvC)},");
								updt.Append($"THighHourRain={station.DayFile[lineNum].HighHourlyRainTime:\\'HH:mm\\'},");
								updt.Append($"LowWindChill={(station.DayFile[lineNum].LowWindChill < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowWindChill.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"TLowWindChill={(station.DayFile[lineNum].LowWindChill < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowWindChillTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"HighDewPoint={(station.DayFile[lineNum].HighDewPoint > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighDewPoint.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"THighDewPoint={(station.DayFile[lineNum].HighDewPoint > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighDewPointTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"LowDewPoint={(station.DayFile[lineNum].LowDewPoint < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowDewPoint.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"TLowDewPoint={(station.DayFile[lineNum].LowDewPoint < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowDewPointTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"DomWindDir={station.DayFile[lineNum].DominantWindBearing},");
								updt.Append($"HeatDegDays={(station.DayFile[lineNum].HeatingDegreeDays > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HeatingDegreeDays.ToString("F1", InvC) : "NULL")},");
								updt.Append($"CoolDegDays={(station.DayFile[lineNum].CoolingDegreeDays > Cumulus.DefaultHiVal ? station.DayFile[lineNum].CoolingDegreeDays.ToString("F1", InvC) : "NULL")},");
								updt.Append($"HighSolarRad={(station.DayFile[lineNum].HighSolar > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighSolar.ToString() : "NULL")},");
								updt.Append($"THighSolarRad={(station.DayFile[lineNum].HighSolar > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighSolarTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"HighUV={(station.DayFile[lineNum].HighUv > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighUv.ToString(cumulus.UVFormat, InvC) : "NULL")},");
								updt.Append($"THighUV={(station.DayFile[lineNum].HighUv > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighUvTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"HWindGBearSym='{station.CompassPoint(station.DayFile[lineNum].HighGustBearing)}',");
								updt.Append($"DomWindDirSym='{station.CompassPoint(station.DayFile[lineNum].DominantWindBearing)}',");
								updt.Append($"MaxFeelsLike={(station.DayFile[lineNum].HighFeelsLike > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighFeelsLike.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"TMaxFeelsLike={(station.DayFile[lineNum].HighFeelsLike > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighFeelsLikeTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"MinFeelsLike={(station.DayFile[lineNum].LowFeelsLike < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowFeelsLike.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"TMinFeelsLike={(station.DayFile[lineNum].LowFeelsLike < Cumulus.DefaultLoVal ? station.DayFile[lineNum].LowFeelsLikeTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"MaxHumidex={(station.DayFile[lineNum].HighHumidex > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighHumidex.ToString(cumulus.TempFormat, InvC) : "NULL")},");
								updt.Append($"TMaxHumidex={(station.DayFile[lineNum].HighHumidex > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighHumidexTime.ToString("\\'HH:mm\\'") : "NULL")},");
								updt.Append($"ChillHours={(station.DayFile[lineNum].ChillHours > Cumulus.DefaultHiVal ? station.DayFile[lineNum].ChillHours.ToString("F1", InvC) : "NULL")},");
								updt.Append($"HighRain24h={(station.DayFile[lineNum].HighRain24h > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighRain24h.ToString(cumulus.RainFormat, InvC) : "NULL")},");
								updt.Append($"THighRain24h={(station.DayFile[lineNum].HighRain24h > Cumulus.DefaultHiVal ? station.DayFile[lineNum].HighRain24hTime.ToString("\\'HH:mm\\'") : "NULL")} ");

								updt.Append($"WHERE LogDate='{station.DayFile[lineNum].Date:yyyy-MM-dd}';");
								updateStr = updt.ToString();

								cumulus.MySqlCommandSync(updateStr, "EditDayFile");
								cumulus.LogMessage($"EditDayFile: SQL Updated");
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"EditDayFile: Failed, to update MySQL. Error = {ex.Message}");
								cumulus.LogMessage($"EditDayFile: SQL Update statement = {updateStr}");
								context.Response.StatusCode = 501;  // Use 501 to signal that SQL failed but file update was OK

								return "{\"errors\":{\"Dayfile\":[\"<br>Updated the dayfile OK\"], \"MySQL\":[\"<br>Failed to update MySQL\"]}}";
							}
						}
					}
					else
					{
						// ohoh! The dates do not match
						cumulus.LogMessage($"EditDayFile: Dates do not match. FormDate: {newData.data[0][0]}, FileDate: {orgLine.Split(sep[0])[0]}");
						return $"{{\"errors\":{{\"General\":[\"<br>Dates do not match. FormDate: {newData.data[0][0]}, FileDate: {orgLine.Split(sep[0])[0]}\"]}}}}";
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"EditDayFile: Failed. Error = {ex.Message}");
					return "{\"errors\":{\"General\":[\"<br>Error occurred: " + ex.Message + "\"]}}";
				}

				// we need to add the line num to the returned data
				return "[" + newData.lines[0] + "," + newData.data[0].ToJson().Substring(1);
			}
			else if (newData.action == "Delete")
			{
				// process the lines in reverse order so we do not mess up the indexes
				for (var i = newData.lines.Length - 1; i >= 0; i--)
				{
					var lineNum = newData.lines[i] - 1; // we want a zero relative index

					// Just double check we are deleting the correct line - see if the dates match
					var sep = Utils.GetLogFileSeparator(lines[lineNum], cumulus.ListSeparator);
					var lineData = lines[lineNum].Split(sep[0]);
					var formDate = newData.data[i][0];
					if (lineData[0] != formDate)
					{
						cumulus.LogMessage($"EditDayFile: Entry deletion failed. Line to delete does not match the file contents");
						cumulus.LogMessage($"EditDayFile: Line: {lineNum + 1}, filedate = {lineData[0]}, formdate = {formDate}");
						context.Response.StatusCode = 500;
						return $"{{\"errors\":{{\"Logfile\":[\"<br>Failed, line to delete does not match the file contents\", \"<br>Line: {lineNum + 1}, filedate = {lineData[0]}, formdate = {formDate}\"]}}}}";
					}

					try
					{
						// remove from file array
						lines.RemoveAt(lineNum);
						// Update the in memory record
						station.DayFile.RemoveAt(lineNum);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"EditDayFile: Entry deletion failed. Error = - " + ex.Message);
						cumulus.LogMessage($"EditDayFile: Entry data = " + newData.data[i]);
						context.Response.StatusCode = 500;
						return "{\"errors\":{\"Logfile\":[\"<br>Failed to delete record. Error: " + ex.Message + "\"]}}";
					}
				}

				// finally re-write the dayfile
				try
				{
					// write dayfile back again
					File.WriteAllLines(cumulus.DayFileName, lines);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"EditDayFile: Error writing to the dayfile. Error = - " + ex.Message);
					context.Response.StatusCode = 500;
					return "{\"errors\":{\"Logfile\":[\"<br>Error writing to the dayfile. Error: " + ex.Message + "\"]}}";
				}

				return "{\"errors\":null}";
			}
			else
			{
				cumulus.LogMessage($"EditDayFile: Unrecognised action = " + newData.action);
				context.Response.StatusCode = 500;
				return "{\"errors\":{\"Logfile\":[\"<br>Failed, unrecognised action = " + newData.action + "\"]}}";
			}
		}

		internal string EditMySqlCache(IHttpContext context)
		{
			var request = context.Request;
			string text;
			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			var newData = text.FromJson<MySqlCacheEditor>();


			if (newData.action == "Edit")
			{
				SqlCache newRec = null;

				try
				{
					newRec = new SqlCache()
					{
						key = newData.keys[0],
						statement = newData.statements[0]
					};

					station.RecentDataDb.Update(newRec);
					station.ReloadFailedMySQLCommands();
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"EditMySqlCache: Failed, to update MySQL statement. Error = {ex.Message}");
					context.Response.StatusCode = 500;

					return "{\"errors\":{\"MySqlCache\":[\"Failed to update MySQL cache\"]}, \"data\":[\"" + newRec.statement + "\"]";
				}

				// return the updated record
				return $"[{newData.keys[0]},\"{newData.statements[0]}\"]";
			}
			else if (newData.action == "Delete")
			{
				var newRec = new SqlCache();

				try
				{
					for (var i = 0; i < newData.keys.Length; i++)
					{
						newRec.key = newData.keys[i];
						newRec.statement = newData.statements[i];

						station.RecentDataDb.Delete(newRec);
					}

					station.ReloadFailedMySQLCommands();
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"EditMySqlCache: Failed, to delete MySQL statement. Error = {ex.Message}");
					context.Response.StatusCode = 500;

					return "{\"errors\":{\"MySqlCache\":[\"Failed to update MySQL cache\"]}, \"data\":[\"" + newRec.statement + "\"]";
				}

				return "{\"errors\":null}";
			}
			else
			{
				cumulus.LogMessage($"EditMySqlCache: Unrecognised action = " + newData.action);
				context.Response.StatusCode = 500;
				return "{\"errors\":{\"SQL cache\":[\"<br>Failed, unrecognised action = " + newData.action + "\"]}}";
			}
		}

		private class DayFileEditor
		{
			public string action { get; set; }
			public int[] lines { get; set; }
			public string[][] data { get; set; }
		}

		internal string EditDatalog(IHttpContext context)
		{
			var request = context.Request;
			string text;
			var InvC = new CultureInfo("");
			var lastMonth = -1;
			var lines = new List<string>();
			var logfile = string.Empty;

			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				text = reader.ReadToEnd();
			}

			var newData = text.FromJson<DatalogEditor>();

			if (newData.action == "Edit")
			{
				// Get the log file date
				var ts = Utils.ddmmyyhhmmStrToDate(newData.data[0][0], newData.data[0][1]);

				logfile = (newData.extra ? cumulus.GetExtraLogFileName(ts) : cumulus.GetLogFileName(ts));

				// read the log file into a List
				lines.Clear();
				lines = File.ReadAllLines(logfile).ToList();


				var lineNum = newData.lines[0] - 1; // our List is zero relative

				// replace the edited line
				var orgLine = lines[lineNum];
				var newLine = String.Join(cumulus.ListSeparator, newData.data[0]);

				// test if we are updating the correct entry
				var orgArr = orgLine.Split(cumulus.ListSeparator[0]);

				if (orgArr[0] == newData.data[0][0] && orgArr[1] == newData.data[0][1])
				{

					lines[lineNum] = newLine;

					try
					{
						// write logfile back again
						File.WriteAllLines(logfile, lines);
						cumulus.LogMessage($"EditDataLog: Changed Log file [{logfile}] line {lineNum + 1}, original = {orgLine}");
						cumulus.LogMessage($"EditDataLog: Changed Log file [{logfile}] line {lineNum + 1},      new = {newLine}");
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("EditDataLog: Failed, error = " + ex.Message);
						cumulus.LogMessage("EditDataLog: Data received - " + newLine);
						context.Response.StatusCode = 500;

						return "{\"errors\":{\"Logfile\":[\"<br>Failed to update, error = " + ex.Message + "\"]}}";
					}

					// Update internal database
					// This does not really work, as the recent data is every minute, the logged data could be every 5, 15, 30 minutes

					var LogRec = station.ParseLogFileRec(newLine, false);
					/*
					// first check if the record is on the recent data table
					try
					{

						var updt = new StringBuilder(1024);
						var updtRec = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp=?)", LogRec.Date)[0];
						if (updtRec != null)
						{
							updtRec.AppTemp = LogRec.ApparentTemperature;
							updtRec.DewPoint = LogRec.OutdoorDewpoint;
							updtRec.FeelsLike = LogRec.FeelsLike;
							updtRec.HeatIndex = LogRec.HeatIndex;
							updtRec.Humidex = LogRec.Humidex;
							updtRec.Humidity = LogRec.OutdoorHumidity;
							updtRec.IndoorHumidity = LogRec.IndoorHumidity;
							updtRec.IndoorTemp = LogRec.IndoorTemperature;
							updtRec.OutsideTemp = LogRec.OutdoorTemperature;
							updtRec.Pressure = LogRec.Pressure;
							updtRec.raincounter = LogRec.Raincounter;
							updtRec.RainRate = LogRec.RainRate;
							updtRec.RainToday = LogRec.RainToday;
							updtRec.SolarMax = (int)LogRec.CurrentSolarMax;
							updtRec.SolarRad = (int)LogRec.SolarRad;
							updtRec.UV = LogRec.UV;
							updtRec.WindAvgDir = LogRec.AvgBearing;
							updtRec.WindChill = LogRec.WindChill;
							updtRec.WindDir = LogRec.Bearing;
							updtRec.WindGust = LogRec.RecentMaxGust;
							updtRec.WindLatest = LogRec.WindLatest;
							updtRec.WindSpeed = LogRec.WindAverage;

							var rowCnt = station.RecentDataDb.Update(updtRec);
							if (rowCnt != 1)
							{
								cumulus.LogMessage("EditDataLog: Failed to update SQLite database");
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"EditDataLog: Failed to update SQLite. Error = {ex.Message}");
					}
					*/

					// Update the MySQL record
					if (!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Server) &&
						!string.IsNullOrEmpty(cumulus.MySqlConnSettings.UserID) &&
						!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Password) &&
						!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Database) &&
						cumulus.MySqlSettings.UpdateOnEdit
						)
					{
						// Only the monthly log file is stored in MySQL
						if (!newData.extra)
						{
							var updateStr = "";

							try
							{
								var updt = new StringBuilder(1024);


								updt.Append($"UPDATE {cumulus.MySqlSettings.Monthly.TableName} SET ");
								updt.Append($"Temp={LogRec.OutdoorTemperature.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"Humidity={LogRec.OutdoorHumidity},");
								updt.Append($"Dewpoint={LogRec.OutdoorDewpoint.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"Windspeed={LogRec.WindAverage.ToString(cumulus.WindAvgFormat, InvC)},");
								updt.Append($"Windgust={LogRec.RecentMaxGust.ToString(cumulus.WindFormat, InvC)},");
								updt.Append($"Windbearing={LogRec.AvgBearing},");
								updt.Append($"RainRate={LogRec.RainRate.ToString(cumulus.RainFormat, InvC)},");
								updt.Append($"TodayRainSoFar={LogRec.RainToday.ToString(cumulus.RainFormat, InvC)},");
								updt.Append($"Pressure={LogRec.Pressure.ToString(cumulus.PressFormat, InvC)},");
								updt.Append($"Raincounter={LogRec.Raincounter.ToString(cumulus.RainFormat, InvC)},");
								updt.Append($"InsideTemp={LogRec.IndoorTemperature.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"InsideHumidity={LogRec.IndoorHumidity},");
								updt.Append($"LatestWindGust={LogRec.WindLatest.ToString(cumulus.WindFormat, InvC)},");
								updt.Append($"WindChill={LogRec.WindChill.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"HeatIndex={LogRec.HeatIndex.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"UVindex={LogRec.UV.ToString(cumulus.UVFormat, InvC)},");
								updt.Append($"SolarRad={LogRec.SolarRad},");
								updt.Append($"Evapotrans={LogRec.ET.ToString(cumulus.ETFormat, InvC)},");
								updt.Append($"AnnualEvapTran={LogRec.AnnualETTotal.ToString(cumulus.ETFormat, InvC)},");
								updt.Append($"ApparentTemp={LogRec.ApparentTemperature.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"MaxSolarRad={(Math.Round(LogRec.CurrentSolarMax))},");
								updt.Append($"HrsSunShine={LogRec.SunshineHours.ToString(cumulus.SunFormat, InvC)},");
								updt.Append($"CurrWindBearing={LogRec.Bearing},");
								updt.Append($"RG11rain={LogRec.RG11RainToday.ToString(cumulus.RainFormat, InvC)},");
								updt.Append($"RainSinceMidnight={LogRec.RainSinceMidnight.ToString(cumulus.RainFormat, InvC)},");
								updt.Append($"WindbearingSym='{station.CompassPoint(LogRec.AvgBearing)}',");
								updt.Append($"CurrWindBearingSym='{station.CompassPoint(LogRec.Bearing)}',");
								updt.Append($"FeelsLike={LogRec.FeelsLike.ToString(cumulus.TempFormat, InvC)},");
								updt.Append($"Humidex={LogRec.Humidex.ToString(cumulus.TempFormat, InvC)} ");

								updt.Append($"WHERE LogDateTime='{LogRec.Date:yyyy-MM-dd HH:mm}';");
								updateStr = updt.ToString();


								cumulus.MySqlCommandSync(updateStr, "EditLogFile");
								cumulus.LogMessage($"EditDataLog: SQL Updated");
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"EditDataLog: Failed, to update MySQL. Error = {ex.Message}");
								cumulus.LogMessage($"EditDataLog: SQL Update statement = {updateStr}");
								context.Response.StatusCode = 501; // Use 501 to signal that SQL failed but file update was OK

								return "{\"errors\": { \"Logfile\":[\"<br>Updated the log file OK\"], \"MySQL\":[\"<br>Failed to update MySQL. Error: " + ex.Message + "\"] }}";
							}

						}
					}
				}
				else
				{
					// oh-oh! The date/times do not match
					// ohoh! The dates do not match
					cumulus.LogMessage($"EditDataLog: Dates do not match. FormDate: {newData.data[0][0]} {newData.data[0][1]}, FileDate: {orgArr[0]} {orgArr[1]}");
					return $"{{\"errors\":{{\"General\":[\"<br>Dates do not match. FormDate: {newData.data[0][0]} {newData.data[0][1]}, FileDate: {orgArr[0]} {orgArr[1]}\"]}}}}";
				}
			}
			else if (newData.action == "Delete")
			{
				// process the lines in reverse order so we do not mess up the indexes
				for (var i = newData.lines.Length - 1; i >= 0; i--)
				{
					// first get the correct log file - if we don't have it already
					// date will format "dd-mm-yy" or "dd/mm/yy"
					// Get a timestamp
					var ts = Utils.ddmmyyStrToDate(newData.data[i][0]);

					if (ts.Month != lastMonth)
					{
						logfile = (newData.extra ? cumulus.GetExtraLogFileName(ts) : cumulus.GetLogFileName(ts));

						// read the log file into a List
						lines.Clear();
						lines = File.ReadAllLines(logfile).ToList();
					}

					var lineNum = newData.lines[i] - 1; // we want a zero relative index

					// Just double check we are deleting the correct line - see if the date and .Ts match
					var sep = Utils.GetLogFileSeparator(lines[lineNum], cumulus.ListSeparator);
					var lineData = lines[lineNum].Split(sep[0]);
					if (lineData[0] == newData.data[i][0] && lineData[1] == newData.data[i][1])
					{
						var thisrec = new List<string>(newData.data[i]);
						thisrec.Insert(0, newData.lines[i].ToString());

						try
						{
							lines.RemoveAt(lineNum);
							cumulus.LogMessage($"EditDataLog: Entry deleted - " + thisrec.ToJson());
						}
						catch (Exception ex)
						{
							cumulus.LogMessage($"EditDataLog: Entry deletion failed. Error = - " + ex.Message);
							cumulus.LogMessage($"EditDataLog: Entry data = - " + thisrec.ToJson());
							context.Response.StatusCode = 500;
							return "{\"errors\": { \"Logfile\": [\"<br>Failed to delete record. Error: " + ex.Message + "\"]}}";
						}
					}
					else
					{
						cumulus.LogMessage($"EditDataLog: Error. Line to delete {newData.data[i][0]} {newData.data[i][1]} does not match the file contents {lineData[0]} {lineData[1]}");
						context.Response.StatusCode = 500;
						return "{\"errors\":{\"Logfile\":[\"Failed, line to delete does not match the file contents\"]}}";
					}
				}

				// finally re-write the dayfile
				try
				{
					// write logfile back again
					File.WriteAllLines(logfile, lines);

				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"EditDataLog: Error writing to the logfile {logfile}. Error = - {ex.Message}");
					context.Response.StatusCode = 500;
					return "{\"errors\":{\"Logfile\":[\"<br>Error writing to the logfile " + logfile + ". Error: " + ex.Message + "\"]}}";
				}

			}

			return "{\"errors\":null}";
		}

		private class DatalogEditor
		{
			public string action { get; set; }
			public int[] lines { get; set; }
			public bool extra { get; set; }
			public string[][] data { get; set; }
		}

		private class MySqlCacheEditor
		{
			public string action { get; set; }
			public int[] keys { get; set; }
			public string[] statements { get; set; }
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

		private void AddLastHoursRainEntry(DateTime ts, double rain, ref Queue<LastHourRainLog> hourQueue, ref Queue<LastHourRainLog> h24Queue)
		{
			var lastrain = new LastHourRainLog(ts, rain);

			hourQueue.Enqueue(lastrain);

			var hoursago = ts.AddHours(-1);

			while ((hourQueue.Count > 0) && (hourQueue.Peek().Timestamp < hoursago))
			{
				// the oldest entry is older than 1 hour ago, delete it
				hourQueue.Dequeue();
			}

			h24Queue.Enqueue(lastrain);

			hoursago = ts.AddHours(-24);

			while ((h24Queue.Count > 0) && (h24Queue.Peek().Timestamp < hoursago))
			{
				// the oldest entry is older than 24 hours ago, delete it
				h24Queue.Dequeue();
			}
		}

		private void Add24HourRainEntry(DateTime ts, double rain, ref Queue<LastHourRainLog> h24Queue)
		{
			var lastrain = new LastHourRainLog(ts, rain);
			h24Queue.Enqueue(lastrain);
		}


		private static DateTime localeDateTimeStrToDate(string dt)
		{
			dt = dt.Replace('+', ' ');

			// let this throw on invalid input
			return DateTime.Parse(dt);
		}

		private static DateTime localeMonthYearStrToDate(string dt)
		{
			dt = dt.Replace('+', ' ');

			// let this throw on invalid input
			return DateTime.ParseExact("01 " + dt, "dd MMM yyyy", CultureInfo.CurrentCulture);
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


		private class LocalRec
		{
			public double Value { get; set; }
			public DateTime Ts { get; set; }

			public LocalRec(bool HighVal)
			{
				Value = HighVal ? Cumulus.DefaultHiVal : Cumulus.DefaultLoVal;
				Ts = DateTime.MinValue;
			}

			public string GetValString(string format = "")
			{
				if (Value == Cumulus.DefaultHiVal || Value == Cumulus.DefaultLoVal)
					return "-";
				else
					return Value.ToString(format);
			}
			public string GetTsString(string format = "")
			{
				if (Ts == DateTime.MinValue)
					return "-";
				else
					return Ts.ToString(format);
			}
		}

	}
}
