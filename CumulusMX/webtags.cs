using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Unosquare.Swan;

namespace CumulusMX
{
	public delegate string WebTagFunction(Dictionary<string,string> tagParams);

	internal class WebTags
	{
		private Dictionary<string, WebTagFunction> webTagDictionary;

		private readonly Cumulus cumulus;
		private readonly WeatherStation station;

		public WebTags(Cumulus cumulus, WeatherStation station)
		{
			this.cumulus = cumulus;
			this.station = station;
		}

		/*
		private void FreeWebTagDictionary()
		{
			WebTagDictionary.Clear();
		}
		*/

		/*
		private string FormatByteSize(long bytes)
		{
			string sign = (bytes < 0 ? "-" : "");
			double readable = (bytes < 0 ? -bytes : bytes);
			string suffix;
			if (bytes >= 0x1000000000000000) // Exabyte
			{
				suffix = "EB";
				readable = (double) (bytes >> 50);
			}
			else if (bytes >= 0x4000000000000) // Petabyte
			{
				suffix = "PB";
				readable = (double) (bytes >> 40);
			}
			else if (bytes >= 0x10000000000) // Terabyte
			{
				suffix = "TB";
				readable = (double) (bytes >> 30);
			}
			else if (bytes >= 0x40000000) // Gigabyte
			{
				suffix = "GB";
				readable = (double) (bytes >> 20);
			}
			else if (bytes >= 0x100000) // Megabyte
			{
				suffix = "MB";
				readable = (double) (bytes >> 10);
			}
			else if (bytes >= 0x400) // Kilobyte
			{
				suffix = "KB";
				readable = (double) bytes;
			}
			else
			{
				return bytes.ToString(sign + "0 B"); // Byte
			}
			readable /= 1024;

			return sign + readable.ToString("0.## ") + suffix;
		}
		*/

		private static string ReadFileIntoString(string fileName)
		{
			string result;
			using (StreamReader streamReader = new StreamReader(fileName))
			{
				result = streamReader.ReadToEnd();
			}
			return result;
		}

		private static string ReplaceCommas(string aStr)
		{
			return aStr.Replace(',', '.');
		}

		private static string CheckRc(string val, Dictionary<string, string> tagParams)
		{
			try
			{
				if (tagParams.Get("rc") == "y") val = ReplaceCommas(val);
				return val;
			}
			catch
			{
				return "error";
			}
		}

		private static string CheckRcDp(double val, Dictionary<string, string> tagParams, int decimals)
		{
			string ret;
			try
			{
				if (tagParams.Get("tc") == "y")
					return Math.Truncate(val).ToString();

				int dp = int.TryParse(tagParams.Get("dp"), out dp) ? dp : decimals;

				ret = val.ToString("F" + dp);

				if (tagParams.Get("rc") == "y")
				{
					ret = ReplaceCommas(ret);
				}
				return ret;
			}
			catch
			{
				return "error";
			}
		}

		private double GetSnowDepth(DateTime day)
		{
			double depth;
			try
			{
				var result = cumulus.DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE Date(Timestamp) = ?", day.ToString("yyyy-MM-dd"));

				depth = result.Count == 1 ? result[0].snowDepth : 0;
			}
			catch(Exception e)
			{
				cumulus.LogMessage("Error reading diary database: " + e.Message);
				depth = 0;
			}
			return depth;
		}

		private int GetSnowLying(DateTime day)
		{
			int lying;
			try
			{
				var result = cumulus.DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE Date(Timestamp) = ?", day.ToString("yyyy-MM-dd"));

				lying = result.Count == 1 ? result[0].snowLying : 0;
			}
			catch (Exception e)
			{
				cumulus.LogMessage("Error reading diary database: " + e.Message);
				lying = 0;
			}
			return lying;
		}

