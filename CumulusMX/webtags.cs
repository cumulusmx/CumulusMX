using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;

using Swan;

namespace CumulusMX
{
	public delegate string WebTagFunction(Dictionary<string, string> tagParams);

	public class WebTags
	{
		private Dictionary<string, WebTagFunction> webTagDictionary;

		private readonly Cumulus cumulus;
		private readonly WeatherStation station;

		internal WebTags(Cumulus cumulus, WeatherStation station)
		{
			this.cumulus = cumulus;
			this.station = station;
		}

		private static string ReadFileIntoString(string fileName)
		{
			string result;
			using (StreamReader streamReader = new StreamReader(fileName))
			{
				result = streamReader.ReadToEnd();
			}
			return result;
		}

		private static string CheckRc(string val, Dictionary<string, string> tagParams)
		{
			try
			{
				return tagParams.Get("rc") == "y" ? val.Replace(',', '.') : val;
			}
			catch
			{
				return "error";
			}
		}

		private static string CheckRcDp(double val, Dictionary<string, string> tagParams, int decimals, string format = null)
		{
			string ret;
			try
			{
				var numFormat = tagParams.Get("rc") == "y" ? CultureInfo.InvariantCulture.NumberFormat : CultureInfo.CurrentCulture.NumberFormat;

				if (tagParams.Get("tc") == "y")
				{
					val = Math.Truncate(val);
					tagParams["dp"] = "0";
				}

				if (null != format)
				{
					ret = val.ToString(format, numFormat);
				}
				else
				{
					int dp = int.TryParse(tagParams.Get("dp"), out dp) ? dp : decimals;
					ret = val.ToString("F" + dp, numFormat);
				}
				return ret;
			}
			catch
			{
				return "error";
			}
		}

		private static string CheckRcDp(decimal val, Dictionary<string, string> tagParams, int decimals)
		{
			string ret;
			try
			{
				var numFormat = tagParams.Get("rc") == "y" ? CultureInfo.InvariantCulture.NumberFormat : CultureInfo.CurrentCulture.NumberFormat;

				if (tagParams.Get("tc") == "y")
				{
					val = Math.Truncate(val);
					tagParams["dp"] = "0";
				}

				int dp = int.TryParse(tagParams.Get("dp"), out dp) ? dp : decimals;

				ret = val.ToString("F" + dp, numFormat);

				return ret;
			}
			catch
			{
				return "error";
			}
		}

		private static double CheckTempUnit(double val, Dictionary<string, string> tagParams)
		{
			if (tagParams.ContainsKey("unit"))
			{
				var unit = tagParams.Get("unit").ToLower();
				if (unit == "c")
					return ConvertUnits.UserTempToC(val);
				else if (unit == "f")
					return ConvertUnits.UserTempToF(val);
			}
			return val;
		}

		private double CheckTempUnitAbs(double val, Dictionary<string, string> tagParams)
		{
			if (tagParams.ContainsKey("unit"))
			{
				var unit = tagParams.Get("unit").ToLower();
				if (unit == "c" && cumulus.Units.Temp == 1)
					return val / 1.8;
				else if (unit == "f" && cumulus.Units.Temp == 0)
					return val * 1.8;
			}
			return val;
		}

		private static double CheckPressUnit(double val, Dictionary<string, string> tagParams)
		{
			if (tagParams.ContainsKey("unit"))
			{
				var unit = tagParams.Get("unit").ToLower();
				if (unit == "hpa" || unit == "mb")
					return ConvertUnits.UserPressToHpa(val);
				else if (unit == "kpa")
					return ConvertUnits.UserPressToHpa(val) / 10;
				else if (unit == "inhg")
					return ConvertUnits.UserPressToIN(val);
			}
			return val;
		}

		private static double CheckRainUnit(double val, Dictionary<string, string> tagParams)
		{
			if (tagParams.ContainsKey("unit"))
			{
				var unit = tagParams.Get("unit").ToLower();
				if (unit == "mm")
					return ConvertUnits.UserRainToMM(val);
				else if (unit == "in")
					return ConvertUnits.UserRainToIN(val);
			}
			return val;
		}

		private static double CheckWindUnit(double val, Dictionary<string, string> tagParams)
		{
			if (tagParams.ContainsKey("unit"))
			{
				var unit = tagParams.Get("unit").ToLower();
				if (unit == "mph")
					return ConvertUnits.UserWindToMPH(val);
				else if (unit == "kph")
					return ConvertUnits.UserWindToKPH(val);
				else if (unit == "ms")
					return ConvertUnits.UserWindToMS(val);
				else if (unit == "kt")
					return ConvertUnits.UserWindToKnots(val);
			}
			return val;
		}

		private static double CheckWindRunUnit(double val, Dictionary<string, string> tagParams)
		{
			if (tagParams.ContainsKey("unit"))
			{
				var unit = tagParams.Get("unit").ToLower();
				if (unit == "mi")
					return ConvertUnits.WindRunToMi(val);
				else if (unit == "km")
					return ConvertUnits.WindRunToKm(val);
				else if (unit == "nm")
					return ConvertUnits.WindRunToNm(val);
			}
			return val;
		}


		private decimal? GetSnowDepth(DateTime day)
		{
			decimal? depth;
			try
			{
				var result = cumulus.DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE Date = ?", day.Date);

				depth = result.Count == 1 ? result[0].SnowDepth : null;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("Error reading diary database: " + e.Message);
				depth = 0;
			}
			return depth;
		}

		private decimal? GetSnow24h(DateTime day)
		{
			decimal? snow24h;
			try
			{
				var result = cumulus.DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE Date = ?", day.Date);

				snow24h = result.Count == 1 ? result[0].Snow24h : null;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("Error reading diary database: " + e.Message);
				snow24h = 0;
			}
			return snow24h;
		}

		private bool GetSnowLying(DateTime day)
		{
			bool lying;
			try
			{
				var result = cumulus.DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE Date = ?", day.Date);

				lying = result.Count == 1 && result[0].SnowDepth > 0;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("Error reading diary database: " + e.Message);
				lying = false;
			}
			return lying;
		}

		private string GetSnowComment(DateTime day)
		{
			string comment;
			try
			{
				var result = cumulus.DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE Date = ?", day.Date);

				comment = result.Count == 1 && !string.IsNullOrEmpty(result[0].Entry) ? result[0].Entry : null;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("Error reading diary database: " + e.Message);
				comment = string.Empty;
			}
			return comment;
		}

		private static string EncodeForWeb(string aStr)
		{
			return HttpUtility.HtmlEncode(aStr);
		}

		private static string EncodeForJs(string aStr)
		{
			var str = HttpUtility.HtmlDecode(aStr);
			return HttpUtility.JavaScriptStringEncode(str);
		}


		private DateTime GetRecentTs(Dictionary<string, string> tagParams)
		{
			var daysagostr = tagParams.Get("d");
			var hoursagostr = tagParams.Get("h");
			var minsagostr = tagParams.Get("m");
			var minsago = 0;
			if (minsagostr != null)
			{
				try
				{
					minsago += Convert.ToInt32(minsagostr);
				}
				catch
				{
					cumulus.LogWarningMessage($"Error: Webtag <#{tagParams.Get("webtag")}>, expecting an integer value for parameter 'm=n', found 'm={minsagostr}'");
				}
			}
			if (hoursagostr != null)
			{
				try
				{
					minsago += (Convert.ToInt32(hoursagostr) * 60);
				}
				catch
				{
					cumulus.LogWarningMessage($"Error: Webtag <#{tagParams.Get("webtag")}>, expecting an integer value for parameter 'h=n', found 'h={hoursagostr}'");
				}
			}
			if (daysagostr != null)
			{
				try
				{
					minsago += (Convert.ToInt32(daysagostr) * 1440);
				}
				catch
				{
					cumulus.LogWarningMessage($"Error: Webtag <#{tagParams.Get("webtag")}>, expecting an integer value for parameter 'd=n', found 'd={daysagostr}'");
				}
			}
			if (minsago < 0)
			{
				minsago = 0;
			}
			return DateTime.Now.AddMinutes(-minsago);
		}

		private int GetMonthParam(Dictionary<string, string> tagParams)
		{
			int month;
			var monthstr = tagParams.Get("mon");
			if (monthstr != null)
			{
				// month parameter supplied
				try
				{
					month = Convert.ToInt32(monthstr);
				}
				catch
				{
					// problem converting parameter, use current month instead
					month = DateTime.Now.Month;
					cumulus.LogWarningMessage($"Error: Webtag <#{tagParams.Get("webtag")}> expecting an integer value for parameter 'mon=n', found 'mon={monthstr}'");
				}
			}
			else
			{
				// no parameter supplied, use current month instead
				month = DateTime.Now.Month;
			}
			// check for valid month, use current momth if invalid
			if ((month < 1) || (month > 12))
			{
				month = DateTime.Now.Month;
			}
			return month;
		}

		public string GetCurrCondText()
		{
			string res;
			string fileName = cumulus.AppDir + "currentconditions.txt";
			if (File.Exists(fileName))
			{
				res = ReadFileIntoString(fileName);
			}
			else
			{
				res = string.Empty;
			}
			return EncodeForJs(res);
		}

		private static string GetFormattedDateTime(DateTime dt, Dictionary<string, string> tagParams)
		{
			string s;
			string dtformat = tagParams.Get("format");
			if (dtformat == null)
			{
				s = dt.ToString();
			}
			else
			{
				try
				{
					if (dtformat == "Unix")
					{
						s = dt.ToUnixEpochDate().ToString();
					}
					else if (dtformat == "JS")
					{
						s = (dt.ToUnixEpochDate() * 1000).ToString();

					}
					else
					{
						s = dt.ToString(dtformat);
					}
				}
				catch (Exception)
				{
					s = dt.ToString();
				}
			}
			return s;
		}

		private string GetFormattedDateTime(DateTime dt, string defaultFormat, Dictionary<string, string> tagParams)
		{
			string s;
			if (dt <= cumulus.defaultRecordTS)
			{
				s = tagParams.Get("nv") ?? "----";
			}
			else
			{
				string dtformat = tagParams.Get("format") ?? defaultFormat;
				if (dtformat == "Unix")
				{
					s = dt.ToUnixEpochDate().ToString();
				}
				else if (dtformat == "JS")
				{
					s = (dt.ToUnixEpochDate() * 1000).ToString();

				}
				else
				{
					s = dt.ToString(dtformat);
				}
			}
			return s;
		}

		private string GetFormattedDateTime(TimeSpan ts, string defaultFormat, Dictionary<string, string> tagParams)
		{
			DateTime dt = DateTime.MinValue.Add(ts);

			return GetFormattedDateTime(dt, defaultFormat, tagParams);
		}

		private static string GetFormattedTimeSpan(TimeSpan ts, string defaultFormat, Dictionary<string, string> tagParams)
		{
			string dtformat = tagParams.Get("format") ?? defaultFormat;
			return String.Format(dtformat, ts);
		}

		private string GetMonthlyAlltimeValueStr(AllTimeRec rec, Dictionary<string, string> tagParams, int decimals)
		{
			if (rec.Ts <= cumulus.defaultRecordTS)
			{
				return tagParams.Get("nv") ?? "---";
			}

			return CheckRcDp(rec.Val, tagParams, decimals);
		}

		private string Tagwlatest(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.WindLatest, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string Tagwindrun(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindRunUnit(station.WindRunToday, tagParams), tagParams, cumulus.WindRunDPlaces);
		}

		private string Tagwindrunmonth(Dictionary<string, string> tagParams)
		{
			if (!int.TryParse(tagParams.Get("year"), out int year))
			{
				year = DateTime.Now.Year;
			}
			if (!int.TryParse(tagParams.Get("month"), out int month))
			{
				month = DateTime.Now.Month;
			}

			return CheckRcDp(CheckWindRunUnit(station.GetWindRunMonth(year, month), tagParams), tagParams, cumulus.WindRunDPlaces);
		}

