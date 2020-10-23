using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using Unosquare.Swan;

namespace CumulusMX
{
	public delegate string WebTagFunction(Dictionary<string,string> tagParams);

	internal class WebTags
	{
		private Dictionary<string, WebTagFunction> WebTagDictionary = null;

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

		private string ReadFileIntoString(string FileName)
		{
			string result;
			using (StreamReader streamReader = new StreamReader(FileName))
			{
				result = streamReader.ReadToEnd();
			}
			return result;
		}

		private string ReplaceCommas(string AStr)
		{
			return AStr.Replace(',', '.');
		}

		private string CheckRC(string val, Dictionary<string, string> TagParams)
		{
			try
			{
				if (TagParams.Get("rc") == "y") val = ReplaceCommas(val);
				return val;
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

				if (result.Count == 1)
				{
					depth = result[0].snowDepth;
				}
				else
				{
					depth = 0;
				}
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

				if (result.Count == 1)
				{
					lying = result[0].snowLying;
				}
				else
				{
					lying = 0;
				}
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

				if (result.Count == 1)
				{
					falling = result[0].snowFalling;
				}
				else
				{
					falling = 0;
				}
			}
			catch (Exception e)
			{
				cumulus.LogMessage("Error reading diary database: " + e.Message);
				falling = 0;
			}
			return falling;
		}

		private string EncodeForWeb(string AStr)
		{
			string result = HttpUtility.HtmlEncode(AStr);
			return result;
		}

		private DateTime GetRecentTS(Dictionary<string,string> TagParams)
		{
			DateTime result;
			string daysagostr;
			string hoursagostr;
			string minsagostr;
			int minsago;
			daysagostr = TagParams.Get("d");
			hoursagostr = TagParams.Get("h");
			minsagostr = TagParams.Get("m");
			minsago = 0;
			if (minsagostr != null)
			{
				try
				{
					minsago += Convert.ToInt32(minsagostr);
				}
				catch
				{
					cumulus.LogMessage($"Error: Webtag <#{TagParams.Get("webtag")}>, expecting an integer value for parameter 'm=n', found 'm={minsagostr}'");
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
					cumulus.LogMessage($"Error: Webtag <#{TagParams.Get("webtag")}>, expecting an integer value for parameter 'h=n', found 'h={hoursagostr}'");
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
					cumulus.LogMessage($"Error: Webtag <#{TagParams.Get("webtag")}>, expecting an integer value for parameter 'd=n', found 'd={daysagostr}'");
				}
			}
			if (minsago < 0)
			{
				minsago = 0;
			}
			result = DateTime.Now.AddMinutes(-minsago);
			return result;
		}

		private int GetMonthParam(Dictionary<string,string> TagParams)
		{
			int result;
			int month;
			string monthstr;
			monthstr = TagParams.Get("mon");
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
					cumulus.LogMessage($"Error: Webtag <#{TagParams.Get("webtag")}> expecting an integer value for parameter 'mon=n', found 'mon={monthstr}'");
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
			result = month;
			return result;
		}

		public string GetCurrCondText()
		{
			string res;
			string fileName = cumulus.AppDir + "currentconditions.txt";
			if (File.Exists(fileName))
			{
				res = ReadFileIntoString(fileName);
				//MainUnit.Units.MainUnit.MainForm.CurrConditions.Text = Res;
			}
			else
			{
				// TODO
				res = String.Empty; // MainUnit.Units.MainUnit.MainForm.CurrConditions.Text;
			}

			return res;
		}

		private string GetFormattedDateTime(DateTime dt, Dictionary<string,string> TagParams)
		{
			string s;
			string dtformat = TagParams.Get("format");
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

		private string GetFormattedDateTime(DateTime dt, string defaultFormat, Dictionary<string,string> TagParams)
		{
			string s;
			if (dt <= cumulus.defaultRecordTS)
			{
				s = "----";
			}
			else
			{
				string dtformat = TagParams.Get("format");
				if (dtformat == null)
				{
					dtformat = defaultFormat;
				}
				s = dt.ToString(dtformat);
			}
			return s;
		}

		private string GetFormattedDateTime(TimeSpan ts, string defaultFormat, Dictionary<string,string> TagParams)
		{
			DateTime dt = DateTime.MinValue.Add(ts);

			return GetFormattedDateTime(dt, defaultFormat, TagParams);
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

		private string GetMonthlyAlltimeValueStr(int identifier, int month, string format)
		{
			if (station.monthlyrecarray[identifier, month].timestamp <= cumulus.defaultRecordTS)
			{
				return "---";
			}
			else
			{
				return station.monthlyrecarray[identifier, month].value.ToString(format);
			}
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

		private string Tagwlatest(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.WindLatest.ToString(cumulus.WindFormat), TagParams);
		}

		private string Tagwindrun(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.WindRunToday.ToString(cumulus.WindRunFormat), TagParams);
		}

		private string Tagwspeed(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.WindAverage.ToString(cumulus.WindAvgFormat), TagParams);
		}

		private string Tagcurrentwdir(Dictionary<string,string> TagParams)
		{
			return station.CompassPoint(station.Bearing);
		}

		private string Tagwdir(Dictionary<string,string> TagParams)
		{
			return station.CompassPoint(station.AvgBearing);
		}

		private string Tagwgust(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RecentMaxGust.ToString(cumulus.WindFormat), TagParams);
		}

		private string Tagwchill(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.WindChill.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagrrate(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RainRate.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagStormRain(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.StormRain.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagStormRainStart(Dictionary<string,string> TagParams)
		{

			if (station.StartOfStorm == DateTime.MinValue)
			{
				return "-----";
			}
			else
			{
				string dtformat = TagParams.Get("format");

				if (dtformat == null)
				{
					dtformat = "d"; // short date format
				}
				return station.StartOfStorm.ToString(dtformat);
			}
		}

		private string Tagtomorrowdaylength(Dictionary<string,string> TagParams)
		{
			return cumulus.TomorrowDayLengthText;
		}

		private string TagwindrunY(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.YesterdayWindRun.ToString(cumulus.WindRunFormat), TagParams);
		}

		private string Tagheatdegdays(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HeatingDegreeDays.ToString("F1"), TagParams);
		}

		private string Tagcooldegdays(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.CoolingDegreeDays.ToString("F1"), TagParams);
		}