		private int GetSnowFalling(DateTime day)
		{
			int falling;
			try
			{
				var result = cumulus.DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE Date(Timestamp) = ?", day.ToString("yyyy-MM-dd"));

				falling = result.Count == 1 ? result[0].snowFalling : 0;
			}
			catch (Exception e)
			{
				cumulus.LogMessage("Error reading diary database: " + e.Message);
				falling = 0;
			}
			return falling;
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


		private DateTime GetRecentTs(Dictionary<string,string> tagParams)
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
					cumulus.LogMessage($"Error: Webtag <#{tagParams.Get("webtag")}>, expecting an integer value for parameter 'm=n', found 'm={minsagostr}'");
				}
			}
			if (hoursagostr != null)
			{
				try
				{
					minsago += (Convert.ToInt32(hoursagostr)*60);
				}
				catch
				{
					cumulus.LogMessage($"Error: Webtag <#{tagParams.Get("webtag")}>, expecting an integer value for parameter 'h=n', found 'h={hoursagostr}'");
				}
			}
			if (daysagostr != null)
			{
				try
				{
					minsago += (Convert.ToInt32(daysagostr)*1440);
				}
				catch
				{
					cumulus.LogMessage($"Error: Webtag <#{tagParams.Get("webtag")}>, expecting an integer value for parameter 'd=n', found 'd={daysagostr}'");
				}
			}
			if (minsago < 0)
			{
				minsago = 0;
			}
			return DateTime.Now.AddMinutes(-minsago);
		}

		private int GetMonthParam(Dictionary<string,string> tagParams)
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
					cumulus.LogMessage($"Error: Webtag <#{tagParams.Get("webtag")}> expecting an integer value for parameter 'mon=n', found 'mon={monthstr}'");
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
			return res;
		}

		private static string GetFormattedDateTime(DateTime dt, Dictionary<string,string> tagParams)
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
					s = dt.ToString(dtformat);
				}
				catch (Exception)
				{
					s = dt.ToString();
				}
			}
			return s;
		}

		private string GetFormattedDateTime(DateTime dt, string defaultFormat, Dictionary<string,string> tagParams)
		{
			string s;
			if (dt <= cumulus.defaultRecordTS)
			{
				s = "----";
			}
			else
			{
				string dtformat = tagParams.Get("format") ?? defaultFormat;
				s = dt.ToString(dtformat);
			}
			return s;
		}

		private string GetFormattedDateTime(TimeSpan ts, string defaultFormat, Dictionary<string,string> tagParams)
		{
			DateTime dt = DateTime.MinValue.Add(ts);

			return GetFormattedDateTime(dt, defaultFormat, tagParams);
		}

		/*
		private string GetFormattedDateTime(string dtstr, Dictionary<string,string> TagParams)
		{
			string dtformat = TagParams.Get("format");
			if (dtformat == null)
			{
				// No format specified, return string as is
				return dtstr;
			}
			DateTime ts = DateTime.Parse(dtstr);
			return ts.ToString(dtformat);
		}
		*/

		private string GetMonthlyAlltimeValueStr(AllTimeRec rec, Dictionary<string, string> tagParams, int decimals)
		{
			if (rec.Ts <= cumulus.defaultRecordTS)
			{
				return "---";
			}

			return CheckRcDp(rec.Val, tagParams, decimals);
		}

		/*
		private string GetMonthlyAlltimeTSStr(int identifier, int month, string format)
		{
			if (station.monthlyrecarray[identifier, month].timestamp <= cumulus.defaultRecordTS)
			{
				return "----";
			}
			else
			{
				return station.monthlyrecarray[identifier, month].timestamp.ToString(format);
			}
		}
		*/

		private string Tagwlatest(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.WindLatest, tagParams, cumulus.WindDPlaces);
		}

		private string Tagwindrun(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.WindRunToday, tagParams, cumulus.WindRunDPlaces);
		}

		private string Tagwspeed(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.WindAverage, tagParams, cumulus.WindAvgDPlaces);
		}

		private string Tagcurrentwdir(Dictionary<string,string> tagParams)
		{
			return station.CompassPoint(station.Bearing);
		}

		private string Tagwdir(Dictionary<string,string> tagParams)
		{
			return station.CompassPoint(station.AvgBearing);
		}

		private string Tagwgust(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RecentMaxGust, tagParams, cumulus.WindDPlaces);
		}

		private string TagwindAvg(Dictionary<string, string> tagParams)
		{
			// we add on the meteo day start hour to 00:00 today
			var startOfDay = DateTime.Today.AddHours(-cumulus.GetHourInc());
			// then if that is later than now we are still in the previous day, so subtract a day
			if (startOfDay > DateTime.Now)
				startOfDay = startOfDay.AddDays(-1);
			var timeToday = station.WindRunHourMult[cumulus.Units.Wind] * (DateTime.Now - startOfDay).TotalHours;
			return CheckRcDp(station.WindRunToday / timeToday, tagParams, cumulus.WindAvgDPlaces);
		}

		private string Tagwchill(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.WindChill, tagParams, cumulus.TempDPlaces);
		}

		private string Tagrrate(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RainRate, tagParams, cumulus.RainDPlaces);
		}

		private string TagStormRain(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.StormRain, tagParams, cumulus.RainDPlaces);
		}

		private string TagStormRainStart(Dictionary<string,string> tagParams)
		{

			if (station.StartOfStorm == DateTime.MinValue)
			{
				return "-----";
			}

			string dtformat = tagParams.Get("format") ?? "d";

			return station.StartOfStorm.ToString(dtformat);
		}

		private string Tagtomorrowdaylength(Dictionary<string,string> tagParams)
		{
			return cumulus.TomorrowDayLengthText;
		}

		private string TagwindrunY(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.YesterdayWindRun, tagParams, cumulus.WindRunDPlaces);
		}

		private string TagwindAvgY(Dictionary<string, string> tagParams)
		{
			var timeYest = station.WindRunHourMult[cumulus.Units.Wind] * 24;
			return CheckRcDp(station.YesterdayWindRun / timeYest, tagParams, cumulus.WindAvgDPlaces);
		}


		private string Tagheatdegdays(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HeatingDegreeDays, tagParams, cumulus.TempDPlaces);
		}

		private string Tagcooldegdays(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.CoolingDegreeDays, tagParams, cumulus.TempDPlaces);
		}

		private string TagheatdegdaysY(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.YestHeatingDegreeDays, tagParams, cumulus.TempDPlaces);
		}

		private string TagcooldegdaysY(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.YestCoolingDegreeDays, tagParams, cumulus.TempDPlaces);
		}

		private string Tagpresstrendval(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.presstrendval, tagParams, cumulus.PressDPlaces);
		}

		private string TagPressChangeLast3Hours(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.presstrendval * 3, tagParams, cumulus.PressDPlaces);
		}

		private string TagTempChangeLastHour(Dictionary<string, string> tagParams)
		{
			return CheckRc(station.TempChangeLastHour.ToString("+##0.0;-##0.0;0"), tagParams);
		}


		private string Tagdew(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.OutdoorDewpoint, tagParams, cumulus.TempDPlaces);
		}

		private string Tagwetbulb(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.WetBulb, tagParams, cumulus.TempDPlaces);
		}

		private string Tagcloudbase(Dictionary<string,string> tagParams)
		{
			return station.CloudBase + (cumulus.CloudBaseInFeet ? " ft" : " m");
		}

		private string Tagcloudbaseunit(Dictionary<string,string> tagParams)
		{
			return cumulus.CloudBaseInFeet ? "ft" : "m";
		}

		private string Tagcloudbasevalue(Dictionary<string,string> tagParams)
		{
			return station.CloudBase.ToString();
		}

		private static string TagTime(Dictionary<string,string> tagParams)
		{
			var dtformat = tagParams.Get("format") ?? "HH:mm\" on \"dd MMMM yyyy";
			var s = DateTime.Now.ToString(dtformat);
			var result = s;
			return result;
		}

		private static string TagDaysSince30Dec1899(Dictionary<string,string> tagParams)
		{
			DateTime startDate = new DateTime(1899,12,30);
			return ((DateTime.Now - startDate).TotalDays).ToString();
		}

		private static string TagTimeUtc(Dictionary<string,string> tagParams)
		{
			string dtformat = tagParams.Get("format") ?? "HH:mm\" on \"dd MMMM yyyy";
			return DateTime.UtcNow.ToString(dtformat);
		}

		private static string TagTimehhmmss(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("HH:mm:ss");
		}

		private static string TagTimeJavascript(Dictionary<string, string> tagParams)
		{
			return ((ulong)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds).ToString();
		}

		private static string TagTimeUnix(Dictionary<string, string> tagParams)
		{
			return DateTime.UtcNow.ToUnixEpochDate().ToString();
		}

		private static string TagDate(Dictionary<string,string> tagParams)
		{
			string dtformat = tagParams.Get("format");
			return dtformat == null ? DateTime.Now.ToString("d") : DateTime.Now.ToString(dtformat);
		}

		private static string TagYesterday(Dictionary<string,string> tagParams)
		{
			var yesterday = DateTime.Now.AddDays(-1);
			var dtformat = tagParams.Get("format");
			var s = dtformat == null ? (yesterday).ToString("d") : yesterday.ToString(dtformat);
			return s;
		}

		private string TagMetDate(Dictionary<string,string> tagParams)
		{

			string dtformat = tagParams.Get("format");

			if (string.IsNullOrEmpty(dtformat))
			{
				dtformat = "d";
			}

			int offset = cumulus.GetHourInc();

			return DateTime.Now.AddHours(offset).ToString(dtformat);
		}

		private string TagMetDateYesterday(Dictionary<string,string> tagParams)
		{
			string dtformat = tagParams.Get("format");

			if (string.IsNullOrEmpty(dtformat))
			{
				dtformat = "d";
			}

			int offset = cumulus.GetHourInc();

			return DateTime.Now.AddHours(offset).AddDays(-1).ToString(dtformat);
		}

		private static string TagDay(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("dd");
		}

		private static string TagDayname(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("dddd");
		}

		private static string TagShortdayname(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("ddd");
		}

		private static string TagMonth(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("MM");
		}

		private static string TagMonthname(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("MMMM");
		}

		private static string TagShortmonthname(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("MMM");
		}

		private static string TagYear(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.Year.ToString();
		}

		private static string TagShortyear(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("yy");
		}

		private static string TagHour(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("HH");
		}

		private static string TagMinute(Dictionary<string,string> tagParams)
		{
			return DateTime.Now.ToString("mm");
		}

		private string Tagforecastnumber(Dictionary<string,string> tagParams)
		{
			return station.Forecastnumber.ToString();
		}

		private string Tagforecast(Dictionary<string,string> tagParams)
		{
			return station.forecaststr;
		}

		private string Tagforecastenc(Dictionary<string,string> tagParams)
		{
			return EncodeForWeb(station.forecaststr);
		}

		private string TagforecastJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(station.forecaststr);
		}

		private string Tagcumulusforecast(Dictionary<string,string> tagParams)
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

		private string Tagwsforecast(Dictionary<string,string> tagParams)
		{
			return station.wsforecast;
		}

		private string Tagwsforecastenc(Dictionary<string,string> tagParams)
		{
			return EncodeForWeb(station.wsforecast);
		}

		private string TagwsforecastJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(station.wsforecast);
		}


		private string Tagtemp(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.OutdoorTemperature, tagParams, cumulus.TempDPlaces);
		}

		private string Tagtemprange(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.TempRange, tagParams, cumulus.TempDPlaces);
		}

		private string TagtemprangeY(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.TempRange, tagParams, cumulus.TempDPlaces);
		}

		private string Tagavgtemp(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.TempTotalToday/station.tempsamplestoday, tagParams, cumulus.TempDPlaces);
		}

		private string TagavgtempY(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.YestAvgTemp, tagParams, cumulus.TempDPlaces);
		}

		private string Tagapptemp(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ApparentTemperature, tagParams, cumulus.TempDPlaces);
		}

		private string Tagfeelsliketemp(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.FeelsLike, tagParams, cumulus.TempDPlaces);
		}

		private string Tagtemptrend(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.temptrendval, tagParams, cumulus.TempDPlaces);
		}

		private string Tagtemptrendtext(Dictionary<string,string> tagParams)
		{
			string text;
			if (Math.Abs(station.temptrendval) < 0.001)
			{
				text =  cumulus.Steady;
			}
			else if (station.temptrendval > 0)
			{
				text = cumulus.Rising;
			}
			else
			{
				text = cumulus.Falling;
			}
			return text;
		}

		private string Tagtemptrendenglish(Dictionary<string,string> tagParams)
		{
			if (Math.Abs(station.temptrendval) < 0.001)
			{
				return "Steady";
			}

			return station.temptrendval > 0 ? "Rising" : "Falling";
		}

		private string Tagheatindex(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HeatIndex, tagParams, cumulus.TempDPlaces);
		}

		private string Taghum(Dictionary<string,string> tagParams)
		{
			return station.OutdoorHumidity.ToString();
		}

		private string Taghumidex(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.Humidex, tagParams, cumulus.TempDPlaces);
		}

		private string Tagpress(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.Pressure, tagParams, cumulus.PressDPlaces);
		}

		private string Tagaltimeterpressure(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AltimeterPressure, tagParams, cumulus.TempDPlaces);
		}

		private string Tagpresstrend(Dictionary<string,string> tagParams)
		{
			return station.Presstrendstr;
		}

		private string Tagpresstrendenglish(Dictionary<string,string> tagParams)
		{
			if (Math.Abs(station.presstrendval) < 0.0001)
			{
				return "Steady";
			}

			return station.presstrendval > 0 ? "Rising" : "Falling";
		}

		private string Tagbearing(Dictionary<string,string> tagParams)
		{
			return station.Bearing.ToString();
		}

		private string Tagavgbearing(Dictionary<string,string> tagParams)
		{
			return station.AvgBearing.ToString();
		}

		private string TagBearingRangeFrom(Dictionary<string,string> tagParams)
		{
			return station.BearingRangeFrom.ToString();
		}

		private string TagBearingRangeTo(Dictionary<string,string> tagParams)
		{
			return station.BearingRangeTo.ToString();
		}

		private string TagBearingRangeFrom10(Dictionary<string,string> tagParams)
		{
			return station.BearingRangeFrom10.ToString("D3");
		}

		private string TagBearingRangeTo10(Dictionary<string,string> tagParams)
		{
			return station.BearingRangeTo10.ToString("D3");
		}

		private string Tagdomwindbearing(Dictionary<string,string> tagParams)
		{
			return station.DominantWindBearing.ToString();
		}

		private string Tagdomwinddir(Dictionary<string,string> tagParams)
		{
			return station.CompassPoint(station.DominantWindBearing);
		}

		private string TagdomwindbearingY(Dictionary<string,string> tagParams)
		{
			return station.YestDominantWindBearing.ToString();
		}

		private string TagdomwinddirY(Dictionary<string,string> tagParams)
		{
			return station.CompassPoint(station.YestDominantWindBearing);
		}

		private string Tagbeaufort(Dictionary<string,string> tagParams)
		{
			return "F" + cumulus.Beaufort(station.WindAverage);
		}

		private string Tagbeaufortnumber(Dictionary<string,string> tagParams)
		{
			return cumulus.Beaufort(station.WindAverage);
		}

		private string Tagbeaudesc(Dictionary<string, string> tagParams)
		{
			return cumulus.BeaufortDesc(station.WindAverage);
		}

		private string Tagwdirdata(Dictionary<string,string> tagParams)
		{
			var sb = new StringBuilder(station.windbears[0].ToString());

			for (var i = 1; i < station.numwindvalues; i++)
			{
				sb.Append("," + station.windbears[i]);
			}

			return sb.ToString();
		}

		private string Tagwspddata(Dictionary<string,string> tagParams)
		{
			var sb = new StringBuilder((station.windspeeds[0]*cumulus.Calib.WindGust.Mult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
			for (var i = 1; i < station.numwindvalues; i++)
			{
				sb.Append("," + (station.windspeeds[i]*cumulus.Calib.WindGust.Mult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
			}

			return sb.ToString();
		}

		private string TagWindSampleCount(Dictionary<string,string> tagParams)
		{
			return station.numwindvalues.ToString();
		}

		private string Tagnextwindindex(Dictionary<string,string> tagParams)
		{
			return station.nextwindvalue.ToString();
		}

		private string TagWindRoseData(Dictionary<string,string> tagParams)
		{
			var sb = new StringBuilder((station.windcounts[0]*cumulus.Calib.WindGust.Mult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));

			for (var i = 1; i < cumulus.NumWindRosePoints; i++)
			{
				sb.Append("," + (station.windcounts[i]*cumulus.Calib.WindGust.Mult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
			}

			return sb.ToString();
		}

		private string TagWindRosePoints(Dictionary<string,string> tagParams)
		{
			return cumulus.NumWindRosePoints.ToString();
		}

		private string Tagrfall(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RainToday, tagParams, cumulus.RainDPlaces);
		}

		private string Tagrmidnight(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RainSinceMidnight, tagParams, cumulus.RainDPlaces);
		}

		private string Tagrmonth(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RainMonth, tagParams, cumulus.RainDPlaces);
		}

		private string Tagrhour(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RainLastHour, tagParams, cumulus.RainDPlaces);
		}

		private string Tagr24Hour(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RainLast24Hour, tagParams, cumulus.RainDPlaces);
		}

		private string Tagryear(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RainYear, tagParams, cumulus.RainDPlaces);
		}

		private string Taginhum(Dictionary<string,string> tagParams)
		{
			return station.IndoorHumidity.ToString();
		}

		private string Tagintemp(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.IndoorTemperature, tagParams, cumulus.TempDPlaces);
		}

		private string Tagbattery(Dictionary<string,string> tagParams)
		{
			return CheckRc(station.ConBatText, tagParams);
		}

		private string TagConsoleSupplyV(Dictionary<string, string> tagParams)
		{
			return CheckRc(station.ConSupplyVoltageText, tagParams);
		}

		private string Tagtxbattery(Dictionary<string,string> tagParams)
		{
			if (string.IsNullOrEmpty(station.TxBatText))
			{
				return "";
			}

			string channeltxt = tagParams.Get("channel");
			if (channeltxt == null)
			{
				return station.TxBatText;
			}

			// extract status for required channel
			char[] delimiters = { ' ', '-' };
			string[] sl = station.TxBatText.Split(delimiters);

			int channel = int.Parse(channeltxt) * 2 - 1;

			if ((channel < sl.Length) && (sl[channel].Length > 0))
			{
				return sl[channel];
			}

			// default
			return station.TxBatText;
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
				return (station.multicastsGood / (float)(station.multicastsBad + station.multicastsGood) * 100).ToString("0.00");
			}
			catch
			{
				return "0.00";
			}
		}

		// AirLink Indoor
		private string TagAirLinkTempIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.temperature, tagParams, cumulus.TempDPlaces);
		}
		private string TagAirLinkHumIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.humidity, tagParams, cumulus.HumDPlaces);
		}
		private string TagAirLinkPm1In(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm1, tagParams, 1);
		}
		private string TagAirLinkPm2p5In(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5, tagParams, 1);
		}
		private string TagAirLinkPm2p5_1hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5_1hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_3hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5_3hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_24hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5_24hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_NowcastIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm2p5_nowcast, tagParams, 1);
		}
		private string TagAirLinkPm10In(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10, tagParams, 1);
		}
		private string TagAirLinkPm10_1hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10_1hr, tagParams, 1);
		}
		private string TagAirLinkPm10_3hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10_3hr, tagParams, 1);
		}
		private string TagAirLinkPm10_24hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10_24hr, tagParams, 1);
		}
		private string TagAirLinkPm10_NowcastIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : CheckRcDp(cumulus.airLinkDataIn.pm10_nowcast, tagParams, 1);
		}
		private string TagAirLinkFirmwareVersionIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : cumulus.airLinkDataIn.firmwareVersion;
		}
		private string TagAirLinkWifiRssiIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : cumulus.airLinkDataIn.wifiRssi.ToString();
		}


		// AirLink Outdoor
		private string TagAirLinkTempOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.temperature, tagParams, cumulus.TempDPlaces);
		}
		private string TagAirLinkHumOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.humidity, tagParams, cumulus.HumDPlaces);
		}
		private string TagAirLinkPm1Out(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm1, tagParams, 1);
		}
		private string TagAirLinkPm2p5Out(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5, tagParams, 1);
		}
		private string TagAirLinkPm2p5_1hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5_1hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_3hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5_3hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_24hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5_24hr, tagParams, 1);
		}
		private string TagAirLinkPm2p5_NowcastOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm2p5_nowcast, tagParams, 1);
		}
		private string TagAirLinkPm10Out(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10, tagParams, 1);
		}
		private string TagAirLinkPm10_1hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10_1hr, tagParams, 1);
		}
		private string TagAirLinkPm10_3hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10_3hr, tagParams, 1);
		}
		private string TagAirLinkPm10_24hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10_24hr, tagParams, 1);
		}
		private string TagAirLinkPm10_NowcastOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : CheckRcDp(cumulus.airLinkDataOut.pm10_nowcast, tagParams, 1);
		}
		private string TagAirLinkFirmwareVersionOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : cumulus.airLinkDataOut.firmwareVersion;
		}
		private string TagAirLinkWifiRssiOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : cumulus.airLinkDataOut.wifiRssi.ToString();
		}

		private string TagAirLinkAqiPm2P5In(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirLinkAqiPm2p5_1hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5_1hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_3hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5_3hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_24hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5_24hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_NowcastIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm2p5_nowcast, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10In(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_1hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10_1hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_3hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10_3hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_24hrIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10_24hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_NowcastIn(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataIn == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataIn.aqiPm10_nowcast, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirLinkAqiPm2P5Out(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_1hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5_1hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_3hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5_3hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_24hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5_24hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm2p5_NowcastOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm2p5_nowcast, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10Out(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_1hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10_1hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_3hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10_3hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_24hrOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10_24hr, tagParams, cumulus.AirQualityDPlaces);
		}
		private string TagAirLinkAqiPm10_NowcastOut(Dictionary<string, string> tagParams)
		{
			if (cumulus.airLinkDataOut == null)
				return "--";

			return CheckRcDp(cumulus.airLinkDataOut.aqiPm10_nowcast, tagParams, cumulus.AirQualityDPlaces);
		}


		private string AirLinkPct_1hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : cumulus.airLinkDataIn.pct_1hr.ToString();
		}
		private string AirLinkPct_3hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : cumulus.airLinkDataIn.pct_3hr.ToString();
		}
		private string AirLinkPct_24hrIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : cumulus.airLinkDataIn.pct_24hr.ToString();
		}
		private string AirLinkPct_NowcastIn(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataIn == null ? "--" : cumulus.airLinkDataIn.pct_nowcast.ToString();
		}
		private string AirLinkPct_1hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : cumulus.airLinkDataOut.pct_1hr.ToString();
		}
		private string AirLinkPct_3hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : cumulus.airLinkDataOut.pct_3hr.ToString();
		}
		private string AirLinkPct_24hrOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : cumulus.airLinkDataOut.pct_24hr.ToString();
		}
		private string AirLinkPct_NowcastOut(Dictionary<string, string> tagParams)
		{
			return cumulus.airLinkDataOut == null ? "--" : cumulus.airLinkDataOut.pct_nowcast.ToString();
		}


		private string Tagsnowdepth(Dictionary<string,string> tagParams)
		{
			var ts = DateTime.Now.Hour < cumulus.SnowDepthHour ? DateTime.Now.AddDays(-1) : DateTime.Now;
			return CheckRc(GetSnowDepth(ts).ToString(), tagParams);
		}

		private string Tagsnowlying(Dictionary<string, string> tagParams)
		{
			var ts = DateTime.Now.Hour < cumulus.SnowDepthHour ? DateTime.Now.AddDays(-1) : DateTime.Now;
			return GetSnowLying(ts).ToString();
		}

		private string Tagsnowfalling(Dictionary<string, string> tagParams)
		{
			var ts = DateTime.Now.Hour < cumulus.SnowDepthHour ? DateTime.Now.AddDays(-1) : DateTime.Now;
			return GetSnowFalling(ts).ToString();
		}

		private string Tagnewrecord(Dictionary<string,string> tagParams)
		{
			return station.AlltimeRecordTimestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagTempRecordSet(Dictionary<string,string> tagParams)
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

		private string TagWindRecordSet(Dictionary<string,string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.HighGust.Ts >= threshold ||
				station.AllTime.HighWind.Ts >= threshold ||
				station.AllTime.HighWindRun.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagRainRecordSet(Dictionary<string,string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.HighRainRate.Ts >= threshold ||
				station.AllTime.DailyRain.Ts >= threshold ||
				station.AllTime.HourlyRain.Ts >= threshold ||
				station.AllTime.LongestDryPeriod.Ts >= threshold ||
				station.AllTime.LongestWetPeriod.Ts >= threshold ||
				station.AllTime.MonthlyRain.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagHumidityRecordSet(Dictionary<string,string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.HighHumidity.Ts >= threshold ||
				station.AllTime.LowHumidity.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagPressureRecordSet(Dictionary<string,string> tagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.AllTime.LowPress.Ts >= threshold ||
				station.AllTime.HighPress.Ts >= threshold
			)
				return "1";

			return "0";
		}

		private string TagHighTempRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowTempRecordSet(Dictionary<string,string> tagParams)
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

		private string TagHighHeatIndexRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighHeatIndex.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowWindChillRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LowChill.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighMinTempRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighMinTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowMaxTempRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LowMaxTemp.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighDewPointRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighDewPoint.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowDewPointRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LowDewPoint.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindGustRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighGust.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindSpeedRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighWind.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighRainRateRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighRainRate.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHourlyRainRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HourlyRain.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighDailyRainRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.DailyRain.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighMonthlyRainRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.MonthlyRain.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHumidityRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighHumidity.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindrunRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighWindRun.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowHumidityRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LowHumidity.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighPressureRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighPress.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowPressureRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LowPress.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLongestDryPeriodRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LongestDryPeriod.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLongestWetPeriodRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LongestWetPeriod.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighTempRangeRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighDailyTempRange.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowTempRangeRecordSet(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LowDailyTempRange.Ts < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string Tagerrorlight(Dictionary<string,string> tagParams)
		{
			// TODO
			return "0";
		}

		private string TagtempTh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempTh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighTempTime, "HH:mm", tagParams);
		}

		private string TagtempTl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.LowTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempTl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowTempTime, "HH:mm", tagParams);
		}

		private string TagSolarTh(Dictionary<string,string> tagParams)
		{
			return ((int)station.HiLoToday.HighSolar).ToString();
		}

		private string TagTsolarTh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighSolarTime, "HH:mm", tagParams);
		}

		private string TagUvth(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighUv, tagParams, cumulus.UVDPlaces);
		}

		private string TagTuvth(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighUvTime, "HH:mm", tagParams);
		}

		private string TagapptempTh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighAppTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagRCapptempTh(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(station.HiLoToday.HighAppTemp.ToString(cumulus.TempFormat));
		}

		private string TagTapptempTh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighAppTempTime, "HH:mm", tagParams);
		}

		private string TagapptempTl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.LowAppTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagRCapptempTl(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(station.HiLoToday.LowAppTemp.ToString(cumulus.TempFormat));
		}

		private string TagTapptempTl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowAppTempTime, "HH:mm", tagParams);
		}

		private string TagfeelslikeTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighFeelsLike, tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighFeelsLikeTime, "HH:mm", tagParams);
		}

		private string TagfeelslikeTl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.LowFeelsLike, tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeTl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowFeelsLikeTime, "HH:mm", tagParams);
		}

		private string TaghumidexTh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighHumidex, tagParams, cumulus.TempDPlaces);
		}

		private string TagThumidexTh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighHumidexTime, "HH:mm", tagParams);
		}

		private string TagdewpointTh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighDewPoint, tagParams, cumulus.TempDPlaces);
		}

		private string TagRCdewpointTh(Dictionary<string,string> tagParams)
		{
		   return ReplaceCommas(station.HiLoToday.HighDewPoint.ToString(cumulus.TempFormat));
		}

		private string TagTdewpointTh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighDewPointTime, "HH:mm", tagParams);
		}

		private string TagdewpointTl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.LowDewPoint, tagParams, cumulus.TempDPlaces);
		}

		private string TagRCdewpointTl(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(station.HiLoToday.LowDewPoint.ToString(cumulus.TempFormat));
		}

		private string TagTdewpointTl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowDewPointTime, "HH:mm", tagParams);
		}

		private string TagwchillTl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.LowWindChill, tagParams, cumulus.TempDPlaces);
		}

		private string TagRCwchillTl(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(station.HiLoToday.LowWindChill.ToString(cumulus.TempFormat));
		}

		private string TagTwchillTl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowWindChillTime, "HH:mm", tagParams);
		}

		private string TagheatindexTh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighHeatIndex, tagParams, cumulus.TempDPlaces);
		}

		private string TagRCheatindexTh(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(station.HiLoToday.HighHeatIndex.ToString(cumulus.TempFormat));
		}

		private string TagTheatindexTh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighHeatIndexTime, "HH:mm", tagParams);
		}

		private string TagheatindexYh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighHeatIndex, tagParams, cumulus.TempDPlaces);
		}

		private string TagTheatindexYh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighHeatIndexTime, "HH:mm", tagParams);
		}

		private string TagpressTh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighPress, tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressTh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighPressTime, "HH:mm", tagParams);
		}

		private string TagpressTl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.LowPress, tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressTl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowPressTime, "HH:mm", tagParams);
		}

		private string TaghumTh(Dictionary<string,string> tagParams)
		{
			return station.HiLoToday.HighHumidity.ToString();
		}

		private string TagThumTh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighHumidityTime, "HH:mm", tagParams);
		}

		private string TaghumTl(Dictionary<string,string> tagParams)
		{
			return station.HiLoToday.LowHumidity.ToString();
		}

		private string TagThumTl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.LowHumidityTime, "HH:mm", tagParams);
		}

		private string TagwindTm(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighWind, tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagTbeaufort(Dictionary<string,string> tagParams)
		{
			return "F" + cumulus.Beaufort(station.HiLoToday.HighWind);
		}

		private string TagTbeaufortnumber(Dictionary<string,string> tagParams)
		{
			return cumulus.Beaufort(station.HiLoToday.HighWind);
		}

		private string TagTbeaudesc(Dictionary<string,string> tagParams)
		{
			return cumulus.BeaufortDesc(station.HiLoToday.HighWind);
		}

		private string TagTwindTm(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighWindTime, "HH:mm", tagParams);
		}

		private string TagwgustTm(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighGust, tagParams, cumulus.WindDPlaces);
		}

		private string TagTwgustTm(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighGustTime, "HH:mm", tagParams);
		}

		private string TagbearingTm(Dictionary<string,string> tagParams)
		{
			return station.HiLoToday.HighGustBearing.ToString();
		}

		private string TagdirectionTm(Dictionary<string, string> tagParams)
		{
			return station.CompassPoint(station.HiLoToday.HighGustBearing);
		}

		private string TagrrateTm(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighRainRate, tagParams, cumulus.RainDPlaces);
		}

		private string TagTrrateTm(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighRainRateTime, "HH:mm", tagParams);
		}

		private string TaghourlyrainTh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoToday.HighHourlyRain, tagParams, cumulus.RainDPlaces);
		}

		private string TagThourlyrainTh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoToday.HighHourlyRainTime, "HH:mm", tagParams);
		}

		private string TaghourlyrainYh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighHourlyRain, tagParams, cumulus.RainDPlaces);
		}

		private string TagThourlyrainYh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighHourlyRainTime, "HH:mm", tagParams);
		}

		private string TagSolarYh(Dictionary<string,string> tagParams)
		{
			return station.HiLoYest.HighSolar.ToString("F0");
		}

		private string TagTsolarYh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighSolarTime, "HH:mm", tagParams);
		}

		private string TagUvyh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighUv, tagParams, cumulus.UVDPlaces);
		}

		private string TagTuvyh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighUvTime, "HH:mm", tagParams);
		}

		private string Tagrollovertime(Dictionary<string,string> tagParams)
		{
			switch (cumulus.GetHourInc())
			{
				case 0:
					return "midnight";
				case -9:
					return "9 am";
				case -10:
					return "10 am";
				default:
					return "unknown";
			}
		}

		private string TagRg11RainToday(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RG11RainToday, tagParams, cumulus.RainDPlaces);
		}

		private string TagRg11RainYest(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RG11RainYesterday, tagParams, cumulus.RainDPlaces);
		}

		private string Tagcurrcond(Dictionary<string,string> tagParams)
		{
			return GetCurrCondText();
		}

		private string Tagcurrcondenc(Dictionary<string,string> tagParams)
		{
			return EncodeForWeb(GetCurrCondText());
		}

		private string TagcurrcondJsEnc(Dictionary<string, string> tagParams)
		{
			return EncodeForJs(GetCurrCondText());
		}


		private string TagtempYh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempYh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighTempTime, "HH:mm", tagParams);
		}

		private string TagtempYl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.LowTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempYl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowTempTime, "HH:mm", tagParams);
		}

		private string TagapptempYh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighAppTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagTapptempYh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighAppTempTime, "HH:mm", tagParams);
		}

		private string TagapptempYl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.LowAppTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagTapptempYl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowAppTempTime, "HH:mm", tagParams);
		}

		private string TagdewpointYh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighDewPoint, tagParams, cumulus.TempDPlaces);
		}

		private string TagfeelslikeYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighFeelsLike, tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighFeelsLikeTime, "HH:mm", tagParams);
		}

		private string TagfeelslikeYl(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.LowFeelsLike, tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeYl(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowFeelsLikeTime, "HH:mm", tagParams);
		}

		private string TaghumidexYh(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighHumidex, tagParams, cumulus.TempDPlaces);
		}

		private string TagThumidexYh(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighHumidexTime, "HH:mm", tagParams);
		}

		private string TagTdewpointYh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighDewPointTime, "HH:mm", tagParams);
		}

		private string TagdewpointYl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.LowDewPoint, tagParams, cumulus.TempDPlaces);
		}

		private string TagTdewpointYl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowDewPointTime, "HH:mm", tagParams);
		}

		private string TagwchillYl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.LowWindChill, tagParams, cumulus.TempDPlaces);
		}

		private string TagTwchillYl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowWindChillTime, "HH:mm", tagParams);
		}

		private string TagpressYh(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighPress, tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressYh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighPressTime, "HH:mm", tagParams);
		}

		private string TagpressYl(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.LowPress, tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressYl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowPressTime, "HH:mm", tagParams);
		}

		private string TaghumYh(Dictionary<string,string> tagParams)
		{
			return station.HiLoYest.HighHumidity.ToString();
		}

		private string TagThumYh(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighHumidityTime, "HH:mm", tagParams);
		}

		private string TaghumYl(Dictionary<string,string> tagParams)
		{
			return station.HiLoYest.LowHumidity.ToString();
		}

		private string TagThumYl(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.LowHumidityTime, "HH:mm", tagParams);
		}

		private string TagwindYm(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighWind, tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagYbeaufort(Dictionary<string,string> tagParams)
		{
			return "F" + station.Beaufort(station.HiLoYest.HighWind);
		}

		private string TagYbeaufortnumber(Dictionary<string,string> tagParams)
		{
			return station.Beaufort(station.HiLoYest.HighWind).ToString();
		}

		private string TagYbeaudesc(Dictionary<string,string> tagParams)
		{
			return cumulus.BeaufortDesc(station.HiLoYest.HighWind);
		}

		private string TagTwindYm(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighWindTime, "HH:mm", tagParams);
		}

		private string TagwgustYm(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighGust, tagParams, cumulus.WindDPlaces);
		}

		private string TagTwgustYm(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighGustTime, "HH:mm", tagParams);
		}

		private string TagbearingYm(Dictionary<string,string> tagParams)
		{
			return station.HiLoYest.HighGustBearing.ToString();
		}

		private string TagdirectionYm(Dictionary<string, string> tagParams)
		{
			return  station.CompassPoint(station.HiLoYest.HighGustBearing);
		}

		private string TagrrateYm(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.HiLoYest.HighRainRate, tagParams, cumulus.RainDPlaces);
		}

		private string TagTrrateYm(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.HiLoYest.HighRainRateTime, "HH:mm", tagParams);
		}

		private string TagrfallY(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.RainYesterday, tagParams, cumulus.RainDPlaces);
		}

		// all time records
		private string TagtempH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagtempL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.LowTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTtempL(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagapptempH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighAppTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTapptempH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighAppTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagapptempL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.LowAppTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTapptempL(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowAppTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagfeelslikeH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighFeelsLike.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighFeelsLike.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagfeelslikeL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AllTime.LowFeelsLike.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTfeelslikeL(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowFeelsLike.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TaghumidexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighHumidex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagThumidexH(Dictionary<string, string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighHumidex.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagdewpointH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighDewPoint.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTdewpointH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighDewPoint.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagdewpointL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.LowDewPoint.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTdewpointL(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowDewPoint.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagheatindexH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighHeatIndex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTheatindexH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighHeatIndex.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TaggustM(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighGust.Val, tagParams, cumulus.WindDPlaces);
		}

		private string TagTgustM(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighGust.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagwspeedH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighWind.Val, tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagTwspeedH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighWind.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagwchillH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.LowChill.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTwchillH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowChill.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagrrateM(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighRainRate.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagTrrateM(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighRainRate.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagwindrunH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighWindRun.Val, tagParams, cumulus.WindRunDPlaces);
		}

		private string TagTwindrunH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighWindRun.Ts, "o\\n dd MMMM yyyy", tagParams);
		}

		private string TagrfallH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.DailyRain.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagTrfallH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.DailyRain.Ts, "o\\n dd MMMM yyyy", tagParams);
		}

		private string TagLongestDryPeriod(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LongestDryPeriod.Val.ToString("F0");
		}

		private string TagTLongestDryPeriod(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LongestDryPeriod.Ts, "\\to dd MMMM yyyy", tagParams);
		}

		private string TagLongestWetPeriod(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LongestWetPeriod.Val.ToString("F0");
		}

		private string TagTLongestWetPeriod(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LongestWetPeriod.Ts, "\\to dd MMMM yyyy", tagParams);
		}

		private string TagLowDailyTempRange(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.LowDailyTempRange.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagHighDailyTempRange(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighDailyTempRange.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTLowDailyTempRange(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowDailyTempRange.Ts, "o\\n dd MMMM yyyy", tagParams);
		}

		private string TagTHighDailyTempRange(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighDailyTempRange.Ts, "o\\n dd MMMM yyyy", tagParams);
		}

		private string TagrfallhH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HourlyRain.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagTrfallhH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HourlyRain.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagrfallmH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.MonthlyRain.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagTrfallmH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.MonthlyRain.Ts, "MMMM yyyy", tagParams);
		}

		private string TagpressH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighPress.Val, tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighPress.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagpressL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.LowPress.Val, tagParams, cumulus.PressDPlaces);
		}

		private string TagTpressL(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowPress.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TaghumH(Dictionary<string,string> tagParams)
		{
			return station.AllTime.HighHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagThumH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighHumidity.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TaghumL(Dictionary<string,string> tagParams)
		{
			return station.AllTime.LowHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagThumL(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowHumidity.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string Tagrecordsbegandate(Dictionary<string,string> tagParams)
		{
			var begandate = DateTime.Parse(cumulus.RecordsBeganDate);
			return GetFormattedDateTime(begandate, "dd MMMM yyyy", tagParams);
		}

		private string TagDaysSinceRecordsBegan(Dictionary<string,string> tagParams)
		{
			var begandate = DateTime.Parse(cumulus.RecordsBeganDate);
			return (DateTime.Now - begandate).Days.ToString();
		}

		private string TagmintempH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.HighMinTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTmintempH(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.HighMinTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagmaxtempL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.AllTime.LowMaxTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagTmaxtempL(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.AllTime.LowMaxTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		// end of all-time records
		// month by month all time records
		private string TagByMonthTempH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthTempHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthTempL(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthTempLt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthAppTempH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighAppTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthAppTempHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighAppTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthAppTempL(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowAppTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthAppTempLt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowAppTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthFeelsLikeH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighFeelsLike, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthFeelsLikeHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighFeelsLike.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthFeelsLikeL(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowFeelsLike, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthFeelsLikeLt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowFeelsLike.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthHumidexH(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighHumidex, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthHumidexHt(Dictionary<string, string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighHumidex.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthDewPointH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighDewPoint, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthDewPointHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighDewPoint.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthDewPointL(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowDewPoint, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthDewPointLt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowDewPoint.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthHeatIndexH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighHeatIndex, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthHeatIndexHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighHeatIndex.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthGustH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighGust, tagParams, cumulus.WindDPlaces);
		}

		private string TagByMonthGustHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighGust.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthWindH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighWind, tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagByMonthWindHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighWind.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthWChillL(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowChill, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthWChillLt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowChill.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthRainRateH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighRainRate, tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthRainRateHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighRainRate.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthWindRunH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighWindRun, tagParams, cumulus.WindRunDPlaces);
		}

		private string TagByMonthWindRunHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighWindRun.Ts, "o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthDailyRainH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].DailyRain, tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthDailyRainHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].DailyRain.Ts, "o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthLongestDryPeriod(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LongestDryPeriod, tagParams, 0);
		}

		private string TagByMonthLongestDryPeriodT(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LongestDryPeriod.Ts, "\\to dd MMMM yyyy", tagParams);
		}

		private string TagByMonthLongestWetPeriod(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LongestWetPeriod, tagParams, 0);
		}

		private string TagByMonthLongestWetPeriodT(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LongestWetPeriod.Ts, "\\to dd MMMM yyyy", tagParams);
		}

		private string TagByMonthLowDailyTempRange(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowDailyTempRange, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthHighDailyTempRange(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighDailyTempRange, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthLowDailyTempRangeT(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowDailyTempRange.Ts, "o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthHighDailyTempRangeT(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighDailyTempRange.Ts, "o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthHourlyRainH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HourlyRain, tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthHourlyRainHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HourlyRain.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthMonthlyRainH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].MonthlyRain, tagParams, cumulus.RainDPlaces);
		}

		private string TagByMonthMonthlyRainHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].MonthlyRain.Ts, "MMMM yyyy", tagParams);
		}

		private string TagByMonthPressH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighPress, tagParams, cumulus.PressDPlaces);
		}

		private string TagByMonthPressHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighPress.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthPressL(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowPress, tagParams, cumulus.PressDPlaces);
		}

		private string TagByMonthPressLt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowPress.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthHumH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighHumidity, tagParams, cumulus.HumDPlaces);
		}

		private string TagByMonthHumHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighHumidity.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthHumL(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowHumidity, tagParams, cumulus.HumDPlaces);
		}

		private string TagByMonthHumLt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowHumidity.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthMinTempH(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].HighMinTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthMinTempHt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].HighMinTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		private string TagByMonthMaxTempL(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetMonthlyAlltimeValueStr(station.MonthlyRecs[month].LowMaxTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagByMonthMaxTempLt(Dictionary<string,string> tagParams)
		{
			var month = GetMonthParam(tagParams);
			return GetFormattedDateTime(station.MonthlyRecs[month].LowMaxTemp.Ts, "\\a\\t HH:mm o\\n dd MMMM yyyy", tagParams);
		}

		// end of month-by-month all-time records

		private string Taggraphperiod(Dictionary<string,string> tagParams)
		{
			return cumulus.GraphHours.ToString();
		}

		private string Tagstationtype(Dictionary<string,string> tagParams)
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

		private string Taglatitude(Dictionary<string,string> tagParams)
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

		private string Taglongitude(Dictionary<string,string> tagParams)
		{
			var dpstr = tagParams.Get("dp");
			if (dpstr == null)
			{
				return cumulus.LonTxt;
			}

			try
			{
				var dp = int.Parse(dpstr);
				return CheckRcDp(cumulus.Longitude, tagParams, 2);
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

		private string Taglocation(Dictionary<string,string> tagParams)
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


		private string Taglonglocation(Dictionary<string,string> tagParams)
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

		private string Tagsunrise(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.SunRiseTime), "HH:mm", tagParams);
		}

		private string Tagsunset(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.SunSetTime), "HH:mm", tagParams);
		}

		private string Tagdaylength(Dictionary<string,string> tagParams)
		{
			if (tagParams.Get("format") == null)
				return ((int)cumulus.DayLength.TotalHours).ToString("D2") + ":" + cumulus.DayLength.Minutes.ToString("D2");
			return GetFormattedDateTime(cumulus.DayLength, "HH:mm", tagParams);
		}

		private string Tagdawn(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.Dawn), "HH:mm", tagParams);
		}

		private string Tagdusk(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.Dusk), "HH:mm", tagParams);
		}

		private string Tagdaylightlength(Dictionary<string,string> tagParams)
		{
			if (tagParams.Get("format") == null)
				return ((int)cumulus.DaylightLength.TotalHours).ToString("D2") + ":" + cumulus.DaylightLength.Minutes.ToString("D2");
			return GetFormattedDateTime(cumulus.DaylightLength, "HH:mm", tagParams);
		}

		private string Tagisdaylight(Dictionary<string,string> tagParams)
		{
			return cumulus.IsDaylight() ? "1" : "0";
		}

		private string TagIsSunUp(Dictionary<string,string> tagParams)
		{
			return cumulus.IsSunUp() ? "1" : "0";
		}

		private string TagSensorContactLost(Dictionary<string,string> tagParams)
		{
			return station.SensorContactLost ? "1" : "0";
		}

		private string TagDataStopped(Dictionary<string,string> tagParams)
		{
			return cumulus.DataStoppedAlarm.Triggered ? "1" : "0";
		}

		private string Tagmoonrise(Dictionary<string,string> tagParams)
		{
			return cumulus.MoonRiseTime.Hours < 0 ? "-----" : GetFormattedDateTime(cumulus.MoonRiseTime, "HH:mm", tagParams);
		}

		private string Tagmoonset(Dictionary<string,string> tagParams)
		{
			return cumulus.MoonSetTime.Hours < 0 ? "-----" : GetFormattedDateTime(cumulus.MoonSetTime, "HH:mm", tagParams);
		}

		private string Tagmoonphase(Dictionary<string,string> tagParams)
		{
			return cumulus.MoonPhaseString;
		}

		private string Tagaltitude(Dictionary<string,string> tagParams)
		{
			return cumulus.Altitude + (cumulus.AltitudeInFeet ? "&nbsp;ft" :"&nbsp;m");
		}

		private string Tagaltitudenoenc(Dictionary<string, string> tagParams)
		{
			return cumulus.Altitude + (cumulus.AltitudeInFeet ? " ft" : " m");
		}

		private string Tagforum(Dictionary<string,string> tagParams)
		{
			if (string.IsNullOrEmpty(cumulus.ForumURL))
			{
				return string.Empty;
			}

			return $":<a href=\\\"{cumulus.ForumURL}\\\">forum</a>:";
		}

		private string Tagforumurl(Dictionary<string, string> tagParams)
		{
			if (string.IsNullOrEmpty(cumulus.ForumURL))
			{
				return string.Empty;
			}

			return cumulus.ForumURL;
		}

		private string Tagwebcam(Dictionary<string,string> tagParams)
		{
			if (string.IsNullOrEmpty(cumulus.WebcamURL))
			{
				return string.Empty;
			}

			return @":<a href=\""" + cumulus.WebcamURL + @"\"">webcam</a>:";
		}
		private string Tagwebcamurl(Dictionary<string, string> tagParams)
		{
			if (string.IsNullOrEmpty(cumulus.WebcamURL))
			{
				return string.Empty;
			}

			return cumulus.WebcamURL;
		}


		private string Tagtempunit(Dictionary<string,string> tagParams)
		{
			return EncodeForWeb(cumulus.Units.TempText);
		}

		private string Tagtempunitnoenc(Dictionary<string, string> tagParams)
		{
			return cumulus.Units.TempText;
		}

		private string Tagtempunitnodeg(Dictionary<string,string> tagParams)
		{
			return cumulus.Units.TempText.Substring(1,1);
		}

		private string Tagwindunit(Dictionary<string,string> tagParams)
		{
			return cumulus.Units.WindText;
		}

		private string Tagwindrununit(Dictionary<string,string> tagParams)
		{
			return cumulus.Units.WindRunText;
		}

		private string Tagpressunit(Dictionary<string,string> tagParams)
		{
			return cumulus.Units.PressText;
		}

		private string Tagrainunit(Dictionary<string,string> tagParams)
		{
			return cumulus.Units.RainText;
		}

		private string Taginterval(Dictionary<string,string> tagParams)
		{
			return cumulus.UpdateInterval.ToString();
		}

		private string Tagrealtimeinterval(Dictionary<string,string> tagParams)
		{
			return (cumulus.RealtimeInterval/1000).ToString();
		}

		private string Tagversion(Dictionary<string,string> tagParams)
		{
			return cumulus.Version;
		}

		private string Tagbuild(Dictionary<string,string> tagParams)
		{
			return cumulus.Build;
		}

		private string TagNewBuildAvailable(Dictionary<string, string> tagParams)
		{
			try
			{
				return (int.Parse(cumulus.Build) < int.Parse(cumulus.LatestBuild)) ? "1" : "0";
			}
			catch
			{
				return "0";
			}
		}

		private string TagNewBuildNumber(Dictionary<string, string> tagParams)
		{
			return cumulus.LatestBuild;
		}

		private static string Tagupdate(Dictionary<string,string> tagParams)
		{
			string dtformat = tagParams.Get("format");

			return dtformat == null ? DateTime.Now.ToString() : DateTime.Now.ToString(dtformat);
		}

		private string TagLatestNoaaMonthlyReport(Dictionary<string,string> tagParams)
		{
			return cumulus.NOAAconf.LatestMonthReport;
		}

		private string TagLatestNoaaYearlyReport(Dictionary<string,string> tagParams)
		{
			return cumulus.NOAAconf.LatestYearReport;
		}

		private string TagMoonPercent(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(cumulus.MoonPercent, tagParams, 0);
		}

		private string TagMoonPercentAbs(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(Math.Abs(cumulus.MoonPercent), tagParams, 0);
		}

		private string TagMoonAge(Dictionary<string,string> tagParams)
		{
			var tcstr = tagParams.Get("tc");

			return tcstr == "y" ? Math.Truncate(cumulus.MoonAge).ToString() : CheckRcDp(cumulus.MoonAge, tagParams, 0);
		}

		private string TagLastRainTipIso(Dictionary<string,string> tagParams)
		{
			return station.LastRainTip;
		}

		private string TagLastRainTip(Dictionary<string, string> tagParams)
		{
			string dtformat = tagParams.Get("format");
			try
			{
				var lastTip = DateTime.Parse(station.LastRainTip);
				return lastTip.ToString(dtformat ?? "d");
			}
			catch (Exception)
			{
				return "---";
			}
		}

		private string TagMinutesSinceLastRainTip(Dictionary<string,string> tagParams)
		{
			DateTime lastTip;
			try
			{
				lastTip = DateTime.Parse(station.LastRainTip);
			}
			catch (Exception)
			{
				return "---";
			}

			return ((int) (DateTime.Now - lastTip).TotalMinutes).ToString();
		}

		private string TagRCtemp(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagtemp(tagParams));
		}

		private string TagRCtempTh(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagtempTh(tagParams));
		}

		private string TagRCtempTl(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagtempTl(tagParams));
		}

		private string TagRCintemp(Dictionary<string, string> tagParams)
		{
			return ReplaceCommas(Tagintemp(tagParams));
		}

		private string TagRCdew(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagdew(tagParams));
		}

		private string TagRCheatindex(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagheatindex(tagParams));
		}

		private string TagRCwchill(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagwchill(tagParams));
		}

		private string TagRChum(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Taghum(tagParams));
		}

		private string TagRCinhum(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Taginhum(tagParams));
		}

		private string TagRCrfall(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagrfall(tagParams));
		}

		private string TagRCrrate(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagrrate(tagParams));
		}

		private string TagRCrrateTm(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagrrateTm(tagParams));
		}

		private string TagRCwlatest(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagwlatest(tagParams));
		}

		private string TagRCwgust(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagwgust(tagParams));
		}

		private string TagRCwspeed(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagwspeed(tagParams));
		}

		private string TagRCwgustTm(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagwgustTm(tagParams));
		}

		private string TagRCpress(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(Tagpress(tagParams));
		}

		private string TagRCpressTh(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagpressTh(tagParams));
		}

		private string TagRCpressTl(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagpressTl(tagParams));
		}

		private string TagEt(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ET, tagParams, cumulus.RainDPlaces + 1);
		}

		private string TagLight(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.LightValue, tagParams, 1);
		}

		private string TagUv(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.UV, tagParams, cumulus.UVDPlaces);
		}

		private string TagSolarRad(Dictionary<string,string> tagParams)
		{
			return ((int) station.SolarRad).ToString();
		}

		private string TagCurrentSolarMax(Dictionary<string,string> tagParams)
		{
			return ((int) Math.Round(station.CurrentSolarMax)).ToString();
		}

		private string TagSunshineHours(Dictionary<string,string> tagParams)
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
				start = new DateTime(int.Parse(year), int.Parse(month), 1);
				end = start.AddMonths(1);
			}
			else if (rel != null)
			{
				end = DateTime.Now;
				start = new DateTime(end.Year, end.Month, 1).AddMonths(int.Parse(rel));
				end = start.AddMonths(1);
			}
			else
			{
				end = DateTime.Now;
				start = new DateTime(end.Year, end.Month, 1);
			}

			return CheckRcDp(station.DayFile.Where(rec => rec.Date >= start && rec.Date < end).Sum(rec => rec.SunShineHours), tagParams, 1);
		}

		private string TagSunshineHoursYear(Dictionary<string, string> tagParams)
		{
			var year = tagParams.Get("y");
			var rel = tagParams.Get("r"); // relative year, -1, -2, etc
			DateTime start;
			DateTime end;

			if (year != null)
			{
				start = new DateTime(int.Parse(year), 1, 1);
				end = start.AddYears(1);
			}
			else if (rel != null)
			{
				end = DateTime.Now;
				start = new DateTime(end.Year, 1, 1).AddYears(int.Parse(rel));
				end = start.AddYears(1);
			}
			else
			{
				end = DateTime.Now;
				start = new DateTime(end.Year, 1, 1);
			}

			return CheckRcDp(station.DayFile.Where(x => x.Date >= start && x.Date < end).Sum(x => x.SunShineHours), tagParams, 1);
		}


		private string TagThwIndex(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.THWIndex, tagParams, 1);
		}

		private string TagThswIndex(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.THSWIndex, tagParams, 1);
		}

		private string TagChillHours(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ChillHours, tagParams, 1);
		}

		private string TagChillHoursToday(Dictionary<string, string> tagParams)
		{
			if (station.YestChillHours == -1)
				return "n/a";

			// subtract today from yesterday, unless it has been reset, then its just today
			var hrs = station.ChillHours > station.YestChillHours ? station.ChillHours - station.YestChillHours : station.ChillHours;
			return CheckRcDp(hrs, tagParams, 1);
		}

		private string TagChillHoursYesterday(Dictionary<string, string> tagParams)
		{
			var dayb4yest = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day).AddDays(-2);
			WeatherStation.dayfilerec rec;
			try
			{
				rec = station.DayFile.Where(r => r.Date == dayb4yest).Single();
			}
			catch
			{
				return "n/a";
			}


			double hrs;
			if (station.YestChillHours == -1)
				return "n/a";

			if (station.YestChillHours > rec.ChillHours)
				hrs = station.YestChillHours - rec.ChillHours;
			else
				hrs = station.YestChillHours;

			return CheckRcDp(hrs, tagParams, 1);
		}


		private string TagYChillHours(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.YestChillHours, tagParams, 1);
		}

		private string TagYSunshineHours(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.YestSunshineHours, tagParams, cumulus.SunshineDPlaces);
		}

		private string TagIsSunny(Dictionary<string,string> tagParams)
		{
			return station.IsSunny ? "1" : "0";
		}

		private string TagIsFreezing(Dictionary<string,string> tagParams)
		{
			return station.ConvertUserTempToC(station.OutdoorTemperature)<0.09 ? "1" : "0";
		}

		private string TagIsRaining(Dictionary<string,string> tagParams)
		{
			return station.IsRaining ? "1" : "0";
		}

		private string TagConsecutiveRainDays(Dictionary<string,string> tagParams)
		{
			return station.ConsecutiveRainDays.ToString();
		}

		private string TagConsecutiveDryDays(Dictionary<string,string> tagParams)
		{
			return station.ConsecutiveDryDays.ToString();
		}

		// Extra sensors
		private string TagExtraTemp1(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[1], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp2(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[2], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp3(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[3], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp4(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[4], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp5(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[5], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp6(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[6], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp7(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[7], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp8(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[8], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp9(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[9], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraTemp10(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraTemp[10], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp1(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[1], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp2(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[2], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp3(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[3], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp4(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[4], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp5(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[5], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp6(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[6], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp7(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[7], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp8(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[8], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp9(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[9], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraDp10(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraDewPoint[10], tagParams, cumulus.TempDPlaces);
		}

		private string TagExtraHum1(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[1], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum2(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[2], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum3(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[3], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum4(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[4], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum5(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[5], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum6(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[6], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum7(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[7], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum8(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[8], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum9(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[9], tagParams, cumulus.HumDPlaces);
		}

		private string TagExtraHum10(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ExtraHum[10], tagParams, cumulus.HumDPlaces);
		}

		private string TagSoilTemp1(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.SoilTemp1, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp2(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.SoilTemp2, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp3(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.SoilTemp3, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp4(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.SoilTemp4, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp5(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp5, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp6(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp6, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp7(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp7, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp8(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp8, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp9(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp9, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp10(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp10, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp11(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp11, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp12(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp12, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp13(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp13, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp14(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp14, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp15(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp15, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilTemp16(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.SoilTemp16, tagParams, cumulus.TempDPlaces);
		}

		private string TagSoilMoisture1(Dictionary<string,string> tagParams)
		{
			return station.SoilMoisture1.ToString();
		}

		private string TagSoilMoisture2(Dictionary<string,string> tagParams)
		{
			return station.SoilMoisture2.ToString();
		}

		private string TagSoilMoisture3(Dictionary<string,string> tagParams)
		{
			return station.SoilMoisture3.ToString();
		}

		private string TagSoilMoisture4(Dictionary<string,string> tagParams)
		{
			return station.SoilMoisture4.ToString();
		}

		private string TagSoilMoisture5(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture5.ToString();
		}

		private string TagSoilMoisture6(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture6.ToString();
		}

		private string TagSoilMoisture7(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture7.ToString();
		}

		private string TagSoilMoisture8(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture8.ToString();
		}

		private string TagSoilMoisture9(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture9.ToString();
		}

		private string TagSoilMoisture10(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture10.ToString();
		}

		private string TagSoilMoisture11(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture12.ToString();
		}

		private string TagSoilMoisture12(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture12.ToString();
		}

		private string TagSoilMoisture13(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture13.ToString();
		}

		private string TagSoilMoisture14(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture14.ToString();
		}

		private string TagSoilMoisture15(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture15.ToString();
		}

		private string TagSoilMoisture16(Dictionary<string, string> tagParams)
		{
			return station.SoilMoisture16.ToString();
		}

		private string TagUserTemp1(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.UserTemp[1], tagParams, cumulus.TempDPlaces);
		}

		private string TagUserTemp2(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.UserTemp[2], tagParams, cumulus.TempDPlaces);
		}

		private string TagUserTemp3(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.UserTemp[3], tagParams, cumulus.TempDPlaces);
		}

		private string TagUserTemp4(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.UserTemp[4], tagParams, cumulus.TempDPlaces);
		}

		private string TagUserTemp5(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.UserTemp[5], tagParams, cumulus.TempDPlaces);
		}

		private string TagUserTemp6(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.UserTemp[6], tagParams, cumulus.TempDPlaces);
		}

		private string TagUserTemp7(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.UserTemp[7], tagParams, cumulus.TempDPlaces);
		}

		private string TagUserTemp8(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.UserTemp[8], tagParams, cumulus.TempDPlaces);
		}

		private string TagAirQuality1(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AirQuality1, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirQuality2(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AirQuality2, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirQuality3(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AirQuality3, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirQuality4(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AirQuality4, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirQualityAvg1(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AirQualityAvg1, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirQualityAvg2(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AirQualityAvg2, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirQualityAvg3(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AirQuality3, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagAirQualityAvg4(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.AirQualityAvg4, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagCo2(Dictionary<string, string> tagParams)
		{
			return station.CO2.ToString();
		}

		private string TagCO2_24h(Dictionary<string, string> tagParams)
		{
			return station.CO2_24h.ToString();
		}

		private string TagCO2_pm2p5(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.CO2_pm2p5, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagCO2_pm2p5_24h(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.CO2_pm2p5_24h, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagCO2_pm10(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.CO2_pm10, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagCO2_pm10_24h(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.CO2_pm10_24h, tagParams, cumulus.AirQualityDPlaces);
		}

		private string TagC02_temp(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.CO2_temperature, tagParams, cumulus.TempDPlaces);
		}

		private string TagC02_hum(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.CO2_humidity, tagParams, cumulus.HumDPlaces);
		}

		private string TagLeafTemp1(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.LeafTemp1, tagParams, cumulus.TempDPlaces);
		}

		private string TagLeafTemp2(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.LeafTemp2, tagParams, cumulus.TempDPlaces);
		}

		private string TagLeafTemp3(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.LeafTemp3, tagParams, cumulus.TempDPlaces);
		}

		private string TagLeafTemp4(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.LeafTemp4, tagParams, cumulus.TempDPlaces);
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
			return station.LightningDistance == 999 ? "--" : CheckRcDp(station.LightningDistance, tagParams, cumulus.WindRunDPlaces);
		}

		private string TagLightningTime(Dictionary<string, string> tagParams)
		{
			return DateTime.Compare(station.LightningTime, new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)) == 0 ? "---" : GetFormattedDateTime(station.LightningTime, "t", tagParams);
		}

		private string TagLightningStrikesToday(Dictionary<string, string> tagParams)
		{
			return station.LightningStrikesToday.ToString();
		}

		private string TagLeafWetness1(Dictionary<string,string> tagParams)
		{
			return station.LeafWetness1.ToString();
		}

		private string TagLeafWetness2(Dictionary<string,string> tagParams)
		{
			return station.LeafWetness2.ToString();
		}

		private string TagLeafWetness3(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness3.ToString();
		}

		private string TagLeafWetness4(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness4.ToString();
		}

		private string TagLeafWetness5(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness5.ToString();
		}

		private string TagLeafWetness6(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness6.ToString();
		}

		private string TagLeafWetness7(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness7.ToString();
		}

		private string TagLeafWetness8(Dictionary<string, string> tagParams)
		{
			return station.LeafWetness8.ToString();
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

		private string TagHighTempAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.HighTempAlarm.Enabled)
			{
				return cumulus.HighTempAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagTempChangeUpAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.TempChangeAlarm.Enabled)
			{
				return cumulus.TempChangeAlarm.UpTriggered ? "1" : "0";
			}

			return "0";
		}

		private string TagTempChangeDownAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.TempChangeAlarm.Enabled)
			{
				return cumulus.TempChangeAlarm.DownTriggered ? "1" : "0";
			}

			return "0";
		}

		private string TagLowPressAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.LowPressAlarm.Enabled)
			{
				return cumulus.LowPressAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighPressAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.HighPressAlarm.Enabled)
			{
				return cumulus.HighPressAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagPressChangeUpAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.PressChangeAlarm.Enabled)
			{
				return cumulus.PressChangeAlarm.UpTriggered ? "1" : "0";
			}

			return "0";
		}

		private string TagPressChangeDownAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.PressChangeAlarm.Enabled)
			{
				return cumulus.PressChangeAlarm.DownTriggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighRainTodayAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.HighRainTodayAlarm.Enabled)
			{
				return cumulus.HighRainTodayAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighRainRateAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.HighRainRateAlarm.Enabled)
			{
				return cumulus.HighRainRateAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}

		private string TagHighWindGustAlarm(Dictionary<string,string> tagParams)
		{
			if (cumulus.HighGustAlarm.Enabled)
			{
				return cumulus.HighGustAlarm.Triggered? "1" : "0";
			}

			return "0";
		}

		private string TagHighWindSpeedAlarm(Dictionary<string,string> tagParams)
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
			if (cumulus.HttpUploadAlarm.Enabled)
			{
				return cumulus.HttpUploadAlarm.Triggered ? "1" : "0";
			}

			return "0";
		}


		// Monthly highs and lows - values
		private string TagMonthTempH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthTempL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.LowTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthHeatIndexH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighHeatIndex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthWChillL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.LowChill.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthAppTempH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighAppTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthAppTempL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.LowAppTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthFeelsLikeH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighFeelsLike.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthFeelsLikeL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.LowFeelsLike.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthHumidexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighHumidex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthDewPointH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighDewPoint.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthDewPointL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.LowDewPoint.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagMonthMinTempH(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.HighMinTemp.Val > -999 ? CheckRcDp(station.ThisMonth.HighMinTemp.Val, tagParams, cumulus.TempDPlaces) : "--";
		}

		private string TagMonthMaxTempL(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.LowMaxTemp.Val < 999 ? CheckRcDp(station.ThisMonth.LowMaxTemp.Val, tagParams, cumulus.TempDPlaces) : "--";
		}

		private string TagMonthPressH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighPress.Val, tagParams, cumulus.PressDPlaces);
		}

		private string TagMonthPressL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.LowPress.Val, tagParams, cumulus.PressDPlaces);
		}

		private string TagMonthHumH(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.HighHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagMonthHumL(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.LowHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagMonthGustH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighGust.Val, tagParams, cumulus.WindDPlaces);
		}

		private string TagMonthWindH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighWind.Val, tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagMonthWindRunH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighWindRun.Val, tagParams, cumulus.WindRunDPlaces);
		}

		private string TagMonthRainRateH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HighRainRate.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthHourlyRainH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.HourlyRain.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthDailyRainH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisMonth.DailyRain.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagMonthLongestDryPeriod(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.LongestDryPeriod.Val.ToString();
		}

		private string TagMonthLongestWetPeriod(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.LongestWetPeriod.Val.ToString();
		}

		private string TagMonthHighDailyTempRange(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.HighDailyTempRange.Val > -999 ? CheckRcDp(station.ThisMonth.HighDailyTempRange.Val, tagParams, cumulus.TempDPlaces) : "--";
		}

		private string TagMonthLowDailyTempRange(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.LowDailyTempRange.Val < 999 ? CheckRcDp(station.ThisMonth.LowDailyTempRange.Val, tagParams, cumulus.TempDPlaces) : "--";
		}

		// Monthly highs and lows - times
		private string TagMonthTempHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighTemp.Ts, "t", tagParams);
		}

		private string TagMonthTempLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowTemp.Ts, "t", tagParams);
		}

		private string TagMonthHeatIndexHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHeatIndex.Ts, "t", tagParams);
		}

		private string TagMonthWChillLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowChill.Ts, "t", tagParams);
		}

		private string TagMonthAppTempHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighAppTemp.Ts, "t", tagParams);
		}

		private string TagMonthAppTempLt(Dictionary<string,string> tagParams)
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

		private string TagMonthDewPointHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighDewPoint.Ts, "t", tagParams);
		}

		private string TagMonthDewPointLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowDewPoint.Ts, "t", tagParams);
		}

		private string TagMonthPressHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighPress.Ts, "t", tagParams);
		}

		private string TagMonthPressLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowPress.Ts, "t", tagParams);
		}

		private string TagMonthHumHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHumidity.Ts, "t", tagParams);
		}

		private string TagMonthHumLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowHumidity.Ts, "t", tagParams);
		}

		private string TagMonthGustHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighGust.Ts, "t", tagParams);
		}

		private string TagMonthWindHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighWind.Ts, "t", tagParams);
		}

		private string TagMonthRainRateHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighRainRate.Ts, "t", tagParams);
		}

		private string TagMonthHourlyRainHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HourlyRain.Ts, "t", tagParams);
		}

		// Monthly highs and lows - dates
		private string TagMonthTempHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthTempLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHeatIndexHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHeatIndex.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthWChillLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowChill.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthAppTempHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighAppTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthAppTempLd(Dictionary<string,string> tagParams)
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

		private string TagMonthDewPointHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighDewPoint.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthDewPointLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowDewPoint.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthMinTempHd(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.HighMinTemp.Val > -999 ? GetFormattedDateTime(station.ThisMonth.HighMinTemp.Ts, "dd MMMM", tagParams) : "---";
		}

		private string TagMonthMaxTempLd(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.LowMaxTemp.Val < 999 ? GetFormattedDateTime(station.ThisMonth.LowMaxTemp.Ts, "dd MMMM", tagParams) : "---";
		}

		private string TagMonthPressHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighPress.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthPressLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowPress.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHumHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighHumidity.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHumLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LowHumidity.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthGustHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighGust.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthWindHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighWind.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthRainRateHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighRainRate.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHourlyRainHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HourlyRain.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthHighDailyTempRangeD(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.HighDailyTempRange.Val < 999 ? GetFormattedDateTime(station.ThisMonth.HighDailyTempRange.Ts, "dd MMMM", tagParams) : "------";
		}

		private string TagMonthLowDailyTempRangeD(Dictionary<string,string> tagParams)
		{
			return station.ThisMonth.LowDailyTempRange.Val > -999 ? GetFormattedDateTime(station.ThisMonth.LowDailyTempRange.Ts, "dd MMMM", tagParams) : "------";
		}

		private string TagMonthWindRunHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.HighWindRun.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthDailyRainHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.DailyRain.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthLongestDryPeriodD(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LongestDryPeriod.Ts, "dd MMMM", tagParams);
		}

		private string TagMonthLongestWetPeriodD(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisMonth.LongestWetPeriod.Ts, "dd MMMM", tagParams);
		}

		// Yearly highs and lows - values
		private string TagYearTempH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearTempL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.LowTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearHeatIndexH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighHeatIndex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearWChillL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.LowChill.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearAppTempH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighAppTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearAppTempL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.LowAppTemp.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearFeelsLikeH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighFeelsLike.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearFeelsLikeL(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ThisYear.LowFeelsLike.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearHumidexH(Dictionary<string, string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighHumidex.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearDewPointH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighDewPoint.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearDewPointL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.LowDewPoint.Val, tagParams, cumulus.TempDPlaces);
		}

		private string TagYearMinTempH(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.HighMinTemp.Val > -999 ? CheckRcDp(station.ThisYear.HighMinTemp.Val, tagParams, cumulus.TempDPlaces) : "--";
		}

		private string TagYearMaxTempL(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.LowMaxTemp.Val < 999 ? CheckRcDp(station.ThisYear.LowMaxTemp.Val, tagParams, cumulus.TempDPlaces) : "--";
		}

		private string TagYearPressH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighPress.Val, tagParams, cumulus.PressDPlaces);
		}

		private string TagYearPressL(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.LowPress.Val, tagParams, cumulus.PressDPlaces);
		}

		private string TagYearHumH(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.HighHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagYearHumL(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.LowHumidity.Val.ToString(cumulus.HumFormat);
		}

		private string TagYearGustH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighGust.Val, tagParams, cumulus.WindDPlaces);
		}

		private string TagYearWindH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighWind.Val, tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagYearWindRunH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighWindRun.Val, tagParams, cumulus.WindRunDPlaces);
		}

		private string TagYearRainRateH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HighRainRate.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagYearHourlyRainH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.HourlyRain.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagYearDailyRainH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.DailyRain.Val, tagParams, cumulus.RainDPlaces);
		}

		private string TagYearLongestDryPeriod(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.LongestDryPeriod.Val.ToString();
		}

		private string TagYearLongestWetPeriod(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.LongestWetPeriod.Val.ToString();
		}

		private string TagYearHighDailyTempRange(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.HighDailyTempRange.Val > -999 ? CheckRcDp(station.ThisYear.HighDailyTempRange.Val, tagParams, cumulus.TempDPlaces) : "--";
		}

		private string TagYearLowDailyTempRange(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.LowDailyTempRange.Val < 999 ? CheckRcDp(station.ThisYear.LowDailyTempRange.Val, tagParams, cumulus.TempDPlaces) : "--";
		}

		private string TagYearMonthlyRainH(Dictionary<string,string> tagParams)
		{
			return CheckRcDp(station.ThisYear.MonthlyRain.Val, tagParams, cumulus.RainDPlaces);
		}

		// Yearly highs and lows - times
		private string TagYearTempHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighTemp.Ts, "t", tagParams);
		}

		private string TagYearTempLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowTemp.Ts, "t", tagParams);
		}

		private string TagYearHeatIndexHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHeatIndex.Ts, "t", tagParams);
		}

		private string TagYearWChillLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowChill.Ts, "t", tagParams);
		}

		private string TagYearAppTempHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighAppTemp.Ts, "t", tagParams);
		}

		private string TagYearAppTempLt(Dictionary<string,string> tagParams)
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
		private string TagYearDewPointHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighDewPoint.Ts, "t", tagParams);
		}

		private string TagYearDewPointLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowDewPoint.Ts, "t", tagParams);
		}

		private string TagYearPressHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighPress.Ts, "t", tagParams);
		}

		private string TagYearPressLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowPress.Ts, "t", tagParams);
		}

		private string TagYearHumHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHumidity.Ts, "t", tagParams);
		}

		private string TagYearHumLt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowHumidity.Ts, "t", tagParams);
		}

		private string TagYearGustHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighGust.Ts, "t", tagParams);
		}

		private string TagYearWindHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighWind.Ts, "t", tagParams);
		}

		private string TagYearRainRateHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighRainRate.Ts, "t", tagParams);
		}

		private string TagYearHourlyRainHt(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HourlyRain.Ts, "t", tagParams);
		}

		// Yearly highs and lows - dates
		private string TagYearTempHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagYearTempLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHeatIndexHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHeatIndex.Ts, "dd MMMM", tagParams);
		}

		private string TagYearWChillLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowChill.Ts, "dd MMMM", tagParams);
		}

		private string TagYearAppTempHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighAppTemp.Ts, "dd MMMM", tagParams);
		}

		private string TagYearAppTempLd(Dictionary<string,string> tagParams)
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
		private string TagYearDewPointHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighDewPoint.Ts, "dd MMMM", tagParams);
		}

		private string TagYearDewPointLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowDewPoint.Ts, "dd MMMM", tagParams);
		}

		private string TagYearMinTempHd(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.HighMinTemp.Val > -999 ? GetFormattedDateTime(station.ThisYear.HighMinTemp.Ts, "dd MMMM", tagParams) : "---";
		}

		private string TagYearMaxTempLd(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.LowMaxTemp.Val < 999 ? GetFormattedDateTime(station.ThisYear.LowMaxTemp.Ts, "dd MMMM", tagParams) : "---";
		}

		private string TagYearPressHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighPress.Ts, "dd MMMM", tagParams);
		}

		private string TagYearPressLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowPress.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHumHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighHumidity.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHumLd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LowHumidity.Ts, "dd MMMM", tagParams);
		}

		private string TagYearGustHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighGust.Ts, "dd MMMM", tagParams);
		}

		private string TagYearWindHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighWind.Ts, "dd MMMM", tagParams);
		}

		private string TagYearRainRateHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighRainRate.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHourlyRainHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HourlyRain.Ts, "dd MMMM", tagParams);
		}

		private string TagYearHighDailyTempRangeD(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.HighDailyTempRange.Val > -999 ? GetFormattedDateTime(station.ThisYear.HighDailyTempRange.Ts, "dd MMMM", tagParams) : "------";
		}

		private string TagYearLowDailyTempRangeD(Dictionary<string,string> tagParams)
		{
			return station.ThisYear.LowDailyTempRange.Val < 999 ? GetFormattedDateTime(station.ThisYear.LowDailyTempRange.Ts, "dd MMMM", tagParams) : "------";
		}

		private string TagYearWindRunHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.HighWindRun.Ts, "dd MMMM", tagParams);
		}

		private string TagYearDailyRainHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.DailyRain.Ts, "dd MMMM", tagParams);
		}

		private string TagYearLongestDryPeriodD(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LongestDryPeriod.Ts, "dd MMMM", tagParams);
		}

		private string TagYearLongestWetPeriodD(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.LongestWetPeriod.Ts, "dd MMMM", tagParams);
		}

		private string TagYearMonthlyRainHd(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.ThisYear.MonthlyRain.Ts, "MMMM", tagParams);
		}

		//------------------------------------------------------------

		private string TagLastDataReadT(Dictionary<string,string> tagParams)
		{
			return GetFormattedDateTime(station.LastDataReadTimestamp, "G",tagParams);
		}

		private string TagLatestError(Dictionary<string,string> tagParams)
		{
			return cumulus.LatestError;
		}

		private string TagLatestErrorDate(Dictionary<string,string> tagParams)
		{
			return cumulus.LatestErrorTS == DateTime.MinValue ? "------" : GetFormattedDateTime(cumulus.LatestErrorTS, "ddddd", tagParams);
		}

		private string TagLatestErrorTime(Dictionary<string,string> tagParams)
		{
			return cumulus.LatestErrorTS == DateTime.MinValue ? "------" : GetFormattedDateTime(cumulus.LatestErrorTS, "t", tagParams);
		}

		private static string TagOsVersion(Dictionary<string,string> tagParams)
		{
			return Environment.OSVersion.ToString();
		}

		private static string TagOsLanguage(Dictionary<string,string> tagParams)
		{
			return CultureInfo.CurrentCulture.DisplayName;
		}

		private string TagSystemUpTime(Dictionary<string,string> tagParams)
		{
			try
			{
				double upTime = 0;
				if (cumulus.Platform.Substring(0, 3) == "Win")
				{
					try
					{
						cumulus.UpTime.NextValue();
						upTime = cumulus.UpTime.NextValue();
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
					double.TryParse(strTime, out upTime);
				}

				TimeSpan ts = TimeSpan.FromSeconds(upTime);

				return string.Format($"{ts.Days} days {ts.Hours} hours");
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error processing SystemUpTime web tag");
				cumulus.LogMessage(ex.Message);
				return "Error";
			}
		}

		private static string TagProgramUpTime(Dictionary<string,string> tagParams)
		{
			// Bug in Mono Process.StartTime - wraps after 24 days
			TimeSpan ts = DateTime.Now - Program.StartTime;
			return string.Format($"{ts.Days} days {ts.Hours} hours");
		}

		private static string TagProgramUpTimeMs(Dictionary<string, string> tagParams)
		{
			// Bug in Mono Process.StartTime - wraps after 24 days
			TimeSpan ts = DateTime.Now - Program.StartTime;
			return ts.TotalMilliseconds.ToString();
		}

		private static string TagCpuName(Dictionary<string,string> tagParams)
		{
			return "n/a";
		}

		private static string TagCpuCount(Dictionary<string,string> tagParams)
		{
			return Environment.ProcessorCount.ToString();
		}

		private static string TagMemoryStatus(Dictionary<string,string> tagParams)
		{
			return "n/a";
		}

		private static string TagDisplayModeString(Dictionary<string,string> tagParams)
		{
			return "n/a";
		}

		private static string TagAllocatedMemory(Dictionary<string,string> tagParams)
		{
			return (Environment.WorkingSet/1048576.0).ToString("F2") + " MB";
		}

		private static string TagDiskSize(Dictionary<string,string> tagParams)
		{
			return "n/a";
		}

		private static string TagDiskFree(Dictionary<string,string> tagParams)
		{
			return "n/a";
		}

		private string TagCpuTemp(Dictionary<string, string> tagParams)
		{
			return cumulus.CPUtemp.ToString(cumulus.TempFormat);
		}

		private string TagDavisTotalPacketsReceived(Dictionary<string,string> tagParams)
		{
			return station.DavisTotalPacketsReceived.ToString();
		}

		private string TagDavisTotalPacketsMissed(Dictionary<string,string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisTotalPacketsMissed[tx].ToString();
		}

		private string TagDavisNumberOfResynchs(Dictionary<string,string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisNumberOfResynchs[tx].ToString();
		}

		private string TagDavisMaxInARow(Dictionary<string,string> tagParams)
		{
			int tx = int.TryParse(tagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisMaxInARow[tx].ToString();
		}

		private string TagDavisNumCrCerrors(Dictionary<string,string> tagParams)
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

		private string TagDavisFirmwareVersion(Dictionary<string,string> tagParams)
		{
			return station.DavisFirmwareVersion;
		}

		private string TagGw1000FirmwareVersion(Dictionary<string, string> tagParams)
		{
			return station.GW1000FirmwareVersion;
		}

		private string Tagdailygraphperiod(Dictionary<string, string> tagparams)
		{
			return cumulus.GraphDays.ToString();
		}

		// Recent history
		private string TagRecentOutsideTemp(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.OutdoorTemperature : result[0].OutsideTemp, tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentWindSpeed(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.WindAverage : result[0].WindSpeed, tagParams, cumulus.WindAvgDPlaces);
		}

		private string TagRecentWindGust(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.RecentMaxGust : result[0].WindGust, tagParams, cumulus.WindDPlaces);
		}

		private string TagRecentWindLatest(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.WindLatest : result[0].WindLatest, tagParams, cumulus.WindDPlaces);
		}

		private string TagRecentWindDir(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return result.Count == 0 ? station.Bearing.ToString() : result[0].WindDir.ToString();
		}

		private string TagRecentWindAvgDir(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return result.Count != 0 ? result[0].WindAvgDir.ToString() : station.AvgBearing.ToString();
		}

		private string TagRecentWindChill(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.WindChill : result[0].WindChill, tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentDewPoint(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.OutdoorDewpoint : result[0].DewPoint, tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentHeatIndex(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.HeatIndex : result[0].HeatIndex, tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentFeelsLike(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.FeelsLike : result[0].FeelsLike, tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentHumidex(Dictionary<string, string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.Humidex : result[0].Humidex, tagParams, cumulus.TempDPlaces);
		}

		private string TagRecentHumidity(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return result.Count == 0 ? station.OutdoorHumidity.ToString() : result[0].Humidity.ToString();
		}

		private string TagRecentPressure(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.Pressure : result[0].Pressure, tagParams, cumulus.PressDPlaces);
		}

		private string TagRecentRainToday(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.RainToday : result[0].RainToday, tagParams, cumulus.RainDPlaces);
		}

		private string TagRecentSolarRad(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return result.Count == 0 ? station.SolarRad.ToString("F0") : result[0].SolarRad.ToString("F0");
		}

		private string TagRecentUv(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return CheckRcDp(result.Count == 0 ? station.UV : result[0].UV, tagParams, cumulus.UVDPlaces);
		}

		private string TagRecentTs(Dictionary<string,string> tagParams)
		{
			var recentTs = GetRecentTs(tagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTs);

			return GetFormattedDateTime(result.Count == 0 ? DateTime.Now : result[0].Timestamp, tagParams);
		}

		// Recent history with commas replaced
		private string TagRcRecentOutsideTemp(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentOutsideTemp(tagParams));
		}

		private string TagRcRecentWindSpeed(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentWindSpeed(tagParams));
		}

		private string TagRcRecentWindGust(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentWindGust(tagParams));
		}

		private string TagRcRecentWindLatest(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentWindLatest(tagParams));
		}

		private string TagRcRecentWindChill(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentWindChill(tagParams));
		}

		private string TagRcRecentDewPoint(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentDewPoint(tagParams));
		}

		private string TagRcRecentHeatIndex(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentHeatIndex(tagParams));
		}

		private string TagRcRecentPressure(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentPressure(tagParams));
		}

		private string TagRcRecentRainToday(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentRainToday(tagParams));
		}

		private string TagRcRecentUv(Dictionary<string,string> tagParams)
		{
			return ReplaceCommas(TagRecentUv(tagParams));
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
				{ "presstrend", Tagpresstrend },
				{ "presstrendenglish", Tagpresstrendenglish },
				{ "cloudbase", Tagcloudbase },
				{ "cloudbasevalue", Tagcloudbasevalue },
				{ "cloudbaseunit", Tagcloudbaseunit },
				{ "dew", Tagdew },
				{ "wetbulb", Tagwetbulb },
				{ "presstrendval", Tagpresstrendval },
				{ "PressChangeLast3Hours", TagPressChangeLast3Hours },
				{ "windrunY", TagwindrunY },
				{ "domwindbearingY", TagdomwindbearingY },
				{ "domwinddirY", TagdomwinddirY },
				{ "tomorrowdaylength", Tagtomorrowdaylength },
				{ "windrun", Tagwindrun },
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
				{ "rhour", Tagrhour },
				{ "r24hour", Tagr24Hour },
				{ "ryear", Tagryear },
				{ "inhum", Taginhum },
				{ "intemp", Tagintemp },
				{ "battery", Tagbattery },
				{ "txbattery", Tagtxbattery },
				{ "ConsoleSupplyV", TagConsoleSupplyV },
				{ "MulticastBadCnt", TagMulticastBadCnt },
				{ "MulticastGoodCnt", TagMulticastGoodCnt },
				{ "MulticastGoodPct", TagMulticastGoodPct },
				{ "snowdepth", Tagsnowdepth },
				{ "snowlying", Tagsnowlying },
				{ "snowfalling", Tagsnowfalling },
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
				{ "hourlyrainYH", TaghourlyrainYh },
				{ "ThourlyrainYH", TagThourlyrainYh },
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
				{ "wchillH", TagwchillH },
				{ "TwchillH", TagTwchillH },
				{ "rrateM", TagrrateM },
				{ "TrrateM", TagTrrateM },
				{ "rfallH", TagrfallH },
				{ "TrfallH", TagTrfallH },
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
				{ "CO2", TagCo2 },
				{ "CO2-24h", TagCO2_24h },
				{ "CO2-pm2p5", TagCO2_pm2p5 },
				{ "CO2-pm2p5-24h", TagCO2_pm2p5_24h },
				{ "CO2-pm10", TagCO2_pm10 },
				{ "CO2-pm10-24h", TagCO2_pm10_24h },
				{ "CO2-temp", TagC02_temp },
				{ "CO2-hum", TagC02_hum },
				{ "LeakSensor1", TagLeakSensor1 },
				{ "LeakSensor2", TagLeakSensor2 },
				{ "LeakSensor3", TagLeakSensor3 },
				{ "LeakSensor4", TagLeakSensor4 },
				{ "LightningDistance", TagLightningDistance },
				{ "LightningTime", TagLightningTime },
				{ "LightningStrikesToday", TagLightningStrikesToday },
				{ "LeafTemp1", TagLeafTemp1 },
				{ "LeafTemp2", TagLeafTemp2 },
				{ "LeafTemp3", TagLeafTemp3 },
				{ "LeafTemp4", TagLeafTemp4 },
				{ "LeafWetness1", TagLeafWetness1 },
				{ "LeafWetness2", TagLeafWetness2 },
				{ "LeafWetness3", TagLeafWetness3 },
				{ "LeafWetness4", TagLeafWetness4 },
				{ "LeafWetness5", TagLeafWetness5 },
				{ "LeafWetness6", TagLeafWetness6 },
				{ "LeafWetness7", TagLeafWetness7 },
				{ "LeafWetness8", TagLeafWetness8 },

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
				{ "HighWindGustAlarm", TagHighWindGustAlarm },
				{ "HighWindSpeedAlarm", TagHighWindSpeedAlarm },
				{ "BatteryLowAlarm", TagBatteryLowAlarm },
				{ "DataSpikeAlarm", TagDataSpikeAlarm },
				{ "MySqlUploadAlarm", TagMySqlUploadAlarm },
				{ "HttpUploadAlarm", TagHttpUploadAlarm },
				{ "UpgradeAlarm", TagUpgradeAlarm },

				{ "RG11RainToday", TagRg11RainToday },
				{ "RG11RainYest", TagRg11RainYest },

				{ "AirLinkFirmwareVersionIn", TagAirLinkFirmwareVersionIn },
				{ "AirLinkWifiRssiIn", TagAirLinkWifiRssiIn },
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

				// Monthly highs and lows - values
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
				{ "MonthDailyRainH", TagMonthDailyRainH },
				{ "MonthDewPointH", TagMonthDewPointH },
				{ "MonthDewPointL", TagMonthDewPointL },
				{ "MonthWindRunH", TagMonthWindRunH },
				{ "MonthLongestDryPeriod", TagMonthLongestDryPeriod },
				{ "MonthLongestWetPeriod", TagMonthLongestWetPeriod },
				{ "MonthHighDailyTempRange", TagMonthHighDailyTempRange },
				{ "MonthLowDailyTempRange", TagMonthLowDailyTempRange },
				// This year"s highs and lows - times
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
				{ "MonthDewPointHT", TagMonthDewPointHt },
				{ "MonthDewPointLT", TagMonthDewPointLt },
				// This month"s highs and lows - dates
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
				{ "MonthDailyRainHD", TagMonthDailyRainHd },
				{ "MonthDewPointHD", TagMonthDewPointHd },
				{ "MonthDewPointLD", TagMonthDewPointLd },
				{ "MonthWindRunHD", TagMonthWindRunHd },
				{ "MonthLongestDryPeriodD", TagMonthLongestDryPeriodD },
				{ "MonthLongestWetPeriodD", TagMonthLongestWetPeriodD },
				{ "MonthHighDailyTempRangeD", TagMonthHighDailyTempRangeD },
				{ "MonthLowDailyTempRangeD", TagMonthLowDailyTempRangeD },
				// This Year"s highs and lows - values
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
				{ "YearDailyRainH", TagYearDailyRainH },
				{ "YearMonthlyRainH", TagYearMonthlyRainH },
				{ "YearDewPointH", TagYearDewPointH },
				{ "YearDewPointL", TagYearDewPointL },
				{ "YearWindRunH", TagYearWindRunH },
				{ "YearLongestDryPeriod", TagYearLongestDryPeriod },
				{ "YearLongestWetPeriod", TagYearLongestWetPeriod },
				{ "YearHighDailyTempRange", TagYearHighDailyTempRange },
				{ "YearLowDailyTempRange", TagYearLowDailyTempRange },
				// This years"s highs and lows - times
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
				{ "YearDewPointHT", TagYearDewPointHt },
				{ "YearDewPointLT", TagYearDewPointLt },
				// Yearly highs and lows - dates
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
				{ "YearDailyRainHD", TagYearDailyRainHd },
				{ "YearMonthlyRainHD", TagYearMonthlyRainHd },
				{ "YearDewPointHD", TagYearDewPointHd },
				{ "YearDewPointLD", TagYearDewPointLd },
				{ "YearWindRunHD", TagYearWindRunHd },
				{ "YearLongestDryPeriodD", TagYearLongestDryPeriodD },
				{ "YearLongestWetPeriodD", TagYearLongestWetPeriodD },
				{ "YearHighDailyTempRangeD", TagYearHighDailyTempRangeD },
				{ "YearLowDailyTempRangeD", TagYearLowDailyTempRangeD },
				// misc
				{ "LatestError", TagLatestError },
				{ "LatestErrorDate", TagLatestErrorDate },
				{ "LatestErrorTime", TagLatestErrorTime },
				{ "ErrorLight", Tagerrorlight },
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
				{ "RecentHumidex", TagRecentHumidex },
				{ "RecentHumidity", TagRecentHumidity },
				{ "RecentPressure", TagRecentPressure },
				{ "RecentRainToday", TagRecentRainToday },
				{ "RecentSolarRad", TagRecentSolarRad },
				{ "RecentUV", TagRecentUv },
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
				// Month-by-month highs and lows - values
				{ "ByMonthTempH", TagByMonthTempH },
				{ "ByMonthTempL", TagByMonthTempL },
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
				//Options
				{ "Option_useApparent", TagOption_useApparent },
				{ "Option_showSolar", TagOption_showSolar },
				{ "Option_showUV", TagOption_showUV }
			};

			cumulus.LogMessage(webTagDictionary.Count + " web tags initialised");

			if (!cumulus.ProgramOptions.ListWebTags) return;

			using (StreamWriter file = new StreamWriter(cumulus.WebTagFile))
			{
				foreach (var pair in webTagDictionary)
				{
					file.WriteLine(pair.Key);
				}
			}
		}

		public string GetWebTagText(string tagString, Dictionary<string,string> tagParams)
		{
			return webTagDictionary.ContainsKey(tagString) ? webTagDictionary[tagString](tagParams) : string.Copy(string.Empty);
		}

		//private static string Utf16ToUtf8(string utf16String)
		//{
		//	/**************************************************************
		//	 * Every .NET string will store text with the UTF16 encoding, *
		//	 * known as Encoding.Unicode. Other encodings may exist as    *
		//	 * Byte-Array or incorrectly stored with the UTF16 encoding.  *
		//	 *                                                            *
		//	 * UTF8 = 1 bytes per char                                    *
		//	 *    ["100" for the ANSI 'd']                                *
		//	 *    ["206" and "186" for the Russian 'κ']                   *
		//	 *                                                            *
		//	 * UTF16 = 2 bytes per char                                   *
		//	 *    ["100, 0" for the ANSI 'd']                             *
		//	 *    ["186, 3" for the Russian 'κ']                          *
		//	 *                                                            *
		//	 * UTF8 inside UTF16                                          *
		//	 *    ["100, 0" for the ANSI 'd']                             *
		//	 *    ["206, 0" and "186, 0" for the Russian 'κ']             *
		//	 *                                                            *
		//	 * We can use the convert encoding function to convert an     *
		//	 * UTF16 Byte-Array to an UTF8 Byte-Array. When we use UTF8   *
		//	 * encoding to string method now, we will get a UTF16 string. *
		//	 *                                                            *
		//	 * So we imitate UTF16 by filling the second byte of a char   *
		//	 * with a 0 byte (binary 0) while creating the string.        *
		//	 **************************************************************/

		//	// Storage for the UTF8 string
		//	string utf8String = String.Empty;

		//	// Get UTF16 bytes and convert UTF16 bytes to UTF8 bytes
		//	byte[] utf16Bytes = Encoding.Unicode.GetBytes(utf16String);
		//	byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);

		//	// Fill UTF8 bytes inside UTF8 string
		//	for (int i = 0; i < utf8Bytes.Length; i++)
		//	{
		//		// Because char always saves 2 bytes, fill char with 0
		//		byte[] utf8Container = new byte[2] { utf8Bytes[i], 0 };
		//		utf8String += BitConverter.ToChar(utf8Container, 0);
		//	}

		//	// Return UTF8
		//	return utf8String;
		//}
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