		private string Tagwspeed(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.WindAverage, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string Tagcurrentwdir(Dictionary<string, string> tagParams)
		{
			return station.CompassPoint(station.Bearing);
		}

		private string Tagwdir(Dictionary<string, string> tagParams)
		{
			return station.CompassPoint(station.AvgBearing);
		}

		private string Tagwgust(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.RecentMaxGust, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagwindAvg(Dictionary<string, string> tagParams)
		{
			// we add on the meteo day start hour to 00:00 today
			var startOfDay = DateTime.Today.AddHours(-cumulus.GetHourInc());
			// then if that is later than now we are still in the previous day, so subtract a day
			// Allow a 20 second grace into the following day so we still have the previous days total just after rollover
			if (startOfDay > DateTime.Now.AddSeconds(-20))
				startOfDay = startOfDay.AddDays(-1);

			var hours = (DateTime.Now.ToUniversalTime() - startOfDay.ToUniversalTime()).TotalHours;
			var timeToday = station.WindRunHourMult[cumulus.Units.Wind] * hours;
			// just after rollover the numbers will be silly, so return zero for the first 15 minutes
			return CheckRcDp(CheckWindUnit(hours > 0.25 ? station.WindRunToday / timeToday : 0, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagWindAvgCust(Dictionary<string, string> tagParams)
		{
			// m parater is the minutes to average
			int mins = int.TryParse(tagParams.Get("m"), out mins) ? mins : cumulus.AvgSpeedTime.Minutes;
			var fromTime = DateTime.Now.AddMinutes(-mins);
			var ws = station.GetWindAverageFromArray(fromTime);
			// we want any calibration to be applied from uncalibrated values
			ws = cumulus.Calib.WindSpeed.Calibrate(ws);
			return CheckRcDp(CheckWindUnit(ws, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagWindGustCust(Dictionary<string, string> tagParams)
		{
			// m parater is the minutes to find the gust
			int mins = int.TryParse(tagParams.Get("m"), out mins) ? mins : cumulus.PeakGustTime.Minutes;
			var fromTime = DateTime.Now.AddMinutes(-mins);
			var ws = station.GetWindGustFromArray(fromTime);
			// we want any calibration to be applied from uncalibrated values
			ws = cumulus.Calib.WindGust.Calibrate(ws);
			return CheckRcDp(CheckWindUnit(ws, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string Tagwchill(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.WindChill, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagrrate(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainRate, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagStormRain(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.StormRain, tagParams, cumulus.RainDPlaces);
		}

		private string TagStormRainStart(Dictionary<string, string> tagParams)
		{

			if (station.StartOfStorm == DateTime.MinValue)
			{
				return tagParams.Get("nv") ?? "-----";
			}

			string dtformat = tagParams.Get("format") ?? "d";

			return station.StartOfStorm.ToString(dtformat);
		}

		private string Tagtomorrowdaylength(Dictionary<string, string> tagParams)
		{
			return cumulus.TomorrowDayLengthText;
		}

		private string TagwindrunY(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindRunUnit(station.YesterdayWindRun, tagParams), tagParams, cumulus.WindRunDPlaces);
		}

		private string TagwindAvgY(Dictionary<string, string> tagParams)
		{
			var timeYest = station.WindRunHourMult[cumulus.Units.Wind] * 24;
			return CheckRcDp(CheckWindUnit(station.YesterdayWindRun / timeYest, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}


		private string Tagheatdegdays(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.HeatingDegreeDays, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagcooldegdays(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.CoolingDegreeDays, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagheatdegdaysY(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.YestHeatingDegreeDays, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagcooldegdaysY(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.YestCoolingDegreeDays, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagpresstrendval(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.presstrendval, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string Tagpresstrendsigned(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.presstrendval, tagParams), tagParams, cumulus.PressDPlaces, cumulus.PressTrendFormat);
		}


		private string TagPressChangeLast3Hours(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.presstrendval * 3, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagTempChangeLastHour(Dictionary<string, string> tagParams)
		{
			return CheckRc(CheckTempUnitAbs(station.TempChangeLastHour, tagParams).ToString("+##0.0;-##0.0;0"), tagParams);
		}


		private string Tagdew(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.OutdoorDewpoint, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagwetbulb(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.WetBulb, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagcloudbase(Dictionary<string, string> tagParams)
		{
			return station.CloudBase + (cumulus.CloudBaseInFeet ? " ft" : " m");
		}

		private string Tagcloudbaseunit(Dictionary<string, string> tagParams)
		{
			return cumulus.CloudBaseInFeet ? "ft" : "m";
		}

		private string Tagcloudbasevalue(Dictionary<string, string> tagParams)
		{
			return station.CloudBase.ToString();
		}

		private string TagTime(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(DateTime.Now, cumulus.Trans.WebTagGenTimeDate, tagParams);
		}

		private string TagDataDateTime(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.DataDateTime, cumulus.Trans.WebTagGenTimeDate, tagParams);
		}

		private static string TagDaysSince30Dec1899(Dictionary<string, string> tagParams)
		{
			DateTime startDate = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Local);
			return ((DateTime.Now.ToUniversalTime() - startDate.ToUniversalTime()).TotalDays).ToString();
		}

		private string TagTimeUtc(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(DateTime.UtcNow, cumulus.Trans.WebTagGenTimeDate, tagParams);
		}

		private static string TagTimehhmmss(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("HH:mm:ss");
		}

		private static string TagTimeJavascript(Dictionary<string, string> tagParams)
		{
			return (DateTime.Now.ToUnixEpochDate() * 1000).ToString();
		}

		private static string TagTimeUnix(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToUnixEpochDate().ToString();
		}

		private string TagDate(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(DateTime.Now, "d", tagParams);
		}

		private string TagYesterday(Dictionary<string, string> tagParams)
		{
			var yesterday = DateTime.Now.AddDays(-1);
			return GetFormattedDateTime(yesterday, "d", tagParams);
		}

		private string TagMetDate(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(cumulus.MeteoDate(), "d", tagParams);
		}

		private string TagMetDateYesterday(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(cumulus.MeteoDate(DateTime.Now.AddDays(-1)), "d", tagParams);
		}

		private static string TagDay(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("dd");
		}

		private static string TagDayname(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("dddd");
		}

		private static string TagShortdayname(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("ddd");
		}

		private static string TagMonth(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("MM");
		}

		private static string TagMonthname(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("MMMM");
		}

		private static string TagShortmonthname(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("MMM");
		}

		private static string TagYear(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.Year.ToString();
		}

		private static string TagShortyear(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("yy");
		}

		private static string TagHour(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("HH");
		}

		private static string TagMinute(Dictionary<string, string> tagParams)
		{
			return DateTime.Now.ToString("mm");
		}

		private string Tagforecastnumber(Dictionary<string, string> tagParams)
		{
			return station.Forecastnumber.ToString();
		}

		private string Tagforecast(Dictionary<string, string> tagParams)
		{
			return station.forecaststr;
		}

		private string Tagforecastenc(Dictionary<string, string> tagParams)
		{
			return EncodeForWeb(station.forecaststr);
		}

		private string TagforecastJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(station.forecaststr);
		}

		private string Tagcumulusforecast(Dictionary<string, string> tagParams)
		{
			return station.CumulusForecast;
		}

		private string Tagcumulusforecastenc(Dictionary<string, string> tagParams)
		{
			return EncodeForWeb(station.CumulusForecast);
		}

		private string TagcumulusforecastJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(station.CumulusForecast);
		}

		private string Tagwsforecast(Dictionary<string, string> tagParams)
		{
			return station.wsforecast;
		}

		private string Tagwsforecastenc(Dictionary<string, string> tagParams)
		{
			return EncodeForWeb(station.wsforecast);
		}

		private string TagwsforecastJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(station.wsforecast);
		}


		private string Tagtemp(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.OutdoorTemperature, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagtemprange(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.HiLoToday.TempRange, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagtemprangeY(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.HiLoYest.TempRange, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagavgtemp(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.TempTotalToday / station.tempsamplestoday, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagavgtempY(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.YestAvgTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagapptemp(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ApparentTemperature, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagfeelsliketemp(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.FeelsLike, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagtemptrend(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.temptrendval, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagtemptrendsigned(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.temptrendval, tagParams), tagParams, cumulus.TempDPlaces, cumulus.TempTrendFormat);
		}

		private string Tagtemptrendtext(Dictionary<string, string> tagParams)
		{
			string text;
			if (Math.Abs(station.temptrendval) < 0.001)
			{
				text = cumulus.Trans.Steady;
			}
			else if (station.temptrendval > 0)
			{
				text = cumulus.Trans.Rising;
			}
			else
			{
				text = cumulus.Trans.Falling;
			}
			return text;
		}

		private string Tagtemptrendenglish(Dictionary<string, string> tagParams)
		{
			if (Math.Abs(station.temptrendval) < 0.001)
			{
				return "Steady";
			}

			return station.temptrendval > 0 ? "Rising" : "Falling";
		}

		private string Tagheatindex(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HeatIndex, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Taghum(Dictionary<string, string> tagParams)
		{
			return station.OutdoorHumidity.ToString();
		}

		private string Taghumidex(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.Humidex, tagParams, cumulus.TempDPlaces);
		}

		private string Tagpress(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.Pressure, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string Tagaltimeterpressure(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.AltimeterPressure, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string Tagstationpressure(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.StationPressure, tagParams), tagParams, cumulus.PressDPlaces);
		}


		private string Tagpresstrend(Dictionary<string, string> tagParams)
		{
			return station.Presstrendstr;
		}

		private string Tagpresstrendenglish(Dictionary<string, string> tagParams)
		{
			if (Math.Abs(station.presstrendval) < 0.0001)
			{
				return "Steady";
			}

			return station.presstrendval > 0 ? "Rising" : "Falling";
		}

		private string Tagbearing(Dictionary<string, string> tagParams)
		{
			return station.Bearing.ToString();
		}

		private string Tagavgbearing(Dictionary<string, string> tagParams)
		{
			return station.AvgBearing.ToString();
		}

		private string TagBearingRangeFrom(Dictionary<string, string> tagParams)
		{
			return station.BearingRangeFrom.ToString();
		}

		private string TagBearingRangeTo(Dictionary<string, string> tagParams)
		{
			return station.BearingRangeTo.ToString();
		}

		private string TagBearingRangeFrom10(Dictionary<string, string> tagParams)
		{
			return station.BearingRangeFrom10.ToString("D3");
		}

		private string TagBearingRangeTo10(Dictionary<string, string> tagParams)
		{
			return station.BearingRangeTo10.ToString("D3");
		}

		private string Tagdomwindbearing(Dictionary<string, string> tagParams)
		{
			return station.DominantWindBearing.ToString();
		}

		private string Tagdomwinddir(Dictionary<string, string> tagParams)
		{
			return station.CompassPoint(station.DominantWindBearing);
		}

		private string TagdomwindbearingY(Dictionary<string, string> tagParams)
		{
			return station.YestDominantWindBearing.ToString();
		}

		private string TagdomwinddirY(Dictionary<string, string> tagParams)
		{
			return station.CompassPoint(station.YestDominantWindBearing);
		}

		private string Tagbeaufort(Dictionary<string, string> tagParams)
		{
			return "F" + Cumulus.Beaufort(station.WindAverage);
		}

		private string Tagbeaufortnumber(Dictionary<string, string> tagParams)
		{
			return Cumulus.Beaufort(station.WindAverage);
		}

		private string Tagbeaudesc(Dictionary<string, string> tagParams)
		{
			return cumulus.BeaufortDesc(station.WindAverage);
		}

		private string Tagwdirdata(Dictionary<string, string> tagParams)
		{
			var sb = new StringBuilder(station.windbears[0].ToString());

			for (var i = 1; i < station.numwindvalues; i++)
			{
				sb.Append("," + station.windbears[i]);
			}

			return sb.ToString();
		}

		private string Tagwspddata(Dictionary<string, string> tagParams)
		{
			var sb = new StringBuilder((station.windspeeds[0]).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
			for (var i = 1; i < station.numwindvalues; i++)
			{
				sb.Append("," + (station.windspeeds[i]).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
			}

			return sb.ToString();
		}

		private string TagWindSampleCount(Dictionary<string, string> tagParams)
		{
			return station.numwindvalues.ToString();
		}

		private string Tagnextwindindex(Dictionary<string, string> tagParams)
		{
			return station.nextwindvalue.ToString();
		}

		private string TagWindRoseData(Dictionary<string, string> tagParams)
		{
			// no need to use multiplier as the rose is all relative
			var sb = new StringBuilder(station.windcounts[0].ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));

			for (var i = 1; i < cumulus.NumWindRosePoints; i++)
			{
				sb.Append("," + station.windcounts[i].ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
			}

			return sb.ToString();
		}

		private string TagWindRosePoints(Dictionary<string, string> tagParams)
		{
			return cumulus.NumWindRosePoints.ToString();
		}

		private string Tagrfall(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainToday, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string Tagrmidnight(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainSinceMidnight, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string Tagrmonth(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainMonth, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string Tagrweek(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainWeek, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string Tagrhour(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainLastHour, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string Tagr24Hour(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainLast24Hour, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string Tagryear(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainYear, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string Taginhum(Dictionary<string, string> tagParams)
		{
			return station.IndoorHumidity.HasValue ? station.IndoorHumidity.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string Tagintemp(Dictionary<string, string> tagParams)
		{
			return station.IndoorTemperature.HasValue ? CheckRcDp(CheckTempUnit(station.IndoorTemperature.Value, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string Tagbattery(Dictionary<string, string> tagParams)
		{
			return CheckRc(station.ConBatText ?? tagParams.Get("nv") ?? "--", tagParams);
		}

		private string TagConsoleSupplyV(Dictionary<string, string> tagParams)
		{
			return CheckRc(station.ConSupplyVoltageText ?? tagParams.Get("nv") ?? "--", tagParams);
		}

		private string Tagtxbattery(Dictionary<string, string> tagParams)
		{
			var json = tagParams.Get("format") == "json";

			if (string.IsNullOrEmpty(station.TxBatText))
			{
				return json ? "{}" : "";
			}

			string[] sl;

			string channeltxt = tagParams.Get("channel");
			if (channeltxt == null)
			{
				if (json)
				{
					var retVal = new StringBuilder("{");
					sl = station.TxBatText.Split(' ');

					for (var i = 0; i < sl.Length; i++)
					{
						retVal.Append($"\"TX{i + 1}\":\"{sl[i][2..]}\",");
					}

					retVal.Length--;
					retVal.Append('}');
					return retVal.ToString();
				}
				else
				{
					return station.TxBatText;
				}
			}

			// extract status for required channel
			char[] delimiters = [' ', '-'];
			sl = station.TxBatText.Split(delimiters);

			int channel = int.Parse(channeltxt) * 2 - 1;

			if ((channel < sl.Length) && (sl[channel].Length > 0))
			{
				return sl[channel];
			}

			// default
			return station.TxBatText;
		}

		private string TagLowBatteryList(Dictionary<string, string> tagParams)
		{
			return string.Join(",", station.LowBatteryDevices);
		}

		private string TagMulticastGoodCnt(Dictionary<string, string> tagParams)
		{
			return station.multicastsGood.ToString();
		}

		private string TagMulticastBadCnt(Dictionary<string, string> tagParams)
		{
			return station.multicastsBad.ToString();
		}

		private string TagMulticastGoodPct(Dictionary<string, string> tagParams)
		{
			try
			{
				return (station.multicastsGood / (float) (station.multicastsBad + station.multicastsGood) * 100).ToString("0.00");
			}
			catch
			{
				return "0.00";
			}
		}

		// AirLink Indoor
		private string TagAirLinkTempIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(CheckTempUnit(cumulus.airLinkDataIn.temperature, tagParams), tagParams, cumulus.TempDPlaces);
		}
		private string TagAirLinkHumIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.humidity, tagParams, cumulus.HumDPlaces);
		}
		private string TagAirLinkPm1In(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm1, tagParams, 1);
		}
		private string TagAirLinkPm2p5In(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5, tagParams, 1);
		}
		private string TagAirLinkPm2p5_1hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5_1hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_3hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5_3hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_24hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5_24hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_NowcastIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5_nowcast, tagParams, 1);
		}
		private string TagAirLinkPm10In(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10, tagParams, 1);
		}
		private string TagAirLinkPm10_1hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10_1hr, tagParams, 1);
		}
		private string TagAirLinkPm10_3hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10_3hr, tagParams, 1);
		}
		private string TagAirLinkPm10_24hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10_24hr, tagParams, 1);
		}
		private string TagAirLinkPm10_NowcastIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10_nowcast, tagParams, 1);
		}
		private string TagAirLinkFirmwareVersionIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataIn.firmwareVersion;
		}
		private string TagAirLinkWifiRssiIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataIn.wifiRssi.ToString();
		}
		private string TagAirLinkUptimeIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : GetFormattedTimeSpan(cumulus.airLinkDataIn.uptime, cumulus.Trans.WebTagElapsedTime, tagParams);
		}
		private string TagAirLinkLinkUptimeIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : GetFormattedTimeSpan(cumulus.airLinkDataIn.linkUptime, cumulus.Trans.WebTagElapsedTime, tagParams);
		}

		// AirLink Outdoor
		private string TagAirLinkTempOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(CheckTempUnit(cumulus.airLinkDataOut.temperature, tagParams), tagParams, cumulus.TempDPlaces);
		}
		private string TagAirLinkHumOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.humidity, tagParams, cumulus.HumDPlaces);
		}
		private string TagAirLinkPm1Out(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm1, tagParams, 1);
		}
		private string TagAirLinkPm2p5Out(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5, tagParams, 1);
		}
		private string TagAirLinkPm2p5_1hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5_1hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_3hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5_3hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_24hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5_24hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_NowcastOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5_nowcast, tagParams, 1);
		}
		private string TagAirLinkPm10Out(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10, tagParams, 1);
		}
		private string TagAirLinkPm10_1hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10_1hr, tagParams, 1);
		}
		private string TagAirLinkPm10_3hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10_3hr, tagParams, 1);
		}
		private string TagAirLinkPm10_24hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10_24hr, tagParams, 1);
		}
		private string TagAirLinkPm10_NowcastOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10_nowcast, tagParams, 1);
		}
		private string TagAirLinkFirmwareVersionOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataOut.firmwareVersion;
		}
		private string TagAirLinkWifiRssiOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataOut.wifiRssi.ToString();
		}
		private string TagAirLinkUptimeOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : GetFormattedTimeSpan(cumulus.airLinkDataOut.uptime, cumulus.Trans.WebTagElapsedTime, tagParams);
		}
		private string TagAirLinkLinkUptimeOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : GetFormattedTimeSpan(cumulus.airLinkDataOut.linkUptime, cumulus.Trans.WebTagElapsedTime, tagParams);
		}

		private string TagAirLinkAqiPm2P5In(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirLinkAqiPm2p5_1hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5_1hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_3hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5_3hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_24hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5_24hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_NowcastIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5_nowcast, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10In(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_1hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10_1hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_3hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10_3hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_24hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10_24hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_NowcastIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10_nowcast, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirLinkAqiPm2P5Out(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_1hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5_1hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_3hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5_3hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_24hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5_24hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_NowcastOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5_nowcast, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10Out(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_1hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10_1hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_3hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10_3hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_24hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10_24hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_NowcastOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid)
				return tagParams.Get("nv") ?? "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10_nowcast, tagParams, cumulus.AirQualityDPlaces);
		}


		private string AirLinkPct_1hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataIn.pct_1hr.ToString();
		}
		private string AirLinkPct_3hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataIn.pct_3hr.ToString();
		}
		private string AirLinkPct_24hrIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataIn.pct_24hr.ToString();
		}
		private string AirLinkPct_NowcastIn(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataIn == null || !cumulus.airLinkDataIn.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataIn.pct_nowcast.ToString();
		}
		private string AirLinkPct_1hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataOut.pct_1hr.ToString();
		}
		private string AirLinkPct_3hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataOut.pct_3hr.ToString();
		}
		private string AirLinkPct_24hrOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataOut.pct_24hr.ToString();
		}
		private string AirLinkPct_NowcastOut(Dictionary<string, string> tagParams)
		{
			return (cumulus.airLinkDataOut == null || !cumulus.airLinkDataOut.dataValid) ? tagParams.Get("nv") ?? "--" : cumulus.airLinkDataOut.pct_nowcast.ToString();
		}


		private string Tagsnowdepth(Dictionary<string, string> tagParams)
		{
			var ts = DateTime.Now.Hour < cumulus.SnowDepthHour ? DateTime.Now.AddDays(-1) : DateTime.Now;
			var val = GetSnowDepth(ts.Date);
			return val.HasValue ? CheckRcDp(val.Value, tagParams, cumulus.SnowDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string Tagsnowlying(Dictionary<string, string> tagParams)
		{
			var ts = DateTime.Now.Hour < cumulus.SnowDepthHour ? DateTime.Now.AddDays(-1) : DateTime.Now;
			return GetSnowLying(ts.Date).ToString();
		}

		private string Tagsnow24hr(Dictionary<string, string> tagParams)
		{
			var ts = DateTime.Now.Hour < cumulus.SnowDepthHour ? DateTime.Now.AddDays(-1) : DateTime.Now;
			var val = GetSnow24h(ts.Date);
			return val.HasValue ? CheckRcDp(val.Value, tagParams, cumulus.SnowDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string Tagsnowcomment(Dictionary<string, string> tagParams)
		{
			var ts = DateTime.Now.Hour < cumulus.SnowDepthHour ? DateTime.Now.AddDays(-1) : DateTime.Now;
			var val = GetSnowComment(ts.Date);
			return string.IsNullOrEmpty(val) ? tagParams.Get("nv") ?? "" : val;
		}

		private static string Tagsnowfalling(Dictionary<string, string> tagParams)
		{
			return string.Empty;
		}

		private static string TagDiaryThunder(Dictionary<string, string> tagParams)
		{
			return GetDiaryBoolean("Thunder", tagParams);
		}

		private static string TagDiaryHail(Dictionary<string, string> tagParams)
		{
			return GetDiaryBoolean("Hail", tagParams);
		}

		private static string TagDiaryFog(Dictionary<string, string> tagParams)
		{
			return GetDiaryBoolean("Fog", tagParams);
		}

		private static string TagDiaryGales(Dictionary<string, string> tagParams)
		{
			return GetDiaryBoolean("Gales", tagParams);
		}

		private static string GetDiaryBoolean(string column, Dictionary<string, string> tagParams)
		{
			var date = tagParams.Get("date");
			var day = DateTime.Now;

			if (date != null)
			{
				if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
				{
					Program.cumulus.LogErrorMessage($"Wegtag Diary{column} Error: Invalid date = {date}");
					return tagParams.Get("nv") ?? "-";
				}
			}

			try
			{
				var result = Program.cumulus.DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE Date = ?", day.Date);

				if (result.Count == 0)
				{
					return tagParams.Get("nv") ?? "-";
				}
				else
				{
					return (result.Count == 1 && (bool) result[0].GetType().GetProperty(column).GetValue(result[0], null)).ToString().ToLower();
				}
			}
			catch (Exception e)
			{
				Program.cumulus.LogErrorMessage($"Wegtag Diary{column} Error reading diary database: " + e.Message);
				return tagParams.Get("nv") ?? "-";
			}
		}


		private string Tagnewrecord(Dictionary<string, string> tagParams)
		{
			return station.AlltimeRecordTimestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagTempRecordSet(Dictionary<string, string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.HighTemp.Ts >= threshold ||
				station.AllTime.LowTemp.Ts >= threshold ||
				station.AllTime.HighDailyTempRange.Ts >= threshold ||
				station.AllTime.LowDailyTempRange.Ts >= threshold ||
				station.AllTime.LowChill.Ts >= threshold ||
				station.AllTime.HighMinTemp.Ts >= threshold ||
				station.AllTime.LowMaxTemp.Ts >= threshold ||
				station.AllTime.HighAppTemp.Ts >= threshold ||
				station.AllTime.LowAppTemp.Ts >= threshold ||
				station.AllTime.HighHeatIndex.Ts >= threshold ||
				station.AllTime.HighDewPoint.Ts >= threshold ||
				station.AllTime.LowDewPoint.Ts >= threshold ||
				station.AllTime.HighFeelsLike.Ts >= threshold ||
				station.AllTime.LowFeelsLike.Ts >= threshold ||
				station.AllTime.HighHumidex.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagWindRecordSet(Dictionary<string, string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.HighGust.Ts >= threshold ||
				station.AllTime.HighWind.Ts >= threshold ||
				station.AllTime.HighWindRun.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagRainRecordSet(Dictionary<string, string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.HighRainRate.Ts >= threshold ||
				station.AllTime.DailyRain.Ts >= threshold ||
				station.AllTime.HourlyRain.Ts >= threshold ||
				station.AllTime.HighRain24Hours.Ts >= threshold ||
				station.AllTime.LongestDryPeriod.Ts >= threshold ||
				station.AllTime.LongestWetPeriod.Ts >= threshold ||
				station.AllTime.MonthlyRain.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagHumidityRecordSet(Dictionary<string, string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.HighHumidity.Ts >= threshold ||
				station.AllTime.LowHumidity.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagPressureRecordSet(Dictionary<string, string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.LowPress.Ts >= threshold ||
				station.AllTime.HighPress.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagHighTempRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowTempRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighAppTempRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighAppTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowAppTempRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowAppTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighFeelsLikeRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighFeelsLike.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowFeelsLikeRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowFeelsLike.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHumidexRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighHumidex.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHeatIndexRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighHeatIndex.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowWindChillRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowChill.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighMinTempRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighMinTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowMaxTempRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowMaxTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighDewPointRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighDewPoint.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowDewPointRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowDewPoint.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindGustRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighGust.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindSpeedRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighWind.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighRainRateRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighRainRate.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHourlyRainRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HourlyRain.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighDailyRainRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.DailyRain.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighRain24HourRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighRain24Hours.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighMonthlyRainRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.MonthlyRain.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHumidityRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighHumidity.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindrunRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighWindRun.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowHumidityRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowHumidity.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighPressureRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighPress.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowPressureRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowPress.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLongestDryPeriodRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LongestDryPeriod.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLongestWetPeriodRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LongestWetPeriod.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighTempRangeRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighDailyTempRange.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowTempRangeRecordSet(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowDailyTempRange.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private static string TagErrorLight(Dictionary<string, string> tagParams)
		{
			return string.IsNullOrEmpty(Cumulus.LatestError) ? "0" : "1";
		}

		private string TagtempTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.HighTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighTempTime, "HH:mm", tagParams);
		}

		private string TagtempTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.LowTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagtempMidnightTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoTodayMidnight.HighTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempMidnightTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoTodayMidnight.HighTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagtempMidnightTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoTodayMidnight.LowTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempMidnightTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoTodayMidnight.LowTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagtempMidnightRangeT(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs((station.HiLoTodayMidnight.HighTemp - station.HiLoTodayMidnight.LowTemp), tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagtemp9amTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday9am.HighTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtemp9amTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday9am.HighTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagtemp9amTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday9am.LowTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtemp9amTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday9am.LowTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagtemp9amRangeT(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs((station.HiLoToday9am.HighTemp - station.HiLoToday9am.LowTemp), tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTempAvg24Hrs(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.TempAvg24Hrs(), tagParams), tagParams, cumulus.TempDPlaces);
		}


		private string TagSolarTh(Dictionary<string, string> tagParams)
		{
			return station.HiLoToday.HighSolar.ToString();
		}

		private string TagTsolarTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighSolarTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagUvth(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighUv, tagParams, cumulus.UVDPlaces);
		}

		private string TagTuvth(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighUvTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagapptempTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.HighAppTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRCapptempTh(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagapptempTh(tagParams);
		}

		private string TagTapptempTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighAppTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagapptempTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.LowAppTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRCapptempTl(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagapptempTl(tagParams);
		}

		private string TagTapptempTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowAppTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagfeelslikeTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.HighFeelsLike, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighFeelsLikeTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagfeelslikeTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.LowFeelsLike, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowFeelsLikeTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TaghumidexTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighHumidex, tagParams, cumulus.TempDPlaces);
		}

		private string TagThumidexTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighHumidexTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagdewpointTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.HighDewPoint, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRCdewpointTh(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagdewpointTh(tagParams);
		}

		private string TagTdewpointTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighDewPointTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagdewpointTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.LowDewPoint, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRCdewpointTl(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagdewpointTl(tagParams);
		}

		private string TagTdewpointTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowDewPointTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagwchillTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.LowWindChill, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRCwchillTl(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagwchillTl(tagParams);
		}

		private string TagTwchillTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowWindChillTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagheatindexTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoToday.HighHeatIndex, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRCheatindexTh(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagheatindexTh(tagParams);
		}

		private string TagTheatindexTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighHeatIndexTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagheatindexYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.HighHeatIndex, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTheatindexYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighHeatIndexTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagpressTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.HiLoToday.HighPress, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighPressTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagpressTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.HiLoToday.LowPress, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowPressTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TaghumTh(Dictionary<string, string> tagParams)
		{
			return station.HiLoToday.HighHumidity.ToString();
		}

		private string TagThumTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighHumidityTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TaghumTl(Dictionary<string, string> tagParams)
		{
			return station.HiLoToday.LowHumidity.ToString();
		}

		private string TagThumTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowHumidityTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagwindTm(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.HiLoToday.HighWind, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagTbeaufort(Dictionary<string, string> tagParams)
		{
			return "F" + Cumulus.Beaufort(station.HiLoToday.HighWind);
		}

		private string TagTbeaufortnumber(Dictionary<string, string> tagParams)
		{
			return Cumulus.Beaufort(station.HiLoToday.HighWind);
		}

		private string TagTbeaudesc(Dictionary<string, string> tagParams)
		{
			return cumulus.BeaufortDesc(station.HiLoToday.HighWind);
		}

		private string TagTwindTm(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighWindTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagwgustTm(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.HiLoToday.HighGust, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagTwgustTm(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighGustTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagbearingTm(Dictionary<string, string> tagParams)
		{
			return station.HiLoToday.HighGustBearing.ToString();
		}

		private string TagdirectionTm(Dictionary<string, string> tagParams)
		{
			return station.CompassPoint(station.HiLoToday.HighGustBearing);
		}

		private string TagrrateTm(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.HiLoToday.HighRainRate, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTrrateTm(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighRainRateTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TaghourlyrainTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.HiLoToday.HighHourlyRain, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagThourlyrainTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighHourlyRainTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagrain24hourTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.HiLoToday.HighRain24h, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTrain24hourTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighRain24hTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TaghourlyrainYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.HiLoYest.HighHourlyRain, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagThourlyrainYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighHourlyRainTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagrain24hourYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.HiLoYest.HighRain24h, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTrain24hourYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighRain24hTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagSolarYh(Dictionary<string, string> tagParams)
		{
			return station.HiLoYest.HighSolar.ToString("F0");
		}

		private string TagTsolarYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighSolarTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagUvyh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighUv, tagParams, cumulus.UVDPlaces);
		}

		private string TagTuvyh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighUvTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagrollovertime(Dictionary<string, string> tagParams)
		{
			return cumulus.GetHourInc() switch
			{
				0 => "midnight",
				-9 => "9 am",
				-10 => "10 am",
				_ => "unknown",
			};
		}

		private string TagRg11RainToday(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RG11RainToday, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagRg11RainYest(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RG11RainYesterday, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string Tagcurrcond(Dictionary<string, string> tagParams)
		{
			return GetCurrCondText();
		}

		private string Tagcurrcondenc(Dictionary<string, string> tagParams)
		{
			return EncodeForWeb(GetCurrCondText());
		}

		private string TagcurrcondJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(GetCurrCondText());
		}


		private string TagtempYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.HighTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagtempYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.LowTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagtempMidnightYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYestMidnight.HighTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempMidnightYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYestMidnight.HighTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagtempMidnightYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYestMidnight.LowTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempMidnightYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYestMidnight.LowTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagtempMidnightRangeY(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs((station.HiLoYestMidnight.HighTemp - station.HiLoYestMidnight.LowTemp), tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string Tagtemp9amYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest9am.HighTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtemp9amYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest9am.HighTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagtemp9amYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest9am.LowTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtemp9amYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest9am.LowTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagtemp9amRangeY(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs((station.HiLoYest9am.HighTemp - station.HiLoYest9am.LowTemp), tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagapptempYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.HighAppTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTapptempYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighAppTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagapptempYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.LowAppTemp, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTapptempYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowAppTempTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagdewpointYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.HighDewPoint, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagfeelslikeYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.HighFeelsLike, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighFeelsLikeTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagfeelslikeYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.LowFeelsLike, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowFeelsLikeTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TaghumidexYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighHumidex, tagParams, cumulus.TempDPlaces);
		}

		private string TagThumidexYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighHumidexTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagTdewpointYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighDewPointTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagdewpointYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.LowDewPoint, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTdewpointYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowDewPointTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagwchillYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.HiLoYest.LowWindChill, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTwchillYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowWindChillTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagpressYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.HiLoYest.HighPress, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighPressTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagpressYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.HiLoYest.LowPress, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowPressTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TaghumYh(Dictionary<string, string> tagParams)
		{
			return station.HiLoYest.HighHumidity.ToString();
		}

		private string TagThumYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighHumidityTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TaghumYl(Dictionary<string, string> tagParams)
		{
			return station.HiLoYest.LowHumidity.ToString();
		}

		private string TagThumYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowHumidityTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagwindYm(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.HiLoYest.HighWind, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagYbeaufort(Dictionary<string, string> tagParams)
		{
			return "F" + ConvertUnits.Beaufort(station.HiLoYest.HighWind);
		}

		private string TagYbeaufortnumber(Dictionary<string, string> tagParams)
		{
			return ConvertUnits.Beaufort(station.HiLoYest.HighWind).ToString();
		}

		private string TagYbeaudesc(Dictionary<string, string> tagParams)
		{
			return cumulus.BeaufortDesc(station.HiLoYest.HighWind);
		}

		private string TagTwindYm(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighWindTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagwgustYm(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.HiLoYest.HighGust, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagTwgustYm(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighGustTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagbearingYm(Dictionary<string, string> tagParams)
		{
			return station.HiLoYest.HighGustBearing.ToString();
		}

		private string TagdirectionYm(Dictionary<string, string> tagParams)
		{
			return station.CompassPoint(station.HiLoYest.HighGustBearing);
		}

		private string TagrrateYm(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.HiLoYest.HighRainRate, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTrrateYm(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighRainRateTime, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string TagrfallY(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.RainYesterday, tagParams), tagParams, cumulus.RainDPlaces);
		}

		// all time records
		private string TagtempH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.HighTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagtempL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.LowTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagapptempH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.HighAppTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTapptempH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighAppTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagapptempL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.LowAppTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTapptempL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowAppTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagfeelslikeH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.HighFeelsLike.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighFeelsLike.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagfeelslikeL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.LowFeelsLike.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowFeelsLike.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TaghumidexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighHumidex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagThumidexH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighHumidex.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagdewpointH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.HighDewPoint.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTdewpointH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighDewPoint.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagdewpointL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.LowDewPoint.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTdewpointL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowDewPoint.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagheatindexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.HighHeatIndex.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTheatindexH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighHeatIndex.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TaggustM(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.AllTime.HighGust.Val, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagTgustM(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighGust.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagwspeedH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.AllTime.HighWind.Val, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagTwspeedH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighWind.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagwchillL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.LowChill.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTwchillL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowChill.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagrrateM(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.AllTime.HighRainRate.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTrrateM(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighRainRate.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagwindrunH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindRunUnit(station.AllTime.HighWindRun.Val, tagParams), tagParams, cumulus.WindRunDPlaces);
		}

		private string TagTwindrunH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighWindRun.Ts, cumulus.Trans.WebTagRecDate, tagParams);
		}

		private string TagrfallH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.AllTime.DailyRain.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTrfallH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.DailyRain.Ts, cumulus.Trans.WebTagRecDate, tagParams);
		}

		private string Tagr24hourH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.AllTime.HighRain24Hours.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTr24hourH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighRain24Hours.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagLongestDryPeriod(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LongestDryPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : station.AllTime.LongestDryPeriod.Val.ToString("F0");
		}

		private string TagTLongestDryPeriod(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LongestDryPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetFormattedDateTime(station.AllTime.LongestDryPeriod.Ts, cumulus.Trans.WebTagRecDryWetDate, tagParams);
		}

		private string TagLongestWetPeriod(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LongestWetPeriod.Val  < 0 ? tagParams.Get("nv") ?? "--" : station.AllTime.LongestWetPeriod.Val.ToString("F0");
		}

		private string TagTLongestWetPeriod(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LongestWetPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetFormattedDateTime(station.AllTime.LongestWetPeriod.Ts, cumulus.Trans.WebTagRecDryWetDate, tagParams);
		}

		private string TagLowDailyTempRange(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.AllTime.LowDailyTempRange.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagHighDailyTempRange(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnitAbs(station.AllTime.HighDailyTempRange.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTLowDailyTempRange(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowDailyTempRange.Ts, cumulus.Trans.WebTagRecDate, tagParams);
		}

		private string TagTHighDailyTempRange(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighDailyTempRange.Ts, cumulus.Trans.WebTagRecDate, tagParams);
		}

		private string TagrfallhH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.AllTime.HourlyRain.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTrfallhH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HourlyRain.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagrfallmH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.AllTime.MonthlyRain.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagTrfallmH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.MonthlyRain.Ts, "MMMM yyyy", tagParams);
		}

		private string TagpressH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.AllTime.HighPress.Val, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighPress.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagpressL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.AllTime.LowPress.Val, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowPress.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TaghumH(Dictionary<string, string> tagParams)
		{
			return station.AllTime.HighHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagThumH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighHumidity.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TaghumL(Dictionary<string, string> tagParams)
		{
			return station.AllTime.LowHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagThumL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowHumidity.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string Tagrecordsbegandate(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(cumulus.RecordsBeganDateTime, cumulus.Trans.WebTagGenDate, tagParams);
		}

		private string TagDaysSinceRecordsBegan(Dictionary<string, string> tagParams)
		{
			return (DateTime.Now.ToUniversalTime() - cumulus.RecordsBeganDateTime.ToUniversalTime()).Days.ToString();
		}

		private string TagmintempH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.HighMinTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTmintempH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighMinTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagmaxtempL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.AllTime.LowMaxTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagTmaxtempL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowMaxTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		// end of all-time records
		// month by month all time records
		private string TagByMonthTempH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighTemp;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthTempHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthTempL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].LowTemp;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthTempLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthTempAvg(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			double avg;
			try
			{
				avg = station.DayFile.Where(rec => rec.Date.Month == month).Average(rec => rec.AvgTemp);
			}
			catch
			{
				// no data found
				return tagParams.Get("nv") ?? "-";
			}
			return CheckRcDp(CheckTempUnit(avg, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthAppTempH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighAppTemp;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthAppTempHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighAppTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthAppTempL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].LowAppTemp;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthAppTempLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowAppTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthFeelsLikeH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighFeelsLike;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthFeelsLikeHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighFeelsLike.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthFeelsLikeL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].LowFeelsLike;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthFeelsLikeLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowFeelsLike.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthHumidexH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighHumidex, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthHumidexHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighHumidex.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthDewPointH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighDewPoint;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthDewPointHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighDewPoint.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthDewPointL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].LowDewPoint;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthDewPointLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowDewPoint.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthHeatIndexH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighHeatIndex;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthHeatIndexHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighHeatIndex.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthGustH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighGust;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckWindUnit(rec.Val, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagByMonthGustHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighGust.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthWindH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighWind;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckWindUnit(rec.Val, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagByMonthWindHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighWind.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthWChillL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].LowChill;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthWChillLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowChill.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthRainRateH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighRainRate;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckRainUnit(rec.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthRainRateHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighRainRate.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthWindRunH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighWindRun;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckWindRunUnit(rec.Val, tagParams), tagParams, cumulus.WindRunDPlaces);
		}

		private string TagByMonthWindRunHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighWindRun.Ts, cumulus.Trans.WebTagRecDate, tagParams);
		}

		private string TagByMonthDailyRainH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].DailyRain;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckRainUnit(rec.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthDailyRainHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].DailyRain.Ts, cumulus.Trans.WebTagRecDate, tagParams);
		}

		private string TagByMonthRain24HourH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighRain24Hours;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckRainUnit(rec.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthRain24HourHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighRain24Hours.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthLongestDryPeriod(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return station.MonthlyRecs[month].LongestDryPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LongestDryPeriod, tagParams, 0);
		}

		private string TagByMonthLongestDryPeriodT(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return station.MonthlyRecs[month].LongestDryPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetFormattedDateTime(station.MonthlyRecs[month].LongestDryPeriod.Ts, cumulus.Trans.WebTagRecDryWetDate, tagParams);
		}

		private string TagByMonthLongestWetPeriod(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return station.MonthlyRecs[month].LongestWetPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LongestWetPeriod, tagParams, 0);
		}

		private string TagByMonthLongestWetPeriodT(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return station.MonthlyRecs[month].LongestWetPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetFormattedDateTime(station.MonthlyRecs[month].LongestWetPeriod.Ts, cumulus.Trans.WebTagRecDryWetDate, tagParams);
		}

		private string TagByMonthLowDailyTempRange(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].LowDailyTempRange;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnitAbs(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthHighDailyTempRange(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighDailyTempRange;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnitAbs(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthLowDailyTempRangeT(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowDailyTempRange.Ts, cumulus.Trans.WebTagRecDate, tagParams);
		}

		private string TagByMonthHighDailyTempRangeT(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighDailyTempRange.Ts, cumulus.Trans.WebTagRecDate, tagParams);
		}

		private string TagByMonthHourlyRainH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HourlyRain;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckRainUnit(rec.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthHourlyRainHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HourlyRain.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthMonthlyRainH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].MonthlyRain;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckRainUnit(rec.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthMonthlyRainHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].MonthlyRain.Ts, "MMMM yyyy", tagParams);
		}

		private string TagByMonthPressH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighPress;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckPressUnit(rec.Val, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagByMonthPressHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighPress.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthPressL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].LowPress;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckPressUnit(rec.Val, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagByMonthPressLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowPress.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthHumH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighHumidity, tagParams, cumulus.HumDPlaces);
		}

		private string TagByMonthHumHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighHumidity.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthHumL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowHumidity, tagParams, cumulus.HumDPlaces);
		}

		private string TagByMonthHumLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowHumidity.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthMinTempH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].HighMinTemp;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthMinTempHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighMinTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		private string TagByMonthMaxTempL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var rec = station.MonthlyRecs[month].LowMaxTemp;
			return rec.Ts <= cumulus.defaultRecordTS ? tagParams.Get("nv") ?? "---" : CheckRcDp(CheckTempUnit(rec.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthMaxTempLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowMaxTemp.Ts, cumulus.Trans.WebTagRecTimeDate, tagParams);
		}

		// end of month-by-month all-time records

		// month averages

		private string TagMonthAvgTemp(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var val = station.GetAverageByMonth(month, d => d.AvgTemp);

			if (val < -998)
			{
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(CheckTempUnit(val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthAvgTempHigh(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var val = station.GetAverageByMonth(month, d => d.HighTemp);

			if (val < -998)
			{
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(CheckTempUnit(val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthAvgTempLow(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var val = station.GetAverageByMonth(month, d => d.LowTemp);

			if (val < -998)
			{
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(CheckTempUnit(val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthAvgTotalRainfall(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var val = station.GetAverageTotalByMonth(month, d => d.AvgTemp);

			if (val < -998)
			{
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(CheckRainUnit(val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthAvgTotalWindRun(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var val = station.GetAverageTotalByMonth(month, d => d.WindRun);

			if (val < -998)
			{
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(CheckRainUnit(val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthAvgTotalSunHours(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var val = station.GetAverageTotalByMonth(month, d => d.SunShineHours);

			if (val < -998)
			{
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(CheckRainUnit(val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthAvgTotalET(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var val = station.GetAverageTotalByMonth(month, d => d.ET);

			if (val < -998)
			{
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(CheckRainUnit(val, tagParams), tagParams, cumulus.RainDPlaces);
		}
		private string TagMonthAvgTotalChillHrs(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			var val = station.GetAverageTotalByMonth(month, d => d.ChillHours);

			if (val < -998)
			{
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(val, tagParams, 1);
		}


		private string Taggraphperiod(Dictionary<string, string> tagParams)
		{
			return cumulus.GraphHours.ToString();
		}

		private string Tagstationtype(Dictionary<string, string> tagParams)
		{
			if (cumulus.StationModel != string.Empty)
			{
				return cumulus.StationModel;
			}

			return cumulus.StationType == -1 ? "undefined" : cumulus.StationDesc[cumulus.StationType];
		}

		private string TagstationtypeJsEnc(Dictionary<string, string> tagParams)
		{
			if (cumulus.StationModel != string.Empty)
			{
				return EncodeForJs(cumulus.StationModel);
			}

			return cumulus.StationType == -1 ? "undefined" : EncodeForJs(cumulus.StationDesc[cumulus.StationType]);
		}

		private string TagstationId(Dictionary<string, string> tagParams)
		{
			return cumulus.StationType == -1 ? "undefined" : cumulus.StationType.ToString();
		}

		private string Taglatitude(Dictionary<string, string> tagParams)
		{
			var dpstr = tagParams.Get("dp");
			if (dpstr == null)
			{
				return cumulus.LatTxt;
			}

			try
			{
				return CheckRcDp(cumulus.Latitude, tagParams, 2);
			}
			catch
			{
				return "error";
			}
		}

		private string TaglatitudeJsEnc(Dictionary<string, string> tagParams)
		{
			var dpstr = tagParams.Get("dp");
			if (dpstr == null)
			{
				return EncodeForJs(cumulus.LatTxt);
			}

			try
			{
				return CheckRcDp(cumulus.Latitude, tagParams, 2);
			}
			catch
			{
				return "error";
			}
		}

		private string Taglongitude(Dictionary<string, string> tagParams)
		{
			var dpstr = tagParams.Get("dp");
			if (dpstr == null)
			{
				return cumulus.LonTxt;
			}

			try
			{
				var dp = int.Parse(dpstr);
				return CheckRcDp(cumulus.Longitude, tagParams, dp);
			}
			catch
			{
				return "error";
			}
		}

		private string TaglongitudeJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(cumulus.LonTxt);
		}

		private string Taglocation(Dictionary<string, string> tagParams)
		{
			return cumulus.LocationName;
		}

		private string Taglocationenc(Dictionary<string, string> tagParams)
		{
			return EncodeForWeb(cumulus.LocationName);
		}

		private string TaglocationJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(cumulus.LocationName);
		}


		private string Taglonglocation(Dictionary<string, string> tagParams)
		{
			return cumulus.LocationDesc;
		}

		private string Taglonglocationenc(Dictionary<string, string> tagParams)
		{
			return EncodeForWeb(cumulus.LocationDesc);
		}

		private string TaglonglocationJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(cumulus.LocationDesc);
		}

		private string Tagsunrise(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.SunRiseTime), cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagsunset(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.SunSetTime), cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagdaylength(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(cumulus.DayLength, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagdawn(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.Dawn), cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagdusk(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.Dusk), cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagdaylightlength(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(cumulus.DaylightLength, cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagisdaylight(Dictionary<string, string> tagParams)
		{
			return cumulus.IsDaylight() ? "1" : "0";
		}

		private string TagIsSunUp(Dictionary<string, string> tagParams)
		{
			return cumulus.IsSunUp() ? "1" : "0";
		}

		private string TagIsDawn(Dictionary<string, string> tagParams)
		{
			return cumulus.IsDawn() ? "1" : "0";
		}

		private string TagIsDusk(Dictionary<string, string> tagParams)
		{
			return cumulus.IsDusk() ? "1" : "0";
		}

		private string TagSensorContactLost(Dictionary<string, string> tagParams)
		{
			return station.SensorContactLost ? "1" : "0";
		}

		private string TagDataStopped(Dictionary<string, string> tagParams)
		{
			return cumulus.DataStoppedAlarm.Triggered ? "1" : "0";
		}

		private string Tagmoonrise(Dictionary<string, string> tagParams)
		{
			return cumulus.MoonRiseTime.Hours < 0 ? tagParams.Get("nv") ?? "-----" : GetFormattedDateTime(DateTime.Now.Date.AddSeconds(cumulus.MoonRiseTime.TotalSeconds), cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagmoonset(Dictionary<string, string> tagParams)
		{
			return cumulus.MoonSetTime.Hours < 0 ? tagParams.Get("nv") ?? "-----" : GetFormattedDateTime(DateTime.Now.Date.AddSeconds(cumulus.MoonSetTime.TotalSeconds), cumulus.Trans.WebTagGenTime, tagParams);
		}

		private string Tagmoonphase(Dictionary<string, string> tagParams)
		{
			return cumulus.MoonPhaseString;
		}

		private string Tagaltitude(Dictionary<string, string> tagParams)
		{
			return cumulus.Altitude + (cumulus.AltitudeInFeet ? "&nbsp;ft" : "&nbsp;m");
		}

		private string Tagaltitudenoenc(Dictionary<string, string> tagParams)
		{
			return cumulus.Altitude + (cumulus.AltitudeInFeet ? " ft" : " m");
		}

		private string Tagforum(Dictionary<string, string> tagParams)
		{
			if (string.IsNullOrEmpty(cumulus.ForumURL))
			{
				return string.Empty;
			}

			return $":<a href=\\\"{cumulus.ForumURL}\\\">forum</a>:";
		}

		private string Tagforumurl(Dictionary<string, string> tagParams)
		{
			return cumulus.ForumURL ?? string.Empty;
		}

		private string Tagwebcam(Dictionary<string, string> tagParams)
		{
			if (string.IsNullOrEmpty(cumulus.WebcamURL))
			{
				return string.Empty;
			}

			return @":<a href=\""" + cumulus.WebcamURL + @"\"">webcam</a>:";
		}
		private string Tagwebcamurl(Dictionary<string, string> tagParams)
		{
			return cumulus.WebcamURL ?? string.Empty;
		}

		private string TagEcowittCameraUrl(Dictionary<string, string> tagParams)
		{
			if (cumulus.ecowittExtra != null && cumulus.ExtraSensorUseCamera)
			{
				return cumulus.ecowittExtra.GetEcowittCameraUrl();
			}
			else if (cumulus.ecowittCloudExtra != null && cumulus.ExtraSensorUseCamera)
			{
				return cumulus.ecowittCloudExtra.GetEcowittCameraUrl();
			}
			else
			{
				return station.GetEcowittCameraUrl();
			}
		}

		private string TagEcowittVideoUrl(Dictionary<string, string> tagParams)
		{
			if (cumulus.ecowittExtra != null && cumulus.ExtraSensorUseCamera)
			{
				return cumulus.ecowittExtra.GetEcowittVideoUrl();
			}
			else if (cumulus.ecowittCloudExtra != null && cumulus.ExtraSensorUseCamera)
			{
				return cumulus.ecowittCloudExtra.GetEcowittVideoUrl();
			}
			else
			{
				return station.GetEcowittVideoUrl();
			}
		}


		private string Tagtempunit(Dictionary<string, string> tagParams)
		{
			return EncodeForWeb(cumulus.Units.TempText);
		}

		private string Tagtempunitnoenc(Dictionary<string, string> tagParams)
		{
			return cumulus.Units.TempText;
		}

		private string Tagtempunitnodeg(Dictionary<string, string> tagParams)
		{
			return cumulus.Units.TempText.Substring(1, 1);
		}

		private string Tagwindunit(Dictionary<string, string> tagParams)
		{
			return cumulus.Units.WindText;
		}

		private string Tagwindrununit(Dictionary<string, string> tagParams)
		{
			return cumulus.Units.WindRunText;
		}

		private string Tagpressunit(Dictionary<string, string> tagParams)
		{
			return cumulus.Units.PressText;
		}

		private string Tagrainunit(Dictionary<string, string> tagParams)
		{
			return cumulus.Units.RainText;
		}

		private string Taginterval(Dictionary<string, string> tagParams)
		{
			return cumulus.UpdateInterval.ToString();
		}

		private string Tagrealtimeinterval(Dictionary<string, string> tagParams)
		{
			return (cumulus.RealtimeInterval / 1000).ToString();
		}

		private string Tagversion(Dictionary<string, string> tagParams)
		{
			return cumulus.Version;
		}

		private string Tagbuild(Dictionary<string, string> tagParams)
		{
			return cumulus.Build;
		}

		private string TagNewBuildAvailable(Dictionary<string, string> tagParams)
		{
			if (int.TryParse(cumulus.Build, out var thisbuild) && int.TryParse(cumulus.LatestBuild, out var latestbuild))
			{
				return thisbuild < latestbuild ? "1" : "0";
			}
			else
			{
				return "n/a";
			}
		}

		private string TagNewBuildNumber(Dictionary<string, string> tagParams)
		{
			return cumulus.LatestBuild;
		}

		private static string Tagupdate(Dictionary<string, string> tagParams)
		{
			string dtformat = tagParams.Get("format");

			return dtformat == null ? DateTime.Now.ToString() : DateTime.Now.ToString(dtformat);
		}

		private string TagLatestNoaaMonthlyReport(Dictionary<string, string> tagParams)
		{
			return cumulus.NOAAconf.LatestMonthReport;
		}

		private string TagLatestNoaaYearlyReport(Dictionary<string, string> tagParams)
		{
			return cumulus.NOAAconf.LatestYearReport;
		}

		private string TagMoonPercent(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(cumulus.MoonPercent, tagParams, 0);
		}

		private string TagMoonPercentAbs(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(Math.Abs(cumulus.MoonPercent), tagParams, 0);
		}

		private string TagMoonAge(Dictionary<string, string> tagParams)
		{
			var tcstr = tagParams.Get("tc");

			return tcstr == "y" ? Math.Truncate(cumulus.MoonAge).ToString() : CheckRcDp(cumulus.MoonAge, tagParams, 0);
		}

		private string TagLastRainTipIso(Dictionary<string, string> tagParams)
		{
			return station.LastRainTip;
		}

		private string TagLastRainTip(Dictionary<string, string> tagParams)
		{
			try
			{
				var lastTip = DateTime.Parse(station.LastRainTip, CultureInfo.CurrentCulture);
				return GetFormattedDateTime(lastTip, "d", tagParams);
			}
			catch (Exception)
			{
				return tagParams.Get("nv") ?? "---";
			}
		}

		private string TagMinutesSinceLastRainTip(Dictionary<string, string> tagParams)
		{
			DateTime lastTip;
			try
			{
				lastTip = DateTime.Parse(station.LastRainTip, CultureInfo.CurrentCulture);
			}
			catch (Exception)
			{
				return tagParams.Get("nv") ?? "---";
			}

			return ((int) (DateTime.Now.ToUniversalTime() - lastTip.ToUniversalTime()).TotalMinutes).ToString();
		}

		private string TagRCtemp(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagtemp(tagParams);
		}

		private string TagRCtempTh(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagtempTh(tagParams);
		}

		private string TagRCtempTl(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagtempTl(tagParams);
		}

		private string TagRCintemp(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagintemp(tagParams);
		}

		private string TagRCdew(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagdew(tagParams);
		}

		private string TagRCheatindex(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagheatindex(tagParams);
		}

		private string TagRCwchill(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagwchill(tagParams);
		}

		private string TagRChum(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Taghum(tagParams);
		}

		private string TagRCinhum(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Taginhum(tagParams);
		}

		private string TagRCrfall(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagrfall(tagParams);
		}

		private string TagRCrrate(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagrrate(tagParams);
		}

		private string TagRCrrateTm(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagrrateTm(tagParams);
		}

		private string TagRCwlatest(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagwlatest(tagParams);
		}

		private string TagRCwgust(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagwgust(tagParams);
		}

		private string TagRCwspeed(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagwspeed(tagParams);
		}

		private string TagRCwgustTm(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagwgustTm(tagParams);
		}

		private string TagRCpress(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return Tagpress(tagParams);
		}

		private string TagRCpressTh(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagpressTh(tagParams);
		}

		private string TagRCpressTl(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagpressTl(tagParams);
		}

		private string TagEt(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ET, tagParams), tagParams, cumulus.RainDPlaces + 1);
		}

		private string TagAnnualEt(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.AnnualETTotal, tagParams), tagParams, cumulus.RainDPlaces + 1);
		}

		private string TagLight(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.LightValue, tagParams, 1);
		}

		private string TagUv(Dictionary<string, string> tagParams)
		{
			return station.UV.HasValue ? CheckRcDp(station.UV.Value, tagParams, cumulus.UVDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagSolarRad(Dictionary<string, string> tagParams)
		{
			return station.SolarRad.HasValue ? station.SolarRad.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagCurrentSolarMax(Dictionary<string, string> tagParams)
		{
			return station.CurrentSolarMax.ToString();
		}

		private string TagSunshineHours(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SunshineHours, tagParams, cumulus.SunshineDPlaces);
		}

		private string TagSunshineHoursMonth(Dictionary<string, string> tagParams)
		{
			var year = tagParams.Get("y");
			var month = tagParams.Get("m");
			var rel = tagParams.Get("r"); // relative month, -1, -2, etc
			DateTime start;
			DateTime end;

			if (year != null && month != null)
			{
				start = new DateTime(int.Parse(year), int.Parse(month), 1, 0, 0, 0, DateTimeKind.Local);
				end = start.AddMonths(1);
			}
			else if (rel != null)
			{
				end = DateTime.Now;
				start = new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(int.Parse(rel));
				end = start.AddMonths(1);
			}
			else
			{
				end = DateTime.Now;
				start = new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Local);
			}

			return CheckRcDp(station.DayFile.Where(rec => rec.Date >= start && rec.Date < end).Sum(rec => rec.SunShineHours < 0 ? 0 : rec.SunShineHours), tagParams, 1);
		}

		private string TagSunshineHoursYear(Dictionary<string, string> tagParams)
		{
			var year = tagParams.Get("y");
			var rel = tagParams.Get("r"); // relative year, -1, -2, etc
			DateTime start;
			DateTime end;

			if (year != null)
			{
				start = new DateTime(int.Parse(year), 1, 1, 0, 0, 0, DateTimeKind.Local);
				end = start.AddYears(1);
			}
			else if (rel != null)
			{
				end = DateTime.Now;
				start = new DateTime(end.Year, 1, 1, 0, 0, 0, DateTimeKind.Local).AddYears(int.Parse(rel));
				end = start.AddYears(1);
			}
			else
			{
				end = DateTime.Now;
				start = new DateTime(end.Year, 1, 1, 0, 0, 0, DateTimeKind.Local);
			}

			return CheckRcDp(station.DayFile.Where(x => x.Date >= start && x.Date < end).Sum(x => x.SunShineHours < 0 ? 0 : x.SunShineHours), tagParams, 1);
		}

		private string TagMonthTempAvg(Dictionary<string, string> tagParams)
		{
			var year = tagParams.Get("y");
			var month = tagParams.Get("m");
			DateTime start;
			DateTime end;
			double avg;

			try
			{
				if (year != null && month != null)
				{
					var yr = int.Parse(year);
					var mon = int.Parse(month);

					if (yr > 1970 && yr <= DateTime.Now.Year && mon > 0 && mon < 13)
					{
						start = new DateTime(int.Parse(year), int.Parse(month), 1, 0, 0, 0, DateTimeKind.Local);
						end = start.AddMonths(1);
					}
					else
					{
						return tagParams.Get("nv") ?? "-";
					}
				}
				else
				{
					end = DateTime.Now.Date;
					start = new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Local);
				}

				if (start.Date == DateTime.Now.AddHours(cumulus.GetHourInc()).Date)
				{
					// first day of the current month, there are no dayfile entries
					// so return the average temp so far today
					return Tagavgtemp(tagParams);
				}

				avg = station.DayFile.Where(x => x.Date >= start && x.Date < end).Average(rec => rec.AvgTemp);
			}
			catch
			{
				// no data found
				return tagParams.Get("nv") ?? "-";
			}

			return CheckRcDp(CheckTempUnit(avg, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearTempAvg(Dictionary<string, string> tagParams)
		{
			var year = tagParams.Get("y");
			DateTime start;
			DateTime end;

			if (year != null)
			{
				start = new DateTime(int.Parse(year), 1, 1, 0, 0, 0, DateTimeKind.Local);
				end = start.AddYears(1);
			}
			else
			{
				end = DateTime.Now.Date;
				start = new DateTime(end.Year, 1, 1, 0, 0, 0, DateTimeKind.Local);
			}

			var avg = station.DayFile.Where(x => x.Date >= start && x.Date < end).Average(rec => rec.AvgTemp);
			return CheckRcDp(CheckTempUnit(avg, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthRainfall(Dictionary<string, string> tagParams)
		{
			var year = tagParams.Get("y");
			var month = tagParams.Get("m");
			DateTime start;
			DateTime end;
			double total;

			if (year != null && month != null)
			{
				try
				{
					var yr = int.Parse(year);
					var mon = int.Parse(month);

					if (yr > 1970 && yr <= DateTime.Now.Year && mon > 0 && mon < 13)
					{
						start = new DateTime(yr, mon, 1, 0, 0, 0, DateTimeKind.Local);
						end = start.AddMonths(1);
						total = station.DayFile.Where(x => x.Date >= start && x.Date < end).Sum(rec => rec.TotalRain);
					}
					else
					{
						return tagParams.Get("nv") ?? "-";
					}
				}
				catch
				{
					// error or no data found
					return tagParams.Get("nv") ?? "-";
				}
			}
			else
			{
				total = station.RainMonth;
			}

			return CheckRcDp(CheckTempUnit(total, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagAnnualRainfall(Dictionary<string, string> tagParams)
		{
			var year = tagParams.Get("y");
			DateTime start;
			DateTime end;
			double total;

			if (year != null)
			{
				if (int.Parse(year) == DateTime.Now.Year)
				{
					total = station.RainYear;
				}
				else
				{
					start = new DateTime(int.Parse(year), 1, 1, 0, 0, 0, DateTimeKind.Local);
					end = start.AddYears(1);
					total = station.DayFile.Where(x => x.Date >= start && x.Date < end).Sum(x => x.TotalRain);
				}
			}
			else
			{
				total = station.RainYear;
			}

			return CheckRcDp(CheckRainUnit(total, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagThwIndex(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.THWIndex, tagParams), tagParams, 1);
		}

		private string TagThswIndex(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.THSWIndex, tagParams), tagParams, 1);
		}

		private string TagChillHours(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ChillHours, tagParams, 1);
		}

		private string TagChillHoursToday(Dictionary<string, string> tagParams)
		{
			if (station.YestChillHours < 0)
				return "n/a";

			// subtract today from yesterday, unless it has been reset, then its just today
			var hrs = station.ChillHours >= station.YestChillHours ? station.ChillHours - station.YestChillHours : station.ChillHours;
			return CheckRcDp(hrs, tagParams, 1);
		}

		private string TagChillHoursYesterday(Dictionary<string, string> tagParams)
		{
			var dayb4yest = DateTime.Now.Date.AddDays(-2);
			DayFileRec rec;
			try
			{
				rec = station.DayFile.Single(r => r.Date == dayb4yest);
			}
			catch
			{
				return "n/a";
			}


			double hrs;
			if (station.YestChillHours < 0)
				return "n/a";

			if (Math.Round(station.YestChillHours, 1) >= rec.ChillHours)
				hrs = station.YestChillHours - rec.ChillHours;
			else
				hrs = station.YestChillHours;

			return CheckRcDp(hrs, tagParams, 1);
		}

		private string TagYChillHours(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.YestChillHours, tagParams, 1);
		}

		private string TagYSunshineHours(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.YestSunshineHours, tagParams, cumulus.SunshineDPlaces);
		}

		private string TagIsSunny(Dictionary<string, string> tagParams)
		{
			return station.IsSunny ? "1" : "0";
		}

		private string TagIsFreezing(Dictionary<string, string> tagParams)
		{
			return ConvertUnits.UserTempToC(station.OutdoorTemperature) < 0.09 ? "1" : "0";
		}

		private string TagIsRaining(Dictionary<string, string> tagParams)
		{
			return station.IsRaining ? "1" : "0";
		}

		private string TagConsecutiveRainDays(Dictionary<string, string> tagParams)
		{
			return station.ConsecutiveRainDays.ToString();
		}

		private string TagConsecutiveDryDays(Dictionary<string, string> tagParams)
		{
			return station.ConsecutiveDryDays.ToString();
		}

		// Extra sensors
		private string TagExtraTemp1(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(1, tagParams);
		}

		private string TagExtraTemp2(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(2, tagParams);
		}

		private string TagExtraTemp3(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(3, tagParams);
		}

		private string TagExtraTemp4(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(4, tagParams);
		}

		private string TagExtraTemp5(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(5, tagParams);
		}

		private string TagExtraTemp6(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(6, tagParams);
		}

		private string TagExtraTemp7(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(7, tagParams);
		}

		private string TagExtraTemp8(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(8, tagParams);
		}

		private string TagExtraTemp9(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(9, tagParams);
		}

		private string TagExtraTemp10(Dictionary<string, string> tagParams)
		{
			return GetExtraTemp(10, tagParams);
		}

		private string GetExtraTemp(int index, Dictionary<string, string> tagParams)
		{
			return station.ExtraTemp[index].HasValue ? CheckRcDp(CheckTempUnit(station.ExtraTemp[index].Value, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagExtraDp1(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(1, tagParams);
		}

		private string TagExtraDp2(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(2, tagParams);
		}

		private string TagExtraDp3(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(3, tagParams);
		}

		private string TagExtraDp4(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(4, tagParams);
		}

		private string TagExtraDp5(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(5, tagParams);
		}

		private string TagExtraDp6(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(6, tagParams);
		}

		private string TagExtraDp7(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(7, tagParams);
		}

		private string TagExtraDp8(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(8, tagParams);
		}

		private string TagExtraDp9(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(9, tagParams);
		}

		private string TagExtraDp10(Dictionary<string, string> tagParams)
		{
			return GetExtraDP(10, tagParams);
		}

		private string GetExtraDP(int index, Dictionary<string, string> tagParams)
		{
			return station.ExtraDewPoint[index].HasValue ? CheckRcDp(CheckTempUnit(station.ExtraDewPoint[index].Value, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagExtraHum1(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(1, tagParams);
		}

		private string TagExtraHum2(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(2, tagParams);
		}

		private string TagExtraHum3(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(3, tagParams);
		}

		private string TagExtraHum4(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(4, tagParams);
		}

		private string TagExtraHum5(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(5, tagParams);
		}

		private string TagExtraHum6(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(6, tagParams);
		}

		private string TagExtraHum7(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(7, tagParams);
		}

		private string TagExtraHum8(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(8, tagParams);
		}

		private string TagExtraHum9(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(9, tagParams);
		}

		private string TagExtraHum10(Dictionary<string, string> tagParams)
		{
			return GetExtraHum(10, tagParams);
		}

		private string GetExtraHum(int index, Dictionary<string, string> tagParams)
		{
			return station.ExtraHum[index].HasValue ? CheckRcDp(station.ExtraHum[index].Value, tagParams, cumulus.HumDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilTemp1(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(1, tagParams);
		}

		private string TagSoilTemp2(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(2, tagParams);
		}

		private string TagSoilTemp3(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(3, tagParams);
		}

		private string TagSoilTemp4(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(4, tagParams);
		}

		private string TagSoilTemp5(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(5, tagParams);
		}

		private string TagSoilTemp6(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(6, tagParams);
		}

		private string TagSoilTemp7(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(7, tagParams);
		}

		private string TagSoilTemp8(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(8, tagParams);
		}

		private string TagSoilTemp9(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(9, tagParams);
		}

		private string TagSoilTemp10(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(10, tagParams);
		}

		private string TagSoilTemp11(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(11, tagParams);
		}

		private string TagSoilTemp12(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(12, tagParams);
		}

		private string TagSoilTemp13(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(13, tagParams);
		}

		private string TagSoilTemp14(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(14, tagParams);
		}

		private string TagSoilTemp15(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(15, tagParams);
		}

		private string TagSoilTemp16(Dictionary<string, string> tagParams)
		{
			return GetSoilTemp(16, tagParams);
		}

		private string GetSoilTemp(int index, Dictionary<string, string> tagParams)
		{
			return station.SoilTemp[index].HasValue ? CheckRcDp(CheckTempUnit(station.SoilTemp[index].Value, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture1(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture1.HasValue ? station.SoilMoisture1.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture2(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture2.HasValue ? station.SoilMoisture2.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture3(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture3.HasValue ? station.SoilMoisture3.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture4(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture4.HasValue ? station.SoilMoisture4.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture5(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture5.HasValue ? station.SoilMoisture5.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture6(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture6.HasValue ? station.SoilMoisture6.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture7(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture7.HasValue ? station.SoilMoisture7.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture8(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture8.HasValue ? station.SoilMoisture8.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture9(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture9.HasValue ? station.SoilMoisture9.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture10(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture10.HasValue ? station.SoilMoisture10.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture11(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture11.HasValue ? station.SoilMoisture11.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture12(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture12.HasValue ? station.SoilMoisture12.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture13(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture13.HasValue ? station.SoilMoisture13.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture14(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture14.HasValue ? station.SoilMoisture14.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture15(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture15.HasValue ? station.SoilMoisture15.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagSoilMoisture16(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture16.HasValue ? station.SoilMoisture16.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagUserTemp1(Dictionary<string, string> tagParams)
		{
			return GetUserTemp(1, tagParams);
		}

		private string TagUserTemp2(Dictionary<string, string> tagParams)
		{
			return GetUserTemp(2, tagParams);
		}

		private string TagUserTemp3(Dictionary<string, string> tagParams)
		{
			return GetUserTemp(3, tagParams);
		}

		private string TagUserTemp4(Dictionary<string, string> tagParams)
		{
			return GetUserTemp(4, tagParams);
		}

		private string TagUserTemp5(Dictionary<string, string> tagParams)
		{
			return GetUserTemp(5, tagParams);
		}

		private string TagUserTemp6(Dictionary<string, string> tagParams)
		{
			return GetUserTemp(6, tagParams);
		}

		private string TagUserTemp7(Dictionary<string, string> tagParams)
		{
			return GetUserTemp(7, tagParams);
		}

		private string TagUserTemp8(Dictionary<string, string> tagParams)
		{
			return GetUserTemp(8, tagParams);
		}

		private string GetUserTemp(int index, Dictionary<string, string> tagParams)
		{
			return station.UserTemp[index].HasValue ? CheckRcDp(CheckTempUnit(station.UserTemp[index].Value, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagLaserDist1(Dictionary<string, string> tagParams)
		{
			return GetLaserDist(1, tagParams);
		}

		private string TagLaserDist2(Dictionary<string, string> tagParams)
		{
			return GetLaserDist(2, tagParams);
		}

		private string TagLaserDist3(Dictionary<string, string> tagParams)
		{
			return GetLaserDist(3, tagParams);
		}

		private string TagLaserDist4(Dictionary<string, string> tagParams)
		{
			return GetLaserDist(4, tagParams);
		}

		private string GetLaserDist(int index, Dictionary<string, string> tagParams)
		{
			return station.LaserDist[index].HasValue ? CheckRcDp(station.LaserDist[index].Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}


		private string TagLaserDepth1(Dictionary<string, string> tagParams)
		{
			return GetLaserDepth(1, tagParams);
		}

		private string TagLaserDepth2(Dictionary<string, string> tagParams)
		{
			return GetLaserDepth(2, tagParams);
		}

		private string TagLaserDepth3(Dictionary<string, string> tagParams)
		{
			return GetLaserDepth(3, tagParams);
		}

		private string TagLaserDepth4(Dictionary<string, string> tagParams)
		{
			return GetLaserDepth(4, tagParams);
		}

		private string GetLaserDepth(int index, Dictionary<string, string> tagParams)
		{
			return station.LaserDepth[index].HasValue ? CheckRcDp(station.LaserDepth[index].Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagSnowAcc24h1(Dictionary<string, string> tagParams)
		{
			return GetSnowAcc24h(1, tagParams);
		}

		private string TagSnowAcc24h2(Dictionary<string, string> tagParams)
		{
			return GetSnowAcc24h(2, tagParams);
		}

		private string TagSnowAcc24h3(Dictionary<string, string> tagParams)
		{
			return GetSnowAcc24h(3, tagParams);
		}

		private string TagSnowAcc24h4(Dictionary<string, string> tagParams)
		{
			return GetSnowAcc24h(4, tagParams);
		}

		private string GetSnowAcc24h(int index,  Dictionary<string, string> tagParams)
		{
			return station.Snow24h[index].HasValue ? CheckRcDp(station.Snow24h[index].Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagSnowAccSeason(Dictionary<string, string> tagParams)
		{
			double? res;
			try
			{
				string startDate, endDate;

				var year = tagParams.Get("y");
				int yr;

				if (year == null)
				{
					var now = DateTime.Now;

					if (DateTime.Now.Month >= cumulus.SnowSeasonStart)
					{
						yr = now.Year;
					}
					else
					{
						yr = now.Year - 1;
					}
				}
				else
				{
					yr = int.Parse(year);
				}

				startDate = yr + "-" + cumulus.SnowSeasonStart.ToString("D2") + "-01";
				endDate = (yr + 1) + "-" + cumulus.SnowSeasonStart.ToString("D2") + "-01";

				var result = cumulus.DiaryDB.ExecuteScalar<double?>("SELECT SUM(Snow24h) FROM DiaryData WHERE Date >= ? AND Date < ?", startDate, endDate);

				res = result;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("Error reading diary database: " + e.Message);
				res = null;
			}

			return res.HasValue ? CheckRcDp(res.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagSnowAccSeason1(Dictionary<string, string> tagParams)
		{
			return GetSnowAccSeason(1, tagParams);
		}

		private string TagSnowAccSeason2(Dictionary<string, string> tagParams)
		{
			return GetSnowAccSeason(2, tagParams);
		}

		private string TagSnowAccSeason3(Dictionary<string, string> tagParams)
		{
			return GetSnowAccSeason(3, tagParams);
		}

		private string TagSnowAccSeason4(Dictionary<string, string> tagParams)
		{
			return GetSnowAccSeason(4, tagParams);
		}

		private string GetSnowAccSeason(int index, Dictionary<string, string> tagParams)
		{
			return station.SnowSeason[index].HasValue ? CheckRcDp(station.SnowSeason[index].Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}


		private string TagAirQuality1(Dictionary<string, string> tagParams)
		{
			return station.AirQuality1.HasValue ? CheckRcDp(station.AirQuality1.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQuality2(Dictionary<string, string> tagParams)
		{
			return station.AirQuality2.HasValue ? CheckRcDp(station.AirQuality2.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQuality3(Dictionary<string, string> tagParams)
		{
			return station.AirQuality3.HasValue ? CheckRcDp(station.AirQuality3.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQuality4(Dictionary<string, string> tagParams)
		{
			return station.AirQuality4.HasValue ? CheckRcDp(station.AirQuality4.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityAvg1(Dictionary<string, string> tagParams)
		{
			return station.AirQualityAvg1.HasValue ? CheckRcDp(station.AirQualityAvg1.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityAvg2(Dictionary<string, string> tagParams)
		{
			return station.AirQualityAvg2.HasValue ? CheckRcDp(station.AirQualityAvg2.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityAvg3(Dictionary<string, string> tagParams)
		{
			return station.AirQualityAvg3.HasValue ? CheckRcDp(station.AirQualityAvg3.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityAvg4(Dictionary<string, string> tagParams)
		{
			return station.AirQualityAvg4.HasValue ? CheckRcDp(station.AirQualityAvg4.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityIdx1(Dictionary<string, string> tagParams)
		{
			return station.AirQualityIdx1.HasValue ? CheckRcDp(station.AirQualityIdx1.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityIdx2(Dictionary<string, string> tagParams)
		{
			return station.AirQualityIdx2.HasValue ? CheckRcDp(station.AirQualityIdx2.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityIdx3(Dictionary<string, string> tagParams)
		{
			return station.AirQualityIdx3.HasValue ? CheckRcDp(station.AirQualityIdx3.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityIdx4(Dictionary<string, string> tagParams)
		{
			return station.AirQualityIdx4.HasValue ? CheckRcDp(station.AirQualityIdx4.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityAvgIdx1(Dictionary<string, string> tagParams)
		{
			return station.AirQualityAvgIdx1.HasValue ? CheckRcDp(station.AirQualityAvgIdx1.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityAvgIdx2(Dictionary<string, string> tagParams)
		{
			return station.AirQualityAvgIdx2.HasValue ? CheckRcDp(station.AirQualityAvgIdx2.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityAvgIdx3(Dictionary<string, string> tagParams)
		{
			return station.AirQualityAvgIdx3.HasValue ? CheckRcDp(station.AirQualityAvgIdx3.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagAirQualityAvgIdx4(Dictionary<string, string> tagParams)
		{
			return station.AirQualityAvgIdx4.HasValue ? CheckRcDp(station.AirQualityAvgIdx4.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCo2(Dictionary<string, string> tagParams)
		{
			return station.CO2.HasValue ? station.CO2.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_24h(Dictionary<string, string> tagParams)
		{
			return station.CO2_24h.HasValue ? station.CO2_24h.ToString() : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm2p5(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm2p5.HasValue ? CheckRcDp(station.CO2_pm2p5.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm2p5_24h(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm2p5_24h.HasValue ? CheckRcDp(station.CO2_pm2p5_24h.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm10(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm10.HasValue ? CheckRcDp(station.CO2_pm10.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm10_24h(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm10_24h.HasValue ? CheckRcDp(station.CO2_pm10_24h.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_temp(Dictionary<string, string> tagParams)
		{
			return station.CO2_temperature.HasValue ? CheckRcDp(CheckTempUnit(station.CO2_temperature.Value, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_hum(Dictionary<string, string> tagParams)
		{
			return station.CO2_humidity.HasValue ? CheckRcDp(station.CO2_humidity.Value, tagParams, cumulus.HumDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm1(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm1.HasValue ? CheckRcDp(station.CO2_pm1.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm1_24h(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm1_24h.HasValue ? CheckRcDp(station.CO2_pm1_24h.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm4(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm4.HasValue ? CheckRcDp(station.CO2_pm4.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm4_24h(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm4_24h.HasValue ? CheckRcDp(station.CO2_pm4_24h.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm2p5_aqi(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm2p5_aqi.HasValue ? CheckRcDp(station.CO2_pm2p5_aqi.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm2p5_24h_aqi(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm2p5_24h_aqi.HasValue ? CheckRcDp(station.CO2_pm2p5_24h_aqi.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm10_aqi(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm10_aqi.HasValue ? CheckRcDp(station.CO2_pm10_aqi.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagCO2_pm10_24h_aqi(Dictionary<string, string> tagParams)
		{
			return station.CO2_pm10_24h_aqi.HasValue ? CheckRcDp(station.CO2_pm10_24h_aqi.Value, tagParams, cumulus.AirQualityDPlaces) : tagParams.Get("nv") ?? "-";
		}

		private string TagLeakSensor1(Dictionary<string, string> tagParams)
		{
			return station.LeakSensor1.ToString();
		}

		private string TagLeakSensor2(Dictionary<string, string> tagParams)
		{
			return station.LeakSensor2.ToString();
		}

		private string TagLeakSensor3(Dictionary<string, string> tagParams)
		{
			return station.LeakSensor3.ToString();
		}

		private string TagLeakSensor4(Dictionary<string, string> tagParams)
		{
			return station.LeakSensor4.ToString();
		}

		private string TagLightningDistance(Dictionary<string, string> tagParams)
		{
			return station.LightningDistance < 0 ? tagParams.Get("nv") ?? "--" : CheckRcDp(station.LightningDistance, tagParams, cumulus.WindRunDPlaces);
		}

		private string TagLightningTime(Dictionary<string, string> tagParams)
		{
			return DateTime.Equals(station.LightningTime, DateTime.MinValue) ? tagParams.Get("nv") ?? "---" : GetFormattedDateTime(station.LightningTime, "t", tagParams);
		}

		private string TagLightningStrikesToday(Dictionary<string, string> tagParams)
		{
			return station.LightningStrikesToday.ToString();
		}

		private string TagLeafWetness1(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness1.HasValue ? CheckRcDp(station.LeafWetness1.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagLeafWetness2(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness2.HasValue ? CheckRcDp(station.LeafWetness2.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagLeafWetness3(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness3.HasValue ? CheckRcDp(station.LeafWetness3.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagLeafWetness4(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness4.HasValue ? CheckRcDp(station.LeafWetness4.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagLeafWetness5(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness5.HasValue ? CheckRcDp(station.LeafWetness5.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagLeafWetness6(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness6.HasValue ? CheckRcDp(station.LeafWetness6.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagLeafWetness7(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness7.HasValue ? CheckRcDp(station.LeafWetness7.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}

		private string TagLeafWetness8(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness8.HasValue ? CheckRcDp(station.LeafWetness8.Value, tagParams, 1) : tagParams.Get("nv") ?? "-";
		}


		private string TagVapourPressDeficit(Dictionary<string, string> tagParams)
		{
			int sensor = 0;
			if (int.TryParse(tagParams.Get("sensor"), out int val))
			{
				sensor = val;
			}

			var vpd = station.VapourPressureDeficit(sensor);

			return vpd.HasValue ? CheckRcDp(CheckPressUnit(vpd.Value, tagParams), tagParams, cumulus.PressDPlaces) : tagParams.Get("nv") ?? "-";
		}


		// Alarms
		private string TagLowTempAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.LowTempAlarm.Enabled)
			{
				return cumulus.LowTempAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighTempAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.HighTempAlarm.Enabled)
			{
				return cumulus.HighTempAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagTempChangeUpAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.TempChangeAlarm.Enabled)
			{
				return cumulus.TempChangeAlarm.UpTriggered ? "1" : "0";
			}

			return "0";
		}

		private string TagTempChangeDownAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.TempChangeAlarm.Enabled)
			{
				return cumulus.TempChangeAlarm.DownTriggered ? "1" : "0";
			}

			return "0";
		}

		private string TagLowPressAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.LowPressAlarm.Enabled)
			{
				return cumulus.LowPressAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighPressAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.HighPressAlarm.Enabled)
			{
				return cumulus.HighPressAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagPressChangeUpAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.PressChangeAlarm.Enabled)
			{
				return cumulus.PressChangeAlarm.UpTriggered ? "1" : "0";
			}

			return "0";
		}

		private string TagPressChangeDownAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.PressChangeAlarm.Enabled)
			{
				return cumulus.PressChangeAlarm.DownTriggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighRainTodayAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.HighRainTodayAlarm.Enabled)
			{
				return cumulus.HighRainTodayAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighRainRateAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.HighRainRateAlarm.Enabled)
			{
				return cumulus.HighRainRateAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagIsRainingAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.IsRainingAlarm.Enabled)
			{
				return cumulus.IsRainingAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighWindGustAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.HighGustAlarm.Enabled)
			{
				return cumulus.HighGustAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighWindSpeedAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.HighWindAlarm.Enabled)
			{
				return cumulus.HighWindAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagBatteryLowAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.BatteryLowAlarm.Enabled)
			{
				return cumulus.BatteryLowAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagDataSpikeAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.SpikeAlarm.Enabled)
			{
				return cumulus.SpikeAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagUpgradeAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.UpgradeAlarm.Enabled)
			{
				return cumulus.UpgradeAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagFirmwareAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.FirmwareAlarm.Enabled)
			{
				return cumulus.FirmwareAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagNewRecordAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.NewRecordAlarm.Enabled)
			{
				return cumulus.NewRecordAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagNewRecordAlarmMessage(Dictionary<string, string> tagParams)
		{
			return cumulus.NewRecordAlarm.LastMessage;
		}


		private string TagMySqlUploadAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.MySqlUploadAlarm.Enabled)
			{
				return cumulus.MySqlUploadAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHttpUploadAlarm(Dictionary<string, string> tagParams)
		{
			if (cumulus.ThirdPartyAlarm.Enabled)
			{
				return cumulus.ThirdPartyAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		// Monthly highs and lows - values
		private string TagMonthTempH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.HighTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthTempL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.LowTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthHeatIndexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.HighHeatIndex.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthWChillL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.LowChill.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthAppTempH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.HighAppTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthAppTempL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.LowAppTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthFeelsLikeH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.HighFeelsLike.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthFeelsLikeL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.LowFeelsLike.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthHumidexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighHumidex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthDewPointH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.HighDewPoint.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthDewPointL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisMonth.LowDewPoint.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthMinTempH(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.HighMinTemp.Val > -999 ? CheckRcDp(CheckTempUnit(station.ThisMonth.HighMinTemp.Val, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "--";
		}

		private string TagMonthMaxTempL(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LowMaxTemp.Val < 999 ? CheckRcDp(CheckTempUnit(station.ThisMonth.LowMaxTemp.Val, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "--";
		}

		private string TagMonthPressH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.ThisMonth.HighPress.Val, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagMonthPressL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.ThisMonth.LowPress.Val, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagMonthHumH(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.HighHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagMonthHumL(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LowHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagMonthGustH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.ThisMonth.HighGust.Val, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagMonthWindH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.ThisMonth.HighWind.Val, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagMonthWindRunH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindRunUnit(station.ThisMonth.HighWindRun.Val, tagParams), tagParams, cumulus.WindRunDPlaces);
		}

		private string TagMonthRainRateH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisMonth.HighRainRate.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthHourlyRainH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisMonth.HourlyRain.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthDailyRainH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisMonth.DailyRain.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthRain24HourH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisMonth.HighRain24Hours.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthLongestDryPeriod(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LongestDryPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : station.ThisMonth.LongestDryPeriod.Val.ToString();
		}

		private string TagMonthLongestWetPeriod(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LongestWetPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : station.ThisMonth.LongestWetPeriod.Val.ToString();
		}

		private string TagMonthHighDailyTempRange(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.HighDailyTempRange.Val > -999 ? CheckRcDp(CheckTempUnitAbs(station.ThisMonth.HighDailyTempRange.Val, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "--";
		}

		private string TagMonthLowDailyTempRange(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LowDailyTempRange.Val < 999 ? CheckRcDp(CheckTempUnitAbs(station.ThisMonth.LowDailyTempRange.Val, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "--";
		}

		// Monthly highs and lows - times
		private string TagMonthTempHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighTemp.Ts, "t", tagParams);
		}

		private string TagMonthTempLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowTemp.Ts, "t", tagParams);
		}

		private string TagMonthHeatIndexHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHeatIndex.Ts, "t", tagParams);
		}

		private string TagMonthWChillLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowChill.Ts, "t", tagParams);
		}

		private string TagMonthAppTempHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighAppTemp.Ts, "t", tagParams);
		}

		private string TagMonthAppTempLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowAppTemp.Ts, "t", tagParams);
		}

		private string TagMonthFeelsLikeHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighFeelsLike.Ts, "t", tagParams);
		}

		private string TagMonthFeelsLikeLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowFeelsLike.Ts, "t", tagParams);
		}

		private string TagMonthHumidexHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHumidex.Ts, "t", tagParams);
		}

		private string TagMonthDewPointHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighDewPoint.Ts, "t", tagParams);
		}

		private string TagMonthDewPointLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowDewPoint.Ts, "t", tagParams);
		}

		private string TagMonthPressHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighPress.Ts, "t", tagParams);
		}

		private string TagMonthPressLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowPress.Ts, "t", tagParams);
		}

		private string TagMonthHumHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHumidity.Ts, "t", tagParams);
		}

		private string TagMonthHumLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowHumidity.Ts, "t", tagParams);
		}

		private string TagMonthGustHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighGust.Ts, "t", tagParams);
		}

		private string TagMonthWindHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighWind.Ts, "t", tagParams);
		}

		private string TagMonthRainRateHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighRainRate.Ts, "t", tagParams);
		}

		private string TagMonthHourlyRainHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HourlyRain.Ts, "t", tagParams);
		}

		private string TagMonthRain24HourHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighRain24Hours.Ts, "t", tagParams);
		}

		// Monthly highs and lows - dates
		private string TagMonthTempHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthTempLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHeatIndexHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHeatIndex.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthWChillLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowChill.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthAppTempHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighAppTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthAppTempLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowAppTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthFeelsLikeHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighFeelsLike.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthFeelsLikeLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowFeelsLike.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHumidexHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHumidex.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthDewPointHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighDewPoint.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthDewPointLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowDewPoint.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthMinTempHd(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.HighMinTemp.Val > -999 ? GetFormattedDateTime(station.ThisMonth.HighMinTemp.Ts, "dd MMMM", tagParams) : tagParams.Get("nv") ?? "---";
		}

		private string TagMonthMaxTempLd(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LowMaxTemp.Val < 999 ? GetFormattedDateTime(station.ThisMonth.LowMaxTemp.Ts, "dd MMMM", tagParams) : tagParams.Get("nv") ?? "---";
		}

		private string TagMonthPressHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighPress.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthPressLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowPress.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHumHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHumidity.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHumLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowHumidity.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthGustHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighGust.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthWindHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighWind.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthRainRateHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighRainRate.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHourlyRainHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HourlyRain.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthRain24HourHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighRain24Hours.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHighDailyTempRangeD(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.HighDailyTempRange.Val < 999 ? GetFormattedDateTime(station.ThisMonth.HighDailyTempRange.Ts, "dd MMMM", tagParams) : tagParams.Get("nv") ?? "------";
		}

		private string TagMonthLowDailyTempRangeD(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LowDailyTempRange.Val > -999 ? GetFormattedDateTime(station.ThisMonth.LowDailyTempRange.Ts, "dd MMMM", tagParams) : tagParams.Get("nv") ?? "------";
		}

		private string TagMonthWindRunHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighWindRun.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthDailyRainHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.DailyRain.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthLongestDryPeriodD(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LongestDryPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetFormattedDateTime(station.ThisMonth.LongestDryPeriod.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthLongestWetPeriodD(Dictionary<string, string> tagParams)
		{
			return station.ThisMonth.LongestWetPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetFormattedDateTime(station.ThisMonth.LongestWetPeriod.Ts, "dd MMMM", tagParams);
		}

		// Yearly highs and lows - values
		private string TagYearTempH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.HighTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearTempL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.LowTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearHeatIndexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.HighHeatIndex.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearWChillL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.LowChill.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearAppTempH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.HighAppTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearAppTempL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.LowAppTemp.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearFeelsLikeH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.HighFeelsLike.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearFeelsLikeL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.LowFeelsLike.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearHumidexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighHumidex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearDewPointH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.HighDewPoint.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearDewPointL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckTempUnit(station.ThisYear.LowDewPoint.Val, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagYearMinTempH(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.HighMinTemp.Val > -999 ? CheckRcDp(CheckTempUnit(station.ThisYear.HighMinTemp.Val, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "--";
		}

		private string TagYearMaxTempL(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LowMaxTemp.Val < 999 ? CheckRcDp(CheckTempUnit(station.ThisYear.LowMaxTemp.Val, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "--";
		}

		private string TagYearPressH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.ThisYear.HighPress.Val, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagYearPressL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckPressUnit(station.ThisYear.LowPress.Val, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagYearHumH(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.HighHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagYearHumL(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LowHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagYearGustH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.ThisYear.HighGust.Val, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagYearWindH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindUnit(station.ThisYear.HighWind.Val, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagYearWindRunH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckWindRunUnit(station.ThisYear.HighWindRun.Val, tagParams), tagParams, cumulus.WindRunDPlaces);
		}

		private string TagYearRainRateH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisYear.HighRainRate.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagYearHourlyRainH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisYear.HourlyRain.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagYearDailyRainH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisYear.DailyRain.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagYearRain24HourH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisYear.HighRain24Hours.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagYearLongestDryPeriod(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LongestDryPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : station.ThisYear.LongestDryPeriod.Val.ToString();
		}

		private string TagYearLongestWetPeriod(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LongestWetPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : station.ThisYear.LongestWetPeriod.Val.ToString();
		}

		private string TagYearHighDailyTempRange(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.HighDailyTempRange.Val > -999 ? CheckRcDp(CheckTempUnitAbs(station.ThisYear.HighDailyTempRange.Val, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "--";
		}

		private string TagYearLowDailyTempRange(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LowDailyTempRange.Val < 999 ? CheckRcDp(CheckTempUnitAbs(station.ThisYear.LowDailyTempRange.Val, tagParams), tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "--";
		}

		private string TagYearMonthlyRainH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(CheckRainUnit(station.ThisYear.MonthlyRain.Val, tagParams), tagParams, cumulus.RainDPlaces);
		}

		// Yearly highs and lows - times
		private string TagYearTempHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighTemp.Ts, "t", tagParams);
		}

		private string TagYearTempLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowTemp.Ts, "t", tagParams);
		}

		private string TagYearHeatIndexHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHeatIndex.Ts, "t", tagParams);
		}

		private string TagYearWChillLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowChill.Ts, "t", tagParams);
		}

		private string TagYearAppTempHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighAppTemp.Ts, "t", tagParams);
		}

		private string TagYearAppTempLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowAppTemp.Ts, "t", tagParams);
		}

		private string TagYearFeelsLikeHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighFeelsLike.Ts, "t", tagParams);
		}

		private string TagYearFeelsLikeLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowFeelsLike.Ts, "t", tagParams);
		}

		private string TagYearHumidexHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHumidex.Ts, "t", tagParams);
		}
		private string TagYearDewPointHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighDewPoint.Ts, "t", tagParams);
		}

		private string TagYearDewPointLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowDewPoint.Ts, "t", tagParams);
		}

		private string TagYearPressHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighPress.Ts, "t", tagParams);
		}

		private string TagYearPressLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowPress.Ts, "t", tagParams);
		}

		private string TagYearHumHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHumidity.Ts, "t", tagParams);
		}

		private string TagYearHumLt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowHumidity.Ts, "t", tagParams);
		}

		private string TagYearGustHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighGust.Ts, "t", tagParams);
		}

		private string TagYearWindHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighWind.Ts, "t", tagParams);
		}

		private string TagYearRainRateHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighRainRate.Ts, "t", tagParams);
		}

		private string TagYearHourlyRainHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HourlyRain.Ts, "t", tagParams);
		}

		private string TagYearRain24HourHt(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighRain24Hours.Ts, "t", tagParams);
		}

		// Yearly highs and lows - dates
		private string TagYearTempHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagYearTempLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHeatIndexHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHeatIndex.Ts, "dd MMMM", tagParams);
		}

		private string TagYearWChillLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowChill.Ts, "dd MMMM", tagParams);
		}

		private string TagYearAppTempHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighAppTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagYearAppTempLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowAppTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagYearFeelsLikeHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighFeelsLike.Ts, "dd MMMM", tagParams);
		}

		private string TagYearFeelsLikeLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowFeelsLike.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHumidexHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHumidex.Ts, "dd MMMM", tagParams);
		}

		private string TagYearDewPointHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighDewPoint.Ts, "dd MMMM", tagParams);
		}

		private string TagYearDewPointLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowDewPoint.Ts, "dd MMMM", tagParams);
		}

		private string TagYearMinTempHd(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.HighMinTemp.Val > -999 ? GetFormattedDateTime(station.ThisYear.HighMinTemp.Ts, "dd MMMM", tagParams) : tagParams.Get("nv") ?? "---";
		}

		private string TagYearMaxTempLd(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LowMaxTemp.Val < 999 ? GetFormattedDateTime(station.ThisYear.LowMaxTemp.Ts, "dd MMMM", tagParams) : tagParams.Get("nv") ?? "---";
		}

		private string TagYearPressHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighPress.Ts, "dd MMMM", tagParams);
		}

		private string TagYearPressLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowPress.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHumHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHumidity.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHumLd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowHumidity.Ts, "dd MMMM", tagParams);
		}

		private string TagYearGustHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighGust.Ts, "dd MMMM", tagParams);
		}

		private string TagYearWindHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighWind.Ts, "dd MMMM", tagParams);
		}

		private string TagYearRainRateHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighRainRate.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHourlyRainHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HourlyRain.Ts, "dd MMMM", tagParams);
		}

		private string TagYearRain24HourHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighRain24Hours.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHighDailyTempRangeD(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.HighDailyTempRange.Val > -999 ? GetFormattedDateTime(station.ThisYear.HighDailyTempRange.Ts, "dd MMMM", tagParams) : tagParams.Get("nv") ?? "------";
		}

		private string TagYearLowDailyTempRangeD(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LowDailyTempRange.Val < 999 ? GetFormattedDateTime(station.ThisYear.LowDailyTempRange.Ts, "dd MMMM", tagParams) : tagParams.Get("nv") ?? "------";
		}

		private string TagYearWindRunHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighWindRun.Ts, "dd MMMM", tagParams);
		}

		private string TagYearDailyRainHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.DailyRain.Ts, "dd MMMM", tagParams);
		}

		private string TagYearLongestDryPeriodD(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LongestDryPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetFormattedDateTime(station.ThisYear.LongestDryPeriod.Ts, "dd MMMM", tagParams);
		}

		private string TagYearLongestWetPeriodD(Dictionary<string, string> tagParams)
		{
			return station.ThisYear.LongestWetPeriod.Val < 0 ? tagParams.Get("nv") ?? "--" : GetFormattedDateTime(station.ThisYear.LongestWetPeriod.Ts, "dd MMMM", tagParams);
		}

		private string TagYearMonthlyRainHd(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.MonthlyRain.Ts, "MMMM", tagParams);
		}

		//------------------------------------------------------------

		private string TagLastDataReadT(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.LastDataReadTimestamp.ToLocalTime(), "G", tagParams);
		}

		private static string TagLatestError(Dictionary<string, string> tagParams)
		{
			return Cumulus.LatestError;
		}

		private static string TagLatestErrorEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForWeb(Cumulus.LatestError);
		}

		private static string TagLatestErrorJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(Cumulus.LatestError);
		}

		private string TagLatestErrorDate(Dictionary<string, string> tagParams)
		{
			return Cumulus.LatestErrorTS == DateTime.MinValue ? tagParams.Get("nv") ?? "------" : GetFormattedDateTime(Cumulus.LatestErrorTS, "ddddd", tagParams);
		}

		private string TagLatestErrorTime(Dictionary<string, string> tagParams)
		{
			return Cumulus.LatestErrorTS == DateTime.MinValue ? tagParams.Get("nv") ?? "------" : GetFormattedDateTime(Cumulus.LatestErrorTS, "t", tagParams);
		}

		private static string TagOsVersion(Dictionary<string, string> tagParams)
		{
			try
			{
				return RuntimeInformation.OSDescription;
			}
			catch
			{
				return Environment.OSVersion.ToString();
			}
		}

		private static string TagOsLanguage(Dictionary<string, string> tagParams)
		{
			return CultureInfo.CurrentCulture.DisplayName;
		}

		private string TagSystemUpTime(Dictionary<string, string> tagParams)
		{
			try
			{
				double upTime = 0;
				if (cumulus.Platform[..3] == "Win")
				{
					try
					{
#pragma warning disable CA1416 // Validate platform compatibility
						cumulus.UpTime.NextValue();
						upTime = cumulus.UpTime.NextValue();
#pragma warning restore CA1416 // Validate platform compatibility
					}
					catch
					{
						// do nothing, already set to zero
					}
				}
				else if (File.Exists(@"/proc/uptime"))
				{
					var text = File.ReadAllText(@"/proc/uptime");
					var strTime = text.Split(' ')[0];
					if (!double.TryParse(strTime, out upTime))
						upTime = 0;
				}

				TimeSpan ts = TimeSpan.FromSeconds(upTime);

				return GetFormattedTimeSpan(ts, cumulus.Trans.WebTagElapsedTime, tagParams);
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error processing SystemUpTime web tag");
				cumulus.LogMessage(ex.Message);
				return "Error";
			}
		}

		private string TagProgramUpTime(Dictionary<string, string> tagParams)
		{
			TimeSpan ts = DateTime.Now.ToUniversalTime() - Program.StartTime.ToUniversalTime();
			return GetFormattedTimeSpan(ts, cumulus.Trans.WebTagElapsedTime, tagParams);
		}

		private static string TagProgramUpTimeMs(Dictionary<string, string> tagParams)
		{
			TimeSpan ts = DateTime.Now.ToUniversalTime() - Program.StartTime.ToUniversalTime();
			return ts.TotalMilliseconds.ToString("F0");
		}

		private static string TagCpuName(Dictionary<string, string> tagParams)
		{
			return "n/a";
		}

		private static string TagCpuCount(Dictionary<string, string> tagParams)
		{
			return Environment.ProcessorCount.ToString();
		}

		private static string TagMemoryStatus(Dictionary<string, string> tagParams)
		{
			return "n/a";
		}

		private static string TagDisplayModeString(Dictionary<string, string> tagParams)
		{
			return "n/a";
		}

		private static string TagAllocatedMemory(Dictionary<string, string> tagParams)
		{
			return (Environment.WorkingSet / 1048576.0).ToString("F2") + " MB";
		}

		private static string TagDiskSize(Dictionary<string, string> tagParams)
		{
			return "n/a";
		}

		private static string TagDiskFree(Dictionary<string, string> tagParams)
		{
			return "n/a";
		}

		private string TagCpuTemp(Dictionary<string, string> tagParams)
		{
			if (cumulus.CPUtemp == -999)
			{
				return tagParams.Get("nv") ?? "-";
			}
			else
			{
				return CheckRcDp(CheckTempUnit(cumulus.CPUtemp, tagParams), tagParams, cumulus.TempDPlaces, cumulus.TempFormat);
			}
		}

		private string TagDavisTotalPacketsReceived(Dictionary<string, string> tagParams)
		{
			return station.DavisTotalPacketsReceived.ToString();
		}

		private string TagDavisTotalPacketsMissed(Dictionary<string, string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisTotalPacketsMissed[tx].ToString();
		}

		private string TagDavisNumberOfResynchs(Dictionary<string, string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisNumberOfResynchs[tx].ToString();
		}

		private string TagDavisMaxInARow(Dictionary<string, string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisMaxInARow[tx].ToString();
		}

		private string TagDavisNumCrCerrors(Dictionary<string, string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisNumCRCerrors[tx].ToString();
		}

		private string TagDavisReceptionPercent(Dictionary<string, string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 1; // Only WLL uses this, default to transmitter 1
			return station.DavisReceptionPct[tx].ToString();
		}

		private string TagDavisTxRssi(Dictionary<string, string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 1; // Only WLL uses this, default to transmitter 1
			return station.DavisTxRssi[tx].ToString();
		}

		private string TagDavisFirmwareVersion(Dictionary<string, string> tagParams)
		{
			return station.DavisFirmwareVersion;
		}

		private string TagGw1000FirmwareVersion(Dictionary<string, string> tagParams)
		{
			return station.GW1000FirmwareVersion;
		}

		private static string TagGw1000Reception(Dictionary<string, string> tagParams)
		{
			var json = tagParams.Get("format") == "json";

			var retVal = new StringBuilder(json ? "{" : string.Empty);


			if (WeatherStation.SensorReception.Count > 0)
			{
				foreach (var pair in WeatherStation.SensorReception)
				{
					if (json)
					{
						retVal.Append($"\"{pair.Key}\":{pair.Value},");
					}
					else
					{
						retVal.Append($"{pair.Key}={pair.Value},");
					}
				}

				retVal.Length--;

				retVal.Append(json ? '}' : "");

				return retVal.ToString();
			}
			else
			{
				return json ? "{}" : tagParams.Get("nv") ?? "n/a";
			}
		}

		private string TagStationFreeMemory(Dictionary<string, string> tagParams)
		{
			return station.StationFreeMemory.ToString();
		}

		private string TagExtraStationFreeMemory(Dictionary<string, string> tagParams)
		{
			return station.ExtraStationFreeMemory.ToString();
		}

		private string TagStationRuntime(Dictionary<string, string> tagParams)
		{
			return station.StationRuntime.ToString();
		}

		private string Tagdailygraphperiod(Dictionary<string, string> tagparams)
		{
			return cumulus.GraphDays.ToString();
		}

		// Recent history
		private string TagRecentOutsideTemp(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select OutsideTemp from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckTempUnit(result.HasValue ? result.Value : station.OutdoorTemperature, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentWindSpeed(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select WindSpeed from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckWindUnit(result.HasValue ? result.Value : station.WindAverage, tagParams), tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagRecentWindGust(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select WindGust from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckWindUnit(result.HasValue ? result.Value : station.RecentMaxGust, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagRecentWindLatest(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select WindLatest from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckWindUnit(result.HasValue ? result.Value : station.WindLatest, tagParams), tagParams, cumulus.WindDPlaces);
		}

		private string TagRecentWindDir(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<int?>("select WindDir from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return result.HasValue ? result.ToString() : station.Bearing.ToString();
		}

		private string TagRecentWindAvgDir(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<int?>("select WindAvgDir from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return result.HasValue ? result.ToString() : station.AvgBearing.ToString();
		}

		private string TagRecentWindChill(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select WindChill from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckTempUnit(result.HasValue ? result.Value : station.WindChill, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentDewPoint(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select DewPoint from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckTempUnit(result.HasValue ? result.Value : station.OutdoorDewpoint, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentHeatIndex(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select HeatIndex from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckTempUnit(result.HasValue ? result.Value : station.HeatIndex, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentFeelsLike(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select FeelsLike from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckTempUnit(result.HasValue ? result.Value : station.FeelsLike, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentApparent(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select AppTemp from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckTempUnit(result.HasValue ? result.Value : station.ApparentTemperature, tagParams), tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentHumidex(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select Humidex from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.HasValue ? result.Value : station.Humidex, tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentHumidity(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select Humidity from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return result.HasValue ? result.Value.ToString() : station.OutdoorHumidity.ToString();
		}

		private string TagRecentPressure(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select Pressure from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckPressUnit(result.HasValue ? result.Value : station.Pressure, tagParams), tagParams, cumulus.PressDPlaces);
		}

		private string TagRecentRain(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			double? result = null;
			// pesky raincounter can reset, so first we have to test if it has.
			// get the max value
			var max = station.RecentDataDb.Query<RecentData>("select Timestamp, max(raincounter) as raincounter from RecentData where Timestamp >= ?", recentTs);
			// get the last value
			var last = station.RecentDataDb.ExecuteScalar<double?>("select raincounter from RecentData order by Timestamp Desc limit 1");

			if (max.Count == 1 && last.HasValue)
			{
				if (last < max[0].raincounter)
				{
					// Counter has reset - we assume only one reset in the period. If there is more than one then this will fail
					// First part = max_value - start_value
					var start = station.RecentDataDb.ExecuteScalar<double?>("select raincounter from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);
					result = max[0].raincounter - start;

					// Now add on any increment since the reset
					var resetval = station.RecentDataDb.ExecuteScalar<double?>("select raincounter from RecentData where Timestamp > ? order by Timestamp limit 1", max[0].Timestamp);

					result += last - resetval;
				}
				else
				{
					// No counter reset
					result = station.RecentDataDb.ExecuteScalar<double?>("select (select raincounter from RecentData order by Timestamp Desc limit 1) - raincounter from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);
				}
			}

			return CheckRcDp(CheckPressUnit(result.HasValue ? result.Value : station.RainToday, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagRecentRainToday(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select RainToday from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(CheckRainUnit(result.HasValue ? result.Value : station.RainToday, tagParams), tagParams, cumulus.RainDPlaces);
		}

		private string TagRecentSolarRad(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<int?>("select SolarRad from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			string solValue;
			if (result.HasValue)
			{
				solValue = result.Value.ToString();
			}
			else
			{
				solValue = station.SolarRad.HasValue ? station.SolarRad.ToString() : tagParams.Get("nv") ?? "-";
			}
			return solValue;
		}

		private string TagRecentUv(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select UV from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			string uvValue;
			if (result.HasValue)
			{
				uvValue = CheckRcDp(result.Value, tagParams, cumulus.UVDPlaces);
			}
			else
			{
				uvValue = station.UV.HasValue ? CheckRcDp(station.UV.Value, tagParams, cumulus.UVDPlaces) : tagParams.Get("nv") ?? "-";
			}
			return uvValue;
		}

		private string TagRecentIndoorTemp(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<double?>("select IndoorTemp from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			string indoorTempValue;
			if (result.HasValue)
			{
				indoorTempValue = CheckRcDp(result.Value, tagParams, cumulus.TempDPlaces);
			}
			else
			{
				indoorTempValue = station.IndoorTemperature.HasValue ? CheckRcDp(station.IndoorTemperature.Value, tagParams, cumulus.TempDPlaces) : tagParams.Get("nv") ?? "-";
			}
			return indoorTempValue;
		}

		private string TagRecentIndoorHumidity(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<int?>("select IndoorHumidity from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			string indoorHumidityValue;
			if (result.HasValue)
			{
				indoorHumidityValue = result.ToString();
			}
			else
			{
				indoorHumidityValue = station.IndoorHumidity.HasValue ? station.IndoorHumidity.ToString() : tagParams.Get("nv") ?? "-";
			}

			return indoorHumidityValue;
		}

		private string TagRecentTs(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.ExecuteScalar<DateTime?>("select Timestamp from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return GetFormattedDateTime(result.HasValue ? result.Value : DateTime.Now , tagParams);
		}

		// Recent history with commas replaced
		private string TagRcRecentOutsideTemp(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentOutsideTemp(tagParams);
		}

		private string TagRcRecentWindSpeed(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentWindSpeed(tagParams);
		}

		private string TagRcRecentWindGust(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentWindGust(tagParams);
		}

		private string TagRcRecentWindLatest(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentWindLatest(tagParams);
		}

		private string TagRcRecentWindChill(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentWindChill(tagParams);
		}

		private string TagRcRecentDewPoint(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentDewPoint(tagParams);
		}

		private string TagRcRecentHeatIndex(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentHeatIndex(tagParams);
		}

		private string TagRcRecentPressure(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentPressure(tagParams);
		}

		private string TagRcRecentRainToday(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentRainToday(tagParams);
		}

		private string TagRcRecentUv(Dictionary<string, string> tagParams)
		{
			tagParams["rc"] = "y";
			return TagRecentUv(tagParams);
		}

		private string TagOption_useApparent(Dictionary<string, string> tagParams)
		{
			return cumulus.DisplayOptions.UseApparent ? "1" : "0";
		}

		private string TagOption_showSolar(Dictionary<string, string> tagParams)
		{
			return cumulus.DisplayOptions.ShowSolar ? "1" : "0";
		}

		private string TagOption_showUV(Dictionary<string, string> tagParams)
		{
			return cumulus.DisplayOptions.ShowUV ? "1" : "0";
		}

		private string TagOption_showSnow(Dictionary<string, string> tagParams)
		{
			return cumulus.DisplayOptions.ShowSnow ? "1" : "0";
		}

		private string TagOption_noaaFormat(Dictionary<string, string> tagParams)
		{
			return cumulus.NOAAconf.OutputText ? "text" : "html";
		}

		private string TagMySqlRealtimeTime(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(cumulus.MySqlLastRealtimeTime, "yyyy-MM-dd HH:mm:ss", tagParams);
		}

		private string TagMySqlIntervalTime(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(cumulus.MySqlILastntervalTime, "yyyy-MM-dd HH:mm", tagParams);
		}

		private string TagQueryDayFile(Dictionary<string, string> tagParams)
		{
			var value = tagParams.Get("value");
			var function = tagParams.Get("function");
			var where = tagParams.Get("where");
			var from = tagParams.Get("from");
			var to = tagParams.Get("to");
			var showDate = tagParams.Get("showDate") == "y";
			var dateOnly = tagParams.Get("dateOnly") == "y";
			var resfunc = tagParams.Get("resFunc");

			tagParams.Add("tc", function == "count" ? "y" : "n");

			var defaultFormat = function == "count" ? "MM/yyyy" : "g";

			var ret = station.DayFileQuery.DayFile(value, function, where, from, to, resfunc);

			if (ret.value < -9998)
			{
				if (showDate)
				{
					return tagParams.Get("nv") ?? "[\"-\",\"-\"]";
				}
				else
				{
					return tagParams.Get("nv") ?? "-";
				}
			}
			else
			{
				if (showDate)
				{
					return "[\"" + CheckRcDp(ret.value, tagParams, 1) + "\",\"" + GetFormattedDateTime(ret.time, defaultFormat, tagParams) + "\"]";
				}
				else if (dateOnly)
				{
					return GetFormattedDateTime(ret.time, defaultFormat, tagParams);
				}
				else
				{
					return CheckRcDp(ret.value, tagParams, 1);
				}
			}
		}

		public void InitialiseWebtags()
		{
			// create the web tag dictionary
			webTagDictionary = new Dictionary<string, WebTagFunction>
			{
				{ "time", TagTime },
				{ "DaysSince30Dec1899", TagDaysSince30Dec1899 },
				{ "timeUTC", TagTimeUtc },
				{ "timehhmmss", TagTimehhmmss },
				{ "timeJavaScript", TagTimeJavascript },
				{ "timeUnix", TagTimeUnix },
				{ "date", TagDate },
				{ "yesterday", TagYesterday },
				{ "metdate", TagMetDate },
				{ "metdateyesterday", TagMetDateYesterday },
				{ "day", TagDay },
				{ "dayname", TagDayname },
				{ "shortdayname", TagShortdayname },
				{ "month", TagMonth },
				{ "monthname", TagMonthname },
				{ "shortmonthname", TagShortmonthname },
				{ "year", TagYear },
				{ "shortyear", TagShortyear },
				{ "hour", TagHour },
				{ "minute", TagMinute },
				{ "forecastnumber", Tagforecastnumber },
				{ "forecast", Tagforecast },
				{ "forecastenc", Tagforecastenc },
				{ "forecastJsEnc", TagforecastJsEnc },
				{ "cumulusforecast", Tagcumulusforecast },
				{ "cumulusforecastenc", Tagcumulusforecastenc },
				{ "cumulusforecastJsEnc", TagcumulusforecastJsEnc },
				{ "wsforecast", Tagwsforecast },
				{ "wsforecastenc", Tagwsforecastenc },
				{ "wsforecastJsEnc", TagwsforecastJsEnc },
				{ "temp", Tagtemp },
				{ "apptemp", Tagapptemp },
				{ "feelslike", Tagfeelsliketemp },
				{ "temprange", Tagtemprange },
				{ "temprangeY", TagtemprangeY },
				{ "temptrend", Tagtemptrend },
				{ "temptrendsigned", Tagtemptrendsigned },
				{ "temptrendtext", Tagtemptrendtext },
				{ "temptrendenglish", Tagtemptrendenglish },
				{ "TempChangeLastHour", TagTempChangeLastHour },
				{ "heatindex", Tagheatindex },
				{ "avgtemp", Tagavgtemp },
				{ "avgtempY", TagavgtempY },
				{ "hum", Taghum },
				{ "humidex", Taghumidex },
				{ "press", Tagpress },
				{ "altimeterpressure", Tagaltimeterpressure },
				{ "stationpressure", Tagstationpressure },
				{ "presstrend", Tagpresstrend },
				{ "presstrendenglish", Tagpresstrendenglish },
				{ "cloudbase", Tagcloudbase },
				{ "cloudbasevalue", Tagcloudbasevalue },
				{ "cloudbaseunit", Tagcloudbaseunit },
				{ "dew", Tagdew },
				{ "wetbulb", Tagwetbulb },
				{ "presstrendval", Tagpresstrendval },
				{ "presstrendsigned", Tagpresstrendsigned },
				{ "PressChangeLast3Hours", TagPressChangeLast3Hours },
				{ "windrunY", TagwindrunY },
				{ "domwindbearingY", TagdomwindbearingY },
				{ "domwinddirY", TagdomwinddirY },
				{ "tomorrowdaylength", Tagtomorrowdaylength },
				{ "windrun", Tagwindrun },
				{ "windrunmonth", Tagwindrunmonth },
				{ "domwindbearing", Tagdomwindbearing },
				{ "domwinddir", Tagdomwinddir },
				{ "heatdegdays", Tagheatdegdays },
				{ "heatdegdaysY", TagheatdegdaysY },
				{ "cooldegdays", Tagcooldegdays },
				{ "cooldegdaysY", TagcooldegdaysY },
				{ "wlatest", Tagwlatest },
				{ "wspeed", Tagwspeed },
				{ "currentwdir", Tagcurrentwdir },
				{ "wdir", Tagwdir },
				{ "wgust", Tagwgust },
				{ "windAvg", TagwindAvg },
				{ "windAvgY", TagwindAvgY },
				{ "wchill", Tagwchill },
				{ "rrate", Tagrrate },
				{ "StormRain", TagStormRain },
				{ "StormRainStart", TagStormRainStart },
				{ "bearing", Tagbearing },
				{ "avgbearing", Tagavgbearing },
				{ "BearingRangeFrom", TagBearingRangeFrom },
				{ "BearingRangeTo", TagBearingRangeTo },
				{ "BearingRangeFrom10", TagBearingRangeFrom10 },
				{ "BearingRangeTo10", TagBearingRangeTo10 },
				{ "beaufort", Tagbeaufort },
				{ "beaufortnumber", Tagbeaufortnumber },
				{ "beaudesc", Tagbeaudesc },
				{ "Tbeaudesc", TagTbeaudesc },
				{ "Ybeaudesc", TagYbeaudesc },
				{ "wdirdata", Tagwdirdata },
				{ "wspddata", Tagwspddata },
				{ "WindSampleCount", TagWindSampleCount },
				{ "WindRosePoints", TagWindRosePoints },
				{ "WindRoseData", TagWindRoseData },
				{ "nextwindindex", Tagnextwindindex },
				{ "rfall", Tagrfall },
				{ "ConsecutiveRainDays", TagConsecutiveRainDays },
				{ "ConsecutiveDryDays", TagConsecutiveDryDays },
				{ "rmidnight", Tagrmidnight },
				{ "rmonth", Tagrmonth },
				{ "rweek", Tagrweek },
				{ "rhour", Tagrhour },
				{ "r24hour", Tagr24Hour },
				{ "ryear", Tagryear },
				{ "inhum", Taginhum },
				{ "intemp", Tagintemp },
				{ "battery", Tagbattery },
				{ "txbattery", Tagtxbattery },
				{ "ConsoleSupplyV", TagConsoleSupplyV },
				{ "LowBatteryList", TagLowBatteryList },
				{ "MulticastBadCnt", TagMulticastBadCnt },
				{ "MulticastGoodCnt", TagMulticastGoodCnt },
				{ "MulticastGoodPct", TagMulticastGoodPct },
				{ "snowdepth", Tagsnowdepth },
				{ "snowlying", Tagsnowlying },
				{ "snowfalling", Tagsnowfalling },
				{ "snow24hr", Tagsnow24hr },
				{ "snowcomment", Tagsnowcomment },
				{ "DiaryThunder", TagDiaryThunder },
				{ "DiaryHail", TagDiaryHail },
				{ "DiaryFog", TagDiaryFog },
				{ "DiaryGales", TagDiaryGales },
				{ "newrecord", Tagnewrecord },
				{ "TempRecordSet", TagTempRecordSet },
				{ "WindRecordSet", TagWindRecordSet },
				{ "RainRecordSet", TagRainRecordSet },
				{ "HumidityRecordSet", TagHumidityRecordSet },
				{ "PressureRecordSet", TagPressureRecordSet },
				{ "HighTempRecordSet", TagHighTempRecordSet },
				{ "LowTempRecordSet", TagLowTempRecordSet },
				{ "HighAppTempRecordSet", TagHighAppTempRecordSet },
				{ "LowAppTempRecordSet", TagLowAppTempRecordSet },
				{ "HighFeelsLikeRecordSet", TagHighFeelsLikeRecordSet },
				{ "LowFeelsLikeRecordSet", TagLowFeelsLikeRecordSet },
				{ "HighHumidexRecordSet", TagHighHumidexRecordSet },
				{ "HighHeatIndexRecordSet", TagHighHeatIndexRecordSet },
				{ "LowWindChillRecordSet", TagLowWindChillRecordSet },
				{ "HighDewPointRecordSet", TagHighDewPointRecordSet },
				{ "LowDewPointRecordSet", TagLowDewPointRecordSet },
				{ "HighMinTempRecordSet", TagHighMinTempRecordSet },
				{ "LowMaxTempRecordSet", TagLowMaxTempRecordSet },
				{ "HighWindGustRecordSet", TagHighWindGustRecordSet },
				{ "HighWindSpeedRecordSet", TagHighWindSpeedRecordSet },
				{ "HighRainRateRecordSet", TagHighRainRateRecordSet },
				{ "HighHourlyRainRecordSet", TagHighHourlyRainRecordSet },
				{ "HighDailyRainRecordSet", TagHighDailyRainRecordSet },
				{ "HighRain24HourRecordSet", TagHighRain24HourRecordSet },
				{ "HighMonthlyRainRecordSet", TagHighMonthlyRainRecordSet },
				{ "HighHumidityRecordSet", TagHighHumidityRecordSet },
				{ "LowHumidityRecordSet", TagLowHumidityRecordSet },
				{ "HighPressureRecordSet", TagHighPressureRecordSet },
				{ "LowPressureRecordSet", TagLowPressureRecordSet },
				{ "HighWindrunRecordSet", TagHighWindrunRecordSet },
				{ "LongestDryPeriodRecordSet", TagLongestDryPeriodRecordSet },
				{ "LongestWetPeriodRecordSet", TagLongestWetPeriodRecordSet },
				{ "LowTempRangeRecordSet", TagLowTempRangeRecordSet },
				{ "HighTempRangeRecordSet", TagHighTempRangeRecordSet },
				{ "tempTH", TagtempTh },
				{ "TtempTH", TagTtempTh },
				{ "tempTL", TagtempTl },
				{ "TtempTL", TagTtempTl },
				{ "tempMidnightTH", TagtempMidnightTh },
				{ "TtempMidnightTH", TagTtempMidnightTh },
				{ "tempMidnightTL", TagtempMidnightTl },
				{ "TtempMidnightTL", TagTtempMidnightTl },
				{ "tempMidnightRangeT", TagtempMidnightRangeT },
				{ "temp9amTH", Tagtemp9amTh },
				{ "Ttemp9amTH", TagTtemp9amTh },
				{ "temp9amTL", Tagtemp9amTl },
				{ "Ttemp9amTL", TagTtemp9amTl },
				{ "temp9amRangeT", Tagtemp9amRangeT },
				{ "wchillTL", TagwchillTl },
				{ "TwchillTL", TagTwchillTl },
				{ "apptempTH", TagapptempTh },
				{ "TapptempTH", TagTapptempTh },
				{ "apptempTL", TagapptempTl },
				{ "TapptempTL", TagTapptempTl },
				{ "feelslikeTH", TagfeelslikeTh },
				{ "TfeelslikeTH", TagTfeelslikeTh },
				{ "feelslikeTL", TagfeelslikeTl },
				{ "TfeelslikeTL", TagTfeelslikeTl },
				{ "humidexTH", TaghumidexTh },
				{ "ThumidexTH", TagThumidexTh },
				{ "dewpointTH", TagdewpointTh },
				{ "TdewpointTH", TagTdewpointTh },
				{ "dewpointTL", TagdewpointTl },
				{ "TdewpointTL", TagTdewpointTl },
				{ "heatindexTH", TagheatindexTh },
				{ "TheatindexTH", TagTheatindexTh },
				{ "TempAvg24Hrs", TagTempAvg24Hrs },
				{ "pressTH", TagpressTh },
				{ "TpressTH", TagTpressTh },
				{ "pressTL", TagpressTl },
				{ "TpressTL", TagTpressTl },
				{ "humTH", TaghumTh },
				{ "ThumTH", TagThumTh },
				{ "humTL", TaghumTl },
				{ "ThumTL", TagThumTl },
				{ "windTM", TagwindTm },
				{ "Tbeaufort", TagTbeaufort },
				{ "Tbeaufortnumber", TagTbeaufortnumber },
				{ "TwindTM", TagTwindTm },
				{ "wgustTM", TagwgustTm },
				{ "TwgustTM", TagTwgustTm },
				{ "bearingTM", TagbearingTm },
				{ "directionTM", TagdirectionTm },
				{ "rrateTM", TagrrateTm },
				{ "TrrateTM", TagTrrateTm },
				{ "hourlyrainTH", TaghourlyrainTh },
				{ "ThourlyrainTH", TagThourlyrainTh },
				{ "rain24hourTH", Tagrain24hourTh },
				{ "Train24hourTH", TagTrain24hourTh },
				{ "hourlyrainYH", TaghourlyrainYh },
				{ "ThourlyrainYH", TagThourlyrainYh },
				{ "rain24hourYH", Tagrain24hourYh },
				{ "Train24hourYH", TagTrain24hourYh },
				{ "solarTH", TagSolarTh },
				{ "TsolarTH", TagTsolarTh },
				{ "UVTH", TagUvth },
				{ "TUVTH", TagTuvth },
				{ "rollovertime", Tagrollovertime },
				{ "currcond", Tagcurrcond },
				{ "currcondenc", Tagcurrcondenc },
				{ "currcondJsEnc", TagcurrcondJsEnc },
				{ "tempYH", TagtempYh },
				{ "TtempYH", TagTtempYh },
				{ "tempYL", TagtempYl },
				{ "TtempYL", TagTtempYl },
				{ "tempMidnightYH", TagtempMidnightYh },
				{ "TtempMidnightYH", TagTtempMidnightYh },
				{ "tempMidnightYL", TagtempMidnightYl },
				{ "TtempMidnightYL", TagTtempMidnightYl },
				{ "tempMidnightRangeY", TagtempMidnightRangeY },
				{ "temp9amYH", Tagtemp9amYh },
				{ "Ttemp9amYH", TagTtemp9amYh },
				{ "temp9amYL", Tagtemp9amYl },
				{ "Ttemp9amYL", TagTtemp9amYl },
				{ "temp9amRangeY", Tagtemp9amRangeY },
				{ "wchillYL", TagwchillYl },
				{ "TwchillYL", TagTwchillYl },
				{ "apptempYH", TagapptempYh },
				{ "TapptempYH", TagTapptempYh },
				{ "apptempYL", TagapptempYl },
				{ "TapptempYL", TagTapptempYl },
				{ "feelslikeYH", TagfeelslikeYh },
				{ "TfeelslikeYH", TagTfeelslikeYh },
				{ "feelslikeYL", TagfeelslikeYl },
				{ "TfeelslikeYL", TagTfeelslikeYl },
				{ "humidexYH", TaghumidexYh },
				{ "ThumidexYH", TagThumidexYh },
				{ "dewpointYH", TagdewpointYh },
				{ "TdewpointYH", TagTdewpointYh },
				{ "dewpointYL", TagdewpointYl },
				{ "TdewpointYL", TagTdewpointYl },
				{ "heatindexYH", TagheatindexYh },
				{ "TheatindexYH", TagTheatindexYh },
				{ "pressYH", TagpressYh },
				{ "TpressYH", TagTpressYh },
				{ "pressYL", TagpressYl },
				{ "TpressYL", TagTpressYl },
				{ "humYH", TaghumYh },
				{ "ThumYH", TagThumYh },
				{ "humYL", TaghumYl },
				{ "ThumYL", TagThumYl },
				{ "windYM", TagwindYm },
				{ "Ybeaufort", TagYbeaufort },
				{ "Ybeaufortnumber", TagYbeaufortnumber },
				{ "TwindYM", TagTwindYm },
				{ "wgustYM", TagwgustYm },
				{ "TwgustYM", TagTwgustYm },
				{ "bearingYM", TagbearingYm },
				{ "directionYM", TagdirectionYm },
				{ "rrateYM", TagrrateYm },
				{ "TrrateYM", TagTrrateYm },
				{ "rfallY", TagrfallY },
				{ "solarYH", TagSolarYh },
				{ "TsolarYH", TagTsolarYh },
				{ "UVYH", TagUvyh },
				{ "TUVYH", TagTuvyh },
				{ "tempH", TagtempH },
				{ "TtempH", TagTtempH },
				{ "tempL", TagtempL },
				{ "TtempL", TagTtempL },
				{ "apptempH", TagapptempH },
				{ "TapptempH", TagTapptempH },
				{ "apptempL", TagapptempL },
				{ "TapptempL", TagTapptempL },
				{ "feelslikeH", TagfeelslikeH },
				{ "TfeelslikeH", TagTfeelslikeH },
				{ "feelslikeL", TagfeelslikeL },
				{ "TfeelslikeL", TagTfeelslikeL },
				{ "humidexH", TaghumidexH },
				{ "ThumidexH", TagThumidexH },
				{ "dewpointH", TagdewpointH },
				{ "TdewpointH", TagTdewpointH },
				{ "dewpointL", TagdewpointL },
				{ "TdewpointL", TagTdewpointL },
				{ "heatindexH", TagheatindexH },
				{ "TheatindexH", TagTheatindexH },
				{ "gustM", TaggustM },
				{ "TgustM", TagTgustM },
				{ "wspeedH", TagwspeedH },
				{ "TwspeedH", TagTwspeedH },
				{ "windrunH", TagwindrunH },
				{ "TwindrunH", TagTwindrunH },
				{ "wchillL", TagwchillL },
				{ "TwchillL", TagTwchillL },
				{ "rrateM", TagrrateM },
				{ "TrrateM", TagTrrateM },
				{ "rfallH", TagrfallH },
				{ "TrfallH", TagTrfallH },
				{ "r24hourH", Tagr24hourH },
				{ "Tr24hourH", TagTr24hourH },
				{ "rfallhH", TagrfallhH },
				{ "TrfallhH", TagTrfallhH },
				{ "rfallmH", TagrfallmH },
				{ "TrfallmH", TagTrfallmH },
				{ "pressH", TagpressH },
				{ "TpressH", TagTpressH },
				{ "pressL", TagpressL },
				{ "TpressL", TagTpressL },
				{ "humH", TaghumH },
				{ "ThumH", TagThumH },
				{ "humL", TaghumL },
				{ "ThumL", TagThumL },
				{ "recordsbegandate", Tagrecordsbegandate },
				{ "DaysSinceRecordsBegan", TagDaysSinceRecordsBegan },
				{ "mintempH", TagmintempH },
				{ "TmintempH", TagTmintempH },
				{ "maxtempL", TagmaxtempL },
				{ "TmaxtempL", TagTmaxtempL },
				{ "LongestDryPeriod", TagLongestDryPeriod },
				{ "TLongestDryPeriod", TagTLongestDryPeriod },
				{ "LongestWetPeriod", TagLongestWetPeriod },
				{ "TLongestWetPeriod", TagTLongestWetPeriod },
				{ "LowDailyTempRange", TagLowDailyTempRange },
				{ "TLowDailyTempRange", TagTLowDailyTempRange },
				{ "HighDailyTempRange", TagHighDailyTempRange },
				{ "THighDailyTempRange", TagTHighDailyTempRange },
				{ "graphperiod", Taggraphperiod },
				{ "stationtype", Tagstationtype },
				{ "stationtypeJsEnc", TagstationtypeJsEnc },
				{ "stationId", TagstationId },
				{ "latitude", Taglatitude },
				{ "latitudeJsEnc", TaglatitudeJsEnc },
				{ "longitude", Taglongitude },
				{ "longitudeJsEnc", TaglongitudeJsEnc },
				{ "location", Taglocation },
				{ "locationenc", Taglocationenc },
				{ "locationJsEnc", TaglocationJsEnc },
				{ "longlocation", Taglonglocation },
				{ "longlocationenc", Taglonglocationenc },
				{ "longlocationJsEnc", TaglonglocationJsEnc },
				{ "sunrise", Tagsunrise },
				{ "sunset", Tagsunset },
				{ "daylength", Tagdaylength },
				{ "dawn", Tagdawn },
				{ "dusk", Tagdusk },
				{ "daylightlength", Tagdaylightlength },
				{ "isdaylight", Tagisdaylight },
				{ "IsSunUp", TagIsSunUp },
				{ "IsDawn", TagIsDawn },
				{ "IsDusk", TagIsDusk },
				{ "SensorContactLost", TagSensorContactLost },
				{ "moonrise", Tagmoonrise },
				{ "moonset", Tagmoonset },
				{ "moonphase", Tagmoonphase },
				{ "chillhours", TagChillHours },
				{ "chillhoursToday", TagChillHoursToday },
				{ "Ychillhours", TagYChillHours },
				{ "chillhoursYest", TagChillHoursYesterday },
				{ "altitude", Tagaltitude },
				{ "altitudenoenc", Tagaltitudenoenc },
				{ "forum", Tagforum },
				{ "forumurl", Tagforumurl },
				{ "webcam", Tagwebcam },
				{ "webcamurl", Tagwebcamurl },
				{ "EcowittCameraUrl", TagEcowittCameraUrl },
				{ "EcowittVideoUrl", TagEcowittVideoUrl },
				{ "tempunit", Tagtempunit },
				{ "tempunitnodeg", Tagtempunitnodeg },
				{ "tempunitnoenc", Tagtempunitnoenc },
				{ "windunit", Tagwindunit },
				{ "windrununit", Tagwindrununit },
				{ "pressunit", Tagpressunit },
				{ "rainunit", Tagrainunit },
				{ "interval", Taginterval },
				{ "realtimeinterval", Tagrealtimeinterval },
				{ "version", Tagversion },
				{ "build", Tagbuild },
				{ "NewBuildAvailable", TagNewBuildAvailable },
				{ "NewBuildNumber", TagNewBuildNumber },
				{ "update", Tagupdate },
				{ "LastRainTip", TagLastRainTip },
				{ "LastRainTipISO", TagLastRainTipIso },
				{ "MinutesSinceLastRainTip", TagMinutesSinceLastRainTip },
				{ "LastDataReadT", TagLastDataReadT },
				{ "LatestNOAAMonthlyReport", TagLatestNoaaMonthlyReport },
				{ "LatestNOAAYearlyReport", TagLatestNoaaYearlyReport },
				{ "dailygraphperiod", Tagdailygraphperiod },
				{ "RCtemp", TagRCtemp },
				{ "RCtempTH", TagRCtempTh },
				{ "RCtempTL", TagRCtempTl },
				{ "RCintemp", TagRCintemp },
				{ "RCdew", TagRCdew },
				{ "RCheatindex", TagRCheatindex },
				{ "RCwchill", TagRCwchill },
				{ "RChum", TagRChum },
				{ "RCinhum", TagRCinhum },
				{ "RCrfall", TagRCrfall },
				{ "RCrrate", TagRCrrate },
				{ "RCrrateTM", TagRCrrateTm },
				{ "RCwgust", TagRCwgust },
				{ "RCwlatest", TagRCwlatest },
				{ "RCwspeed", TagRCwspeed },
				{ "RCwgustTM", TagRCwgustTm },
				{ "RCpress", TagRCpress },
				{ "RCpressTH", TagRCpressTh },
				{ "RCpressTL", TagRCpressTl },
				{ "RCdewpointTH", TagRCdewpointTh },
				{ "RCdewpointTL", TagRCdewpointTl },
				{ "RCwchillTL", TagRCwchillTl },
				{ "RCheatindexTH", TagRCheatindexTh },
				{ "RCapptempTH", TagRCapptempTh },
				{ "RCapptempTL", TagRCapptempTl },
				{ "ET", TagEt },
				{ "AnnualET", TagAnnualEt },
				{ "UV", TagUv },
				{ "SolarRad", TagSolarRad },
				{ "Light", TagLight },
				{ "CurrentSolarMax", TagCurrentSolarMax },
				{ "SunshineHours", TagSunshineHours },
				{ "YSunshineHours", TagYSunshineHours },
				{ "SunshineHoursMonth", TagSunshineHoursMonth },
				{ "SunshineHoursYear", TagSunshineHoursYear },
				{ "IsSunny", TagIsSunny },
				{ "IsRaining", TagIsRaining },
				{ "IsFreezing", TagIsFreezing },
				{ "THWindex", TagThwIndex },
				{ "THSWindex", TagThswIndex },
				{ "ExtraTemp1", TagExtraTemp1 },
				{ "ExtraTemp2", TagExtraTemp2 },
				{ "ExtraTemp3", TagExtraTemp3 },
				{ "ExtraTemp4", TagExtraTemp4 },
				{ "ExtraTemp5", TagExtraTemp5 },
				{ "ExtraTemp6", TagExtraTemp6 },
				{ "ExtraTemp7", TagExtraTemp7 },
				{ "ExtraTemp8", TagExtraTemp8 },
				{ "ExtraTemp9", TagExtraTemp9 },
				{ "ExtraTemp10", TagExtraTemp10 },
				{ "ExtraDP1", TagExtraDp1 },
				{ "ExtraDP2", TagExtraDp2 },
				{ "ExtraDP3", TagExtraDp3 },
				{ "ExtraDP4", TagExtraDp4 },
				{ "ExtraDP5", TagExtraDp5 },
				{ "ExtraDP6", TagExtraDp6 },
				{ "ExtraDP7", TagExtraDp7 },
				{ "ExtraDP8", TagExtraDp8 },
				{ "ExtraDP9", TagExtraDp9 },
				{ "ExtraDP10", TagExtraDp10 },
				{ "ExtraHum1", TagExtraHum1 },
				{ "ExtraHum2", TagExtraHum2 },
				{ "ExtraHum3", TagExtraHum3 },
				{ "ExtraHum4", TagExtraHum4 },
				{ "ExtraHum5", TagExtraHum5 },
				{ "ExtraHum6", TagExtraHum6 },
				{ "ExtraHum7", TagExtraHum7 },
				{ "ExtraHum8", TagExtraHum8 },
				{ "ExtraHum9", TagExtraHum9 },
				{ "ExtraHum10", TagExtraHum10 },
				{ "SoilTemp1", TagSoilTemp1 },
				{ "SoilTemp2", TagSoilTemp2 },
				{ "SoilTemp3", TagSoilTemp3 },
				{ "SoilTemp4", TagSoilTemp4 },
				{ "SoilTemp5", TagSoilTemp5 },
				{ "SoilTemp6", TagSoilTemp6 },
				{ "SoilTemp7", TagSoilTemp7 },
				{ "SoilTemp8", TagSoilTemp8 },
				{ "SoilTemp9", TagSoilTemp9 },
				{ "SoilTemp10", TagSoilTemp10 },
				{ "SoilTemp11", TagSoilTemp11 },
				{ "SoilTemp12", TagSoilTemp12 },
				{ "SoilTemp13", TagSoilTemp13 },
				{ "SoilTemp14", TagSoilTemp14 },
				{ "SoilTemp15", TagSoilTemp15 },
				{ "SoilTemp16", TagSoilTemp16 },
				{ "SoilMoisture1", TagSoilMoisture1 },
				{ "SoilMoisture2", TagSoilMoisture2 },
				{ "SoilMoisture3", TagSoilMoisture3 },
				{ "SoilMoisture4", TagSoilMoisture4 },
				{ "SoilMoisture5", TagSoilMoisture5 },
				{ "SoilMoisture6", TagSoilMoisture6 },
				{ "SoilMoisture7", TagSoilMoisture7 },
				{ "SoilMoisture8", TagSoilMoisture8 },
				{ "SoilMoisture9", TagSoilMoisture9 },
				{ "SoilMoisture10", TagSoilMoisture10 },
				{ "SoilMoisture11", TagSoilMoisture11 },
				{ "SoilMoisture12", TagSoilMoisture12 },
				{ "SoilMoisture13", TagSoilMoisture13 },
				{ "SoilMoisture14", TagSoilMoisture14 },
				{ "SoilMoisture15", TagSoilMoisture15 },
				{ "SoilMoisture16", TagSoilMoisture16 },
				{ "UserTemp1", TagUserTemp1 },
				{ "UserTemp2", TagUserTemp2 },
				{ "UserTemp3", TagUserTemp3 },
				{ "UserTemp4", TagUserTemp4 },
				{ "UserTemp5", TagUserTemp5 },
				{ "UserTemp6", TagUserTemp6 },
				{ "UserTemp7", TagUserTemp7 },
				{ "UserTemp8", TagUserTemp8 },
				{ "AirQuality1", TagAirQuality1 },
				{ "AirQuality2", TagAirQuality2 },
				{ "AirQuality3", TagAirQuality3 },
				{ "AirQuality4", TagAirQuality4 },
				{ "AirQualityAvg1", TagAirQualityAvg1 },
				{ "AirQualityAvg2", TagAirQualityAvg2 },
				{ "AirQualityAvg3", TagAirQualityAvg3 },
				{ "AirQualityAvg4", TagAirQualityAvg4 },
				{ "AirQualityIdx1", TagAirQualityIdx1 },
				{ "AirQualityIdx2", TagAirQualityIdx2 },
				{ "AirQualityIdx3", TagAirQualityIdx3 },
				{ "AirQualityIdx4", TagAirQualityIdx4 },
				{ "AirQualityAvgIdx1", TagAirQualityAvgIdx1 },
				{ "AirQualityAvgIdx2", TagAirQualityAvgIdx2 },
				{ "AirQualityAvgIdx3", TagAirQualityAvgIdx3 },
				{ "AirQualityAvgIdx4", TagAirQualityAvgIdx4 },
				{ "CO2", TagCo2 },
				{ "CO2_24h", TagCO2_24h },
				{ "CO2_pm2p5", TagCO2_pm2p5 },
				{ "CO2_pm2p5_24h", TagCO2_pm2p5_24h },
				{ "CO2_pm10", TagCO2_pm10 },
				{ "CO2_pm10_24h", TagCO2_pm10_24h },
				{ "CO2_temp", TagCO2_temp },
				{ "CO2_hum", TagCO2_hum },
				{ "CO2_pm1", TagCO2_pm1 },
				{ "CO2_pm1_24h", TagCO2_pm1_24h },
				{ "CO2_pm4", TagCO2_pm4 },
				{ "CO2_pm4_24h", TagCO2_pm4_24h },
				{ "CO2_pm2p5_aqi", TagCO2_pm2p5_aqi },
				{ "CO2_pm2p5_24h_aqi", TagCO2_pm2p5_24h_aqi },
				{ "CO2_pm10_aqi", TagCO2_pm10_aqi },
				{ "CO2_pm10_24h_aqi", TagCO2_pm10_24h_aqi },
				{ "LeakSensor1", TagLeakSensor1 },
				{ "LeakSensor2", TagLeakSensor2 },
				{ "LeakSensor3", TagLeakSensor3 },
				{ "LeakSensor4", TagLeakSensor4 },
				{ "LightningDistance", TagLightningDistance },
				{ "LightningTime", TagLightningTime },
				{ "LightningStrikesToday", TagLightningStrikesToday },
				{ "LeafWetness1", TagLeafWetness1 },
				{ "LeafWetness2", TagLeafWetness2 },
				{ "LeafWetness3", TagLeafWetness3 },
				{ "LeafWetness4", TagLeafWetness4 },
				{ "LeafWetness5", TagLeafWetness5 },
				{ "LeafWetness6", TagLeafWetness6 },
				{ "LeafWetness7", TagLeafWetness7 },
				{ "LeafWetness8", TagLeafWetness8 },

				{ "VapourPressDeficit", TagVapourPressDeficit },

				{ "LowTempAlarm", TagLowTempAlarm },
				{ "HighTempAlarm", TagHighTempAlarm },
				{ "TempChangeUpAlarm", TagTempChangeUpAlarm },
				{ "TempChangeDownAlarm", TagTempChangeDownAlarm },
				{ "LowPressAlarm", TagLowPressAlarm },
				{ "HighPressAlarm", TagHighPressAlarm },
				{ "PressChangeUpAlarm", TagPressChangeUpAlarm },
				{ "PressChangeDownAlarm", TagPressChangeDownAlarm },
				{ "HighRainTodayAlarm", TagHighRainTodayAlarm },
				{ "HighRainRateAlarm", TagHighRainRateAlarm },
				{ "IsRainingAlarm", TagIsRainingAlarm },
				{ "HighWindGustAlarm", TagHighWindGustAlarm },
				{ "HighWindSpeedAlarm", TagHighWindSpeedAlarm },
				{ "BatteryLowAlarm", TagBatteryLowAlarm },
				{ "DataSpikeAlarm", TagDataSpikeAlarm },
				{ "MySqlUploadAlarm", TagMySqlUploadAlarm },
				{ "HttpUploadAlarm", TagHttpUploadAlarm },
				{ "UpgradeAlarm", TagUpgradeAlarm },
				{ "FirmwareAlarm", TagFirmwareAlarm },
				{ "NewRecordAlarm", TagNewRecordAlarm },
				{ "NewRecordAlarmMessage", TagNewRecordAlarmMessage },

				{ "RG11RainToday", TagRg11RainToday },
				{ "RG11RainYest", TagRg11RainYest },

				{ "AirLinkFirmwareVersionIn", TagAirLinkFirmwareVersionIn },
				{ "AirLinkWifiRssiIn", TagAirLinkWifiRssiIn },
				{ "AirLinkUptimeIn", TagAirLinkUptimeIn },
				{ "AirLinkLinkUptimeIn", TagAirLinkLinkUptimeIn },
				{ "AirLinkTempIn", TagAirLinkTempIn },
				{ "AirLinkHumIn", TagAirLinkHumIn },
				{ "AirLinkPm1In", TagAirLinkPm1In },
				{ "AirLinkPm2p5In", TagAirLinkPm2p5In },
				{ "AirLinkPm2p5_1hrIn", TagAirLinkPm2p5_1hrIn },
				{ "AirLinkPm2p5_3hrIn", TagAirLinkPm2p5_3hrIn },
				{ "AirLinkPm2p5_24hrIn", TagAirLinkPm2p5_24hrIn },
				{ "AirLinkPm2p5_NowcastIn", TagAirLinkPm2p5_NowcastIn },
				{ "AirLinkPm10In", TagAirLinkPm10In },
				{ "AirLinkPm10_1hrIn", TagAirLinkPm10_1hrIn },
				{ "AirLinkPm10_3hrIn", TagAirLinkPm10_3hrIn },
				{ "AirLinkPm10_24hrIn", TagAirLinkPm10_24hrIn },
				{ "AirLinkPm10_NowcastIn", TagAirLinkPm10_NowcastIn },

				{ "AirLinkFirmwareVersionOut", TagAirLinkFirmwareVersionOut },
				{ "AirLinkWifiRssiOut", TagAirLinkWifiRssiOut },
				{ "AirLinkUptimeOut", TagAirLinkUptimeOut },
				{ "AirLinkLinkUptimeOut", TagAirLinkLinkUptimeOut },
				{ "AirLinkTempOut", TagAirLinkTempOut },
				{ "AirLinkHumOut", TagAirLinkHumOut },
				{ "AirLinkPm1Out", TagAirLinkPm1Out },
				{ "AirLinkPm2p5Out", TagAirLinkPm2p5Out },
				{ "AirLinkPm2p5_1hrOut", TagAirLinkPm2p5_1hrOut },
				{ "AirLinkPm2p5_3hrOut", TagAirLinkPm2p5_3hrOut },
				{ "AirLinkPm2p5_24hrOut", TagAirLinkPm2p5_24hrOut },
				{ "AirLinkPm2p5_NowcastOut", TagAirLinkPm2p5_NowcastOut },
				{ "AirLinkPm10Out", TagAirLinkPm10Out },
				{ "AirLinkPm10_1hrOut", TagAirLinkPm10_1hrOut },
				{ "AirLinkPm10_3hrOut", TagAirLinkPm10_3hrOut },
				{ "AirLinkPm10_24hrOut", TagAirLinkPm10_24hrOut },
				{ "AirLinkPm10_NowcastOut", TagAirLinkPm10_NowcastOut },

				{ "AirLinkAqiPm2p5In", TagAirLinkAqiPm2P5In },
				{ "AirLinkAqiPm2p5_1hrIn", TagAirLinkAqiPm2p5_1hrIn },
				{ "AirLinkAqiPm2p5_3hrIn", TagAirLinkAqiPm2p5_3hrIn },
				{ "AirLinkAqiPm2p5_24hrIn", TagAirLinkAqiPm2p5_24hrIn },
				{ "AirLinkAqiPm2p5_NowcastIn", TagAirLinkAqiPm2p5_NowcastIn },
				{ "AirLinkAqiPm10In", TagAirLinkAqiPm10In },
				{ "AirLinkAqiPm10_1hrIn", TagAirLinkAqiPm10_1hrIn },
				{ "AirLinkAqiPm10_3hrIn", TagAirLinkAqiPm10_3hrIn },
				{ "AirLinkAqiPm10_24hrIn", TagAirLinkAqiPm10_24hrIn },
				{ "AirLinkAqiPm10_NowcastIn", TagAirLinkAqiPm10_NowcastIn },

				{ "AirLinkAqiPm2p5Out", TagAirLinkAqiPm2P5Out },
				{ "AirLinkAqiPm2p5_1hrOut", TagAirLinkAqiPm2p5_1hrOut },
				{ "AirLinkAqiPm2p5_3hrOut", TagAirLinkAqiPm2p5_3hrOut },
				{ "AirLinkAqiPm2p5_24hrOut", TagAirLinkAqiPm2p5_24hrOut },
				{ "AirLinkAqiPm2p5_NowcastOut", TagAirLinkAqiPm2p5_NowcastOut },
				{ "AirLinkAqiPm10Out", TagAirLinkAqiPm10Out },
				{ "AirLinkAqiPm10_1hrOut", TagAirLinkAqiPm10_1hrOut },
				{ "AirLinkAqiPm10_3hrOut", TagAirLinkAqiPm10_3hrOut },
				{ "AirLinkAqiPm10_24hrOut", TagAirLinkAqiPm10_24hrOut },
				{ "AirLinkAqiPm10_NowcastOut", TagAirLinkAqiPm10_NowcastOut },

				{ "AirLinkPct_1hrIn", AirLinkPct_1hrIn },
				{ "AirLinkPct_3hrIn", AirLinkPct_3hrIn },
				{ "AirLinkPct_24hrIn", AirLinkPct_24hrIn },
				{ "AirLinkPct_NowcastIn", AirLinkPct_NowcastIn },
				{ "AirLinkPct_1hrOut", AirLinkPct_1hrOut },
				{ "AirLinkPct_3hrOut", AirLinkPct_3hrOut },
				{ "AirLinkPct_24hrOut", AirLinkPct_24hrOut },
				{ "AirLinkPct_NowcastOut", AirLinkPct_NowcastOut },

				{ "LaserDist1", TagLaserDist1 },
				{ "LaserDist2", TagLaserDist2 },
				{ "LaserDist3", TagLaserDist3 },
				{ "LaserDist4", TagLaserDist4 },

				{ "LaserDepth1", TagLaserDepth1 },
				{ "LaserDepth2", TagLaserDepth2 },
				{ "LaserDepth3", TagLaserDepth3 },
				{ "LaserDepth4", TagLaserDepth4 },

				{ "SnowAccum24h1", TagSnowAcc24h1 },
				{ "SnowAccum24h2", TagSnowAcc24h2 },
				{ "SnowAccum24h3", TagSnowAcc24h3 },
				{ "SnowAccum24h4", TagSnowAcc24h4 },

				{ "SnowAccumSeason", TagSnowAccSeason },
				{ "SnowAccumSeason1", TagSnowAccSeason1 },
				{ "SnowAccumSeason2", TagSnowAccSeason2 },
				{ "SnowAccumSeason3", TagSnowAccSeason3 },
				{ "SnowAccumSeason4", TagSnowAccSeason4 },

				// This month's highs and lows - values
				{ "MonthTempH", TagMonthTempH },
				{ "MonthTempL", TagMonthTempL },
				{ "MonthHeatIndexH", TagMonthHeatIndexH },
				{ "MonthWChillL", TagMonthWChillL },
				{ "MonthAppTempH", TagMonthAppTempH },
				{ "MonthAppTempL", TagMonthAppTempL },
				{ "MonthFeelsLikeH", TagMonthFeelsLikeH },
				{ "MonthFeelsLikeL", TagMonthFeelsLikeL },
				{ "MonthHumidexH", TagMonthHumidexH },
				{ "MonthMinTempH", TagMonthMinTempH },
				{ "MonthMaxTempL", TagMonthMaxTempL },
				{ "MonthPressH", TagMonthPressH },
				{ "MonthPressL", TagMonthPressL },
				{ "MonthHumH", TagMonthHumH },
				{ "MonthHumL", TagMonthHumL },
				{ "MonthGustH", TagMonthGustH },
				{ "MonthWindH", TagMonthWindH },
				{ "MonthRainRateH", TagMonthRainRateH },
				{ "MonthHourlyRainH", TagMonthHourlyRainH },
				{ "MonthRain24HourH", TagMonthRain24HourH },
				{ "MonthDailyRainH", TagMonthDailyRainH },
				{ "MonthDewPointH", TagMonthDewPointH },
				{ "MonthDewPointL", TagMonthDewPointL },
				{ "MonthWindRunH", TagMonthWindRunH },
				{ "MonthLongestDryPeriod", TagMonthLongestDryPeriod },
				{ "MonthLongestWetPeriod", TagMonthLongestWetPeriod },
				{ "MonthHighDailyTempRange", TagMonthHighDailyTempRange },
				{ "MonthLowDailyTempRange", TagMonthLowDailyTempRange },
				// This month's highs and lows - times
				{ "MonthTempHT", TagMonthTempHt },
				{ "MonthTempLT", TagMonthTempLt },
				{ "MonthHeatIndexHT", TagMonthHeatIndexHt },
				{ "MonthWChillLT", TagMonthWChillLt },
				{ "MonthAppTempHT", TagMonthAppTempHt },
				{ "MonthAppTempLT", TagMonthAppTempLt },
				{ "MonthFeelsLikeHT", TagMonthFeelsLikeHt },
				{ "MonthFeelsLikeLT", TagMonthFeelsLikeLt },
				{ "MonthHumidexHT", TagMonthHumidexHt },
				{ "MonthPressHT", TagMonthPressHt },
				{ "MonthPressLT", TagMonthPressLt },
				{ "MonthHumHT", TagMonthHumHt },
				{ "MonthHumLT", TagMonthHumLt },
				{ "MonthGustHT", TagMonthGustHt },
				{ "MonthWindHT", TagMonthWindHt },
				{ "MonthRainRateHT", TagMonthRainRateHt },
				{ "MonthHourlyRainHT", TagMonthHourlyRainHt },
				{ "MonthRain24HourHT", TagMonthRain24HourHt },
				{ "MonthDewPointHT", TagMonthDewPointHt },
				{ "MonthDewPointLT", TagMonthDewPointLt },
				// This month's highs and lows - dates
				{ "MonthTempHD", TagMonthTempHd },
				{ "MonthTempLD", TagMonthTempLd },
				{ "MonthHeatIndexHD", TagMonthHeatIndexHd },
				{ "MonthWChillLD", TagMonthWChillLd },
				{ "MonthAppTempHD", TagMonthAppTempHd },
				{ "MonthAppTempLD", TagMonthAppTempLd },
				{ "MonthFeelsLikeHD", TagMonthFeelsLikeHd },
				{ "MonthFeelsLikeLD", TagMonthFeelsLikeLd },
				{ "MonthHumidexHD", TagMonthHumidexHd },
				{ "MonthMinTempHD", TagMonthMinTempHd },
				{ "MonthMaxTempLD", TagMonthMaxTempLd },
				{ "MonthPressHD", TagMonthPressHd },
				{ "MonthPressLD", TagMonthPressLd },
				{ "MonthHumHD", TagMonthHumHd },
				{ "MonthHumLD", TagMonthHumLd },
				{ "MonthGustHD", TagMonthGustHd },
				{ "MonthWindHD", TagMonthWindHd },
				{ "MonthRainRateHD", TagMonthRainRateHd },
				{ "MonthHourlyRainHD", TagMonthHourlyRainHd },
				{ "MonthRain24HourHD", TagMonthRain24HourHd },
				{ "MonthDailyRainHD", TagMonthDailyRainHd },
				{ "MonthDewPointHD", TagMonthDewPointHd },
				{ "MonthDewPointLD", TagMonthDewPointLd },
				{ "MonthWindRunHD", TagMonthWindRunHd },
				{ "MonthLongestDryPeriodD", TagMonthLongestDryPeriodD },
				{ "MonthLongestWetPeriodD", TagMonthLongestWetPeriodD },
				{ "MonthHighDailyTempRangeD", TagMonthHighDailyTempRangeD },
				{ "MonthLowDailyTempRangeD", TagMonthLowDailyTempRangeD },
				// This Year's highs and lows - values
				{ "YearTempH", TagYearTempH },
				{ "YearTempL", TagYearTempL },
				{ "YearHeatIndexH", TagYearHeatIndexH },
				{ "YearWChillL", TagYearWChillL },
				{ "YearAppTempH", TagYearAppTempH },
				{ "YearAppTempL", TagYearAppTempL },
				{ "YearFeelsLikeH", TagYearFeelsLikeH },
				{ "YearFeelsLikeL", TagYearFeelsLikeL },
				{ "YearHumidexH", TagYearHumidexH },
				{ "YearMinTempH", TagYearMinTempH },
				{ "YearMaxTempL", TagYearMaxTempL },
				{ "YearPressH", TagYearPressH },
				{ "YearPressL", TagYearPressL },
				{ "YearHumH", TagYearHumH },
				{ "YearHumL", TagYearHumL },
				{ "YearGustH", TagYearGustH },
				{ "YearWindH", TagYearWindH },
				{ "YearRainRateH", TagYearRainRateH },
				{ "YearHourlyRainH", TagYearHourlyRainH },
				{ "YearRain24HourH", TagYearRain24HourH },
				{ "YearDailyRainH", TagYearDailyRainH },
				{ "YearMonthlyRainH", TagYearMonthlyRainH },
				{ "YearDewPointH", TagYearDewPointH },
				{ "YearDewPointL", TagYearDewPointL },
				{ "YearWindRunH", TagYearWindRunH },
				{ "YearLongestDryPeriod", TagYearLongestDryPeriod },
				{ "YearLongestWetPeriod", TagYearLongestWetPeriod },
				{ "YearHighDailyTempRange", TagYearHighDailyTempRange },
				{ "YearLowDailyTempRange", TagYearLowDailyTempRange },
				// This years highs and lows - times
				{ "YearTempHT", TagYearTempHt },
				{ "YearTempLT", TagYearTempLt },
				{ "YearHeatIndexHT", TagYearHeatIndexHt },
				{ "YearWChillLT", TagYearWChillLt },
				{ "YearAppTempHT", TagYearAppTempHt },
				{ "YearAppTempLT", TagYearAppTempLt },
				{ "YearFeelsLikeHT", TagYearFeelsLikeHt },
				{ "YearFeelsLikeLT", TagYearFeelsLikeLt },
				{ "YearHumidexHT", TagYearHumidexHt },
				{ "YearPressHT", TagYearPressHt },
				{ "YearPressLT", TagYearPressLt },
				{ "YearHumHT", TagYearHumHt },
				{ "YearHumLT", TagYearHumLt },
				{ "YearGustHT", TagYearGustHt },
				{ "YearWindHT", TagYearWindHt },
				{ "YearRainRateHT", TagYearRainRateHt },
				{ "YearHourlyRainHT", TagYearHourlyRainHt },
				{ "YearRain24HourHT", TagYearRain24HourHt },
				{ "YearDewPointHT", TagYearDewPointHt },
				{ "YearDewPointLT", TagYearDewPointLt },
				// This years highs and lows - dates
				{ "YearTempHD", TagYearTempHd },
				{ "YearTempLD", TagYearTempLd },
				{ "YearHeatIndexHD", TagYearHeatIndexHd },
				{ "YearWChillLD", TagYearWChillLd },
				{ "YearAppTempHD", TagYearAppTempHd },
				{ "YearAppTempLD", TagYearAppTempLd },
				{ "YearFeelsLikeHD", TagYearFeelsLikeHd },
				{ "YearFeelsLikeLD", TagYearFeelsLikeLd },
				{ "YearHumidexHD", TagYearHumidexHd },
				{ "YearMinTempHD", TagYearMinTempHd },
				{ "YearMaxTempLD", TagYearMaxTempLd },
				{ "YearPressHD", TagYearPressHd },
				{ "YearPressLD", TagYearPressLd },
				{ "YearHumHD", TagYearHumHd },
				{ "YearHumLD", TagYearHumLd },
				{ "YearGustHD", TagYearGustHd },
				{ "YearWindHD", TagYearWindHd },
				{ "YearRainRateHD", TagYearRainRateHd },
				{ "YearHourlyRainHD", TagYearHourlyRainHd },
				{ "YearRain24HourHD", TagYearRain24HourHd },
				{ "YearDailyRainHD", TagYearDailyRainHd },
				{ "YearMonthlyRainHD", TagYearMonthlyRainHd },
				{ "YearDewPointHD", TagYearDewPointHd },
				{ "YearDewPointLD", TagYearDewPointLd },
				{ "YearWindRunHD", TagYearWindRunHd },
				{ "YearLongestDryPeriodD", TagYearLongestDryPeriodD },
				{ "YearLongestWetPeriodD", TagYearLongestWetPeriodD },
				{ "YearHighDailyTempRangeD", TagYearHighDailyTempRangeD },
				{ "YearLowDailyTempRangeD", TagYearLowDailyTempRangeD },
				// misc CMX and System values
				{ "LatestError", TagLatestError },
				{ "LatestErrorEnc", TagLatestErrorEnc },
				{ "LatestErrorJsEnc", TagLatestErrorJsEnc },
				{ "LatestErrorDate", TagLatestErrorDate },
				{ "LatestErrorTime", TagLatestErrorTime },
				{ "ErrorLight", TagErrorLight },
				{ "MoonPercent", TagMoonPercent },
				{ "MoonPercentAbs", TagMoonPercentAbs },
				{ "MoonAge", TagMoonAge },
				{ "OsVersion", TagOsVersion },
				{ "OsLanguage", TagOsLanguage },
				{ "SystemUpTime", TagSystemUpTime },
				{ "ProgramUpTime", TagProgramUpTime },
				{ "ProgramUpTimeMs", TagProgramUpTimeMs },
				{ "CpuName", TagCpuName },
				{ "CpuCount", TagCpuCount },
				{ "MemoryStatus", TagMemoryStatus },
				{ "DisplayMode", TagDisplayModeString },
				{ "AllocatedMemory", TagAllocatedMemory },
				{ "DiskSize", TagDiskSize },
				{ "DiskFree", TagDiskFree },
				{ "CPUTemp", TagCpuTemp },
				{ "DavisTotalPacketsReceived", TagDavisTotalPacketsReceived },
				{ "DavisTotalPacketsMissed", TagDavisTotalPacketsMissed },
				{ "DavisNumberOfResynchs", TagDavisNumberOfResynchs },
				{ "DavisMaxInARow", TagDavisMaxInARow },
				{ "DavisNumCRCerrors", TagDavisNumCrCerrors },
				{ "DavisReceptionPercent", TagDavisReceptionPercent },
				{ "DavisFirmwareVersion", TagDavisFirmwareVersion },
				{ "DavisTxRssi", TagDavisTxRssi },
				{ "GW1000FirmwareVersion", TagGw1000FirmwareVersion },
				{ "EcowittFirmwareVersion", TagGw1000FirmwareVersion },
				{ "EcowittReception", TagGw1000Reception },
				{ "StationFreeMemory", TagStationFreeMemory },
				{ "ExtraStationFreeMemory", TagExtraStationFreeMemory },
				{ "StationRuntime", TagStationRuntime },
				{ "DataStopped", TagDataStopped },
				// Recent history
				{ "RecentOutsideTemp", TagRecentOutsideTemp },
				{ "RecentWindSpeed", TagRecentWindSpeed },
				{ "RecentWindGust", TagRecentWindGust },
				{ "RecentWindLatest", TagRecentWindLatest },
				{ "RecentWindDir", TagRecentWindDir },
				{ "RecentWindAvgDir", TagRecentWindAvgDir },
				{ "RecentWindChill", TagRecentWindChill },
				{ "RecentDewPoint", TagRecentDewPoint },
				{ "RecentHeatIndex", TagRecentHeatIndex },
				{ "RecentFeelsLike", TagRecentFeelsLike },
				{ "RecentApparent", TagRecentApparent },
				{ "RecentHumidex", TagRecentHumidex },
				{ "RecentHumidity", TagRecentHumidity },
				{ "RecentPressure", TagRecentPressure },
				{ "RecentRain", TagRecentRain },
				{ "RecentRainToday", TagRecentRainToday },
				{ "RecentSolarRad", TagRecentSolarRad },
				{ "RecentUV", TagRecentUv },
				{ "RecentIndoorTemp", TagRecentIndoorTemp },
				{ "RecentIndoorHumidity", TagRecentIndoorHumidity },
				{ "RecentTS", TagRecentTs },
				// Recent history with comma decimals replaced
				{ "RCRecentOutsideTemp", TagRcRecentOutsideTemp },
				{ "RCRecentWindSpeed", TagRcRecentWindSpeed },
				{ "RCRecentWindGust", TagRcRecentWindGust },
				{ "RCRecentWindLatest", TagRcRecentWindLatest },
				{ "RCRecentWindChill", TagRcRecentWindChill },
				{ "RCRecentDewPoint", TagRcRecentDewPoint },
				{ "RCRecentHeatIndex", TagRcRecentHeatIndex },
				{ "RCRecentPressure", TagRcRecentPressure },
				{ "RCRecentRainToday", TagRcRecentRainToday },
				{ "RCRecentUV", TagRcRecentUv },
				// Custom Intervals
				{ "WindAvgCust", TagWindAvgCust },
				{ "WindGustCust", TagWindGustCust },
				// Month-by-month highs and lows - values
				{ "ByMonthTempH", TagByMonthTempH },
				{ "ByMonthTempL", TagByMonthTempL },
				{ "ByMonthTempAvg", TagByMonthTempAvg },
				{ "ByMonthAppTempH", TagByMonthAppTempH },
				{ "ByMonthAppTempL", TagByMonthAppTempL },
				{ "ByMonthFeelsLikeH", TagByMonthFeelsLikeH },
				{ "ByMonthFeelsLikeL", TagByMonthFeelsLikeL },
				{ "ByMonthHumidexH", TagByMonthHumidexH },
				//{ "ByMonthHumidexL", TagByMonthHumidexL },
				{ "ByMonthDewPointH", TagByMonthDewPointH },
				{ "ByMonthDewPointL", TagByMonthDewPointL },
				{ "ByMonthHeatIndexH", TagByMonthHeatIndexH },
				{ "ByMonthGustH", TagByMonthGustH },
				{ "ByMonthWindH", TagByMonthWindH },
				{ "ByMonthWindRunH", TagByMonthWindRunH },
				{ "ByMonthWChillL", TagByMonthWChillL },
				{ "ByMonthRainRateH", TagByMonthRainRateH },
				{ "ByMonthDailyRainH", TagByMonthDailyRainH },
				{ "ByMonthHourlyRainH", TagByMonthHourlyRainH },
				{ "ByMonthRain24HourH", TagByMonthRain24HourH },
				{ "ByMonthMonthlyRainH", TagByMonthMonthlyRainH },
				{ "ByMonthPressH", TagByMonthPressH },
				{ "ByMonthPressL", TagByMonthPressL },
				{ "ByMonthHumH", TagByMonthHumH },
				{ "ByMonthHumL", TagByMonthHumL },
				{ "ByMonthMinTempH", TagByMonthMinTempH },
				{ "ByMonthMaxTempL", TagByMonthMaxTempL },
				{ "ByMonthLongestDryPeriod", TagByMonthLongestDryPeriod },
				{ "ByMonthLongestWetPeriod", TagByMonthLongestWetPeriod },
				{ "ByMonthLowDailyTempRange", TagByMonthLowDailyTempRange },
				{ "ByMonthHighDailyTempRange", TagByMonthHighDailyTempRange },
				// Month-by-month highs and lows - timestamps
				{ "ByMonthTempHT", TagByMonthTempHt },
				{ "ByMonthTempLT", TagByMonthTempLt },
				{ "ByMonthAppTempHT", TagByMonthAppTempHt },
				{ "ByMonthAppTempLT", TagByMonthAppTempLt },
				{ "ByMonthFeelsLikeHT", TagByMonthFeelsLikeHt },
				{ "ByMonthFeelsLikeLT", TagByMonthFeelsLikeLt },
				{ "ByMonthHumidexHT", TagByMonthHumidexHt },
				//{ "ByMonthHumidexLT", TagByMonthHumidexLT },
				{ "ByMonthDewPointHT", TagByMonthDewPointHt },
				{ "ByMonthDewPointLT", TagByMonthDewPointLt },
				{ "ByMonthHeatIndexHT", TagByMonthHeatIndexHt },
				{ "ByMonthGustHT", TagByMonthGustHt },
				{ "ByMonthWindHT", TagByMonthWindHt },
				{ "ByMonthWindRunHT", TagByMonthWindRunHt },
				{ "ByMonthWChillLT", TagByMonthWChillLt },
				{ "ByMonthRainRateHT", TagByMonthRainRateHt },
				{ "ByMonthDailyRainHT", TagByMonthDailyRainHt },
				{ "ByMonthHourlyRainHT", TagByMonthHourlyRainHt },
				{ "ByMonthMonthlyRainHT", TagByMonthMonthlyRainHt },
				{ "ByMonthRain24HourHT", TagByMonthRain24HourHt },
				{ "ByMonthPressHT", TagByMonthPressHt },
				{ "ByMonthPressLT", TagByMonthPressLt },
				{ "ByMonthHumHT", TagByMonthHumHt },
				{ "ByMonthHumLT", TagByMonthHumLt },
				{ "ByMonthMinTempHT", TagByMonthMinTempHt },
				{ "ByMonthMaxTempLT", TagByMonthMaxTempLt },
				{ "ByMonthLongestDryPeriodT", TagByMonthLongestDryPeriodT },
				{ "ByMonthLongestWetPeriodT", TagByMonthLongestWetPeriodT },
				{ "ByMonthLowDailyTempRangeT", TagByMonthLowDailyTempRangeT },
				{ "ByMonthHighDailyTempRangeT", TagByMonthHighDailyTempRangeT },
				// Monthly averages
				{ "MonthAvgTemp", TagMonthAvgTemp },
				{ "MonthAvgTempHigh", TagMonthAvgTempHigh },
				{ "MonthAvgTempLow", TagMonthAvgTempLow },
				{ "MonthAvgTotalRainfall", TagMonthAvgTotalRainfall },
				{ "MonthAvgTotalWindRun", TagMonthAvgTotalWindRun },
				{ "MonthAvgTotalSunHours", TagMonthAvgTotalSunHours },
				{ "MonthAvgTotalET", TagMonthAvgTotalET },
				{ "MonthAvgTotalChillHrs", TagMonthAvgTotalChillHrs },

				// Specifc Month/Year values
				{ "MonthTempAvg", TagMonthTempAvg },
				{ "YearTempAvg", TagYearTempAvg },
				{ "MonthRainfall", TagMonthRainfall },
				{ "AnnualRainfall", TagAnnualRainfall },
				// Options
				{ "Option_useApparent", TagOption_useApparent },
				{ "Option_showSolar", TagOption_showSolar },
				{ "Option_showUV", TagOption_showUV },
				{ "Option_showSnow", TagOption_showSnow },
				{ "Option_noaaFormat", TagOption_noaaFormat },
				// MySQL insert times
				{ "MySqlRealtimeTime", TagMySqlRealtimeTime },
				{ "MySqlIntervalTime", TagMySqlIntervalTime },
				// DateTime of current data
				{ "DataDateTime", TagDataDateTime},
				// General queries
				{ "QueryDayFile", TagQueryDayFile }
			};

			cumulus.LogMessage(webTagDictionary.Count + " web tags initialised");

			if (!cumulus.ProgramOptions.ListWebTags) return;

			using StreamWriter file = new StreamWriter(cumulus.WebTagFile);

			foreach (var pair in webTagDictionary)
			{
				file.WriteLine(pair.Key);
			}
		}

		public string GetWebTagText(string tagString, Dictionary<string, string> tagParams)
		{
			return webTagDictionary.TryGetValue(tagString, out var value) ? value(tagParams) : string.Empty;
		}

	} // end WebTags class

	//Jan 2010 - www.haiders.net
	//An Extension Method to Get a Dictionary Item by Key
	//If the Key does not exist, null is returned for reference types
	//For Value types, default value is returned (default(TValue))
	public static class DictionaryExtension
	{
		public static TTvalue Get<TTkey, TTvalue>(this Dictionary<TTkey, TTvalue> dictionary, TTkey key)
		{
			TTvalue value;
			dictionary.TryGetValue(key, out value);
			return value;
		}
	}
}