		private string TagheatdegdaysY(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.YestHeatingDegreeDays.ToString("F1"), TagParams);
		}

		private string TagcooldegdaysY(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.YestCoolingDegreeDays.ToString("F1"), TagParams);
		}

		private string Tagpresstrendval(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.presstrendval.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagTempChangeLastHour(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.TempChangeLastHour.ToString("+##0.0;-##0.0;0"), TagParams); ;
		}

		private string Tagdew(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.OutdoorDewpoint.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagwetbulb(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.WetBulb.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagcloudbase(Dictionary<string,string> TagParams)
		{
			if (cumulus.CloudBaseInFeet)
			{
				return (station.CloudBase).ToString() + " ft";
			}
			else
			{
				return (station.CloudBase).ToString() + " m";
			}
		}

		private string Tagcloudbaseunit(Dictionary<string,string> TagParams)
		{
			if (cumulus.CloudBaseInFeet)
			{
				return "ft";
			}
			else
			{
				return "m";
			}
		}

		private string Tagcloudbasevalue(Dictionary<string,string> TagParams)
		{
			return (station.CloudBase).ToString();
		}

		private string TagTime(Dictionary<string,string> TagParams)
		{
			string result;
			string dtformat;
			string s;
			dtformat = TagParams.Get("format");
			if (dtformat == null)
			{
				dtformat = "HH:mm\" on \"dd MMMM yyyy";
			}
			s = DateTime.Now.ToString(dtformat);
			result = s;
			return result;
		}

		private string TagDaysSince30Dec1899(Dictionary<string,string> TagParams)
		{
			DateTime startDate = new DateTime(1899,12,30);
			return ((DateTime.Now - startDate).TotalDays).ToString();
		}

		private string TagTimeUTC(Dictionary<string,string> TagParams)
		{
			string dtformat = TagParams.Get("format");
			if (dtformat == null)
			{
				dtformat = "HH:mm\" on \"dd MMMM yyyy";
			}
			return DateTime.UtcNow.ToString(dtformat);
		}

		private string TagTimehhmmss(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("HH:mm:ss");
		}

		private string TagTimeJavascript(Dictionary<string, string> TagParams)
		{
			return ((UInt64)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds).ToString();
		}

		private string TagTimeUnix(Dictionary<string, string> TagParams)
		{
			return DateTime.UtcNow.ToUnixEpochDate().ToString();
		}

		private string TagDate(Dictionary<string,string> TagParams)
		{
			string dtformat = TagParams.Get("format");
			if (dtformat == null)
			{
				return DateTime.Now.ToString("d");
			}
			else
			{
				return DateTime.Now.ToString(dtformat);
			}
		}

		private string TagYesterday(Dictionary<string,string> TagParams)
		{
			string result;
			string dtformat;
			string s;
			DateTime yesterday;
			yesterday = DateTime.Now.AddDays(-1);
			dtformat = TagParams.Get("format");
			if (dtformat == null)
			{
				s = (yesterday).ToString("d");
			}
			else
			{
				s = yesterday.ToString(dtformat);
			}
			result = s;
			return result;
		}

		private string TagMetDate(Dictionary<string,string> TagParams)
		{

			string dtformat = TagParams.Get("format");

			if (String.IsNullOrEmpty(dtformat))
			{
				dtformat = "d";
			}

			int offset = cumulus.GetHourInc();

			return DateTime.Now.AddHours(offset).ToString(dtformat);
		}

		private string TagMetDateYesterday(Dictionary<string,string> TagParams)
		{
			string dtformat = TagParams.Get("format");

			if (String.IsNullOrEmpty(dtformat))
			{
				dtformat = "d";
			}

			int offset = cumulus.GetHourInc();

			return DateTime.Now.AddHours(offset).AddDays(-1).ToString(dtformat);
		}

		private string TagDay(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("dd");
		}

		private string TagDayname(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("dddd");
		}

		private string TagShortdayname(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("ddd");
		}

		private string TagMonth(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("MM");
		}

		private string TagMonthname(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("MMMM");
		}

		private string TagShortmonthname(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("MMM");
		}

		private string TagYear(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.Year.ToString();
		}

		private string TagShortyear(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("yy");
		}

		private string TagHour(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("HH");
		}

		private string TagMinute(Dictionary<string,string> TagParams)
		{
			return DateTime.Now.ToString("mm");
		}

		private string Tagforecast(Dictionary<string,string> TagParams)
		{
			return station.forecaststr;
		}

		private string Tagforecastnumber(Dictionary<string,string> TagParams)
		{
			return station.Forecastnumber.ToString();
		}

		private string Tagcumulusforecast(Dictionary<string,string> TagParams)
		{
			return station.CumulusForecast;
		}

		private string Tagwsforecast(Dictionary<string,string> TagParams)
		{
			return station.wsforecast;
		}

		private string Tagforecastenc(Dictionary<string,string> TagParams)
		{
			return EncodeForWeb(station.forecaststr);
		}

		private string Tagcumulusforecastenc(Dictionary<string,string> TagParams)
		{
			return EncodeForWeb(station.CumulusForecast);
		}

		private string Tagwsforecastenc(Dictionary<string,string> TagParams)
		{
			return EncodeForWeb(station.wsforecast);
		}

		private string Tagtemp(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.OutdoorTemperature.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagtemprange(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.TempRangeToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagtemprangeY(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.TempRangeYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagavgtemp(Dictionary<string,string> TagParams)
		{
			return CheckRC((station.TempTotalToday/station.tempsamplestoday).ToString(cumulus.TempFormat), TagParams);
		}

		private string TagavgtempY(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.YestAvgTemp.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagapptemp(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ApparentTemperature.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagfeelsliketemp(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.FeelsLike.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagtemptrend(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.temptrendval.ToString(cumulus.TempTrendFormat), TagParams);
		}

		private string Tagtemptrendtext(Dictionary<string,string> TagParams)
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

		private string Tagtemptrendenglish(Dictionary<string,string> TagParams)
		{
			if (Math.Abs(station.temptrendval) < 0.001)
			{
				return "Steady";
			}
			else if (station.temptrendval > 0)
			{
				return "Rising";
			}
			else
			{
				return "Falling";
			}
		}

		private string Tagheatindex(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HeatIndex.ToString(cumulus.TempFormat), TagParams);
		}

		private string Taghum(Dictionary<string,string> TagParams)
		{
			return station.OutdoorHumidity.ToString();
		}

		private string Taghumidex(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.Humidex.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagpress(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.Pressure.ToString(cumulus.PressFormat), TagParams);
		}

		private string Tagaltimeterpressure(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.AltimeterPressure.ToString(cumulus.PressFormat), TagParams);
		}

		private string Tagpresstrend(Dictionary<string,string> TagParams)
		{
			return station.Presstrendstr;
		}

		private string Tagpresstrendenglish(Dictionary<string,string> TagParams)
		{
			if (Math.Abs(station.presstrendval) < 0.0001)
			{
				return "Steady";
			}
			else if (station.presstrendval > 0)
			{
				return "Rising";
			}
			else
			{
				return "Falling";
			}
		}

		private string Tagbearing(Dictionary<string,string> TagParams)
		{
			return station.Bearing.ToString();
		}

		private string Tagavgbearing(Dictionary<string,string> TagParams)
		{
			return station.AvgBearing.ToString();
		}

		private string TagBearingRangeFrom(Dictionary<string,string> TagParams)
		{
			return station.BearingRangeFrom.ToString();
		}

		private string TagBearingRangeTo(Dictionary<string,string> TagParams)
		{
			return station.BearingRangeTo.ToString();
		}

		private string TagBearingRangeFrom10(Dictionary<string,string> TagParams)
		{
			return station.BearingRangeFrom10.ToString("D3");
		}

		private string TagBearingRangeTo10(Dictionary<string,string> TagParams)
		{
			return station.BearingRangeTo10.ToString("D3");
		}

		private string Tagdomwindbearing(Dictionary<string,string> TagParams)
		{
			return station.DominantWindBearing.ToString();
		}

		private string Tagdomwinddir(Dictionary<string,string> TagParams)
		{
			return station.CompassPoint(station.DominantWindBearing);
		}

		private string TagdomwindbearingY(Dictionary<string,string> TagParams)
		{
			return station.YestDominantWindBearing.ToString();
		}

		private string TagdomwinddirY(Dictionary<string,string> TagParams)
		{
			return station.CompassPoint(station.YestDominantWindBearing);
		}

		private string Tagbeaufort(Dictionary<string,string> TagParams)
		{
			return "F" + cumulus.Beaufort(station.WindAverage);
		}

		private string Tagbeaufortnumber(Dictionary<string,string> TagParams)
		{
			return cumulus.Beaufort(station.WindAverage);
		}

		private string Tagbeaudesc(Dictionary<string, string> TagParams)
		{
			return cumulus.BeaufortDesc(station.WindAverage);
		}

		private string Tagwdirdata(Dictionary<string,string> TagParams)
		{
			//String s = station.windbears[0].ToString();
			var sb = new StringBuilder(station.windbears[0].ToString());

			for (var i = 1; i < station.numwindvalues; i++)
			{
				//s = s + "," + station.windbears[i];
				sb.Append("," + station.windbears[i]);
			}

			//return s;
			return sb.ToString();
		}

		private string Tagwspddata(Dictionary<string,string> TagParams)
		{
			//String s = (station.windspeeds[0]*cumulus.WindGustMult).ToString(cumulus.WindFormat).Replace(",", ".");
			var sb = new StringBuilder((station.windspeeds[0]*cumulus.WindGustMult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
			for (var i = 1; i < station.numwindvalues; i++)
			{
				sb.Append("," + (station.windspeeds[i]*cumulus.WindGustMult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
				//s = s + "," + (station.windspeeds[i]*cumulus.WindGustMult).ToString(cumulus.WindFormat).Replace(",", ".");
			}

			//return s;
			return sb.ToString();
		}

		private string TagWindSampleCount(Dictionary<string,string> TagParams)
		{
			return station.numwindvalues.ToString();
		}

		private string Tagnextwindindex(Dictionary<string,string> TagParams)
		{
			return station.nextwindvalue.ToString();
		}

		private string TagWindRoseData(Dictionary<string,string> TagParams)
		{
			String s = (station.windcounts[0]*cumulus.WindGustMult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture);

			for (var i = 1; i < cumulus.NumWindRosePoints; i++)
			{
				s = s + "," + (station.windcounts[i]*cumulus.WindGustMult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture);
			}

			return s;
		}

		private string TagWindRosePoints(Dictionary<string,string> TagParams)
		{
			return cumulus.NumWindRosePoints.ToString();
		}

		private string Tagrfall(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RainToday.ToString(cumulus.RainFormat), TagParams);
		}

		private string Tagrmidnight(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RainSinceMidnight.ToString(cumulus.RainFormat), TagParams);
		}

		private string Tagrmonth(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RainMonth.ToString(cumulus.RainFormat), TagParams);
		}

		private string Tagrhour(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RainLastHour.ToString(cumulus.RainFormat), TagParams);
		}

		private string Tagr24hour(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RainLast24Hour.ToString(cumulus.RainFormat), TagParams);
		}

		private string Tagryear(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RainYear.ToString(cumulus.RainFormat), TagParams);
		}

		private string Taginhum(Dictionary<string,string> TagParams)
		{
			return station.IndoorHumidity.ToString();
		}

		private string Tagintemp(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.IndoorTemperature.ToString(cumulus.TempFormat), TagParams);
		}

		private string Tagbattery(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ConBatText, TagParams);
		}

		private string TagConsoleSupplyV(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.ConSupplyVoltageText, TagParams);
		}

		private string Tagtxbattery(Dictionary<string,string> TagParams)
		{
			if (String.IsNullOrEmpty(station.TxBatText))
			{
				return "";
			}
			else
			{
				String channeltxt = TagParams.Get("channel");
				if (channeltxt == null)
				{
					return station.TxBatText;
				}
				else
				{
					// extract status for required channel
					char[] delimiters = new char[] { ' ', '-' };
					String[] sl = station.TxBatText.Split(delimiters);

					int channel = int.Parse(channeltxt) * 2 - 1;

					if ((channel < sl.Length) && (sl[channel].Length > 0))
					{
						return sl[channel];
					}
					else
					{
						// default
						return station.TxBatText;
					}
				}
			}
		}

		private string TagMulticastGoodCnt(Dictionary<string, string> TagParams)
		{
			return station.multicastsGood.ToString();
		}

		private string TagMulticastBadCnt(Dictionary<string, string> TagParams)
		{
			return station.multicastsBad.ToString();
		}

		private string TagMulticastGoodPct(Dictionary<string, string> TagParams)
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
		private string TagAirLinkTempIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.temperature.ToString(cumulus.TempFormat), TagParams);
		}
		private string TagAirLinkHumIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.humidity.ToString(cumulus.HumFormat), TagParams);
		}
		private string TagAirLinkPm1In(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm1.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5In(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm2p5.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5_1hrIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm2p5_1hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5_3hrIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm2p5_3hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5_24hrIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm2p5_24hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5_NowcastIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm2p5_nowcast.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10In(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm10.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10_1hrIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm10_1hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10_3hrIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm10_3hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10_24hrIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm10_24hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10_NowcastIn(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataIn.pm10_nowcast.ToString("F1"), TagParams);
		}
		private string TagAirLinkFirmwareVersionIn(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataIn.firmwareVersion;
		}
		private string TagAirLinkWifiRssiIn(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataIn.wifiRssi.ToString();
		}


		// AirLink Outdoor
		private string TagAirLinkTempOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.temperature.ToString(cumulus.TempFormat), TagParams);
		}
		private string TagAirLinkHumOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.humidity.ToString(cumulus.HumFormat), TagParams);
		}
		private string TagAirLinkPm1Out(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm1.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5Out(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm2p5.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5_1hrOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm2p5_1hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5_3hrOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm2p5_3hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5_24hrOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm2p5_24hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm2p5_NowcastOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm2p5_nowcast.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10Out(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm10.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10_1hrOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm10_1hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10_3hrOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm10_3hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10_24hrOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm10_24hr.ToString("F1"), TagParams);
		}
		private string TagAirLinkPm10_NowcastOut(Dictionary<string, string> TagParams)
		{
			return CheckRC(cumulus.airLinkDataOut.pm10_nowcast.ToString("F1"), TagParams);
		}
		private string TagAirLinkFirmwareVersionOut(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataOut.firmwareVersion;
		}
		private string TagAirLinkWifiRssiOut(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataOut.wifiRssi.ToString();
		}

		private string TagAirLinkAqiPm2p5In(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm2p5).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm2p5.ToString("F" + dp), TagParams);
			}
		}

		private string TagAirLinkAqiPm2p5_1hrIn(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm2p5_1hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm2p5_1hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm2p5_3hrIn(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm2p5_3hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm2p5_3hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm2p5_24hrIn(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm2p5_24hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm2p5_24hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm2p5_NowcastIn(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm2p5_nowcast).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm2p5_nowcast.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10In(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm10).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm10.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10_1hrIn(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm10_1hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm10_1hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10_3hrIn(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm10_3hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm10_3hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10_24hrIn(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm10_24hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm10_24hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10_NowcastIn(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataIn.aqiPm10_nowcast).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataIn.aqiPm10_nowcast.ToString("F" + dp), TagParams);
			}
		}

		private string TagAirLinkAqiPm2p5Out(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm2p5).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm2p5.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm2p5_1hrOut(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm2p5_1hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm2p5_1hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm2p5_3hrOut(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm2p5_3hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm2p5_3hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm2p5_24hrOut(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm2p5_24hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm2p5_24hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm2p5_NowcastOut(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm2p5_nowcast).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm2p5_nowcast.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10Out(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm10).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm10.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10_1hrOut(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm10_1hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm10_1hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10_3hrOut(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm10_3hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm10_3hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10_24hrOut(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm10_24hr).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm10_24hr.ToString("F" + dp), TagParams);
			}
		}
		private string TagAirLinkAqiPm10_NowcastOut(Dictionary<string, string> TagParams)
		{
			if (TagParams.Get("tc") == "y")
				return Math.Truncate(cumulus.airLinkDataOut.aqiPm10_nowcast).ToString();
			else
			{
				int dp = int.TryParse(TagParams.Get("dp"), out dp) ? dp : 1;
				return CheckRC(cumulus.airLinkDataOut.aqiPm10_nowcast.ToString("F" + dp), TagParams);
			}
		}


		private string AirLinkPct_1hrIn(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataIn.pct_1hr.ToString();
		}
		private string AirLinkPct_3hrIn(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataIn.pct_3hr.ToString();
		}
		private string AirLinkPct_24hrIn(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataIn.pct_24hr.ToString();
		}
		private string AirLinkPct_NowcastIn(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataIn.pct_nowcast.ToString();
		}
		private string AirLinkPct_1hrOut(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataOut.pct_1hr.ToString();
		}
		private string AirLinkPct_3hrOut(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataOut.pct_3hr.ToString();
		}
		private string AirLinkPct_24hrOut(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataOut.pct_24hr.ToString();
		}
		private string AirLinkPct_NowcastOut(Dictionary<string, string> TagParams)
		{
			return cumulus.airLinkDataOut.pct_nowcast.ToString();
		}


		private string Tagsnowdepth(Dictionary<string,string> TagParams)
		{
			DateTime ts;

			if (DateTime.Now.Hour < cumulus.SnowDepthHour)
			{
				// Not reached today's snow measurement time, use yesterday
				ts = DateTime.Now.AddDays(-1);
			}
			else
			{
				ts = DateTime.Now;
			}
			return CheckRC(GetSnowDepth(ts).ToString(), TagParams);
		}

		private string Tagsnowlying(Dictionary<string, string> TagParams)
		{
			DateTime ts;

			if (DateTime.Now.Hour < cumulus.SnowDepthHour)
			{
				// Not reached today's snow measurement time, use yesterday
				ts = DateTime.Now.AddDays(-1);
			}
			else
			{
				ts = DateTime.Now;
			}
			return GetSnowLying(ts).ToString();
		}

		private string Tagsnowfalling(Dictionary<string, string> TagParams)
		{
			DateTime ts;

			if (DateTime.Now.Hour < cumulus.SnowDepthHour)
			{
				// Not reached today's snow measurement time, use yesterday
				ts = DateTime.Now.AddDays(-1);
			}
			else
			{
				ts = DateTime.Now;
			}
			return GetSnowFalling(ts).ToString();
		}

		private string Tagnewrecord(Dictionary<string,string> TagParams)
		{
			return station.AlltimeRecordTimestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagTempRecordSet(Dictionary<string,string> TagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.alltimerecarray[WeatherStation.AT_HighTemp].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LowTemp].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighDailyTempRange].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LowDailyTempRange].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LowChill].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighMinTemp].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LowMaxTemp].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighAppTemp].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LowAppTemp].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighHeatIndex].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighDewPoint].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LowDewpoint].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighFeelsLike].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LowFeelsLike].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighHumidex].timestamp >= threshold
			)
				return "1";
			else
				return "0";
		}

		private string TagWindRecordSet(Dictionary<string,string> TagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.alltimerecarray[WeatherStation.AT_HighGust].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighWind].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighWindrun].timestamp >= threshold
			)
				return "1";
			else
				return "0";
		}

		private string TagRainRecordSet(Dictionary<string,string> TagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.alltimerecarray[WeatherStation.AT_HighRainRate].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_DailyRain].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HourlyRain].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LongestDryPeriod].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_LongestWetPeriod].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_WetMonth].timestamp >= threshold
			)
				return "1";
			else
				return "0";
		}

		private string TagHumidityRecordSet(Dictionary<string,string> TagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.alltimerecarray[WeatherStation.AT_HighHumidity].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_lowhumidity].timestamp >= threshold
			)
				return "1";
			else
				return "0";
		}

		private string TagPressureRecordSet(Dictionary<string,string> TagParams)
		{
			var threshold = DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs);
			if (station.alltimerecarray[WeatherStation.AT_LowPress].timestamp >= threshold ||
				station.alltimerecarray[WeatherStation.AT_HighPress].timestamp >= threshold
			)
				return "1";
			else
				return "0";
		}

		private string TagHighTempRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighTemp].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowTempRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LowTemp].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighAppTempRecordSet(Dictionary<string, string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighAppTemp].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowAppTempRecordSet(Dictionary<string, string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LowAppTemp].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighFeelsLikeRecordSet(Dictionary<string, string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighFeelsLike].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowFeelsLikeRecordSet(Dictionary<string, string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LowFeelsLike].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHumidexRecordSet(Dictionary<string, string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighHumidex].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHeatIndexRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighHeatIndex].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowWindChillRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LowChill].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighMinTempRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighMinTemp].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowMaxTempRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LowMaxTemp].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighDewPointRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighDewPoint].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowDewPointRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LowDewpoint].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindGustRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighGust].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindSpeedRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighWind].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighRainRateRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighRainRate].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHourlyRainRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HourlyRain].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighDailyRainRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_DailyRain].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighMonthlyRainRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_WetMonth].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighHumidityRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighHumidity].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighWindrunRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighWindrun].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowHumidityRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_lowhumidity].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighPressureRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighPress].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowPressureRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LowPress].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLongestDryPeriodRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LongestDryPeriod].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLongestWetPeriodRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LongestWetPeriod].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagHighTempRangeRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighDailyTempRange].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string TagLowTempRangeRecordSet(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LowDailyTempRange].timestamp < DateTime.Now.AddHours(-cumulus.RecordSetTimeoutHrs) ? "0" : "1";
		}

		private string Tagerrorlight(Dictionary<string,string> TagParams)
		{
			// TODO
			return "0";
		}

		private string TagtempTH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighTempToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTtempTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighTempTodayTime, "HH:mm", TagParams);
		}

		private string TagtempTL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowTempToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTtempTL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowTempTodayTime, "HH:mm", TagParams);
		}

		private string TagSolarTH(Dictionary<string,string> TagParams)
		{
			return ((int)station.HighSolarToday).ToString();
		}

		private string TagTsolarTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighSolarTodayTime, "HH:mm", TagParams);
		}

		private string TagUVTH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighUVToday.ToString(cumulus.UVFormat), TagParams);
		}

		private string TagTUVTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighUvTodayTime, "HH:mm", TagParams);
		}

		private string TagapptempTH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighAppTempToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagRCapptempTH(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(station.HighAppTempToday.ToString(cumulus.TempFormat));
		}

		private string TagTapptempTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighAppTempTodayTime, "HH:mm", TagParams);
		}

		private string TagapptempTL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowAppTempToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagRCapptempTL(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(station.LowAppTempToday.ToString(cumulus.TempFormat));
		}

		private string TagTapptempTL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowAppTempTodayTime, "HH:mm", TagParams);
		}

		private string TagfeelslikeTH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.HighFeelsLikeToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTfeelslikeTH(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighFeelsLikeTodayTime, "HH:mm", TagParams);
		}

		private string TagfeelslikeTL(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.LowFeelsLikeToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTfeelslikeTL(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.LowFeelsLikeTodayTime, "HH:mm", TagParams);
		}

		private string TaghumidexTH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.HighHumidexToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagThumidexTH(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidexTodayTime, "HH:mm", TagParams);
		}

		private string TagdewpointTH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighDewpointToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagRCdewpointTH(Dictionary<string,string> TagParams)
		{
		   return ReplaceCommas(station.HighDewpointToday.ToString(cumulus.TempFormat));
		}

		private string TagTdewpointTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDewpointTodayTime, "HH:mm", TagParams);
		}

		private string TagdewpointTL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowDewpointToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagRCdewpointTL(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(station.LowDewpointToday.ToString(cumulus.TempFormat));
		}

		private string TagTdewpointTL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowDewpointTodayTime, "HH:mm", TagParams);
		}

		private string TagwchillTL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowWindChillToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagRCwchillTL(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(station.LowWindChillToday.ToString(cumulus.TempFormat));
		}

		private string TagTwchillTL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowWindChillTodayTime, "HH:mm", TagParams);
		}

		private string TagheatindexTH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighHeatIndexToday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagRCheatindexTH(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(station.HighHeatIndexToday.ToString(cumulus.TempFormat));
		}

		private string TagTheatindexTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHeatIndexTodayTime, "HH:mm", TagParams);
		}

		private string TagheatindexYH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighHeatIndexYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTheatindexYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHeatIndexYesterdayTime, "HH:mm", TagParams);
		}

		private string TagpressTH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighPressToday.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagTpressTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighPressTodayTime, "HH:mm", TagParams);
		}

		private string TagpressTL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowPressToday.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagTpressTL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowPressTodayTime, "HH:mm", TagParams);
		}

		private string TaghumTH(Dictionary<string,string> TagParams)
		{
			return station.HighHumidityToday.ToString();
		}

		private string TagThumTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidityTodayTime, "HH:mm", TagParams);
		}

		private string TaghumTL(Dictionary<string,string> TagParams)
		{
			return station.LowHumidityToday.ToString();
		}

		private string TagThumTL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowHumidityTodayTime, "HH:mm", TagParams);
		}

		private string TagwindTM(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighWindToday.ToString(cumulus.WindAvgFormat), TagParams);
		}

		private string TagTbeaufort(Dictionary<string,string> TagParams)
		{
			return "F" + cumulus.Beaufort(station.HighWindToday);
		}

		private string TagTbeaufortnumber(Dictionary<string,string> TagParams)
		{
			return cumulus.Beaufort(station.HighWindToday);
		}

		private string TagTbeaudesc(Dictionary<string,string> TagParams)
		{
			return cumulus.BeaufortDesc(station.HighWindToday);
		}

		private string TagTwindTM(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighWindTodayTime, "HH:mm", TagParams);
		}

		private string TagwgustTM(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighGustToday.ToString(cumulus.WindFormat), TagParams);
		}

		private string TagTwgustTM(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighGustTodayTime, "HH:mm", TagParams);
		}

		private string TagbearingTM(Dictionary<string,string> TagParams)
		{
			return station.HighGustBearing.ToString();
		}

		private string TagdirectionTM(Dictionary<string, string> TagParams)
		{
			return station.CompassPoint(station.HighGustBearing);
		}

		private string TagrrateTM(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighRainToday.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagTrrateTM(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighRainTodayTime, "HH:mm", TagParams);
		}

		private string TaghourlyrainTH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighHourlyRainToday.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagThourlyrainTH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHourlyRainTodayTime, "HH:mm", TagParams);
		}

		private string TaghourlyrainYH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighHourlyRainYesterday.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagThourlyrainYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHourlyRainYesterdayTime, "HH:mm", TagParams);
		}

		private string TagSolarYH(Dictionary<string,string> TagParams)
		{
			return station.HighSolarYesterday.ToString("F0");
		}

		private string TagTsolarYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighSolarYesterdayTime, "HH:mm", TagParams);
		}

		private string TagUVYH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighUVYesterday.ToString(cumulus.UVFormat), TagParams);
		}

		private string TagTUVYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighUvYesterdayTime, "HH:mm", TagParams);
		}

		private string Tagrollovertime(Dictionary<string,string> TagParams)
		{
			int hourInc = cumulus.GetHourInc();
			switch (hourInc)
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

		private string TagRG11RainToday(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RG11RainToday.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagRG11RainYest(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RG11RainYesterday.ToString(cumulus.RainFormat), TagParams);
		}

		private string Tagcurrcond(Dictionary<string,string> TagParams)
		{
			return GetCurrCondText();
		}

		private string Tagcurrcondenc(Dictionary<string,string> TagParams)
		{
			return EncodeForWeb(GetCurrCondText());
		}

		private string TagtempYH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighTempYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTtempYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighTempYesterdayTime, "HH:mm", TagParams);
		}

		private string TagtempYL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowTempYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTtempYL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowTempYesterdayTime, "HH:mm", TagParams);
		}

		private string TagapptempYH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighAppTempYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTapptempYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighAppTempYesterdayTime, "HH:mm", TagParams);
		}

		private string TagapptempYL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowAppTempYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTapptempYL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowAppTempYesterdayTime, "HH:mm", TagParams);
		}

		private string TagdewpointYH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighDewpointYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagfeelslikeYH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.HighFeelsLikeYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTfeelslikeYH(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighFeelsLikeYesterdayTime, "HH:mm", TagParams);
		}

		private string TagfeelslikeYL(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.LowFeelsLikeYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTfeelslikeYL(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.LowFeelsLikeYesterdayTime, "HH:mm", TagParams);
		}

		private string TaghumidexYH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.HighHumidexYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagThumidexYH(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidexYesterdayTime, "HH:mm", TagParams);
		}

		private string TagTdewpointYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDewpointYesterdayTime, "HH:mm", TagParams);
		}

		private string TagdewpointYL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowDewpointYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTdewpointYL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowDewpointYesterdayTime, "HH:mm", TagParams);
		}

		private string TagwchillYL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowWindChillYesterday.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTwchillYL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowWindChillYesterdayTime, "HH:mm", TagParams);
		}

		private string TagpressYH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighPressYesterday.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagTpressYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighPressYesterdayTime, "HH:mm", TagParams);
		}

		private string TagpressYL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowPressYesterday.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagTpressYL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowPressYesterdayTime, "HH:mm", TagParams);
		}

		private string TaghumYH(Dictionary<string,string> TagParams)
		{
			return station.HighHumidityYesterday.ToString();
		}

		private string TagThumYH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidityYesterdayTime, "HH:mm", TagParams);
		}

		private string TaghumYL(Dictionary<string,string> TagParams)
		{
			return station.LowHumidityYesterday.ToString();
		}

		private string TagThumYL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowHumidityYesterdayTime, "HH:mm", TagParams);
		}

		private string TagwindYM(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighWindYesterday.ToString(cumulus.WindAvgFormat), TagParams);
		}

		private string TagYbeaufort(Dictionary<string,string> TagParams)
		{
			return "F" + station.Beaufort(station.HighWindYesterday);
		}

		private string TagYbeaufortnumber(Dictionary<string,string> TagParams)
		{
			return station.Beaufort(station.HighWindYesterday).ToString();
		}

		private string TagYbeaudesc(Dictionary<string,string> TagParams)
		{
			return cumulus.BeaufortDesc(station.HighWindYesterday);
		}

		private string TagTwindYM(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighWindYesterdayTime, "HH:mm", TagParams);
		}

		private string TagwgustYM(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighGustYesterday.ToString(cumulus.WindFormat), TagParams);
		}

		private string TagTwgustYM(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighGustYesterdayTime, "HH:mm", TagParams);
		}

		private string TagbearingYM(Dictionary<string,string> TagParams)
		{
			return station.HighGustBearingYesterday.ToString();
		}

		private string TagdirectionYM(Dictionary<string, string> TagParams)
		{
			return  station.CompassPoint(station.HighGustBearingYesterday);
		}

		private string TagrrateYM(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighRainYesterday.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagTrrateYM(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighRainYesterdayTime, "HH:mm", TagParams);
		}

		private string TagrfallY(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.RainYesterday.ToString(cumulus.RainFormat), TagParams);
		}

		// all time records
		private string TagtempH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighTemp].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTtempH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighTemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagtempL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_LowTemp].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTtempL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LowTemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagapptempH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighAppTemp].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTapptempH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighAppTemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagapptempL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_LowAppTemp].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTapptempL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LowAppTemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagfeelslikeH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighFeelsLike].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTfeelslikeH(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighFeelsLike].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagfeelslikeL(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_LowFeelsLike].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTfeelslikeL(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LowFeelsLike].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TaghumidexH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighHumidex].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagThumidexH(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighHumidex].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagdewpointH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighDewPoint].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTdewpointH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighDewPoint].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagdewpointL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_LowDewpoint].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTdewpointL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LowDewpoint].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagheatindexH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighHeatIndex].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTheatindexH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighHeatIndex].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TaggustM(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighGust].value.ToString(cumulus.WindFormat), TagParams);
		}

		private string TagTgustM(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighGust].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagwspeedH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighWind].value.ToString(cumulus.WindAvgFormat), TagParams);
		}

		private string TagTwspeedH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighWind].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagwchillH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_LowChill].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTwchillH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LowChill].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagrrateM(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighRainRate].value.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagTrrateM(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighRainRate].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagwindrunH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighWindrun].value.ToString(cumulus.WindRunFormat), TagParams);
		}

		private string TagTwindrunH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighWindrun].timestamp, "o\\n dd MMMM yyyy", TagParams);
		}

		private string TagrfallH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_DailyRain].value.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagTrfallH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_DailyRain].timestamp, "o\\n dd MMMM yyyy", TagParams);
		}

		private string TagLongestDryPeriod(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LongestDryPeriod].value.ToString("F0");
		}

		private string TagTLongestDryPeriod(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LongestDryPeriod].timestamp, "\\to dd MMMM yyyy", TagParams);
		}

		private string TagLongestWetPeriod(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_LongestWetPeriod].value.ToString("F0");
		}

		private string TagTLongestWetPeriod(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LongestWetPeriod].timestamp, "\\to dd MMMM yyyy", TagParams);
		}

		private string TagLowDailyTempRange(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_LowDailyTempRange].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagHighDailyTempRange(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighDailyTempRange].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTLowDailyTempRange(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LowDailyTempRange].timestamp, "o\\n dd MMMM yyyy", TagParams);
		}

		private string TagTHighDailyTempRange(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighDailyTempRange].timestamp, "o\\n dd MMMM yyyy", TagParams);
		}

		private string TagrfallhH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HourlyRain].value.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagTrfallhH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HourlyRain].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagrfallmH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_WetMonth].value.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagTrfallmH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_WetMonth].timestamp, "MMMM yyyy", TagParams);
		}

		private string TagpressH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighPress].value.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagTpressH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighPress].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagpressL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_LowPress].value.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagTpressL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LowPress].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TaghumH(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_HighHumidity].value.ToString(cumulus.HumFormat);
		}

		private string TagThumH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighHumidity].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TaghumL(Dictionary<string,string> TagParams)
		{
			return station.alltimerecarray[WeatherStation.AT_lowhumidity].value.ToString(cumulus.HumFormat);
		}

		private string TagThumL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_lowhumidity].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string Tagrecordsbegandate(Dictionary<string,string> TagParams)
		{
			var begandate = DateTime.Parse(cumulus.RecordsBeganDate);
			return GetFormattedDateTime(begandate, "dd MMMM yyyy", TagParams);
		}

		private string TagDaysSinceRecordsBegan(Dictionary<string,string> TagParams)
		{
			var begandate = DateTime.Parse(cumulus.RecordsBeganDate);
			return (DateTime.Now - begandate).Days.ToString();
		}

		private string TagmintempH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_HighMinTemp].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTmintempH(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_HighMinTemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagmaxtempL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.alltimerecarray[WeatherStation.AT_LowMaxTemp].value.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagTmaxtempL(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_LowMaxTemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		// end of all-time records
		// month by month all time records
		private string TagByMonthTempH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighTemp, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthTempHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighTemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthTempL(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_LowTemp, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthTempLT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LowTemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthAppTempH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighAppTemp, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthAppTempHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighAppTemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthAppTempL(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_LowAppTemp, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthAppTempLT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LowAppTemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthFeelsLikeH(Dictionary<string, string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighFeelsLike, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthFeelsLikeHT(Dictionary<string, string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighFeelsLike, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthFeelsLikeL(Dictionary<string, string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_LowFeelsLike, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthFeelsLikeLT(Dictionary<string, string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LowFeelsLike, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthHumidexH(Dictionary<string, string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighHumidex, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthHumidexHT(Dictionary<string, string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighHumidex, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthDewPointH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighDewPoint, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthDewPointHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighDewPoint, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthDewPointL(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_LowDewpoint, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthDewPointLT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LowDewpoint, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthHeatIndexH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighHeatIndex, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthHeatIndexHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighHeatIndex, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthGustH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighGust, month, cumulus.WindFormat), TagParams);
		}

		private string TagByMonthGustHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighGust, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthWindH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighWind, month, cumulus.WindAvgFormat), TagParams);
		}

		private string TagByMonthWindHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighWind, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthWChillL(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_LowChill, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthWChillLT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LowChill, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthRainRateH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighRainRate, month, cumulus.RainFormat), TagParams);
		}

		private string TagByMonthRainRateHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighRainRate, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthWindRunH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighWindrun, month, cumulus.WindRunFormat), TagParams);
		}

		private string TagByMonthWindRunHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighWindrun, month].timestamp, "o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthDailyRainH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_DailyRain, month, cumulus.RainFormat), TagParams);
		}

		private string TagByMonthDailyRainHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_DailyRain, month].timestamp, "o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthLongestDryPeriod(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetMonthlyAlltimeValueStr(WeatherStation.AT_LongestDryPeriod, month, "F0");
		}

		private string TagByMonthLongestDryPeriodT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LongestDryPeriod, month].timestamp, "\\to dd MMMM yyyy", TagParams);
		}

		private string TagByMonthLongestWetPeriod(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetMonthlyAlltimeValueStr(WeatherStation.AT_LongestWetPeriod, month, "F0");
		}

		private string TagByMonthLongestWetPeriodT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LongestWetPeriod, month].timestamp, "\\to dd MMMM yyyy", TagParams);
		}

		private string TagByMonthLowDailyTempRange(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_LowDailyTempRange, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthHighDailyTempRange(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighDailyTempRange, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthLowDailyTempRangeT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LowDailyTempRange, month].timestamp, "o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthHighDailyTempRangeT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighDailyTempRange, month].timestamp, "o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthHourlyRainH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HourlyRain, month, cumulus.RainFormat), TagParams);
		}

		private string TagByMonthHourlyRainHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HourlyRain, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthMonthlyRainH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_WetMonth, month, cumulus.RainFormat), TagParams);
		}

		private string TagByMonthMonthlyRainHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_WetMonth, month].timestamp, "MMMM yyyy", TagParams);
		}

		private string TagByMonthPressH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighPress, month, cumulus.PressFormat), TagParams);
		}

		private string TagByMonthPressHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighPress, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthPressL(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_LowPress, month, cumulus.PressFormat), TagParams);
		}

		private string TagByMonthPressLT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LowPress, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthHumH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetMonthlyAlltimeValueStr(WeatherStation.AT_HighHumidity, month, cumulus.HumFormat);
		}

		private string TagByMonthHumHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighHumidity, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthHumL(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetMonthlyAlltimeValueStr(WeatherStation.AT_lowhumidity, month, cumulus.HumFormat);
		}

		private string TagByMonthHumLT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_lowhumidity, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthMinTempH(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_HighMinTemp, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthMinTempHT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_HighMinTemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		private string TagByMonthMaxTempL(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return CheckRC(GetMonthlyAlltimeValueStr(WeatherStation.AT_LowMaxTemp, month, cumulus.TempFormat), TagParams);
		}

		private string TagByMonthMaxTempLT(Dictionary<string,string> TagParams)
		{
			var month = GetMonthParam(TagParams);
			return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_LowMaxTemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
		}

		// end of month-by-month all-time records

		private string Taggraphperiod(Dictionary<string,string> TagParams)
		{
			return cumulus.GraphHours.ToString();
		}

		private string Tagstationtype(Dictionary<string,string> TagParams)
		{
			if (cumulus.StationModel != String.Empty)
			{
				return cumulus.StationModel;
			}
			else
			{
				if (cumulus.StationType == -1)
				{
					return "undefined";
				}
				else
				{
					return cumulus.StationDesc[cumulus.StationType];
				}
			}
		}

		private string Taglatitude(Dictionary<string,string> TagParams)
		{
			var dpstr = TagParams.Get("dp");
			if (dpstr == null)
			{
				return cumulus.LatTxt;
			}
			else
			{
				try

				{
					var dp = int.Parse(dpstr);
					return CheckRC(String.Format("{0:F"+dp+"}",cumulus.Latitude), TagParams);
				}
				catch
				{
					return "error";
				}
			}
		}

		private string Taglongitude(Dictionary<string,string> TagParams)
		{
			var dpstr = TagParams.Get("dp");
			if (dpstr == null)
			{
				return cumulus.LonTxt;
			}
			else
			{
				try
				{
					var dp = int.Parse(dpstr);
					return CheckRC(String.Format("{0:F" + dp + "}", cumulus.Longitude), TagParams);
				}
				catch
				{
					return "error";
				}
			}
		}

		private string Taglocation(Dictionary<string,string> TagParams)
		{
			return cumulus.LocationName;
		}

		private string Taglonglocation(Dictionary<string,string> TagParams)
		{
			return cumulus.LocationDesc;
		}

		private string Tagsunrise(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.SunRiseTime), "HH:mm", TagParams);
		}

		private string Tagsunset(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.SunSetTime), "HH:mm", TagParams);
		}

		private string Tagdaylength(Dictionary<string,string> TagParams)
		{
			if (TagParams.Get("format") == null)
				return ((int)cumulus.DayLength.TotalHours).ToString("D2") + ":" + cumulus.DayLength.Minutes.ToString("D2");
			else
				return GetFormattedDateTime(cumulus.DayLength, "HH:mm", TagParams);
		}

		private string Tagdawn(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.Dawn), "HH:mm", TagParams);
		}

		private string Tagdusk(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(SunriseSunset.RoundToMinute(cumulus.Dusk), "HH:mm", TagParams);
		}

		private string Tagdaylightlength(Dictionary<string,string> TagParams)
		{
			if (TagParams.Get("format") == null)
				return ((int)cumulus.DaylightLength.TotalHours).ToString("D2") + ":" + cumulus.DaylightLength.Minutes.ToString("D2");
			else
				return GetFormattedDateTime(cumulus.DaylightLength, "HH:mm", TagParams);
		}

		private string Tagisdaylight(Dictionary<string,string> TagParams)
		{
			return cumulus.IsDaylight() ? "1" : "0";
		}

		private string TagIsSunUp(Dictionary<string,string> TagParams)
		{
			return cumulus.IsSunUp() ? "1" : "0";
		}

		private string TagSensorContactLost(Dictionary<string,string> TagParams)
		{
			return station.SensorContactLost ? "1" : "0";
		}

		private string TagDataStopped(Dictionary<string,string> TagParams)
		{
			return cumulus.DataStoppedAlarm.triggered ? "1" : "0";
		}

		private string Tagmoonrise(Dictionary<string,string> TagParams)
		{
			return cumulus.MoonRiseTime.Hours < 0 ? "-----" : GetFormattedDateTime(cumulus.MoonRiseTime, "HH:mm", TagParams);
		}

		private string Tagmoonset(Dictionary<string,string> TagParams)
		{
			return cumulus.MoonSetTime.Hours < 0 ? "-----" : GetFormattedDateTime(cumulus.MoonSetTime, "HH:mm", TagParams);
		}

		private string Tagmoonphase(Dictionary<string,string> TagParams)
		{
			return cumulus.MoonPhaseString;
		}

		private string Tagaltitude(Dictionary<string,string> TagParams)
		{
			if (cumulus.AltitudeInFeet)
			{
				return cumulus.Altitude + "&nbsp;ft";
			}
			else
			{
				return cumulus.Altitude + "&nbsp;m";
			}
		}

		private string Tagforum(Dictionary<string,string> TagParams)
		{
			if (String.IsNullOrEmpty(cumulus.ForumURL))
			{
				return String.Empty;
			}
			else
			{
				return @":<a href=""" + cumulus.ForumURL + @""">forum</a>:";
			}
		}

		private string Tagwebcam(Dictionary<string,string> TagParams)
		{
			if (String.IsNullOrEmpty(cumulus.WebcamURL))
			{
				return String.Empty;
			}
			else
			{
				return @":<a href=""" + cumulus.WebcamURL + @""">webcam</a>:";
			}
		}

		private string Tagtempunit(Dictionary<string,string> TagParams)
		{
			return EncodeForWeb(cumulus.TempUnitText);
		}

		private string Tagtempunitnodeg(Dictionary<string,string> TagParams)
		{
			return EncodeForWeb(cumulus.TempUnitText.Substring(1,1));
		}

		private string Tagwindunit(Dictionary<string,string> TagParams)
		{
			return cumulus.WindUnitText;
		}

		private string Tagwindrununit(Dictionary<string,string> TagParams)
		{
			return cumulus.WindRunUnitText;
		}

		private string Tagpressunit(Dictionary<string,string> TagParams)
		{
			return cumulus.PressUnitText;
		}

		private string Tagrainunit(Dictionary<string,string> TagParams)
		{
			return cumulus.RainUnitText;
		}

		private string Taginterval(Dictionary<string,string> TagParams)
		{
			return cumulus.UpdateInterval.ToString();
		}

		private string Tagrealtimeinterval(Dictionary<string,string> TagParams)
		{
			return (cumulus.RealtimeInterval/1000).ToString();
		}

		private string Tagversion(Dictionary<string,string> TagParams)
		{
			return cumulus.Version;
		}

		private string Tagbuild(Dictionary<string,string> TagParams)
		{
			return cumulus.Build;
		}

		private string TagNewBuildAvailable(Dictionary<string, string> TagParams)
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

		private string TagNewBuildNumber(Dictionary<string, string> TagParams)
		{
			return cumulus.LatestBuild;
		}

		private string Tagupdate(Dictionary<string,string> TagParams)
		{
			string dtformat = TagParams.Get("format");

			if (dtformat == null)
			{
				return DateTime.Now.ToString();
			}
			else
			{
				return DateTime.Now.ToString(dtformat);
			}
		}

		private string TagLatestNOAAMonthlyReport(Dictionary<string,string> TagParams)
		{
			return cumulus.NOAALatestMonthlyReport;
		}

		private string TagLatestNOAAYearlyReport(Dictionary<string,string> TagParams)
		{
			return cumulus.NOAALatestYearlyReport;
		}

		private string TagMoonPercent(Dictionary<string,string> TagParams)
		{
			var dpstr = TagParams.Get("dp");
			int i, dp;
			dp = Int32.TryParse(dpstr, out i) ? i : 0;

			return CheckRC(Math.Round(cumulus.MoonPercent, dp).ToString(), TagParams);
		}

		private string TagMoonPercentAbs(Dictionary<string,string> TagParams)
		{
			var dpstr = TagParams.Get("dp");
			int i, dp;
			dp = Int32.TryParse(dpstr, out i) ? i : 0;

			return CheckRC(Math.Round(Math.Abs(cumulus.MoonPercent), dp).ToString(), TagParams);
		}

		private string TagMoonAge(Dictionary<string,string> TagParams)
		{
			var dpstr = TagParams.Get("dp");
			var tcstr = TagParams.Get("tc");
			int i, dp;
			dp = Int32.TryParse(dpstr, out i) ? i : 0;

			if (tcstr == "y")
			{
				return Math.Truncate(cumulus.MoonAge).ToString();
			}
			else
			{
				return CheckRC(Math.Round(cumulus.MoonAge, dp).ToString(), TagParams);
			}
		}

		private string TagLastRainTipISO(Dictionary<string,string> TagParams)
		{
			return station.LastRainTip;
		}

		private string TagLastRainTip(Dictionary<string, string> TagParams)
		{
			DateTime lastTip;
			string dtformat = TagParams.Get("format");
			try
			{
				lastTip = DateTime.Parse(station.LastRainTip);
				if (dtformat == null)
				{
					return lastTip.ToString("d");
				}
				else
				{
					return lastTip.ToString(dtformat);
				}
			}
			catch (Exception)
			{
				return "---";
			}
		}

		private string TagMinutesSinceLastRainTip(Dictionary<string,string> TagParams)
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

		private string TagRCtemp(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagtemp(TagParams));
		}

		private string TagRCtempTH(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagtempTH(TagParams));
		}

		private string TagRCtempTL(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagtempTL(TagParams));
		}

		private string TagRCintemp(Dictionary<string, string> TagParams)
		{
			return ReplaceCommas(Tagintemp(TagParams));
		}

		private string TagRCdew(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagdew(TagParams));
		}

		private string TagRCheatindex(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagheatindex(TagParams));
		}

		private string TagRCwchill(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagwchill(TagParams));
		}

		private string TagRChum(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Taghum(TagParams));
		}

		private string TagRCinhum(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Taginhum(TagParams));
		}

		private string TagRCrfall(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagrfall(TagParams));
		}

		private string TagRCrrate(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagrrate(TagParams));
		}

		private string TagRCrrateTM(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagrrateTM(TagParams));
		}

		private string TagRCwlatest(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagwlatest(TagParams));
		}

		private string TagRCwgust(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagwgust(TagParams));
		}

		private string TagRCwspeed(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagwspeed(TagParams));
		}

		private string TagRCwgustTM(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagwgustTM(TagParams));
		}

		private string TagRCpress(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(Tagpress(TagParams));
		}

		private string TagRCpressTH(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagpressTH(TagParams));
		}

		private string TagRCpressTL(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagpressTL(TagParams));
		}

		private string TagET(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ET.ToString(cumulus.ETFormat), TagParams);
		}

		private string TagLight(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LightValue.ToString("F1"), TagParams);
		}

		private string TagUV(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.UV.ToString(cumulus.UVFormat), TagParams);
		}

		private string TagSolarRad(Dictionary<string,string> TagParams)
		{
			return ((int) station.SolarRad).ToString();
		}

		private string TagCurrentSolarMax(Dictionary<string,string> TagParams)
		{
			return ((int) Math.Round(station.CurrentSolarMax)).ToString();
		}

		private string TagSunshineHours(Dictionary<string,string> TagParams)
		{
			var dpstr = TagParams.Get("dp");
			int i, dp;
			dp = Int32.TryParse(dpstr, out i) ? i : cumulus.SunshineDPlaces;

			return CheckRC(station.SunshineHours.ToString("F" + dp), TagParams);
		}

		private string TagTHWIndex(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.THWIndex.ToString("F1"), TagParams);
		}

		private string TagTHSWIndex(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.THSWIndex.ToString("F1"), TagParams);
		}

		private string TagChillHours(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ChillHours.ToString("F1"), TagParams);
		}

		private string TagYSunshineHours(Dictionary<string,string> TagParams)
		{
			var dpstr = TagParams.Get("dp");
			int i, dp;
			dp = Int32.TryParse(dpstr, out i) ? i : cumulus.SunshineDPlaces;
			return CheckRC(station.YestSunshineHours.ToString("F" + dp), TagParams);
		}

		private string TagIsSunny(Dictionary<string,string> TagParams)
		{
			return station.IsSunny ? "1" : "0";
		}

		private string TagIsFreezing(Dictionary<string,string> TagParams)
		{
			return station.ConvertUserTempToC(station.OutdoorTemperature)<0.09 ? "1" : "0";
		}

		private string TagIsRaining(Dictionary<string,string> TagParams)
		{
			return station.IsRaining ? "1" : "0";
		}

		private string TagConsecutiveRainDays(Dictionary<string,string> TagParams)
		{
			return station.ConsecutiveRainDays.ToString();
		}

		private string TagConsecutiveDryDays(Dictionary<string,string> TagParams)
		{
			return station.ConsecutiveDryDays.ToString();
		}

		// Extra sensors
		private string TagExtraTemp1(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[1].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp2(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[2].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp3(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[3].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp4(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[4].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp5(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[5].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp6(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[6].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp7(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[7].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp8(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[8].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp9(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[9].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraTemp10(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraTemp[10].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP1(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[1].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP2(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[2].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP3(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[3].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP4(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[4].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP5(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[5].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP6(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[6].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP7(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[7].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP8(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[8].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP9(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[9].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraDP10(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraDewPoint[10].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagExtraHum1(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[1].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum2(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[2].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum3(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[3].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum4(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[4].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum5(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[5].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum6(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[6].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum7(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[7].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum8(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[8].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum9(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[9].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagExtraHum10(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.ExtraHum[10].ToString(cumulus.HumFormat), TagParams);
		}

		private string TagSoilTemp1(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.SoilTemp1.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp2(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.SoilTemp2.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp3(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.SoilTemp3.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp4(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.SoilTemp4.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp5(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp5.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp6(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp6.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp7(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp7.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp8(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp8.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp9(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp9.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp10(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp10.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp11(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp11.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp12(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp12.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp13(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp13.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp14(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp14.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp15(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp15.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilTemp16(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.SoilTemp16.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagSoilMoisture1(Dictionary<string,string> TagParams)
		{
			return station.SoilMoisture1.ToString();
		}

		private string TagSoilMoisture2(Dictionary<string,string> TagParams)
		{
			return station.SoilMoisture2.ToString();
		}

		private string TagSoilMoisture3(Dictionary<string,string> TagParams)
		{
			return station.SoilMoisture3.ToString();
		}

		private string TagSoilMoisture4(Dictionary<string,string> TagParams)
		{
			return station.SoilMoisture4.ToString();
		}

		private string TagSoilMoisture5(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture5.ToString();
		}

		private string TagSoilMoisture6(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture6.ToString();
		}

		private string TagSoilMoisture7(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture7.ToString();
		}

		private string TagSoilMoisture8(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture8.ToString();
		}

		private string TagSoilMoisture9(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture9.ToString();
		}

		private string TagSoilMoisture10(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture10.ToString();
		}

		private string TagSoilMoisture11(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture12.ToString();
		}

		private string TagSoilMoisture12(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture12.ToString();
		}

		private string TagSoilMoisture13(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture13.ToString();
		}

		private string TagSoilMoisture14(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture14.ToString();
		}

		private string TagSoilMoisture15(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture15.ToString();
		}

		private string TagSoilMoisture16(Dictionary<string, string> TagParams)
		{
			return station.SoilMoisture16.ToString();
		}

		private string TagUserTemp1(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.UserTemp[1].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagUserTemp2(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.UserTemp[2].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagUserTemp3(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.UserTemp[3].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagUserTemp4(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.UserTemp[4].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagUserTemp5(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.UserTemp[5].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagUserTemp6(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.UserTemp[6].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagUserTemp7(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.UserTemp[7].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagUserTemp8(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.UserTemp[8].ToString(cumulus.TempFormat), TagParams);
		}

		private string TagAirQuality1(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.AirQuality1.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagAirQuality2(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.AirQuality2.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagAirQuality3(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.AirQuality3.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagAirQuality4(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.AirQuality4.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagAirQualityAvg1(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.AirQualityAvg1.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagAirQualityAvg2(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.AirQualityAvg2.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagAirQualityAvg3(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.AirQuality3.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagAirQualityAvg4(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.AirQualityAvg4.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagCO2(Dictionary<string, string> TagParams)
		{
			return station.CO2.ToString();
		}

		private string TagCO2_24h(Dictionary<string, string> TagParams)
		{
			return station.CO2_24h.ToString();
		}

		private string TagLeafTemp1(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LeafTemp1.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagLeafTemp2(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LeafTemp2.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagLeafTemp3(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.LeafTemp3.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagLeafTemp4(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.LeafTemp4.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagLeakSensor1(Dictionary<string, string> TagParams)
		{
			return station.LeakSensor1.ToString();
		}

		private string TagLeakSensor2(Dictionary<string, string> TagParams)
		{
			return station.LeakSensor2.ToString();
		}

		private string TagLeakSensor3(Dictionary<string, string> TagParams)
		{
			return station.LeakSensor3.ToString();
		}

		private string TagLeakSensor4(Dictionary<string, string> TagParams)
		{
			return station.LeakSensor4.ToString();
		}

		private string TagLightningDistance(Dictionary<string, string> TagParams)
		{
			if (station.LightningDistance == 999)
				return "--";
			else
				return CheckRC(station.LightningDistance.ToString(cumulus.WindRunFormat), TagParams);
		}

		private string TagLightningTime(Dictionary<string, string> TagParams)
		{
			if (DateTime.Compare(station.LightningTime, new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)) == 0)
				return "---";
			else
				return GetFormattedDateTime(station.LightningTime, "t", TagParams);
		}

		private string TagLightningStrikesToday(Dictionary<string, string> TagParams)
		{
			return station.LightningStrikesToday.ToString();
		}

		private string TagLeafWetness1(Dictionary<string,string> TagParams)
		{
			return station.LeafWetness1.ToString();
		}

		private string TagLeafWetness2(Dictionary<string,string> TagParams)
		{
			return station.LeafWetness2.ToString();
		}

		private string TagLeafWetness3(Dictionary<string, string> TagParams)
		{
			return station.LeafWetness3.ToString();
		}

		private string TagLeafWetness4(Dictionary<string, string> TagParams)
		{
			return station.LeafWetness4.ToString();
		}

		// Alarms
		private string TagLowTempAlarm(Dictionary<string, string> TagParams)
		{
			if (cumulus.LowTempAlarm.enabled)
			{
				return cumulus.LowTempAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagHighTempAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.HighTempAlarm.enabled)
			{
				return cumulus.HighTempAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagTempChangeUpAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.TempChangeAlarm.enabled)
			{
				return cumulus.TempChangeAlarm.upTriggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagTempChangeDownAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.TempChangeAlarm.enabled)
			{
				return cumulus.TempChangeAlarm.downTriggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagLowPressAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.LowPressAlarm.enabled)
			{
				return cumulus.LowPressAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagHighPressAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.HighPressAlarm.enabled)
			{
				return cumulus.HighPressAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagPressChangeUpAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.PressChangeAlarm.enabled)
			{
				return cumulus.PressChangeAlarm.upTriggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagPressChangeDownAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.PressChangeAlarm.enabled)
			{
				return cumulus.PressChangeAlarm.downTriggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagHighRainTodayAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.HighRainTodayAlarm.enabled)
			{
				return cumulus.HighRainTodayAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagHighRainRateAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.HighRainRateAlarm.enabled)
			{
				return cumulus.HighRainRateAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagHighWindGustAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.HighGustAlarm.enabled)
			{
				return cumulus.HighGustAlarm.triggered? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagHighWindSpeedAlarm(Dictionary<string,string> TagParams)
		{
			if (cumulus.HighWindAlarm.enabled)
			{
				return cumulus.HighWindAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagBatteryLowAlarm(Dictionary<string, string> TagParams)
		{
			if (cumulus.BatteryLowAlarm.enabled)
			{
				return cumulus.BatteryLowAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		private string TagDataSpikeAlarm(Dictionary<string, string> TagParams)
		{
			if (cumulus.SpikeAlarm.enabled)
			{
				return cumulus.SpikeAlarm.triggered ? "1" : "0";
			}
			else
			{
				return "0";
			}
		}

		// Monthly highs and lows - values
		private string TagMonthTempH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighTempThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthTempL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowTempThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthHeatIndexH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighHeatIndexThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthWChillL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowWindChillThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthAppTempH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighAppTempThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthAppTempL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowAppTempThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthFeelsLikeH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.HighFeelsLikeThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthFeelsLikeL(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.LowFeelsLikeThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthHumidexH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.HighHumidexThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthDewPointH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighDewpointThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthDewPointL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowDewpointThisMonth.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagMonthMinTempH(Dictionary<string,string> TagParams)
		{
			if (station.HighMinTempThisMonth > -999)
			{
				return CheckRC(station.HighMinTempThisMonth.ToString(cumulus.TempFormat), TagParams);
			}
			return "--";
		}

		private string TagMonthMaxTempL(Dictionary<string,string> TagParams)
		{
			if (station.LowMaxTempThisMonth < 999)
			{
				return CheckRC(station.LowMaxTempThisMonth.ToString(cumulus.TempFormat), TagParams);
			}
			return "--";
		}

		private string TagMonthPressH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighPressThisMonth.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagMonthPressL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowPressThisMonth.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagMonthHumH(Dictionary<string,string> TagParams)
		{
			return station.HighHumidityThisMonth.ToString(cumulus.HumFormat);
		}

		private string TagMonthHumL(Dictionary<string,string> TagParams)
		{
			return station.LowHumidityThisMonth.ToString(cumulus.HumFormat);
		}

		private string TagMonthGustH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighGustThisMonth.ToString(cumulus.WindFormat), TagParams);
		}

		private string TagMonthWindH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighWindThisMonth.ToString(cumulus.WindAvgFormat), TagParams);
		}

		private string TagMonthWindRunH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighDailyWindrunThisMonth.ToString(cumulus.WindRunFormat), TagParams);
		}

		private string TagMonthRainRateH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighRainThisMonth.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagMonthHourlyRainH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighHourlyRainThisMonth.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagMonthDailyRainH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighDailyRainThisMonth.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagMonthLongestDryPeriod(Dictionary<string,string> TagParams)
		{
			return station.LongestDryPeriodThisMonth.ToString();
		}

		private string TagMonthLongestWetPeriod(Dictionary<string,string> TagParams)
		{
			return station.LongestWetPeriodThisMonth.ToString();
		}

		private string TagMonthHighDailyTempRange(Dictionary<string,string> TagParams)
		{
			if (station.HighDailyTempRangeThisMonth > -999)
			{
				return CheckRC(station.HighDailyTempRangeThisMonth.ToString(cumulus.TempFormat), TagParams);
			}
			return "--";
		}

		private string TagMonthLowDailyTempRange(Dictionary<string,string> TagParams)
		{
			if (station.LowDailyTempRangeThisMonth < 999)
			{
				return CheckRC(station.LowDailyTempRangeThisMonth.ToString(cumulus.TempFormat), TagParams);
			}
			return "--";
		}

		// Monthly highs and lows - times
		private string TagMonthTempHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighTempThisMonthTS, "t", TagParams);
		}

		private string TagMonthTempLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowTempThisMonthTS, "t", TagParams);
		}

		private string TagMonthHeatIndexHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHeatIndexThisMonthTS, "t", TagParams);
		}

		private string TagMonthWChillLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowWindChillThisMonthTS, "t", TagParams);
		}

		private string TagMonthAppTempHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighAppTempThisMonthTS, "t", TagParams);
		}

		private string TagMonthAppTempLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowAppTempThisMonthTS, "t", TagParams);
		}

		private string TagMonthFeelsLikeHT(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighFeelsLikeThisMonthTS, "t", TagParams);
		}

		private string TagMonthFeelsLikeLT(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.LowFeelsLikeThisMonthTS, "t", TagParams);
		}

		private string TagMonthHumidexHT(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidexThisMonthTS, "t", TagParams);
		}

		private string TagMonthDewPointHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDewpointThisMonthTS, "t", TagParams);
		}

		private string TagMonthDewPointLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowDewpointThisMonthTS, "t", TagParams);
		}

		private string TagMonthPressHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighPressThisMonthTS, "t", TagParams);
		}

		private string TagMonthPressLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowPressThisMonthTS, "t", TagParams);
		}

		private string TagMonthHumHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidityThisMonthTS, "t", TagParams);
		}

		private string TagMonthHumLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowHumidityThisMonthTS, "t", TagParams);
		}

		private string TagMonthGustHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighGustThisMonthTS, "t", TagParams);
		}

		private string TagMonthWindHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighWindThisMonthTS, "t", TagParams);
		}

		private string TagMonthRainRateHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighRainThisMonthTS, "t", TagParams);
		}

		private string TagMonthHourlyRainHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHourlyRainThisMonthTS, "t", TagParams);
		}

		// Monthly highs and lows - dates
		private string TagMonthTempHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighTempThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthTempLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowTempThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthHeatIndexHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHeatIndexThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthWChillLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowWindChillThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthAppTempHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighAppTempThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthAppTempLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowAppTempThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthFeelsLikeHD(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighFeelsLikeThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthFeelsLikeLD(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.LowFeelsLikeThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthHumidexHD(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidexThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthDewPointHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDewpointThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthDewPointLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowDewpointThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthMinTempHD(Dictionary<string,string> TagParams)
		{
			if (station.HighMinTempThisMonth > -999)
			{
				return GetFormattedDateTime(station.HighMinTempThisMonthTS, "dd MMMM", TagParams);
			}
			return "---";
		}

		private string TagMonthMaxTempLD(Dictionary<string,string> TagParams)
		{
			if (station.LowMaxTempThisMonth < 999)
			{
				return GetFormattedDateTime(station.LowMaxTempThisMonthTS, "dd MMMM", TagParams);
			}
			return "---";
		}

		private string TagMonthPressHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighPressThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthPressLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowPressThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthHumHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidityThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthHumLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowHumidityThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthGustHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighGustThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthWindHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighWindThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthRainRateHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighRainThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthHourlyRainHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHourlyRainThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthHighDailyTempRangeD(Dictionary<string,string> TagParams)
		{
			if (station.HighDailyTempRangeThisMonth < 999)
			{
				return GetFormattedDateTime(station.HighDailyTempRangeThisMonthTS, "dd MMMM", TagParams);
			}
			return "------";
		}

		private string TagMonthLowDailyTempRangeD(Dictionary<string,string> TagParams)
		{
			if (station.LowDailyTempRangeThisMonth > -999)
			{
				return GetFormattedDateTime(station.LowDailyTempRangeThisMonthTS, "dd MMMM", TagParams);
			}
			return "------";
		}

		private string TagMonthWindRunHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDailyWindrunThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthDailyRainHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDailyRainThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthLongestDryPeriodD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LongestDryPeriodThisMonthTS, "dd MMMM", TagParams);
		}

		private string TagMonthLongestWetPeriodD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LongestWetPeriodThisMonthTS, "dd MMMM", TagParams);
		}

		// Yearly highs and lows - values
		private string TagYearTempH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighTempThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearTempL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowTempThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearHeatIndexH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighHeatIndexThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearWChillL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowWindChillThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearAppTempH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighAppTempThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearAppTempL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowAppTempThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearFeelsLikeH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.HighFeelsLikeThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearFeelsLikeL(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.LowFeelsLikeThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearHumidexH(Dictionary<string, string> TagParams)
		{
			return CheckRC(station.HighHumidexThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearDewPointH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighDewpointThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearDewPointL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowDewpointThisYear.ToString(cumulus.TempFormat), TagParams);
		}

		private string TagYearMinTempH(Dictionary<string,string> TagParams)
		{
			if (station.HighMinTempThisYear > -999)
			{
				return CheckRC(station.HighMinTempThisYear.ToString(cumulus.TempFormat), TagParams);
			}
			return "--";
		}

		private string TagYearMaxTempL(Dictionary<string,string> TagParams)
		{
			if (station.LowMaxTempThisYear < 999)
			{
				return CheckRC(station.LowMaxTempThisYear.ToString(cumulus.TempFormat), TagParams);
			}
			return "--";
		}

		private string TagYearPressH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighPressThisYear.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagYearPressL(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.LowPressThisYear.ToString(cumulus.PressFormat), TagParams);
		}

		private string TagYearHumH(Dictionary<string,string> TagParams)
		{
			return station.HighHumidityThisYear.ToString(cumulus.HumFormat);
		}

		private string TagYearHumL(Dictionary<string,string> TagParams)
		{
			return station.LowHumidityThisYear.ToString(cumulus.HumFormat);
		}

		private string TagYearGustH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighGustThisYear.ToString(cumulus.WindFormat), TagParams);
		}

		private string TagYearWindH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighWindThisYear.ToString(cumulus.WindAvgFormat), TagParams);
		}

		private string TagYearWindRunH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighDailyWindrunThisYear.ToString(cumulus.WindRunFormat), TagParams);
		}

		private string TagYearRainRateH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighRainThisYear.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagYearHourlyRainH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighHourlyRainThisYear.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagYearDailyRainH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighDailyRainThisYear.ToString(cumulus.RainFormat), TagParams);
		}

		private string TagYearLongestDryPeriod(Dictionary<string,string> TagParams)
		{
			return station.LongestDryPeriodThisYear.ToString();
		}

		private string TagYearLongestWetPeriod(Dictionary<string,string> TagParams)
		{
			return station.LongestWetPeriodThisYear.ToString();
		}

		private string TagYearHighDailyTempRange(Dictionary<string,string> TagParams)
		{
			if (station.HighDailyTempRangeThisYear > -999)
			{
				return CheckRC(station.HighDailyTempRangeThisYear.ToString(cumulus.TempFormat), TagParams);
			}
			return "--";
		}

		private string TagYearLowDailyTempRange(Dictionary<string,string> TagParams)
		{
			if (station.LowDailyTempRangeThisYear < 999)
			{
				return CheckRC(station.LowDailyTempRangeThisYear.ToString(cumulus.TempFormat), TagParams);
			}
			return "--";
		}

		private string TagYearMonthlyRainH(Dictionary<string,string> TagParams)
		{
			return CheckRC(station.HighMonthlyRainThisYear.ToString(cumulus.RainFormat), TagParams);
		}

		// Yearly highs and lows - times
		private string TagYearTempHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighTempThisYearTS, "t", TagParams);
		}

		private string TagYearTempLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowTempThisYearTS, "t", TagParams);
		}

		private string TagYearHeatIndexHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHeatIndexThisYearTS, "t", TagParams);
		}

		private string TagYearWChillLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowWindChillThisYearTS, "t", TagParams);
		}

		private string TagYearAppTempHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighAppTempThisYearTS, "t", TagParams);
		}

		private string TagYearAppTempLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowAppTempThisYearTS, "t", TagParams);
		}

		private string TagYearFeelsLikeHT(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighFeelsLikeThisYearTS, "t", TagParams);
		}

		private string TagYearFeelsLikeLT(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.LowFeelsLikeThisYearTS, "t", TagParams);
		}

		private string TagYearHumidexHT(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidexThisYearTS, "t", TagParams);
		}
		private string TagYearDewPointHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDewpointThisYearTS, "t", TagParams);
		}

		private string TagYearDewPointLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowDewpointThisYearTS, "t", TagParams);
		}

		private string TagYearPressHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighPressThisYearTS, "t", TagParams);
		}

		private string TagYearPressLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowPressThisYearTS, "t", TagParams);
		}

		private string TagYearHumHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidityThisYearTS, "t", TagParams);
		}

		private string TagYearHumLT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowHumidityThisYearTS, "t", TagParams);
		}

		private string TagYearGustHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighGustThisYearTS, "t", TagParams);
		}

		private string TagYearWindHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighWindThisYearTS, "t", TagParams);
		}

		private string TagYearRainRateHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighRainThisYearTS, "t", TagParams);
		}

		private string TagYearHourlyRainHT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHourlyRainThisYearTS, "t", TagParams);
		}

		// Yearly highs and lows - dates
		private string TagYearTempHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighTempThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearTempLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowTempThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearHeatIndexHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHeatIndexThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearWChillLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowWindChillThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearAppTempHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighAppTempThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearAppTempLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowAppTempThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearFeelsLikeHD(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighFeelsLikeThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearFeelsLikeLD(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.LowFeelsLikeThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearHumidexHD(Dictionary<string, string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidexThisYearTS, "dd MMMM", TagParams);
		}
		private string TagYearDewPointHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDewpointThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearDewPointLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowDewpointThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearMinTempHD(Dictionary<string,string> TagParams)
		{
			if (station.HighMinTempThisYear > -999)
			{
				return GetFormattedDateTime(station.HighMinTempThisYearTS, "dd MMMM", TagParams);
			}
			return "---";
		}

		private string TagYearMaxTempLD(Dictionary<string,string> TagParams)
		{
			if (station.LowMaxTempThisYear < 999)
			{
				return GetFormattedDateTime(station.LowMaxTempThisYearTS, "dd MMMM", TagParams);
			}
			return "---";
		}

		private string TagYearPressHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighPressThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearPressLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowPressThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearHumHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHumidityThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearHumLD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LowHumidityThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearGustHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighGustThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearWindHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighWindThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearRainRateHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighRainThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearHourlyRainHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighHourlyRainThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearHighDailyTempRangeD(Dictionary<string,string> TagParams)
		{
			if (station.HighDailyTempRangeThisYear > -999)
			{
				return GetFormattedDateTime(station.HighDailyTempRangeThisYearTS, "dd MMMM", TagParams);
			}
			return "------";
		}

		private string TagYearLowDailyTempRangeD(Dictionary<string,string> TagParams)
		{
			if (station.LowDailyTempRangeThisYear < 999)
			{
				return GetFormattedDateTime(station.LowDailyTempRangeThisYearTS, "dd MMMM", TagParams);
			}
			return "------";
		}

		private string TagYearWindRunHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDailyWindrunThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearDailyRainHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighDailyRainThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearLongestDryPeriodD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LongestDryPeriodThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearLongestWetPeriodD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LongestWetPeriodThisYearTS, "dd MMMM", TagParams);
		}

		private string TagYearMonthlyRainHD(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.HighMonthlyRainThisYearTS, "MMMM", TagParams);
		}

		//------------------------------------------------------------

		private string TagLastDataReadT(Dictionary<string,string> TagParams)
		{
			return GetFormattedDateTime(station.LastDataReadTimestamp, "G",TagParams);
		}

		private string TagLatestError(Dictionary<string,string> TagParams)
		{
			return cumulus.LatestError;
		}

		private string TagLatestErrorDate(Dictionary<string,string> TagParams)
		{
			if (cumulus.LatestErrorTS == DateTime.MinValue)
			{
				return "------";
			}
			else
			{
				return GetFormattedDateTime(cumulus.LatestErrorTS, "ddddd", TagParams);
			}
		}

		private string TagLatestErrorTime(Dictionary<string,string> TagParams)
		{
			if (cumulus.LatestErrorTS == DateTime.MinValue)
			{
				return "------";
			}
			else
			{
				return GetFormattedDateTime(cumulus.LatestErrorTS, "t", TagParams);
			}
		}

		private string TagOsVersion(Dictionary<string,string> TagParams)
		{
			return Environment.OSVersion.ToString();
		}

		private string TagOsLanguage(Dictionary<string,string> TagParams)
		{
			return CultureInfo.CurrentCulture.DisplayName;
		}

		private string TagSystemUpTime(Dictionary<string,string> TagParams)
		{
			try
			{
				var upTime = new PerformanceCounter("System", "System Up Time");
				upTime.NextValue();
				TimeSpan ts = TimeSpan.FromSeconds(upTime.NextValue());
				return String.Format("{0} days {1} hours", ts.Days, ts.Hours);
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error processing SystemUpTime web tag");
				cumulus.LogMessage(ex.Message);
				return "Error";
			}
		}

		private string TagProgramUpTime(Dictionary<string,string> TagParams)
		{
			// Bug in Mono Process.StartTime - wraps after 24 days
			TimeSpan ts = DateTime.Now - Program.StartTime;
			return String.Format("{0} days {1} hours", ts.Days, ts.Hours);
		}

		private string TagProgramUpTimeMs(Dictionary<string, string> TagParams)
		{
			// Bug in Mono Process.StartTime - wraps after 24 days
			TimeSpan ts = DateTime.Now - Program.StartTime;
			return ts.TotalMilliseconds.ToString();
		}

		private string TagCpuName(Dictionary<string,string> TagParams)
		{
			return "n/a";
		}

		private string TagCpuCount(Dictionary<string,string> TagParams)
		{
			return Environment.ProcessorCount.ToString();
		}

		private string TagMemoryStatus(Dictionary<string,string> TagParams)
		{
			return "n/a";
		}

		private string TagDisplayModeString(Dictionary<string,string> TagParams)
		{
			return "n/a";
		}

		private string TagAllocatedMemory(Dictionary<string,string> TagParams)
		{
			return (Environment.WorkingSet/1048576.0).ToString("F2") + " MB";
		}

		private string TagDiskSize(Dictionary<string,string> TagParams)
		{
			return "n/a";
		}

		private string TagDiskFree(Dictionary<string,string> TagParams)
		{
			return "n/a";
		}

		private string TagCpuTemp(Dictionary<string, string> TagParams)
		{
			return cumulus.CPUtemp.ToString(cumulus.TempFormat);
		}

		private string TagDavisTotalPacketsReceived(Dictionary<string,string> TagParams)
		{
			return station.DavisTotalPacketsReceived.ToString();
		}

		private string TagDavisTotalPacketsMissed(Dictionary<string,string> TagParams)
		{
			int tx = Int32.TryParse(TagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisTotalPacketsMissed[tx].ToString();
		}

		private string TagDavisNumberOfResynchs(Dictionary<string,string> TagParams)
		{
			int tx = Int32.TryParse(TagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisNumberOfResynchs[tx].ToString();
		}

		private string TagDavisMaxInARow(Dictionary<string,string> TagParams)
		{
			int tx = Int32.TryParse(TagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisMaxInARow[tx].ToString();
		}

		private string TagDavisNumCRCerrors(Dictionary<string,string> TagParams)
		{
			int tx = Int32.TryParse(TagParams.Get("tx"), out tx) ? tx : 0; // Default to transmitter 0=VP2
			return station.DavisNumCRCerrors[tx].ToString();
		}

		private string TagDavisReceptionPercent(Dictionary<string, string> TagParams)
		{
			int tx = Int32.TryParse(TagParams.Get("tx"), out tx) ? tx : 1; // Only WLL uses this, default to transmitter 1
			return station.DavisReceptionPct[tx].ToString();
		}

		private string TagDavisTxRssi(Dictionary<string, string> TagParams)
		{
			int tx = Int32.TryParse(TagParams.Get("tx"), out tx) ? tx : 1; // Only WLL uses this, default to transmitter 1
			return station.DavisTxRssi[tx].ToString();
		}

		private string TagDavisFirmwareVersion(Dictionary<string,string> TagParams)
		{
			return station.DavisFirmwareVersion;
		}

		private string TagGW1000FirmwareVersion(Dictionary<string, string> TagParams)
		{
			return station.GW1000FirmwareVersion;
		}

		private string Tagdailygraphperiod(Dictionary<string, string> tagparams)
		{
			return cumulus.GraphDays.ToString();
		}

		// Recent history
		private string TagRecentOutsideTemp(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.OutdoorTemperature.ToString(cumulus.TempFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].OutsideTemp.ToString(cumulus.TempFormat), TagParams);
			}
		}

		private string TagRecentWindSpeed(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.WindAverage.ToString(cumulus.WindAvgFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].WindSpeed.ToString(cumulus.WindAvgFormat), TagParams);
			}
		}

		private string TagRecentWindGust(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.RecentMaxGust.ToString(cumulus.WindFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].WindGust.ToString(cumulus.WindFormat), TagParams);
			}
		}

		private string TagRecentWindLatest(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.WindLatest.ToString(cumulus.WindFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].WindLatest.ToString(cumulus.WindFormat), TagParams);
			}
		}

		private string TagRecentWindDir(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return station.Bearing.ToString();
			}
			else
			{
				return result[0].WindDir.ToString();
			}
		}

		private string TagRecentWindAvgDir(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return station.AvgBearing.ToString();
			}
			else
			{
				return result[0].WindAvgDir.ToString();
			}
		}

		private string TagRecentWindChill(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return  CheckRC(station.WindChill.ToString(cumulus.TempFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].WindChill.ToString(cumulus.TempFormat), TagParams);
			}
		}

		private string TagRecentDewPoint(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.OutdoorDewpoint.ToString(cumulus.TempFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].DewPoint.ToString(cumulus.TempFormat), TagParams);
			}
		}

		private string TagRecentHeatIndex(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.HeatIndex.ToString(cumulus.TempFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].HeatIndex.ToString(cumulus.TempFormat), TagParams);
			}
		}

		private string TagRecentFeelsLike(Dictionary<string, string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.FeelsLike.ToString(cumulus.TempFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].FeelsLike.ToString(cumulus.TempFormat), TagParams);
			}
		}

		private string TagRecentHumidex(Dictionary<string, string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.Humidex.ToString(cumulus.TempFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].Humidex.ToString(cumulus.TempFormat), TagParams);
			}
		}

		private string TagRecentHumidity(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return station.OutdoorHumidity.ToString();
			}
			else
			{
				return result[0].Humidity.ToString();
			}
		}

		private string TagRecentPressure(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.Pressure.ToString(cumulus.PressFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].Pressure.ToString(cumulus.PressFormat), TagParams);
			}
		}

		private string TagRecentRainToday(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.RainToday.ToString(cumulus.RainFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].RainToday.ToString(cumulus.RainFormat), TagParams);
			}
		}

		private string TagRecentSolarRad(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return station.SolarRad.ToString("F0");
			}
			else
			{
				return result[0].SolarRad.ToString("F0");
			}
		}

		private string TagRecentUV(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return CheckRC(station.UV.ToString(cumulus.UVFormat), TagParams);
			}
			else
			{
				return CheckRC(result[0].UV.ToString(cumulus.UVFormat), TagParams);
			}
		}

		private string TagRecentTS(Dictionary<string,string> TagParams)
		{
			var recentTS = GetRecentTS(TagParams);

			var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

			if (result.Count == 0)
			{
				return GetFormattedDateTime(DateTime.Now,TagParams);
			}
			else
			{
				return GetFormattedDateTime(result[0].Timestamp,TagParams);
			}
		}

		// Recent history with commas replaced
		private string TagRCRecentOutsideTemp(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentOutsideTemp(TagParams));
		}

		private string TagRCRecentWindSpeed(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentWindSpeed(TagParams));
		}

		private string TagRCRecentWindGust(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentWindGust(TagParams));
		}

		private string TagRCRecentWindLatest(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentWindLatest(TagParams));
		}

		private string TagRCRecentWindChill(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentWindChill(TagParams));
		}

		private string TagRCRecentDewPoint(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentDewPoint(TagParams));
		}

		private string TagRCRecentHeatIndex(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentHeatIndex(TagParams));
		}

		private string TagRCRecentPressure(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentPressure(TagParams));
		}

		private string TagRCRecentRainToday(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentRainToday(TagParams));
		}

		private string TagRCRecentUV(Dictionary<string,string> TagParams)
		{
			return ReplaceCommas(TagRecentUV(TagParams));
		}

		public void InitialiseWebtags()
		{
			// create the web tag dictionary
			WebTagDictionary = new Dictionary<string, WebTagFunction>
			{
				{ "time", TagTime },
				{ "DaysSince30Dec1899", TagDaysSince30Dec1899 },
				{ "timeUTC", TagTimeUTC },
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
				{ "forecast", Tagforecast },
				{ "forecastnumber", Tagforecastnumber },
				{ "cumulusforecast", Tagcumulusforecast },
				{ "wsforecast", Tagwsforecast },
				{ "forecastenc", Tagforecastenc },
				{ "cumulusforecastenc", Tagcumulusforecastenc },
				{ "wsforecastenc", Tagwsforecastenc },
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
				{ "r24hour", Tagr24hour },
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
				{ "tempTH", TagtempTH },
				{ "TtempTH", TagTtempTH },
				{ "tempTL", TagtempTL },
				{ "TtempTL", TagTtempTL },
				{ "wchillTL", TagwchillTL },
				{ "TwchillTL", TagTwchillTL },
				{ "apptempTH", TagapptempTH },
				{ "TapptempTH", TagTapptempTH },
				{ "apptempTL", TagapptempTL },
				{ "TapptempTL", TagTapptempTL },
				{ "feelslikeTH", TagfeelslikeTH },
				{ "TfeelslikeTH", TagTfeelslikeTH },
				{ "feelslikeTL", TagfeelslikeTL },
				{ "TfeelslikeTL", TagTfeelslikeTL },
				{ "humidexTH", TaghumidexTH },
				{ "ThumidexTH", TagThumidexTH },
				{ "dewpointTH", TagdewpointTH },
				{ "TdewpointTH", TagTdewpointTH },
				{ "dewpointTL", TagdewpointTL },
				{ "TdewpointTL", TagTdewpointTL },
				{ "heatindexTH", TagheatindexTH },
				{ "TheatindexTH", TagTheatindexTH },
				{ "pressTH", TagpressTH },
				{ "TpressTH", TagTpressTH },
				{ "pressTL", TagpressTL },
				{ "TpressTL", TagTpressTL },
				{ "humTH", TaghumTH },
				{ "ThumTH", TagThumTH },
				{ "humTL", TaghumTL },
				{ "ThumTL", TagThumTL },
				{ "windTM", TagwindTM },
				{ "Tbeaufort", TagTbeaufort },
				{ "Tbeaufortnumber", TagTbeaufortnumber },
				{ "TwindTM", TagTwindTM },
				{ "wgustTM", TagwgustTM },
				{ "TwgustTM", TagTwgustTM },
				{ "bearingTM", TagbearingTM },
				{ "directionTM", TagdirectionTM },
				{ "rrateTM", TagrrateTM },
				{ "TrrateTM", TagTrrateTM },
				{ "hourlyrainTH", TaghourlyrainTH },
				{ "ThourlyrainTH", TagThourlyrainTH },
				{ "hourlyrainYH", TaghourlyrainYH },
				{ "ThourlyrainYH", TagThourlyrainYH },
				{ "solarTH", TagSolarTH },
				{ "TsolarTH", TagTsolarTH },
				{ "UVTH", TagUVTH },
				{ "TUVTH", TagTUVTH },
				{ "rollovertime", Tagrollovertime },
				{ "currcond", Tagcurrcond },
				{ "currcondenc", Tagcurrcondenc },
				{ "tempYH", TagtempYH },
				{ "TtempYH", TagTtempYH },
				{ "tempYL", TagtempYL },
				{ "TtempYL", TagTtempYL },
				{ "wchillYL", TagwchillYL },
				{ "TwchillYL", TagTwchillYL },
				{ "apptempYH", TagapptempYH },
				{ "TapptempYH", TagTapptempYH },
				{ "apptempYL", TagapptempYL },
				{ "TapptempYL", TagTapptempYL },
				{ "feelslikeYH", TagfeelslikeYH },
				{ "TfeelslikeYH", TagTfeelslikeYH },
				{ "feelslikeYL", TagfeelslikeYL },
				{ "TfeelslikeYL", TagTfeelslikeYL },
				{ "humidexYH", TaghumidexYH },
				{ "ThumidexYH", TagThumidexYH },
				{ "dewpointYH", TagdewpointYH },
				{ "TdewpointYH", TagTdewpointYH },
				{ "dewpointYL", TagdewpointYL },
				{ "TdewpointYL", TagTdewpointYL },
				{ "heatindexYH", TagheatindexYH },
				{ "TheatindexYH", TagTheatindexYH },
				{ "pressYH", TagpressYH },
				{ "TpressYH", TagTpressYH },
				{ "pressYL", TagpressYL },
				{ "TpressYL", TagTpressYL },
				{ "humYH", TaghumYH },
				{ "ThumYH", TagThumYH },
				{ "humYL", TaghumYL },
				{ "ThumYL", TagThumYL },
				{ "windYM", TagwindYM },
				{ "Ybeaufort", TagYbeaufort },
				{ "Ybeaufortnumber", TagYbeaufortnumber },
				{ "TwindYM", TagTwindYM },
				{ "wgustYM", TagwgustYM },
				{ "TwgustYM", TagTwgustYM },
				{ "bearingYM", TagbearingYM },
				{ "directionYM", TagdirectionYM },
				{ "rrateYM", TagrrateYM },
				{ "TrrateYM", TagTrrateYM },
				{ "rfallY", TagrfallY },
				{ "solarYH", TagSolarYH },
				{ "TsolarYH", TagTsolarYH },
				{ "UVYH", TagUVYH },
				{ "TUVYH", TagTUVYH },
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
				{ "latitude", Taglatitude },
				{ "longitude", Taglongitude },
				{ "location", Taglocation },
				{ "longlocation", Taglonglocation },
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
				{ "altitude", Tagaltitude },
				{ "forum", Tagforum },
				{ "webcam", Tagwebcam },
				{ "tempunit", Tagtempunit },
				{ "tempunitnodeg", Tagtempunitnodeg },
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
				{ "LastRainTipISO", TagLastRainTipISO },
				{ "MinutesSinceLastRainTip", TagMinutesSinceLastRainTip },
				{ "LastDataReadT", TagLastDataReadT },
				{ "LatestNOAAMonthlyReport", TagLatestNOAAMonthlyReport },
				{ "LatestNOAAYearlyReport", TagLatestNOAAYearlyReport },
				{ "dailygraphperiod", Tagdailygraphperiod },
				{ "RCtemp", TagRCtemp },
				{ "RCtempTH", TagRCtempTH },
				{ "RCtempTL", TagRCtempTL },
				{ "RCintemp", TagRCintemp },
				{ "RCdew", TagRCdew },
				{ "RCheatindex", TagRCheatindex },
				{ "RCwchill", TagRCwchill },
				{ "RChum", TagRChum },
				{ "RCinhum", TagRCinhum },
				{ "RCrfall", TagRCrfall },
				{ "RCrrate", TagRCrrate },
				{ "RCrrateTM", TagRCrrateTM },
				{ "RCwgust", TagRCwgust },
				{ "RCwlatest", TagRCwlatest },
				{ "RCwspeed", TagRCwspeed },
				{ "RCwgustTM", TagRCwgustTM },
				{ "RCpress", TagRCpress },
				{ "RCpressTH", TagRCpressTH },
				{ "RCpressTL", TagRCpressTL },
				{ "RCdewpointTH", TagRCdewpointTH },
				{ "RCdewpointTL", TagRCdewpointTL },
				{ "RCwchillTL", TagRCwchillTL },
				{ "RCheatindexTH", TagRCheatindexTH },
				{ "RCapptempTH", TagRCapptempTH },
				{ "RCapptempTL", TagRCapptempTL },
				{ "ET", TagET },
				{ "UV", TagUV },
				{ "SolarRad", TagSolarRad },
				{ "Light", TagLight },
				{ "CurrentSolarMax", TagCurrentSolarMax },
				{ "SunshineHours", TagSunshineHours },
				{ "YSunshineHours", TagYSunshineHours },
				{ "IsSunny", TagIsSunny },
				{ "IsRaining", TagIsRaining },
				{ "IsFreezing", TagIsFreezing },
				{ "THWindex", TagTHWIndex },
				{ "THSWindex", TagTHSWIndex },
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
				{ "ExtraDP1", TagExtraDP1 },
				{ "ExtraDP2", TagExtraDP2 },
				{ "ExtraDP3", TagExtraDP3 },
				{ "ExtraDP4", TagExtraDP4 },
				{ "ExtraDP5", TagExtraDP5 },
				{ "ExtraDP6", TagExtraDP6 },
				{ "ExtraDP7", TagExtraDP7 },
				{ "ExtraDP8", TagExtraDP8 },
				{ "ExtraDP9", TagExtraDP9 },
				{ "ExtraDP10", TagExtraDP10 },
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
				{ "CO2", TagCO2 },
				{ "CO2-24h", TagCO2_24h },
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
				{ "RG11RainToday", TagRG11RainToday },
				{ "RG11RainYest", TagRG11RainYest },
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

				{ "AirLinkAqiPm2p5In", TagAirLinkAqiPm2p5In },
				{ "AirLinkAqiPm2p5_1hrIn", TagAirLinkAqiPm2p5_1hrIn },
				{ "AirLinkAqiPm2p5_3hrIn", TagAirLinkAqiPm2p5_3hrIn },
				{ "AirLinkAqiPm2p5_24hrIn", TagAirLinkAqiPm2p5_24hrIn },
				{ "AirLinkAqiPm2p5_NowcastIn", TagAirLinkAqiPm2p5_NowcastIn },
				{ "AirLinkAqiPm10In", TagAirLinkAqiPm10In },
				{ "AirLinkAqiPm10_1hrIn", TagAirLinkAqiPm10_1hrIn },
				{ "AirLinkAqiPm10_3hrIn", TagAirLinkAqiPm10_3hrIn },
				{ "AirLinkAqiPm10_24hrIn", TagAirLinkAqiPm10_24hrIn },
				{ "AirLinkAqiPm10_NowcastIn", TagAirLinkAqiPm10_NowcastIn },

				{ "AirLinkAqiPm2p5Out", TagAirLinkAqiPm2p5Out },
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
				{ "MonthTempHT", TagMonthTempHT },
				{ "MonthTempLT", TagMonthTempLT },
				{ "MonthHeatIndexHT", TagMonthHeatIndexHT },
				{ "MonthWChillLT", TagMonthWChillLT },
				{ "MonthAppTempHT", TagMonthAppTempHT },
				{ "MonthAppTempLT", TagMonthAppTempLT },
				{ "MonthFeelsLikeHT", TagMonthFeelsLikeHT },
				{ "MonthFeelsLikeLT", TagMonthFeelsLikeLT },
				{ "MonthHumidexHT", TagMonthHumidexHT },
				{ "MonthPressHT", TagMonthPressHT },
				{ "MonthPressLT", TagMonthPressLT },
				{ "MonthHumHT", TagMonthHumHT },
				{ "MonthHumLT", TagMonthHumLT },
				{ "MonthGustHT", TagMonthGustHT },
				{ "MonthWindHT", TagMonthWindHT },
				{ "MonthRainRateHT", TagMonthRainRateHT },
				{ "MonthHourlyRainHT", TagMonthHourlyRainHT },
				{ "MonthDewPointHT", TagMonthDewPointHT },
				{ "MonthDewPointLT", TagMonthDewPointLT },
				// This month"s highs and lows - dates
				{ "MonthTempHD", TagMonthTempHD },
				{ "MonthTempLD", TagMonthTempLD },
				{ "MonthHeatIndexHD", TagMonthHeatIndexHD },
				{ "MonthWChillLD", TagMonthWChillLD },
				{ "MonthAppTempHD", TagMonthAppTempHD },
				{ "MonthAppTempLD", TagMonthAppTempLD },
				{ "MonthFeelsLikeHD", TagMonthFeelsLikeHD },
				{ "MonthFeelsLikeLD", TagMonthFeelsLikeLD },
				{ "MonthHumidexHD", TagMonthHumidexHD },
				{ "MonthMinTempHD", TagMonthMinTempHD },
				{ "MonthMaxTempLD", TagMonthMaxTempLD },
				{ "MonthPressHD", TagMonthPressHD },
				{ "MonthPressLD", TagMonthPressLD },
				{ "MonthHumHD", TagMonthHumHD },
				{ "MonthHumLD", TagMonthHumLD },
				{ "MonthGustHD", TagMonthGustHD },
				{ "MonthWindHD", TagMonthWindHD },
				{ "MonthRainRateHD", TagMonthRainRateHD },
				{ "MonthHourlyRainHD", TagMonthHourlyRainHD },
				{ "MonthDailyRainHD", TagMonthDailyRainHD },
				{ "MonthDewPointHD", TagMonthDewPointHD },
				{ "MonthDewPointLD", TagMonthDewPointLD },
				{ "MonthWindRunHD", TagMonthWindRunHD },
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
				{ "YearTempHT", TagYearTempHT },
				{ "YearTempLT", TagYearTempLT },
				{ "YearHeatIndexHT", TagYearHeatIndexHT },
				{ "YearWChillLT", TagYearWChillLT },
				{ "YearAppTempHT", TagYearAppTempHT },
				{ "YearAppTempLT", TagYearAppTempLT },
				{ "YearFeelsLikeHT", TagYearFeelsLikeHT },
				{ "YearFeelsLikeLT", TagYearFeelsLikeLT },
				{ "YearHumidexHT", TagYearHumidexHT },
				{ "YearPressHT", TagYearPressHT },
				{ "YearPressLT", TagYearPressLT },
				{ "YearHumHT", TagYearHumHT },
				{ "YearHumLT", TagYearHumLT },
				{ "YearGustHT", TagYearGustHT },
				{ "YearWindHT", TagYearWindHT },
				{ "YearRainRateHT", TagYearRainRateHT },
				{ "YearHourlyRainHT", TagYearHourlyRainHT },
				{ "YearDewPointHT", TagYearDewPointHT },
				{ "YearDewPointLT", TagYearDewPointLT },
				// Yearly highs and lows - dates
				{ "YearTempHD", TagYearTempHD },
				{ "YearTempLD", TagYearTempLD },
				{ "YearHeatIndexHD", TagYearHeatIndexHD },
				{ "YearWChillLD", TagYearWChillLD },
				{ "YearAppTempHD", TagYearAppTempHD },
				{ "YearAppTempLD", TagYearAppTempLD },
				{ "YearFeelsLikeHD", TagYearFeelsLikeHD },
				{ "YearFeelsLikeLD", TagYearFeelsLikeLD },
				{ "YearHumidexHD", TagYearHumidexHD },
				{ "YearMinTempHD", TagYearMinTempHD },
				{ "YearMaxTempLD", TagYearMaxTempLD },
				{ "YearPressHD", TagYearPressHD },
				{ "YearPressLD", TagYearPressLD },
				{ "YearHumHD", TagYearHumHD },
				{ "YearHumLD", TagYearHumLD },
				{ "YearGustHD", TagYearGustHD },
				{ "YearWindHD", TagYearWindHD },
				{ "YearRainRateHD", TagYearRainRateHD },
				{ "YearHourlyRainHD", TagYearHourlyRainHD },
				{ "YearDailyRainHD", TagYearDailyRainHD },
				{ "YearMonthlyRainHD", TagYearMonthlyRainHD },
				{ "YearDewPointHD", TagYearDewPointHD },
				{ "YearDewPointLD", TagYearDewPointLD },
				{ "YearWindRunHD", TagYearWindRunHD },
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
				{ "DavisNumCRCerrors", TagDavisNumCRCerrors },
				{ "DavisReceptionPercent", TagDavisReceptionPercent },
				{ "DavisFirmwareVersion", TagDavisFirmwareVersion },
				{ "DavisTxRssi", TagDavisTxRssi },
				{ "GW1000FirmwareVersion", TagGW1000FirmwareVersion },
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
				{ "RecentUV", TagRecentUV },
				{ "RecentTS", TagRecentTS },
				// Recent history with comma decimals replaced
				{ "RCRecentOutsideTemp", TagRCRecentOutsideTemp },
				{ "RCRecentWindSpeed", TagRCRecentWindSpeed },
				{ "RCRecentWindGust", TagRCRecentWindGust },
				{ "RCRecentWindLatest", TagRCRecentWindLatest },
				{ "RCRecentWindChill", TagRCRecentWindChill },
				{ "RCRecentDewPoint", TagRCRecentDewPoint },
				{ "RCRecentHeatIndex", TagRCRecentHeatIndex },
				{ "RCRecentPressure", TagRCRecentPressure },
				{ "RCRecentRainToday", TagRCRecentRainToday },
				{ "RCRecentUV", TagRCRecentUV },
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
				{ "ByMonthTempHT", TagByMonthTempHT },
				{ "ByMonthTempLT", TagByMonthTempLT },
				{ "ByMonthAppTempHT", TagByMonthAppTempHT },
				{ "ByMonthAppTempLT", TagByMonthAppTempLT },
				{ "ByMonthFeelsLikeHT", TagByMonthFeelsLikeHT },
				{ "ByMonthFeelsLikeLT", TagByMonthFeelsLikeLT },
				{ "ByMonthHumidexHT", TagByMonthHumidexHT },
				//{ "ByMonthHumidexLT", TagByMonthHumidexLT },
				{ "ByMonthDewPointHT", TagByMonthDewPointHT },
				{ "ByMonthDewPointLT", TagByMonthDewPointLT },
				{ "ByMonthHeatIndexHT", TagByMonthHeatIndexHT },
				{ "ByMonthGustHT", TagByMonthGustHT },
				{ "ByMonthWindHT", TagByMonthWindHT },
				{ "ByMonthWindRunHT", TagByMonthWindRunHT },
				{ "ByMonthWChillLT", TagByMonthWChillLT },
				{ "ByMonthRainRateHT", TagByMonthRainRateHT },
				{ "ByMonthDailyRainHT", TagByMonthDailyRainHT },
				{ "ByMonthHourlyRainHT", TagByMonthHourlyRainHT },
				{ "ByMonthMonthlyRainHT", TagByMonthMonthlyRainHT },
				{ "ByMonthPressHT", TagByMonthPressHT },
				{ "ByMonthPressLT", TagByMonthPressLT },
				{ "ByMonthHumHT", TagByMonthHumHT },
				{ "ByMonthHumLT", TagByMonthHumLT },
				{ "ByMonthMinTempHT", TagByMonthMinTempHT },
				{ "ByMonthMaxTempLT", TagByMonthMaxTempLT },
				{ "ByMonthLongestDryPeriodT", TagByMonthLongestDryPeriodT },
				{ "ByMonthLongestWetPeriodT", TagByMonthLongestWetPeriodT },
				{ "ByMonthLowDailyTempRangeT", TagByMonthLowDailyTempRangeT },
				{ "ByMonthHighDailyTempRangeT", TagByMonthHighDailyTempRangeT }
			};

			cumulus.LogMessage(WebTagDictionary.Count + " web tags initialised");

			if (cumulus.ListWebTags)
			{
				using (StreamWriter file = new StreamWriter(cumulus.WebTagFile))
				{
					foreach (var pair in WebTagDictionary)
					{
						file.WriteLine(pair.Key);
					}
				}
			}
		}

		public string GetWebTagText(string TagString, Dictionary<string,string> TagParams)
		{
			if (WebTagDictionary.ContainsKey(TagString))
			{
				return WebTagDictionary[TagString](TagParams);
			}

			return String.Empty;
		}

		//private static string Utf16ToUtf8(string utf16String)
		//{
		//	/**************************************************************
		//	 * Every .NET string will store text with the UTF16 encoding, *
		//	 * known as Encoding.Unicode. Other encodings may exist as    *
		//	 * Byte-Array or incorrectly stored with the UTF16 encoding.  *
		//	 *                                                            *
		//	 * UTF8 = 1 bytes per char                                    *
		//	 *    ["100" for the ansi 'd']                                *
		//	 *    ["206" and "186" for the russian 'κ']                   *
		//	 *                                                            *
		//	 * UTF16 = 2 bytes per char                                   *
		//	 *    ["100, 0" for the ansi 'd']                             *
		//	 *    ["186, 3" for the russian 'κ']                          *
		//	 *                                                            *
		//	 * UTF8 inside UTF16                                          *
		//	 *    ["100, 0" for the ansi 'd']                             *
		//	 *    ["206, 0" and "186, 0" for the russian 'κ']             *
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
		public static tvalue Get<tkey, tvalue>(this Dictionary<tkey, tvalue> dictionary, tkey key)
		{
			tvalue value;
			dictionary.TryGetValue(key, out value);
			return value;
		}
	}
}
