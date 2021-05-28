using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unosquare.Labs.EmbedIO;
using ServiceStack;

namespace CumulusMX
{
	internal class DataEditor
	{
		private WeatherStation station;
		private Cumulus cumulus;
		private WebTags webtags;

		private readonly List<LastHourRainLog> hourRainLog = new List<LastHourRainLog>();

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

				// Formats to use for the different date kinds
				string utcTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
				string localTimeFormat = "yyyy-MM-dd'T'HH:mm:ss";

				// Override the ServiceStack Deserialization function
				// Check which format provided, attempt to parse as datetime or return minValue.
				ServiceStack.Text.JsConfig<DateTime>.DeSerializeFn = datetimeStr =>
				{
					if (string.IsNullOrWhiteSpace(datetimeStr))
					{
						return DateTime.MinValue;
					}

					if (datetimeStr.EndsWith("Z") &&
						DateTime.TryParseExact(datetimeStr, utcTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime resultUtc))
					{
						return resultUtc;
					}
					else if (!datetimeStr.EndsWith("Z") &&
						DateTime.TryParseExact(datetimeStr, localTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime resultLocal))
					{
						return resultLocal;
					}

					return DateTime.MinValue;
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

				// Formats to use for the different date kinds
				string utcTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
				string localTimeFormat = "yyyy-MM-dd'T'HH:mm:ss";

				// Override the ServiceStack Deserialization function
				// Check which format provided, attempt to parse as datetime or return minValue.
				ServiceStack.Text.JsConfig<DateTime>.DeSerializeFn = datetimeStr =>
				{
					if (string.IsNullOrWhiteSpace(datetimeStr))
					{
						return DateTime.MinValue;
					}

					if (datetimeStr.EndsWith("Z") &&
						DateTime.TryParseExact(datetimeStr, utcTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime resultUtc))
					{
						return resultUtc;
					}
					else if (!datetimeStr.EndsWith("Z") &&
						DateTime.TryParseExact(datetimeStr, localTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime resultLocal))
					{
						return resultLocal;
					}

					return DateTime.MinValue;
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
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";
			// Records - Temperature values
			var json = new StringBuilder("{", 1700);
			json.Append($"\"highTempVal\":\"{station.AllTime.HighTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempVal\":\"{station.AllTime.LowTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.AllTime.HighDewPoint.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.AllTime.LowDewPoint.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.AllTime.HighAppTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.AllTime.LowAppTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.AllTime.HighFeelsLike.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.AllTime.LowFeelsLike.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.AllTime.HighHumidex.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.AllTime.LowChill.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.AllTime.HighHeatIndex.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.AllTime.HighMinTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.AllTime.LowMaxTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.AllTime.HighDailyTempRange.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.AllTime.LowDailyTempRange.Val.ToString(cumulus.TempFormat)}\",");
			// Records - Temperature timestamps
			json.Append($"\"highTempTime\":\"{station.AllTime.HighTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.AllTime.LowTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.AllTime.HighDewPoint.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.AllTime.LowDewPoint.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.AllTime.HighAppTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.AllTime.LowAppTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.AllTime.HighFeelsLike.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.AllTime.LowFeelsLike.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.AllTime.HighHumidex.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.AllTime.LowChill.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.AllTime.HighHeatIndex.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.AllTime.HighMinTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.AllTime.LowMaxTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.AllTime.HighDailyTempRange.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.AllTime.LowDailyTempRange.Ts.ToString(dateStampFormat)}\",");
			// Records - Humidity values
			json.Append($"\"highHumidityVal\":\"{station.AllTime.HighHumidity.Val.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.AllTime.LowHumidity.Val.ToString(cumulus.HumFormat)}\",");
			// Records - Humidity times
			json.Append($"\"highHumidityTime\":\"{station.AllTime.HighHumidity.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.AllTime.LowHumidity.Ts.ToString(timeStampFormat)}\",");
			// Records - Pressure values
			json.Append($"\"highBarometerVal\":\"{station.AllTime.HighPress.Val.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.AllTime.LowPress.Val.ToString(cumulus.PressFormat)}\",");
			// Records - Pressure times
			json.Append($"\"highBarometerTime\":\"{station.AllTime.HighPress.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.AllTime.LowPress.Ts.ToString(timeStampFormat)}\",");
			// Records - Wind values
			json.Append($"\"highGustVal\":\"{station.AllTime.HighGust.Val.ToString(cumulus.WindFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.AllTime.HighWind.Val.ToString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.AllTime.HighWindRun.Val.ToString(cumulus.WindRunFormat)}\",");
			// Records - Wind times
			json.Append($"\"highGustTime\":\"{station.AllTime.HighGust.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.AllTime.HighWind.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.AllTime.HighWindRun.Ts.ToString(dateStampFormat)}\",");
			// Records - Rain values
			json.Append($"\"highRainRateVal\":\"{station.AllTime.HighRainRate.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.AllTime.HourlyRain.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.AllTime.DailyRain.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highMonthlyRainVal\":\"{station.AllTime.MonthlyRain.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.AllTime.LongestDryPeriod.Val:f0}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.AllTime.LongestWetPeriod.Val:f0}\",");
			// Records - Rain times
			json.Append($"\"highRainRateTime\":\"{station.AllTime.HighRainRate.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.AllTime.HourlyRain.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.AllTime.DailyRain.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"highMonthlyRainTime\":\"{station.AllTime.MonthlyRain.Ts:yyyy/MM}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.AllTime.LongestDryPeriod.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.AllTime.LongestWetPeriod.Ts.ToString(dateStampFormat)}\"");
			json.Append("}");

			return json.ToString();
		}

		internal string GetRecordsDayFile(string recordType)
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

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
				var data = station.DayFile.Where(r => r.Date >= startDate).ToList();
				foreach (var rec in data)
				{
					// This assumes the day file is in date order!
					if (thisDate.Month != rec.Date.Month)
					{
						// monthly rain
						if (rainThisMonth > highRainMonthVal)
						{
							highRainMonthVal = rainThisMonth;
							highRainMonthTime = thisDate;
						}
						// reset the date and counter for a new month
						thisDate = rec.Date;
						rainThisMonth = 0;
					}
					// hi gust
					if (rec.HighGust > highGustVal)
					{
						highGustVal = rec.HighGust;
						highGustTime = rec.HighGustTime;
					}
					// hi temp
					if (rec.HighTemp > highTempVal)
					{
						highTempVal = rec.HighTemp;
						highTempTime = rec.HighTempTime;
					}
					// lo temp
					if (rec.LowTemp < lowTempVal)
					{
						lowTempVal = rec.LowTemp;
						lowTempTime = rec.LowTempTime;
					}
					// hi min temp
					if (rec.LowTemp > highMinTempVal)
					{
						highMinTempVal = rec.LowTemp;
						highMinTempTime = rec.LowTempTime;
					}
					// lo max temp
					if (rec.HighTemp < lowMaxTempVal)
					{
						lowMaxTempVal = rec.HighTemp;
						lowMaxTempTime = rec.HighTempTime;
					}
					// hi temp range
					if ((rec.HighTemp - rec.LowTemp) > highTempRangeVal)
					{
						highTempRangeVal = rec.HighTemp - rec.LowTemp;
						highTempRangeTime = rec.Date;
					}
					// lo temp range
					if ((rec.HighTemp - rec.LowTemp) < lowTempRangeVal)
					{
						lowTempRangeVal = rec.HighTemp - rec.LowTemp;
						lowTempRangeTime = rec.Date;
					}
					// lo baro
					if (rec.LowPress < lowBaroVal)
					{
						lowBaroVal = rec.LowPress;
						lowBaroTime = rec.LowPressTime;
					}
					// hi baro
					if (rec.HighPress > highBaroVal)
					{
						highBaroVal = rec.HighPress;
						highBaroTime = rec.HighPressTime;
					}
					// hi rain rate
					if (rec.HighRainRate > highRainRateVal)
					{
						highRainRateVal = rec.HighRainRate;
						highRainRateTime = rec.HighRainRateTime;
					}
					// hi rain day
					if (rec.TotalRain > highRainDayVal)
					{
						highRainDayVal = rec.TotalRain;
						highRainDayTime = rec.Date;
					}

					// monthly rain
					rainThisMonth += rec.TotalRain;

					// dry/wet period
					if (Convert.ToInt32(rec.TotalRain * 1000) >= rainThreshold)
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
							thisDateWet = rec.Date;
						}
					}
					else
					{
						if (isDryNow)
						{
							currentDryPeriod++;
							thisDateDry = rec.Date;
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
					// hi wind run
					if (rec.WindRun > highWindRunVal)
					{
						highWindRunVal = rec.WindRun;
						highWindRunTime = rec.Date;
					}
					// hi wind
					if (rec.HighAvgWind > highWindVal)
					{
						highWindVal = rec.HighAvgWind;
						highWindTime = rec.HighAvgWindTime;
					}
					// lo humidity
					if (rec.LowHumidity < lowHumVal)
					{
						lowHumVal = rec.LowHumidity;
						lowHumTime = rec.LowHumidityTime;
					}
					// hi humidity
					if (rec.HighHumidity > highHumVal)
					{
						highHumVal = rec.HighHumidity;
						highHumTime = rec.HighHumidityTime;
					}
					// hi heat index
					if (rec.HighHeatIndex > highHeatIndVal)
					{
						highHeatIndVal = rec.HighHeatIndex;
						highHeatIndTime = rec.HighHeatIndexTime;
					}
					// hi app temp
					if (rec.HighAppTemp > highAppTempVal)
					{
						highAppTempVal = rec.HighAppTemp;
						highAppTempTime = rec.HighAppTempTime;
					}
					// lo app temp
					if (rec.LowAppTemp < lowAppTempVal)
					{
						lowAppTempVal = rec.LowAppTemp;
						lowAppTempTime = rec.LowAppTempTime;
					}
					// hi rain hour
					if (rec.HighHourlyRain > highRainHourVal)
					{
						highRainHourVal = rec.HighHourlyRain;
						highRainHourTime = rec.HighHourlyRainTime;
					}
					// lo wind chill
					if (rec.LowWindChill < lowWindChillVal)
					{
						lowWindChillVal = rec.LowWindChill;
						lowWindChillTime = rec.LowWindChillTime;
					}
					// hi dewpt
					if (rec.HighDewPoint > highDewPtVal)
					{
						highDewPtVal = rec.HighDewPoint;
						highDewPtTime = rec.HighDewPointTime;
					}
					// lo dewpt
					if (rec.LowDewPoint < lowDewPtVal)
					{
						lowDewPtVal = rec.LowDewPoint;
						lowDewPtTime = rec.LowDewPointTime;
					}
					// hi feels like
					if (rec.HighFeelsLike > highFeelsLikeVal)
					{
						highFeelsLikeVal = rec.HighFeelsLike;
						highFeelsLikeTime = rec.HighFeelsLikeTime;
					}
					// lo feels like
					if (rec.LowFeelsLike < lowFeelsLikeVal)
					{
						lowFeelsLikeVal = rec.LowFeelsLike;
						lowFeelsLikeTime = rec.LowFeelsLikeTime;
					}
					// hi humidex
					if (rec.HighHumidex > highHumidexVal)
					{
						highHumidexVal = rec.HighHumidex;
						highHumidexTime = rec.HighHumidexTime;
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
				json.Append($"\"highMinTempTimeDayfile\":\"{highMinTempTime.ToString(timeStampFormat)}\",");
				json.Append($"\"lowMaxTempValDayfile\":\"{lowMaxTempVal.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"lowMaxTempTimeDayfile\":\"{lowMaxTempTime.ToString(timeStampFormat)}\",");
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
			else
			{
				cumulus.LogMessage("GetRecordsDayFile: Error no day file records found");
			}

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

			var currentDay = datefrom;
			var dayHighTemp = highTempVal;
			DateTime dayHighTempTime = highTempTime;
			double dayLowTemp = lowTempVal;
			DateTime dayLowTempTime = highTempTime;
			double dayWindRun = 0;
			double dayRain = 0;


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
								{
									dayHighTemp = outsidetemp;
									dayHighTempTime = entrydate;
								}

								if (outsidetemp < dayLowTemp)
								{
									dayLowTemp = outsidetemp;
									dayLowTempTime = entrydate;
								}

								if (dayRain < raintoday)
									dayRain = raintoday;

								dayWindRun += entrydate.Subtract(lastentrydate).TotalHours * speed;
							}
							else // new meto day
							{
								if (dayHighTemp < lowMaxTempVal)
								{
									lowMaxTempVal = dayHighTemp;
									lowMaxTempTime = dayHighTempTime;
								}
								if (dayLowTemp > highMinTempVal)
								{
									highMinTempVal = dayLowTemp;
									highMinTempTime = dayLowTempTime;
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
								if (Convert.ToInt32(dayRain * 1000) >= rainThreshold)
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
			json.Append($"\"highMinTempTimeLogfile\":\"{highMinTempTime.ToString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempValLogfile\":\"{lowMaxTempVal.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTimeLogfile\":\"{lowMaxTempTime.ToString(timeStampFormat)}\",");
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
			var result = 1;
			try
			{
				string[] dt;
				switch (field)
				{
					case "highTempVal":
						station.SetAlltime(station.AllTime.HighTemp, double.Parse(value), station.AllTime.HighTemp.Ts);
						break;
					case "highTempTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighTemp, station.AllTime.HighTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowTempVal":
						station.SetAlltime(station.AllTime.LowTemp, double.Parse(value), station.AllTime.LowTemp.Ts);
						break;
					case "lowTempTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.LowTemp, station.AllTime.LowTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDewPointVal":
						station.SetAlltime(station.AllTime.HighDewPoint, double.Parse(value), station.AllTime.HighDewPoint.Ts);
						break;
					case "highDewPointTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighDewPoint, station.AllTime.HighDewPoint.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowDewPointVal":
						station.SetAlltime(station.AllTime.LowDewPoint, double.Parse(value), station.AllTime.LowDewPoint.Ts);
						break;
					case "lowDewPointTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.LowDewPoint, station.AllTime.LowDewPoint.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highApparentTempVal":
						station.SetAlltime(station.AllTime.HighAppTemp, double.Parse(value), station.AllTime.HighAppTemp.Ts);
						break;
					case "highApparentTempTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighAppTemp, station.AllTime.HighAppTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowApparentTempVal":
						station.SetAlltime(station.AllTime.LowAppTemp, double.Parse(value), station.AllTime.LowAppTemp.Ts);
						break;
					case "lowApparentTempTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.LowAppTemp, station.AllTime.LowAppTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highFeelsLikeVal":
						station.SetAlltime(station.AllTime.HighFeelsLike, double.Parse(value), station.AllTime.HighFeelsLike.Ts);
						break;
					case "highFeelsLikeTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighFeelsLike, station.AllTime.HighFeelsLike.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowFeelsLikeVal":
						station.SetAlltime(station.AllTime.LowFeelsLike, double.Parse(value), station.AllTime.LowFeelsLike.Ts);
						break;
					case "lowFeelsLikeTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.LowFeelsLike, station.AllTime.LowFeelsLike.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHumidexVal":
						station.SetAlltime(station.AllTime.HighHumidex, double.Parse(value), station.AllTime.HighHumidex.Ts);
						break;
					case "highHumidexTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighHumidex, station.AllTime.HighHumidex.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowWindChillVal":
						station.SetAlltime(station.AllTime.LowChill, double.Parse(value), station.AllTime.LowChill.Ts);
						break;
					case "lowWindChillTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.LowChill, station.AllTime.LowChill.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHeatIndexVal":
						station.SetAlltime(station.AllTime.HighHeatIndex, double.Parse(value), station.AllTime.HighHeatIndex.Ts);
						break;
					case "highHeatIndexTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighHeatIndex, station.AllTime.HighHeatIndex.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highMinTempVal":
						station.SetAlltime(station.AllTime.HighMinTemp, double.Parse(value), station.AllTime.HighMinTemp.Ts);
						break;
					case "highMinTempTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighMinTemp, station.AllTime.HighMinTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowMaxTempVal":
						station.SetAlltime(station.AllTime.LowMaxTemp, double.Parse(value), station.AllTime.LowMaxTemp.Ts);
						break;
					case "lowMaxTempTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.LowMaxTemp, station.AllTime.LowMaxTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDailyTempRangeVal":
						station.SetAlltime(station.AllTime.HighDailyTempRange, double.Parse(value), station.AllTime.HighDailyTempRange.Ts);
						break;
					case "highDailyTempRangeTime":
						station.SetAlltime(station.AllTime.HighDailyTempRange, station.AllTime.HighDailyTempRange.Val, station.ddmmyyStrToDate(value));
						break;
					case "lowDailyTempRangeVal":
						station.SetAlltime(station.AllTime.LowDailyTempRange, double.Parse(value), station.AllTime.LowDailyTempRange.Ts);
						break;
					case "lowDailyTempRangeTime":
						station.SetAlltime(station.AllTime.LowDailyTempRange, station.AllTime.LowDailyTempRange.Val, station.ddmmyyStrToDate(value));
						break;
					case "highHumidityVal":
						station.SetAlltime(station.AllTime.HighHumidity, double.Parse(value), station.AllTime.HighHumidity.Ts);
						break;
					case "highHumidityTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighHumidity, station.AllTime.HighHumidity.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowHumidityVal":
						station.SetAlltime(station.AllTime.LowHumidity, double.Parse(value), station.AllTime.LowHumidity.Ts);
						break;
					case "lowHumidityTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.LowHumidity, station.AllTime.LowHumidity.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highBarometerVal":
						station.SetAlltime(station.AllTime.HighPress, double.Parse(value), station.AllTime.HighPress.Ts);
						break;
					case "highBarometerTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighPress, station.AllTime.HighPress.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "lowBarometerVal":
						station.SetAlltime(station.AllTime.LowPress, double.Parse(value), station.AllTime.LowPress.Ts);
						break;
					case "lowBarometerTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.LowPress, station.AllTime.LowPress.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highGustVal":
						station.SetAlltime(station.AllTime.HighGust, double.Parse(value), station.AllTime.HighGust.Ts);
						break;
					case "highGustTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighGust, station.AllTime.HighGust.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highWindVal":
						station.SetAlltime(station.AllTime.HighWind, double.Parse(value), station.AllTime.HighWind.Ts);
						break;
					case "highWindTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighWind, station.AllTime.HighWind.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highWindRunVal":
						station.SetAlltime(station.AllTime.HighWindRun, double.Parse(value), station.AllTime.HighWindRun.Ts);
						break;
					case "highWindRunTime":
						station.SetAlltime(station.AllTime.HighWindRun, station.AllTime.HighWindRun.Val, station.ddmmyyStrToDate(value));
						break;
					case "highRainRateVal":
						station.SetAlltime(station.AllTime.HighRainRate, double.Parse(value), station.AllTime.HighRainRate.Ts);
						break;
					case "highRainRateTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HighRainRate, station.AllTime.HighRainRate.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highHourlyRainVal":
						station.SetAlltime(station.AllTime.HourlyRain, double.Parse(value), station.AllTime.HourlyRain.Ts);
						break;
					case "highHourlyRainTime":
						dt = value.Split('+');
						station.SetAlltime(station.AllTime.HourlyRain, station.AllTime.HourlyRain.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
						break;
					case "highDailyRainVal":
						station.SetAlltime(station.AllTime.DailyRain, double.Parse(value), station.AllTime.DailyRain.Ts);
						break;
					case "highDailyRainTime":
						station.SetAlltime(station.AllTime.DailyRain, station.AllTime.DailyRain.Val, station.ddmmyyStrToDate(value));
						break;
					case "highMonthlyRainVal":
						station.SetAlltime(station.AllTime.MonthlyRain, double.Parse(value), station.AllTime.MonthlyRain.Ts);
						break;
					case "highMonthlyRainTime":
						dt = value.Split('/');
						var datstr = "01/" + dt[1] + "/" + dt[0].Substring(2, 2);
						station.SetAlltime(station.AllTime.MonthlyRain, station.AllTime.MonthlyRain.Val, station.ddmmyyStrToDate(datstr));
						break;
					case "longestDryPeriodVal":
						station.SetAlltime(station.AllTime.LongestDryPeriod, double.Parse(value), station.AllTime.LongestDryPeriod.Ts);
						break;
					case "longestDryPeriodTime":
						station.SetAlltime(station.AllTime.LongestDryPeriod, station.AllTime.LongestDryPeriod.Val, station.ddmmyyStrToDate(value));
						break;
					case "longestWetPeriodVal":
						station.SetAlltime(station.AllTime.LongestWetPeriod, double.Parse(value), station.AllTime.LongestWetPeriod.Ts);
						break;
					case "longestWetPeriodTime":
						station.SetAlltime(station.AllTime.LongestWetPeriod, station.AllTime.LongestWetPeriod.Val, station.ddmmyyStrToDate(value));
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
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighTemp, double.Parse(value), station.MonthlyRecs[month].HighTemp.Ts);
							break;
						case "highTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighTemp, station.MonthlyRecs[month].HighTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "lowTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowTemp, double.Parse(value), station.MonthlyRecs[month].LowTemp.Ts);
							break;
						case "lowTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowTemp, station.MonthlyRecs[month].LowTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highDewPointVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighDewPoint, double.Parse(value), station.MonthlyRecs[month].HighDewPoint.Ts);
							break;
						case "highDewPointTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighDewPoint, station.MonthlyRecs[month].HighDewPoint.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "lowDewPointVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowDewPoint, double.Parse(value), station.MonthlyRecs[month].LowDewPoint.Ts);
							break;
						case "lowDewPointTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowDewPoint, station.MonthlyRecs[month].LowDewPoint.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highApparentTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighAppTemp, double.Parse(value), station.MonthlyRecs[month].HighAppTemp.Ts);
							break;
						case "highApparentTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighAppTemp, station.MonthlyRecs[month].HighAppTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "lowApparentTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowAppTemp, double.Parse(value), station.MonthlyRecs[month].LowAppTemp.Ts);
							break;
						case "lowApparentTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowAppTemp, station.MonthlyRecs[month].LowAppTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highFeelsLikeVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighFeelsLike, double.Parse(value), station.MonthlyRecs[month].HighFeelsLike.Ts);
							break;
						case "highFeelsLikeTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighFeelsLike, station.MonthlyRecs[month].HighFeelsLike.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "lowFeelsLikeVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowFeelsLike, double.Parse(value), station.MonthlyRecs[month].LowFeelsLike.Ts);
							break;
						case "lowFeelsLikeTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowFeelsLike, station.MonthlyRecs[month].LowFeelsLike.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highHumidexVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHumidex, double.Parse(value), station.MonthlyRecs[month].HighHumidex.Ts);
							break;
						case "highHumidexTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHumidex, station.MonthlyRecs[month].HighHumidex.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "lowWindChillVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowChill, double.Parse(value), station.MonthlyRecs[month].LowChill.Ts);
							break;
						case "lowWindChillTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowChill, station.MonthlyRecs[month].LowChill.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highHeatIndexVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHeatIndex, double.Parse(value), station.MonthlyRecs[month].HighHeatIndex.Ts);
							break;
						case "highHeatIndexTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHeatIndex, station.MonthlyRecs[month].HighHeatIndex.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highMinTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighMinTemp, double.Parse(value), station.MonthlyRecs[month].HighMinTemp.Ts);
							break;
						case "highMinTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighMinTemp, station.MonthlyRecs[month].HighMinTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "lowMaxTempVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowMaxTemp, double.Parse(value), station.MonthlyRecs[month].LowMaxTemp.Ts);
							break;
						case "lowMaxTempTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowMaxTemp, station.MonthlyRecs[month].LowMaxTemp.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highDailyTempRangeVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighDailyTempRange, double.Parse(value), station.MonthlyRecs[month].HighDailyTempRange.Ts);
							break;
						case "highDailyTempRangeTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighDailyTempRange, station.MonthlyRecs[month].HighDailyTempRange.Val, station.ddmmyyStrToDate(value));
							break;
						case "lowDailyTempRangeVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowDailyTempRange, double.Parse(value), station.MonthlyRecs[month].LowDailyTempRange.Ts);
							break;
						case "lowDailyTempRangeTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowDailyTempRange, station.MonthlyRecs[month].LowDailyTempRange.Val, station.ddmmyyStrToDate(value));
							break;
						case "highHumidityVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHumidity, double.Parse(value), station.MonthlyRecs[month].HighHumidity.Ts);
							break;
						case "highHumidityTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighHumidity, station.MonthlyRecs[month].HighHumidity.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "lowHumidityVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowHumidity, double.Parse(value), station.MonthlyRecs[month].LowHumidity.Ts);
							break;
						case "lowHumidityTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowHumidity, station.MonthlyRecs[month].LowHumidity.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highBarometerVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighPress, double.Parse(value), station.MonthlyRecs[month].HighPress.Ts);
							break;
						case "highBarometerTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighPress, station.MonthlyRecs[month].HighPress.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "lowBarometerVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowPress, double.Parse(value), station.MonthlyRecs[month].LowPress.Ts);
							break;
						case "lowBarometerTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LowPress, station.MonthlyRecs[month].LowPress.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highGustVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighGust, double.Parse(value), station.MonthlyRecs[month].HighGust.Ts);
							break;
						case "highGustTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighGust, station.MonthlyRecs[month].HighGust.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highWindVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighWind, double.Parse(value), station.MonthlyRecs[month].HighWind.Ts);
							break;
						case "highWindTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighWind, station.MonthlyRecs[month].HighWind.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highWindRunVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighWindRun, double.Parse(value), station.MonthlyRecs[month].HighWindRun.Ts);
							break;
						case "highWindRunTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighWindRun, station.MonthlyRecs[month].HighWindRun.Val, station.ddmmyyStrToDate(value));
							break;
						case "highRainRateVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighRainRate, double.Parse(value), station.MonthlyRecs[month].HighRainRate.Ts);
							break;
						case "highRainRateTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HighRainRate, station.MonthlyRecs[month].HighRainRate.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highHourlyRainVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HourlyRain, double.Parse(value), station.MonthlyRecs[month].HourlyRain.Ts);
							break;
						case "highHourlyRainTime":
							dt = value.Split('+');
							station.SetMonthlyAlltime(station.MonthlyRecs[month].HourlyRain, station.MonthlyRecs[month].HourlyRain.Val, station.ddmmyyhhmmStrToDate(dt[0], dt[1]));
							break;
						case "highDailyRainVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].DailyRain, double.Parse(value), station.MonthlyRecs[month].DailyRain.Ts);
							break;
						case "highDailyRainTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].DailyRain, station.MonthlyRecs[month].DailyRain.Val, station.ddmmyyStrToDate(value));
							break;
						case "highMonthlyRainVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].MonthlyRain, double.Parse(value), station.MonthlyRecs[month].MonthlyRain.Ts);
							break;
						case "highMonthlyRainTime":
							dt = value.Split('/');
							var datstr = "01/" + dt[1] + "/" + dt[0].Substring(2, 2);
							station.SetMonthlyAlltime(station.MonthlyRecs[month].MonthlyRain, station.MonthlyRecs[month].MonthlyRain.Val, station.ddmmyyStrToDate(datstr));
							break;
						case "longestDryPeriodVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LongestDryPeriod, double.Parse(value), station.MonthlyRecs[month].LongestDryPeriod.Ts);
							break;
						case "longestDryPeriodTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LongestDryPeriod, station.MonthlyRecs[month].LongestDryPeriod.Val, station.ddmmyyStrToDate(value));
							break;
						case "longestWetPeriodVal":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LongestWetPeriod, double.Parse(value), station.MonthlyRecs[month].LongestWetPeriod.Ts);
							break;
						case "longestWetPeriodTime":
							station.SetMonthlyAlltime(station.MonthlyRecs[month].LongestWetPeriod, station.MonthlyRecs[month].LongestWetPeriod.Val, station.ddmmyyStrToDate(value));
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
				json.Append($"\"{m}-highTempVal\":\"{station.MonthlyRecs[m].HighTemp.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowTempVal\":\"{station.MonthlyRecs[m].LowTemp.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDewPointVal\":\"{station.MonthlyRecs[m].HighDewPoint.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDewPointVal\":\"{station.MonthlyRecs[m].LowDewPoint.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highApparentTempVal\":\"{station.MonthlyRecs[m].HighAppTemp.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowApparentTempVal\":\"{station.MonthlyRecs[m].LowAppTemp.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeVal\":\"{station.MonthlyRecs[m].HighFeelsLike.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeVal\":\"{station.MonthlyRecs[m].LowFeelsLike.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHumidexVal\":\"{station.MonthlyRecs[m].HighHumidex.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowWindChillVal\":\"{station.MonthlyRecs[m].LowChill.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highHeatIndexVal\":\"{station.MonthlyRecs[m].HighHeatIndex.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highMinTempVal\":\"{station.MonthlyRecs[m].HighMinTemp.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowMaxTempVal\":\"{station.MonthlyRecs[m].LowMaxTemp.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeVal\":\"{station.MonthlyRecs[m].HighDailyTempRange.Val.ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeVal\":\"{station.MonthlyRecs[m].LowDailyTempRange.Val.ToString(cumulus.TempFormat)}\",");
				// Records - Temperature timestamps
				json.Append($"\"{m}-highTempTime\":\"{station.MonthlyRecs[m].HighTemp.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowTempTime\":\"{station.MonthlyRecs[m].LowTemp.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDewPointTime\":\"{station.MonthlyRecs[m].HighDewPoint.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowDewPointTime\":\"{station.MonthlyRecs[m].LowDewPoint.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highApparentTempTime\":\"{station.MonthlyRecs[m].HighAppTemp.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowApparentTempTime\":\"{station.MonthlyRecs[m].LowAppTemp.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highFeelsLikeTime\":\"{station.MonthlyRecs[m].HighFeelsLike.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowFeelsLikeTime\":\"{station.MonthlyRecs[m].LowFeelsLike.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHumidexTime\":\"{station.MonthlyRecs[m].HighHumidex.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowWindChillTime\":\"{station.MonthlyRecs[m].LowChill.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHeatIndexTime\":\"{station.MonthlyRecs[m].HighHeatIndex.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highMinTempTime\":\"{station.MonthlyRecs[m].HighMinTemp.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowMaxTempTime\":\"{station.MonthlyRecs[m].LowMaxTemp.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyTempRangeTime\":\"{station.MonthlyRecs[m].HighDailyTempRange.Ts.ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-lowDailyTempRangeTime\":\"{station.MonthlyRecs[m].LowDailyTempRange.Ts.ToString(dateStampFormat)}\",");
				// Records - Humidity values
				json.Append($"\"{m}-highHumidityVal\":\"{station.MonthlyRecs[m].HighHumidity.Val.ToString(cumulus.HumFormat)}\",");
				json.Append($"\"{m}-lowHumidityVal\":\"{station.MonthlyRecs[m].LowHumidity.Val.ToString(cumulus.HumFormat)}\",");
				// Records - Humidity times
				json.Append($"\"{m}-highHumidityTime\":\"{station.MonthlyRecs[m].HighHumidity.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowHumidityTime\":\"{station.MonthlyRecs[m].LowHumidity.Ts.ToString(timeStampFormat)}\",");
				// Records - Pressure values
				json.Append($"\"{m}-highBarometerVal\":\"{station.MonthlyRecs[m].HighPress.Val.ToString(cumulus.PressFormat)}\",");
				json.Append($"\"{m}-lowBarometerVal\":\"{station.MonthlyRecs[m].LowPress.Val.ToString(cumulus.PressFormat)}\",");
				// Records - Pressure times
				json.Append($"\"{m}-highBarometerTime\":\"{station.MonthlyRecs[m].HighPress.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowBarometerTime\":\"{station.MonthlyRecs[m].LowPress.Ts.ToString(timeStampFormat)}\",");
				// Records - Wind values
				json.Append($"\"{m}-highGustVal\":\"{station.MonthlyRecs[m].HighGust.Val.ToString(cumulus.WindFormat)}\",");
				json.Append($"\"{m}-highWindVal\":\"{station.MonthlyRecs[m].HighWind.Val.ToString(cumulus.WindAvgFormat)}\",");
				json.Append($"\"{m}-highWindRunVal\":\"{station.MonthlyRecs[m].HighWindRun.Val.ToString(cumulus.WindRunFormat)}\",");
				// Records - Wind times
				json.Append($"\"{m}-highGustTime\":\"{station.MonthlyRecs[m].HighGust.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindTime\":\"{station.MonthlyRecs[m].HighWind.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highWindRunTime\":\"{station.MonthlyRecs[m].HighWindRun.Ts.ToString(dateStampFormat)}\",");
				// Records - Rain values
				json.Append($"\"{m}-highRainRateVal\":\"{station.MonthlyRecs[m].HighRainRate.Val.ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highHourlyRainVal\":\"{station.MonthlyRecs[m].HourlyRain.Val.ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highDailyRainVal\":\"{station.MonthlyRecs[m].DailyRain.Val.ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainVal\":\"{station.MonthlyRecs[m].MonthlyRain.Val.ToString(cumulus.RainFormat)}\",");
				json.Append($"\"{m}-longestDryPeriodVal\":\"{station.MonthlyRecs[m].LongestDryPeriod.Val:f0}\",");
				json.Append($"\"{m}-longestWetPeriodVal\":\"{station.MonthlyRecs[m].LongestWetPeriod.Val:f0}\",");
				// Records - Rain times
				json.Append($"\"{m}-highRainRateTime\":\"{station.MonthlyRecs[m].HighRainRate.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highHourlyRainTime\":\"{station.MonthlyRecs[m].HourlyRain.Ts.ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-highDailyRainTime\":\"{station.MonthlyRecs[m].DailyRain.Ts.ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-highMonthlyRainTime\":\"{station.MonthlyRecs[m].MonthlyRain.Ts:yyyy/MM}\",");
				json.Append($"\"{m}-longestDryPeriodTime\":\"{station.MonthlyRecs[m].LongestDryPeriod.Ts.ToString(dateStampFormat)}\",");
				json.Append($"\"{m}-longestWetPeriodTime\":\"{station.MonthlyRecs[m].LongestWetPeriod.Ts.ToString(dateStampFormat)}\",");
			}
			json.Remove(json.Length - 1, 1);
			json.Append("}");

			return json.ToString();
		}

		internal string GetMonthlyRecDayFile()
		{
			const string timeStampFormat = "dd/MM/yy HH:mm";
			const string dateStampFormat = "dd/MM/yy";

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
			if (station.DayFile.Count() > 0)
			{
				for (var i = 0; i < station.DayFile.Count(); i++)
				{
					var loggedDate = station.DayFile[i].Date;
					var monthOffset = loggedDate.Month - 1;

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
					if (station.DayFile[i].HighGust > highGustVal[monthOffset])
					{
						highGustVal[monthOffset] = station.DayFile[i].HighGust;
						highGustTime[monthOffset] = station.DayFile[i].HighGustTime;
					}
					// lo temp
					if (station.DayFile[i].LowTemp < lowTempVal[monthOffset])
					{
						lowTempVal[monthOffset] = station.DayFile[i].LowTemp;
						lowTempTime[monthOffset] = station.DayFile[i].LowTempTime;
					}
					// hi min temp
					if (station.DayFile[i].LowTemp > highMinTempVal[monthOffset])
					{
						highMinTempVal[monthOffset] = station.DayFile[i].LowTemp;
						highMinTempTime[monthOffset] = station.DayFile[i].LowTempTime;
					}
					// hi temp
					if (station.DayFile[i].HighTemp > highTempVal[monthOffset])
					{
						highTempVal[monthOffset] = station.DayFile[i].HighTemp;
						highTempTime[monthOffset] = station.DayFile[i].HighTempTime;
					}
					// lo max temp
					if (station.DayFile[i].HighTemp < lowMaxTempVal[monthOffset])
					{
						lowMaxTempVal[monthOffset] = station.DayFile[i].HighTemp;
						lowMaxTempTime[monthOffset] = station.DayFile[i].HighTempTime;
					}

					// temp ranges
					// hi temp range
					if ((station.DayFile[i].HighTemp - station.DayFile[i].LowTemp) > highTempRangeVal[monthOffset])
					{
						highTempRangeVal[monthOffset] = station.DayFile[i].HighTemp - station.DayFile[i].LowTemp;
						highTempRangeTime[monthOffset] = loggedDate;
					}
					// lo temp range
					if ((station.DayFile[i].HighTemp - station.DayFile[i].LowTemp) < lowTempRangeVal[monthOffset])
					{
						lowTempRangeVal[monthOffset] = station.DayFile[i].HighTemp - station.DayFile[i].LowTemp;
						lowTempRangeTime[monthOffset] = loggedDate;
					}

					// lo baro
					if (station.DayFile[i].LowPress < lowBaroVal[monthOffset])
					{
						lowBaroVal[monthOffset] = station.DayFile[i].LowPress;
						lowBaroTime[monthOffset] = station.DayFile[i].LowPressTime;
					}
					// hi baro
					if (station.DayFile[i].HighPress > highBaroVal[monthOffset])
					{
						highBaroVal[monthOffset] = station.DayFile[i].HighPress;
						highBaroTime[monthOffset] = station.DayFile[i].HighPressTime;
					}
					// hi rain rate
					if (station.DayFile[i].HighRainRate > highRainRateVal[monthOffset])
					{
						highRainRateVal[monthOffset] = station.DayFile[i].HighRainRate;
						highRainRateTime[monthOffset] = station.DayFile[i].HighRainRateTime;
					}
					// hi rain day
					if (station.DayFile[i].TotalRain > highRainDayVal[monthOffset])
					{
						highRainDayVal[monthOffset] = station.DayFile[i].TotalRain;
						highRainDayTime[monthOffset] = loggedDate;
					}

					// monthly rain
					rainThisMonth += station.DayFile[i].TotalRain;

					// dry/wet period
					if (Convert.ToInt32(station.DayFile[i].TotalRain * 100) >= rainThreshold)
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

					// hi wind run
					if (station.DayFile[i].WindRun > highWindRunVal[monthOffset])
					{
						highWindRunVal[monthOffset] = station.DayFile[i].WindRun;
						highWindRunTime[monthOffset] = loggedDate;
					}
					// hi wind
					if (station.DayFile[i].HighAvgWind > highWindVal[monthOffset])
					{
						highWindVal[monthOffset] = station.DayFile[i].HighAvgWind;
						highWindTime[monthOffset] = station.DayFile[i].HighAvgWindTime;
					}

					// lo humidity
					if (station.DayFile[i].LowHumidity < lowHumVal[monthOffset])
					{
						lowHumVal[monthOffset] = station.DayFile[i].LowHumidity;
						lowHumTime[monthOffset] = station.DayFile[i].LowHumidityTime;
					}
					// hi humidity
					if (station.DayFile[i].HighHumidity > highHumVal[monthOffset])
					{
						highHumVal[monthOffset] = station.DayFile[i].HighHumidity;
						highHumTime[monthOffset] = station.DayFile[i].HighHumidityTime;
					}

					// hi heat index
					if (station.DayFile[i].HighHeatIndex > highHeatIndVal[monthOffset])
					{
						highHeatIndVal[monthOffset] = station.DayFile[i].HighHeatIndex;
						highHeatIndTime[monthOffset] = station.DayFile[i].HighHeatIndexTime;
					}
					// hi app temp
					if (station.DayFile[i].HighAppTemp > highAppTempVal[monthOffset])
					{
						highAppTempVal[monthOffset] = station.DayFile[i].HighAppTemp;
						highAppTempTime[monthOffset] = station.DayFile[i].HighAppTempTime;
					}
					// lo app temp
					if (station.DayFile[i].LowAppTemp < lowAppTempVal[monthOffset])
					{
						lowAppTempVal[monthOffset] = station.DayFile[i].LowAppTemp;
						lowAppTempTime[monthOffset] = station.DayFile[i].LowAppTempTime;
					}

					// hi rain hour
					if (station.DayFile[i].HighHourlyRain > highRainHourVal[monthOffset])
					{
						highRainHourVal[monthOffset] = station.DayFile[i].HighHourlyRain;
						highRainHourTime[monthOffset] = station.DayFile[i].HighHourlyRainTime;
					}

					// lo wind chill
					if (station.DayFile[i].LowWindChill < lowWindChillVal[monthOffset])
					{
						lowWindChillVal[monthOffset] = station.DayFile[i].LowWindChill;
						lowWindChillTime[monthOffset] = station.DayFile[i].LowWindChillTime;
					}

					// hi dewpt
					if (station.DayFile[i].HighDewPoint > highDewPtVal[monthOffset])
					{
						highDewPtVal[monthOffset] = station.DayFile[i].HighDewPoint;
						highDewPtTime[monthOffset] = station.DayFile[i].HighDewPointTime;
					}
					// lo dewpt
					if (station.DayFile[i].LowDewPoint < lowDewPtVal[monthOffset])
					{
						lowDewPtVal[monthOffset] = station.DayFile[i].LowDewPoint;
						lowDewPtTime[monthOffset] = station.DayFile[i].LowDewPointTime;
					}

					// hi feels like
					if (station.DayFile[i].HighFeelsLike > highFeelsLikeVal[monthOffset])
					{
						highFeelsLikeVal[monthOffset] = station.DayFile[i].HighFeelsLike;
						highFeelsLikeTime[monthOffset] = station.DayFile[i].HighFeelsLikeTime;
					}
					// lo feels like
					if (station.DayFile[i].LowFeelsLike < lowFeelsLikeVal[monthOffset])
					{
						lowFeelsLikeVal[monthOffset] = station.DayFile[i].LowFeelsLike;
						lowFeelsLikeTime[monthOffset] = station.DayFile[i].LowFeelsLikeTime;
					}

					// hi humidex
					if (station.DayFile[i].HighHumidex > highHumidexVal[monthOffset])
					{
						highHumidexVal[monthOffset] = station.DayFile[i].HighHumidex;
						highHumidexTime[monthOffset] = station.DayFile[i].HighHumidexTime;
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
					json.Append($"\"{m}-highMinTempTimeDayfile\":\"{highMinTempTime[i].ToString(timeStampFormat)}\",");
					json.Append($"\"{m}-lowMaxTempValDayfile\":\"{lowMaxTempVal[i].ToString(cumulus.TempFormat)}\",");
					json.Append($"\"{m}-lowMaxTempTimeDayfile\":\"{lowMaxTempTime[i].ToString(timeStampFormat)}\",");
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
			else
			{
				cumulus.LogMessage("Error failed to find day records");
			}

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

			var currentDay = datefrom;
			double dayHighTemp = -999;
			var dayHighTempTime = thisDate;
			double dayLowTemp = 999;
			var dayLowTempTime = thisDate;
			double dayWindRun = 0;
			double dayRain = 0;

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
								{
									dayHighTemp = outsidetemp;
									dayHighTempTime = entrydate;
								}

								if (outsidetemp < dayLowTemp)
								{
									dayLowTemp = outsidetemp;
									dayLowTempTime = entrydate;
								}

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
									lowMaxTempTime[lastEntryMonthOffset] = dayHighTempTime;
								}
								if (dayLowTemp > highMinTempVal[lastEntryMonthOffset])
								{
									highMinTempVal[lastEntryMonthOffset] = dayLowTemp;
									highMinTempTime[lastEntryMonthOffset] = dayLowTempTime;
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
								if (Convert.ToInt32(dayRain * 1000) >= rainThreshold)
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
				json.Append($"\"{m}-highMinTempTimeLogfile\":\"{highMinTempTime[i].ToString(timeStampFormat)}\",");
				json.Append($"\"{m}-lowMaxTempValLogfile\":\"{lowMaxTempVal[i].ToString(cumulus.TempFormat)}\",");
				json.Append($"\"{m}-lowMaxTempTimeLogfile\":\"{lowMaxTempTime[i].ToString(timeStampFormat)}\",");
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
			json.Append($"\"highTempVal\":\"{station.ThisMonth.HighTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTime\":\"{station.ThisMonth.HighTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowTempVal\":\"{station.ThisMonth.LowTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.ThisMonth.LowTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.ThisMonth.HighDewPoint.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.ThisMonth.HighDewPoint.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.ThisMonth.LowDewPoint.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.ThisMonth.LowDewPoint.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.ThisMonth.HighAppTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.ThisMonth.HighAppTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.ThisMonth.LowAppTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.ThisMonth.LowAppTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.ThisMonth.HighFeelsLike.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.ThisMonth.HighFeelsLike.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.ThisMonth.LowFeelsLike.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.ThisMonth.LowFeelsLike.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.ThisMonth.HighHumidex.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.ThisMonth.HighHumidex.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.ThisMonth.LowChill.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.ThisMonth.LowChill.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.ThisMonth.HighHeatIndex.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.ThisMonth.HighHeatIndex.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.ThisMonth.HighMinTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.ThisMonth.HighMinTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.ThisMonth.LowMaxTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.ThisMonth.LowMaxTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.ThisMonth.HighDailyTempRange.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.ThisMonth.HighDailyTempRange.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.ThisMonth.LowDailyTempRange.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.ThisMonth.LowDailyTempRange.Ts.ToString(dateStampFormat)}\",");
			// Records - Humidty
			json.Append($"\"highHumidityVal\":\"{station.ThisMonth.HighHumidity.Val.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTime\":\"{station.ThisMonth.HighHumidity.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.ThisMonth.LowHumidity.Val.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.ThisMonth.LowHumidity.Ts.ToString(timeStampFormat)}\",");
			// Records - Pressure
			json.Append($"\"highBarometerVal\":\"{station.ThisMonth.HighPress.Val.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTime\":\"{station.ThisMonth.HighPress.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.ThisMonth.LowPress.Val.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.ThisMonth.LowPress.Ts.ToString(timeStampFormat)}\",");
			// Records - Wind
			json.Append($"\"highGustVal\":\"{station.ThisMonth.HighGust.Val.ToString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTime\":\"{station.ThisMonth.HighGust.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.ThisMonth.HighWind.Val.ToString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.ThisMonth.HighWind.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.ThisMonth.HighWindRun.Val.ToString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.ThisMonth.HighWindRun.Ts.ToString(dateStampFormat)}\",");
			// Records - Rain
			json.Append($"\"highRainRateVal\":\"{station.ThisMonth.HighRainRate.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTime\":\"{station.ThisMonth.HighRainRate.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.ThisMonth.HourlyRain.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.ThisMonth.HourlyRain.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.ThisMonth.DailyRain.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.ThisMonth.DailyRain.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.ThisMonth.LongestDryPeriod.Val:F0}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.ThisMonth.LongestDryPeriod.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.ThisMonth.LongestWetPeriod.Val:F0}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.ThisMonth.LongestWetPeriod.Ts.ToString(dateStampFormat)}\"");

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
						station.ThisMonth.HighTemp.Val = double.Parse(value);
						break;
					case "highTempTime":
						dt = value.Split('+');
						station.ThisMonth.HighTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowTempVal":
						station.ThisMonth.LowTemp.Val = double.Parse(value);
						break;
					case "lowTempTime":
						dt = value.Split('+');
						station.ThisMonth.LowTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDewPointVal":
						station.ThisMonth.HighDewPoint.Val = double.Parse(value);
						break;
					case "highDewPointTime":
						dt = value.Split('+');
						station.ThisMonth.HighDewPoint.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowDewPointVal":
						station.ThisMonth.LowDewPoint.Val = double.Parse(value);
						break;
					case "lowDewPointTime":
						dt = value.Split('+');
						station.ThisMonth.LowDewPoint.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highApparentTempVal":
						station.ThisMonth.HighAppTemp.Val = double.Parse(value);
						break;
					case "highApparentTempTime":
						dt = value.Split('+');
						station.ThisMonth.HighAppTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowApparentTempVal":
						station.ThisMonth.LowAppTemp.Val = double.Parse(value);
						break;
					case "lowApparentTempTime":
						dt = value.Split('+');
						station.ThisMonth.LowAppTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highFeelsLikeVal":
						station.ThisMonth.HighFeelsLike.Val = double.Parse(value);
						break;
					case "highFeelsLikeTime":
						dt = value.Split('+');
						station.ThisMonth.HighFeelsLike.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowFeelsLikeVal":
						station.ThisMonth.LowFeelsLike.Val = double.Parse(value);
						break;
					case "lowFeelsLikeTime":
						dt = value.Split('+');
						station.ThisMonth.LowFeelsLike.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHumidexVal":
						station.ThisMonth.HighHumidex.Val = double.Parse(value);
						break;
					case "highHumidexTime":
						dt = value.Split('+');
						station.ThisMonth.HighHumidex.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowWindChillVal":
						station.ThisMonth.LowChill.Val = double.Parse(value);
						break;
					case "lowWindChillTime":
						dt = value.Split('+');
						station.ThisMonth.LowChill.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHeatIndexVal":
						station.ThisMonth.HighHeatIndex.Val = double.Parse(value);
						break;
					case "highHeatIndexTime":
						dt = value.Split('+');
						station.ThisMonth.HighHeatIndex.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highMinTempVal":
						station.ThisMonth.HighMinTemp.Val = double.Parse(value);
						break;
					case "highMinTempTime":
						dt = value.Split('+');
						station.ThisMonth.HighMinTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowMaxTempVal":
						station.ThisMonth.LowMaxTemp.Val = double.Parse(value);
						break;
					case "lowMaxTempTime":
						dt = value.Split('+');
						station.ThisMonth.LowMaxTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDailyTempRangeVal":
						station.ThisMonth.HighDailyTempRange.Val = double.Parse(value);
						break;
					case "highDailyTempRangeTime":
						station.ThisMonth.HighDailyTempRange.Ts = station.ddmmyyStrToDate(value);
						break;
					case "lowDailyTempRangeVal":
						station.ThisMonth.LowDailyTempRange.Val = double.Parse(value);
						break;
					case "lowDailyTempRangeTime":
						station.ThisMonth.LowDailyTempRange.Ts = station.ddmmyyStrToDate(value);
						break;
					case "highHumidityVal":
						station.ThisMonth.HighHumidity.Val = int.Parse(value);
						break;
					case "highHumidityTime":
						dt = value.Split('+');
						station.ThisMonth.HighHumidity.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowHumidityVal":
						station.ThisMonth.LowHumidity.Val = int.Parse(value);
						break;
					case "lowHumidityTime":
						dt = value.Split('+');
						station.ThisMonth.LowHumidity.Ts =  station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highBarometerVal":
						station.ThisMonth.HighPress.Val = double.Parse(value);
						break;
					case "highBarometerTime":
						dt = value.Split('+');
						station.ThisMonth.HighPress.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowBarometerVal":
						station.ThisMonth.LowPress.Val = double.Parse(value);
						break;
					case "lowBarometerTime":
						dt = value.Split('+');
						station.ThisMonth.LowPress.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highGustVal":
						station.ThisMonth.HighGust.Val = double.Parse(value);
						break;
					case "highGustTime":
						dt = value.Split('+');
						station.ThisMonth.HighGust.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highWindVal":
						station.ThisMonth.HighWind.Val = double.Parse(value);
						break;
					case "highWindTime":
						dt = value.Split('+');
						station.ThisMonth.HighWind.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highWindRunVal":
						station.ThisMonth.HighWindRun.Val = double.Parse(value);
						break;
					case "highWindRunTime":
						station.ThisMonth.HighWindRun.Ts = station.ddmmyyStrToDate(value);
						break;
					case "highRainRateVal":
						station.ThisMonth.HighRainRate.Val = double.Parse(value);
						break;
					case "highRainRateTime":
						dt = value.Split('+');
						station.ThisMonth.HighRainRate.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHourlyRainVal":
						station.ThisMonth.HourlyRain.Val = double.Parse(value);
						break;
					case "highHourlyRainTime":
						dt = value.Split('+');
						station.ThisMonth.HourlyRain.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDailyRainVal":
						station.ThisMonth.DailyRain.Val = double.Parse(value);
						break;
					case "highDailyRainTime":
						station.ThisMonth.DailyRain.Ts = station.ddmmyyStrToDate(value);
						break;
					case "longestDryPeriodVal":
						station.ThisMonth.LongestDryPeriod.Val = int.Parse(value);
						break;
					case "longestDryPeriodTime":
						station.ThisMonth.LongestDryPeriod.Ts = station.ddmmyyStrToDate(value);
						break;
					case "longestWetPeriodVal":
						station.ThisMonth.LongestWetPeriod.Val = int.Parse(value);
						break;
					case "longestWetPeriodTime":
						station.ThisMonth.LongestWetPeriod.Ts = station.ddmmyyStrToDate(value);
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
			json.Append($"\"highTempVal\":\"{station.ThisYear.HighTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highTempTime\":\"{station.ThisYear.HighTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowTempVal\":\"{station.ThisYear.LowTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowTempTime\":\"{station.ThisYear.LowTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDewPointVal\":\"{station.ThisYear.HighDewPoint.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDewPointTime\":\"{station.ThisYear.HighDewPoint.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowDewPointVal\":\"{station.ThisYear.LowDewPoint.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDewPointTime\":\"{station.ThisYear.LowDewPoint.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highApparentTempVal\":\"{station.ThisYear.HighAppTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highApparentTempTime\":\"{station.ThisYear.HighAppTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowApparentTempVal\":\"{station.ThisYear.LowAppTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowApparentTempTime\":\"{station.ThisYear.LowAppTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highFeelsLikeVal\":\"{station.ThisYear.HighFeelsLike.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highFeelsLikeTime\":\"{station.ThisYear.HighFeelsLike.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowFeelsLikeVal\":\"{station.ThisYear.LowFeelsLike.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowFeelsLikeTime\":\"{station.ThisYear.LowFeelsLike.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHumidexVal\":\"{station.ThisYear.HighHumidex.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHumidexTime\":\"{station.ThisYear.HighHumidex.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowWindChillVal\":\"{station.ThisYear.LowChill.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowWindChillTime\":\"{station.ThisYear.LowChill.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHeatIndexVal\":\"{station.ThisYear.HighHeatIndex.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highHeatIndexTime\":\"{station.ThisYear.HighHeatIndex.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highMinTempVal\":\"{station.ThisYear.HighMinTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highMinTempTime\":\"{station.ThisYear.HighMinTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowMaxTempVal\":\"{station.ThisYear.LowMaxTemp.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowMaxTempTime\":\"{station.ThisYear.LowMaxTemp.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyTempRangeVal\":\"{station.ThisYear.HighDailyTempRange.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"highDailyTempRangeTime\":\"{station.ThisYear.HighDailyTempRange.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"lowDailyTempRangeVal\":\"{station.ThisYear.LowDailyTempRange.Val.ToString(cumulus.TempFormat)}\",");
			json.Append($"\"lowDailyTempRangeTime\":\"{station.ThisYear.LowDailyTempRange.Ts.ToString(dateStampFormat)}\",");
			// Records - Humidty
			json.Append($"\"highHumidityVal\":\"{station.ThisYear.HighHumidity.Val.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"highHumidityTime\":\"{station.ThisYear.HighHumidity.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowHumidityVal\":\"{station.ThisYear.LowHumidity.Val.ToString(cumulus.HumFormat)}\",");
			json.Append($"\"lowHumidityTime\":\"{station.ThisYear.LowHumidity.Ts.ToString(timeStampFormat)}\",");
			// Records - Pressure
			json.Append($"\"highBarometerVal\":\"{station.ThisYear.HighPress.Val.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"highBarometerTime\":\"{station.ThisYear.HighPress.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"lowBarometerVal\":\"{station.ThisYear.LowPress.Val.ToString(cumulus.PressFormat)}\",");
			json.Append($"\"lowBarometerTime\":\"{station.ThisYear.LowPress.Ts.ToString(timeStampFormat)}\",");
			// Records - Wind
			json.Append($"\"highGustVal\":\"{station.ThisYear.HighGust.Val.ToString(cumulus.WindFormat)}\",");
			json.Append($"\"highGustTime\":\"{station.ThisYear.HighGust.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindVal\":\"{station.ThisYear.HighWind.Val.ToString(cumulus.WindAvgFormat)}\",");
			json.Append($"\"highWindTime\":\"{station.ThisYear.HighWind.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highWindRunVal\":\"{station.ThisYear.HighWindRun.Val.ToString(cumulus.WindRunFormat)}\",");
			json.Append($"\"highWindRunTime\":\"{station.ThisYear.HighWindRun.Ts.ToString(dateStampFormat)}\",");
			// Records - Rain
			json.Append($"\"highRainRateVal\":\"{station.ThisYear.HighRainRate.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highRainRateTime\":\"{station.ThisYear.HighRainRate.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highHourlyRainVal\":\"{station.ThisYear.HourlyRain.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highHourlyRainTime\":\"{station.ThisYear.HourlyRain.Ts.ToString(timeStampFormat)}\",");
			json.Append($"\"highDailyRainVal\":\"{station.ThisYear.DailyRain.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highDailyRainTime\":\"{station.ThisYear.DailyRain.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"highMonthlyRainVal\":\"{station.ThisYear.MonthlyRain.Val.ToString(cumulus.RainFormat)}\",");
			json.Append($"\"highMonthlyRainTime\":\"{station.ThisYear.MonthlyRain.Ts:yyyy/MM}\",");
			json.Append($"\"longestDryPeriodVal\":\"{station.ThisYear.LongestDryPeriod.Val:F0}\",");
			json.Append($"\"longestDryPeriodTime\":\"{station.ThisYear.LongestDryPeriod.Ts.ToString(dateStampFormat)}\",");
			json.Append($"\"longestWetPeriodVal\":\"{station.ThisYear.LongestWetPeriod.Val:F0}\",");
			json.Append($"\"longestWetPeriodTime\":\"{station.ThisYear.LongestWetPeriod.Ts.ToString(dateStampFormat)}\"");

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
						station.ThisYear.HighTemp.Val = double.Parse(value);
						break;
					case "highTempTime":
						dt = value.Split('+');
						station.ThisYear.HighTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowTempVal":
						station.ThisYear.LowTemp.Val = double.Parse(value);
						break;
					case "lowTempTime":
						dt = value.Split('+');
						station.ThisYear.LowTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDewPointVal":
						station.ThisYear.HighDewPoint.Val = double.Parse(value);
						break;
					case "highDewPointTime":
						dt = value.Split('+');
						station.ThisYear.HighDewPoint.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowDewPointVal":
						station.ThisYear.LowDewPoint.Val = double.Parse(value);
						break;
					case "lowDewPointTime":
						dt = value.Split('+');
						station.ThisYear.LowDewPoint.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highApparentTempVal":
						station.ThisYear.HighAppTemp.Val = double.Parse(value);
						break;
					case "highApparentTempTime":
						dt = value.Split('+');
						station.ThisYear.HighAppTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowApparentTempVal":
						station.ThisYear.LowAppTemp.Val = double.Parse(value);
						break;
					case "lowApparentTempTime":
						dt = value.Split('+');
						station.ThisYear.LowAppTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highFeelsLikeVal":
						station.ThisYear.HighFeelsLike.Val = double.Parse(value);
						break;
					case "highFeelsLikeTime":
						dt = value.Split('+');
						station.ThisYear.HighFeelsLike.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowFeelsLikeVal":
						station.ThisYear.LowFeelsLike.Val = double.Parse(value);
						break;
					case "lowFeelsLikeTime":
						dt = value.Split('+');
						station.ThisYear.LowFeelsLike.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHumidexVal":
						station.ThisYear.HighHumidex.Val = double.Parse(value);
						break;
					case "highHumidexTime":
						dt = value.Split('+');
						station.ThisYear.HighHumidex.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowWindChillVal":
						station.ThisYear.LowChill.Val = double.Parse(value);
						break;
					case "lowWindChillTime":
						dt = value.Split('+');
						station.ThisYear.LowChill.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHeatIndexVal":
						station.ThisYear.HighHeatIndex.Val = double.Parse(value);
						break;
					case "highHeatIndexTime":
						dt = value.Split('+');
						station.ThisYear.HighHeatIndex.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highMinTempVal":
						station.ThisYear.HighMinTemp.Val = double.Parse(value);
						break;
					case "highMinTempTime":
						dt = value.Split('+');
						station.ThisYear.HighMinTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowMaxTempVal":
						station.ThisYear.LowMaxTemp.Val = double.Parse(value);
						break;
					case "lowMaxTempTime":
						dt = value.Split('+');
						station.ThisYear.LowMaxTemp.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDailyTempRangeVal":
						station.ThisYear.HighDailyTempRange.Val = double.Parse(value);
						break;
					case "highDailyTempRangeTime":
						station.ThisYear.HighDailyTempRange.Ts = station.ddmmyyStrToDate(value);
						break;
					case "lowDailyTempRangeVal":
						station.ThisYear.LowDailyTempRange.Val = double.Parse(value);
						break;
					case "lowDailyTempRangeTime":
						station.ThisYear.LowDailyTempRange.Ts = station.ddmmyyStrToDate(value);
						break;
					case "highHumidityVal":
						station.ThisYear.HighHumidity.Val = int.Parse(value);
						break;
					case "highHumidityTime":
						dt = value.Split('+');
						station.ThisYear.HighHumidity.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowHumidityVal":
						station.ThisYear.LowHumidity.Val = int.Parse(value);
						break;
					case "lowHumidityTime":
						dt = value.Split('+');
						station.ThisYear.LowHumidity.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highBarometerVal":
						station.ThisYear.HighPress.Val = double.Parse(value);
						break;
					case "highBarometerTime":
						dt = value.Split('+');
						station.ThisYear.HighPress.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "lowBarometerVal":
						station.ThisYear.LowPress.Val = double.Parse(value);
						break;
					case "lowBarometerTime":
						dt = value.Split('+');
						station.ThisYear.LowPress.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highGustVal":
						station.ThisYear.HighGust.Val = double.Parse(value);
						break;
					case "highGustTime":
						dt = value.Split('+');
						station.ThisYear.HighGust.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highWindVal":
						station.ThisYear.HighWind.Val = double.Parse(value);
						break;
					case "highWindTime":
						dt = value.Split('+');
						station.ThisYear.HighWind.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highWindRunVal":
						station.ThisYear.HighWindRun.Val = double.Parse(value);
						break;
					case "highWindRunTime":
						station.ThisYear.HighWindRun.Ts = station.ddmmyyStrToDate(value);
						break;
					case "highRainRateVal":
						station.ThisYear.HighRainRate.Val = double.Parse(value);
						break;
					case "highRainRateTime":
						dt = value.Split('+');
						station.ThisYear.HighRainRate.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highHourlyRainVal":
						station.ThisYear.HourlyRain.Val = double.Parse(value);
						break;
					case "highHourlyRainTime":
						dt = value.Split('+');
						station.ThisYear.HourlyRain.Ts = station.ddmmyyhhmmStrToDate(dt[0], dt[1]);
						break;
					case "highDailyRainVal":
						station.ThisYear.DailyRain.Val = double.Parse(value);
						break;
					case "highDailyRainTime":
						station.ThisYear.DailyRain.Ts = station.ddmmyyStrToDate(value);
						break;
					case "highMonthlyRainVal":
						station.ThisYear.MonthlyRain.Val = double.Parse(value);
						break;
					case "highMonthlyRainTime":
						var dat = value.Split('/');  // yyyy/MM
						station.ThisYear.MonthlyRain.Ts = new DateTime(int.Parse(dat[0]), int.Parse(dat[1]), 1);
						break;
					case "longestDryPeriodVal":
						station.ThisYear.LongestDryPeriod.Val = int.Parse(value);
						break;
					case "longestDryPeriodTime":
						station.ThisYear.LongestDryPeriod.Ts = station.ddmmyyStrToDate(value);
						break;
					case "longestWetPeriodVal":
						station.ThisYear.LongestWetPeriod.Val = int.Parse(value);
						break;
					case "longestWetPeriodTime":
						station.ThisYear.LongestWetPeriod.Ts = station.ddmmyyStrToDate(value);
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
			var lines = File.ReadAllLines(cumulus.DayFileName).ToList();

			var lineNum = newData.line - 1; // our List is zero relative

			if (newData.action == "Edit")
			{
				// replace the edited line
				var newLine = string.Join(cumulus.ListSeparator, newData.data);

				lines[lineNum] = newLine;

				// Update the in memory record
				try
				{
					station.DayFile[lineNum] = station.ParseDayFileRec(newLine);

					// write dayfile back again
					File.WriteAllLines(cumulus.DayFileName, lines);
				}
				catch
				{
					return "{\"result\":\"Failed, new data does not match required values\"}";
				}

				// Update the MySQL record
				if (!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Server) &&
					!string.IsNullOrEmpty(cumulus.MySqlConnSettings.UserID) &&
					!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Password) &&
					!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Database) &&
					cumulus.MySqlUpdateOnEdit
					)
				{
					try
					{
						var InvC = new CultureInfo("");
						var updt = new StringBuilder(1024);

						updt.Append($"UPDATE {cumulus.MySqlDayfileTable} SET ");
						updt.Append($"HighWindGust={station.DayFile[lineNum].HighGust.ToString(cumulus.WindFormat, InvC)},");
						updt.Append($"HWindGBear={station.DayFile[lineNum].HighGustBearing},");
						updt.Append($"THWindG={station.DayFile[lineNum].HighGustTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"MinTemp={station.DayFile[lineNum].LowTemp.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TMinTemp={station.DayFile[lineNum].LowTempTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"MaxTemp={station.DayFile[lineNum].HighTemp.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TMaxTemp={station.DayFile[lineNum].HighTempTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"MinPress={station.DayFile[lineNum].LowPress.ToString(cumulus.PressFormat, InvC)},");
						updt.Append($"TMinPress={station.DayFile[lineNum].LowPressTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"MaxPress={station.DayFile[lineNum].HighPress.ToString(cumulus.PressFormat, InvC)},");
						updt.Append($"TMaxPress={station.DayFile[lineNum].HighPressTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"MaxRainRate={station.DayFile[lineNum].HighRainRate.ToString(cumulus.RainFormat, InvC)},");
						updt.Append($"TMaxRR={station.DayFile[lineNum].HighRainRateTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"TotRainFall={station.DayFile[lineNum].TotalRain.ToString(cumulus.RainFormat, InvC)},");
						updt.Append($"AvgTemp={station.DayFile[lineNum].AvgTemp.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TotWindRun={station.DayFile[lineNum].WindRun.ToString("F1", InvC)},");
						updt.Append($"HighAvgWSpeed{station.DayFile[lineNum].HighAvgWind.ToString(cumulus.WindAvgFormat, InvC)},");
						updt.Append($"THAvgWSpeed={station.DayFile[lineNum].HighAvgWindTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"LowHum={station.DayFile[lineNum].LowHumidity},");
						updt.Append($"TLowHum={station.DayFile[lineNum].LowHumidityTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"HighHum={station.DayFile[lineNum].HighHumidity},");
						updt.Append($"THighHum{station.DayFile[lineNum].HighHumidityTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"TotalEvap{station.DayFile[lineNum].ET.ToString(cumulus.ETFormat, InvC)},");
						updt.Append($"HoursSun={station.DayFile[lineNum].SunShineHours.ToString(cumulus.SunFormat, InvC)},");
						updt.Append($"HighHeatInd={station.DayFile[lineNum].HighHeatIndex.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"THighHeatInd={station.DayFile[lineNum].HighHeatIndexTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"HighAppTemp={station.DayFile[lineNum].HighAppTemp.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"THighAppTemp{station.DayFile[lineNum].HighAppTempTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"LowAppTemp={station.DayFile[lineNum].LowAppTemp.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TLowAppTemp={station.DayFile[lineNum].LowAppTempTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"HighHourRain={station.DayFile[lineNum].HighHourlyRain.ToString(cumulus.RainFormat, InvC)},");
						updt.Append($"THighHourRain={station.DayFile[lineNum].HighHourlyRainTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"LowWindChill={station.DayFile[lineNum].LowWindChill.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TLowWindChill={station.DayFile[lineNum].LowWindChillTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"HighDewPoint={station.DayFile[lineNum].HighDewPoint.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"THighDewPoint={station.DayFile[lineNum].HighDewPointTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"LowDewPoint={station.DayFile[lineNum].LowDewPoint.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TLowDewPoint={station.DayFile[lineNum].LowDewPointTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"DomWindDir={station.DayFile[lineNum].DominantWindBearing},");
						updt.Append($"HeatDegDays={station.DayFile[lineNum].HeatingDegreeDays.ToString("F1", InvC)},");
						updt.Append($"CoolDegDays={station.DayFile[lineNum].CoolingDegreeDays.ToString("F1", InvC)},");
						updt.Append($"HighSolarRad={station.DayFile[lineNum].HighSolar},");
						updt.Append($"THighSolarRad={station.DayFile[lineNum].HighSolarTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"HighUV={station.DayFile[lineNum].HighUv.ToString(cumulus.UVFormat, InvC)},");
						updt.Append($"THighUV={station.DayFile[lineNum].HighUvTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"HWindGBearSym='{station.CompassPoint(station.DayFile[lineNum].HighGustBearing)}',");
						updt.Append($"HWindGBearSym={station.CompassPoint(station.DayFile[lineNum].DominantWindBearing)}',");
						updt.Append($"MaxFeelsLike={station.DayFile[lineNum].HighFeelsLike.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TMaxFeelsLike={station.DayFile[lineNum].HighFeelsLikeTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"MinFeelsLike={station.DayFile[lineNum].LowFeelsLike.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TMinFeelsLike={station.DayFile[lineNum].LowFeelsLikeTime.ToString("\\'HH:mm\\'")},");
						updt.Append($"MaxHumidex={station.DayFile[lineNum].HighHumidex.ToString(cumulus.TempFormat, InvC)},");
						updt.Append($"TMaxHumidex={station.DayFile[lineNum].HighFeelsLikeTime.ToString("\\'HH:mm\\'")} ");

						updt.Append($"WHERE LogDate={station.DayFile[lineNum].Date.ToString("yy-MM-dd")};");

						cumulus.MySqlCommandSync(updt.ToString(), "EditDayFile");
					}
					catch
					{
						return "{\"result\":\"Updated the dayfile OK, but failed, to update MySQL\"}";
					}
				}

			}
			else if (newData.action == "Delete")
			{
				// Just double check we are deleting the correct line - see if the dates match
				var lineData = lines[lineNum].Split(cumulus.ListSeparator.ToCharArray()[0]);
				if (lineData[0] == newData.data[0])
				{
					lines.RemoveAt(lineNum);
					// Update the in memory record
					station.DayFile.RemoveAt(lineNum);

					// write dayfile back again
					File.WriteAllLines(cumulus.DayFileName, lines);
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

			// return the updated record
			var rec = new List<string>(newData.data);
			rec.Insert(0, newData.line.ToString());
			return rec.ToJson();
		}

		private class DayFileEditor
		{
			public string action { get; set; }
			public int line { get; set; }
			public string[] data { get; set; }
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
			int month = Convert.ToInt32(newData.month.Split('-')[0]);
			int year = Convert.ToInt32(newData.month.Split('-')[1]);

			// Get a timestamp, use 15th day to avoid wrap issues
			var ts = new DateTime(year, month, 15);

			var logfile = (newData.extra ? cumulus.GetExtraLogFileName(ts) : cumulus.GetLogFileName(ts));

			// read the log file into a List
			var lines = File.ReadAllLines(logfile).ToList();

			var lineNum = newData.line - 1; // our List is zero relative

			if (newData.action == "Edit")
			{
				// replace the edited line
				var newLine = String.Join(cumulus.ListSeparator, newData.data);
				lines[lineNum] = newLine;

				// write logfile back again
				File.WriteAllLines(logfile, lines);

				// Update the MySQL record
				if (!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Server) &&
					!string.IsNullOrEmpty(cumulus.MySqlConnSettings.UserID) &&
					!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Password) &&
					!string.IsNullOrEmpty(cumulus.MySqlConnSettings.Database) &&
					cumulus.MySqlUpdateOnEdit
					)
				{
					// Only the monhtly log file is stored in MySQL
					if (!newData.extra)
					{
						try
						{
							var InvC = new CultureInfo("");
							var updt = new StringBuilder(1024);

							var LogRec = station.ParseLogFileRec(newLine);

							updt.Append($"UPDATE {cumulus.MySqlMonthlyTable} SET ");
							updt.Append($"Temp={LogRec.OutdoorTemperature.ToString(cumulus.TempFormat, InvC)},");
							updt.Append($"Humidity={ LogRec.OutdoorHumidity},");
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

							cumulus.MySqlCommandSync(updt.ToString(), "EditLogFile");
						}
						catch
						{
							return "{\"result\":\"Log file updated OK, but failed, to update MySQL\"}";
						}

					}
				}
			}
			else if (newData.action == "Delete")
			{
				// Just double check we are deleting the correct line - see if the dates match
				var lineData = lines[lineNum].Split(cumulus.ListSeparator.ToCharArray()[0]);
				if (lineData[0] == newData.data[0])
				{
					lines.RemoveAt(lineNum);
					// write logfile back again
					File.WriteAllLines(logfile, lines);
				}
				else
				{
					//throw("Failed, line to delete does not match the file contents");
					return "{\"result\":\"Failed, line to delete does not match the file contents\"}";
				}
			}


			// return the updated record
			var rec = new List<string>(newData.data);
			rec.Insert(0, newData.line.ToString());
			return rec.ToJson();
		}

		private class DatalogEditor
		{
			public string action { get; set; }
			public int line { get; set; }
			public string month { get; set; }
			public bool extra { get; set; }
			public string[] data { get; set; }
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
