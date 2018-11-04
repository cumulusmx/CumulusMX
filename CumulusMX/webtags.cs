using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Web;

namespace CumulusMX
{
    public delegate string WebTagFunction(Dictionary<string,string> tagParams);

    internal class WebTags
    {
        private Dictionary<string, WebTagFunction> WebTagDictionary = null;

        private Cumulus cumulus;
        private WeatherStation station;

        public WebTags(Cumulus cumulus, WeatherStation station)
        {
            this.cumulus = cumulus;
            this.station = station;
        }

        private void FreeWebTagDictionary()
        {
            WebTagDictionary.Clear();
        }

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

        private int GetSnowDepth(DateTime day)
        {
            int result = 0;
            /* TODO: private int GetSnowDepth(DateTime day)
             * int depth;
            try {
                try {
                    //@ Unsupported property or method(C): 'Open'
                    MainUnit.Units.MainUnit.MainForm.ClientDataSet1.Open;
                    //@ Unsupported property or method(C): 'LoadFromFile'
                    MainUnit.Units.MainUnit.MainForm.ClientDataSet1.LoadFromFile;
                    //@ Unsupported property or method(A): 'Locate'
                    if (MainUnit.Units.MainUnit.MainForm.ClientDataSet1.Locate("EntryDate", day, new object.Get(] {}))
                    {
                        //@ Unsupported property or method(A): 'Lookup'
                        depth = MainUnit.Units.MainUnit.MainForm.ClientDataSet1.Lookup("EntryDate", day, "SnowDepth");
                    }
                    else
                    {
                        depth = 0;
                    }
                }
                catch(Exception E) {
                    MainUnit.Units.MainUnit.cumulus.LogMessage("Error reading diary file: " + E.Message);
                    depth = 0;
                }
            } finally {
                //@ Unsupported property or method(C): 'Close'
                MainUnit.Units.MainUnit.MainForm.ClientDataSet1.Close;
            }
            result = depth; */
            return result;
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
                    minsago = minsago + Convert.ToInt32(minsagostr);
                }
                catch
                {
                }
            }
            if (hoursagostr != null)
            {
                try
                {
                    minsago = minsago + (Convert.ToInt32(hoursagostr)*60);
                }
                catch
                {
                }
            }
            if (daysagostr != null)
            {
                try
                {
                    minsago = minsago + (Convert.ToInt32(daysagostr)*1440);
                }
                catch
                {
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

        private string GetCurrCondText()
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
            if (dt <= DateTime.MinValue)
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

        private string GetMonthlyAlltimeValueStr(int identifier, int month, string format)
        {
            if (station.monthlyrecarray[identifier, month].timestamp <= DateTime.MinValue)
            {
                return "---";
            }
            else
            {
                return station.monthlyrecarray[identifier, month].value.ToString(format);
            }
        }

        private string GetMonthlyAlltimeTSStr(int identifier, int month, string format)
        {
            if (station.monthlyrecarray[identifier, month].timestamp <= DateTime.MinValue)
            {
                return "----";
            }
            else
            {
                return station.monthlyrecarray[identifier, month].timestamp.ToString(format);
            }
        }

        private string Tagwlatest(Dictionary<string,string> TagParams)
        {
            return station.WindLatest.ToString(cumulus.WindFormat);
        }

        private string Tagwindrun(Dictionary<string,string> TagParams)
        {
            return station.WindRunToday.ToString(cumulus.WindRunFormat);
        }

        private string Tagwspeed(Dictionary<string,string> TagParams)
        {
            return station.WindAverage.ToString(cumulus.WindFormat);
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
            return station.RecentMaxGust.ToString(cumulus.WindFormat);
        }

        private string Tagwchill(Dictionary<string,string> TagParams)
        {
            return station.WindChill.ToString(cumulus.TempFormat);
        }

        private string Tagrrate(Dictionary<string,string> TagParams)
        {
            return station.RainRate.ToString(cumulus.RainFormat);
        }

        private string TagStormRain(Dictionary<string,string> TagParams)
        {
            return station.StormRain.ToString(cumulus.RainFormat);
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
            return station.YesterdayWindRun.ToString(cumulus.WindRunFormat);
        }

        private string Tagheatdegdays(Dictionary<string,string> TagParams)
        {
            return station.HeatingDegreeDays.ToString("F1");
        }

        private string Tagcooldegdays(Dictionary<string,string> TagParams)
        {
            return station.CoolingDegreeDays.ToString("F1");
        }

        private string TagheatdegdaysY(Dictionary<string,string> TagParams)
        {
            return station.YestHeatingDegreeDays.ToString("F1");
        }

        private string TagcooldegdaysY(Dictionary<string,string> TagParams)
        {
            return station.YestCoolingDegreeDays.ToString("F1");
        }

        private string Tagpresstrendval(Dictionary<string,string> TagParams)
        {
            return station.presstrendval.ToString(cumulus.PressFormat);
        }

        private string TagTempChangeLastHour(Dictionary<string,string> TagParams)
        {
            return station.TempChangeLastHour.ToString("+##0.0;-##0.0;0");
        }

        private string Tagdew(Dictionary<string,string> TagParams)
        {
            return station.OutdoorDewpoint.ToString(cumulus.TempFormat);
        }

        private string Tagwetbulb(Dictionary<string,string> TagParams)
        {
            return station.WetBulb.ToString(cumulus.TempFormat);
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
            return station.OutdoorTemperature.ToString(cumulus.TempFormat);
        }

        private string Tagtemprange(Dictionary<string,string> TagParams)
        {
            return station.TempRangeToday.ToString(cumulus.TempFormat);
        }

        private string TagtemprangeY(Dictionary<string,string> TagParams)
        {
            return station.TempRangeYesterday.ToString(cumulus.TempFormat);
        }

        private string Tagavgtemp(Dictionary<string,string> TagParams)
        {
            return (station.TempTotalToday/station.tempsamplestoday).ToString(cumulus.TempFormat);
        }

        private string TagavgtempY(Dictionary<string,string> TagParams)
        {
            return station.YestAvgTemp.ToString(cumulus.TempFormat);
        }

        private string Tagapptemp(Dictionary<string,string> TagParams)
        {
            return station.ApparentTemperature.ToString(cumulus.TempFormat);
        }

        private string Tagtemptrend(Dictionary<string,string> TagParams)
        {
            return station.temptrendval.ToString(cumulus.TempTrendFormat);
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
            return station.HeatIndex.ToString(cumulus.TempFormat);
        }

        private string Taghum(Dictionary<string,string> TagParams)
        {
            return station.OutdoorHumidity.ToString();
        }

        private string Taghumidex(Dictionary<string,string> TagParams)
        {
            return station.Humidex.ToString(cumulus.TempFormat);
        }

        private string Tagpress(Dictionary<string,string> TagParams)
        {
            return station.Pressure.ToString(cumulus.PressFormat);
        }

        private string Tagaltimeterpressure(Dictionary<string,string> TagParams)
        {
            return station.AltimeterPressure.ToString(cumulus.PressFormat);
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
            var sb = new StringBuilder((station.windspeeds[0]*cumulus.WindGustMult).ToString(cumulus.WindFormat).Replace(",", "."));
            for (var i = 1; i < station.numwindvalues; i++)
            {
                sb.Append("," + (station.windspeeds[i]*cumulus.WindGustMult).ToString(cumulus.WindFormat).Replace(",", "."));
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
            String s = (station.windcounts[0]*cumulus.WindGustMult).ToString(cumulus.WindFormat).Replace(",", ".");

            for (var i = 1; i < cumulus.NumWindRosePoints; i++)
            {
                s = s + "," + (station.windcounts[i]*cumulus.WindGustMult).ToString(cumulus.WindFormat).Replace(",", ".");
            }

            return s;
        }

        private string TagWindRosePoints(Dictionary<string,string> TagParams)
        {
            return cumulus.NumWindRosePoints.ToString();
        }

        private string Tagrfall(Dictionary<string,string> TagParams)
        {
            return station.RainToday.ToString(cumulus.RainFormat);
        }

        private string Tagrmidnight(Dictionary<string,string> TagParams)
        {
            return station.RainSinceMidnight.ToString(cumulus.RainFormat);
        }

        private string Tagrmonth(Dictionary<string,string> TagParams)
        {
            return station.RainMonth.ToString(cumulus.RainFormat);
        }

        private string Tagrhour(Dictionary<string,string> TagParams)
        {
            return station.RainLastHour.ToString(cumulus.RainFormat);
        }

        private string Tagr24hour(Dictionary<string,string> TagParams)
        {
            return station.RainLast24Hour.ToString(cumulus.RainFormat);
        }

        private string Tagryear(Dictionary<string,string> TagParams)
        {
            return station.RainYear.ToString(cumulus.RainFormat);
        }

        private string Taginhum(Dictionary<string,string> TagParams)
        {
            return station.IndoorHumidity.ToString();
        }

        private string Tagintemp(Dictionary<string,string> TagParams)
        {
            return station.IndoorTemperature.ToString(cumulus.TempFormat);
        }

        private string Tagbattery(Dictionary<string,string> TagParams)
        {
            return station.ConBatText;
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

                    if ((channel < sl.Length) && (sl[channel] != ""))
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
            return GetSnowDepth(ts).ToString();
        }

        private string Tagnewrecord(Dictionary<string,string> TagParams)
        {
            return station.AlltimeRecordTimestamp < DateTime.Now.AddDays(-1) ? "0" : "1";
        }

        private string TagTempRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagWindRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagRainRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHumidityRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagPressureRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighTempRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLowTempRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighAppTempRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLowAppTempRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighHeatIndexRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLowWindChillRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighMinTempRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLowMaxTempRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighDewPointRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLowDewPointRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighWindGustRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighWindSpeedRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighRainRateRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighHourlyRainRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighDailyRainRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighMonthlyRainRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighHumidityRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighWindrunRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLowHumidityRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighPressureRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLowPressureRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLongestDryPeriodRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLongestWetPeriodRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagHighTempRangeRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagLowTempRangeRecordSet(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string Tagerrorlight(Dictionary<string,string> TagParams)
        {
            // TODO
            return "0";
        }

        private string TagtempTH(Dictionary<string,string> TagParams)
        {
            return station.HighTempToday.ToString(cumulus.TempFormat);
        }

        private string TagTtempTH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.hightemptodaytime, "HH:mm", TagParams);
        }

        private string TagtempTL(Dictionary<string,string> TagParams)
        {
            return station.LowTempToday.ToString(cumulus.TempFormat);
        }

        private string TagTtempTL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowtemptodaytime, "HH:mm", TagParams);
        }

        private string TagSolarTH(Dictionary<string,string> TagParams)
        {
            return ((int)station.HighSolarToday).ToString();
        }

        private string TagTsolarTH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highsolartodaytime, "HH:mm", TagParams);
        }

        private string TagUVTH(Dictionary<string,string> TagParams)
        {
            return station.HighUVToday.ToString(cumulus.UVFormat);
        }

        private string TagTUVTH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highuvtodaytime, "HH:mm", TagParams);
        }

        private string TagapptempTH(Dictionary<string,string> TagParams)
        {
            return station.HighAppTempToday.ToString(cumulus.TempFormat);
        }

        private string TagRCapptempTH(Dictionary<string,string> TagParams)
        {
            return ReplaceCommas(station.HighAppTempToday.ToString(cumulus.TempFormat));
        }

        private string TagTapptempTH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highapptemptodaytime, "HH:mm", TagParams);
        }

        private string TagapptempTL(Dictionary<string,string> TagParams)
        {
            return station.LowAppTempToday.ToString(cumulus.TempFormat);
        }

        private string TagRCapptempTL(Dictionary<string,string> TagParams)
        {
            return ReplaceCommas(station.LowAppTempToday.ToString(cumulus.TempFormat));   
        }

        private string TagTapptempTL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowapptemptodaytime, "HH:mm", TagParams);
        }

        private string TagdewpointTH(Dictionary<string,string> TagParams)
        {
            return station.HighDewpointToday.ToString(cumulus.TempFormat);
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
            return station.LowDewpointToday.ToString(cumulus.TempFormat);
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
            return station.LowWindChillToday.ToString(cumulus.TempFormat);
        }

        private string TagRCwchillTL(Dictionary<string,string> TagParams)
        {
            return ReplaceCommas(station.LowWindChillToday.ToString(cumulus.TempFormat));
        }

        private string TagTwchillTL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowwindchilltodaytime, "HH:mm", TagParams);
        }

        private string TagheatindexTH(Dictionary<string,string> TagParams)
        {
            return station.HighHeatIndexToday.ToString(cumulus.TempFormat);
        }

        private string TagRCheatindexTH(Dictionary<string,string> TagParams)
        {
            return ReplaceCommas(station.HighHeatIndexToday.ToString(cumulus.TempFormat));
        }

        private string TagTheatindexTH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highheatindextodaytime, "HH:mm", TagParams);
        }

        private string TagheatindexYH(Dictionary<string,string> TagParams)
        {
            return station.HighHeatIndexYesterday.ToString(cumulus.TempFormat);
        }

        private string TagTheatindexYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highheatindexyesterdaytime, "HH:mm", TagParams);
        }

        private string TagpressTH(Dictionary<string,string> TagParams)
        {
            return station.highpresstoday.ToString(cumulus.PressFormat);
        }

        private string TagTpressTH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highpresstodaytime, "HH:mm", TagParams);
        }

        private string TagpressTL(Dictionary<string,string> TagParams)
        {
            return station.lowpresstoday.ToString(cumulus.PressFormat);
        }

        private string TagTpressTL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowpresstodaytime, "HH:mm", TagParams);
        }

        private string TaghumTH(Dictionary<string,string> TagParams)
        {
            return station.highhumiditytoday.ToString();
        }

        private string TagThumTH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highhumiditytodaytime, "HH:mm", TagParams);
        }

        private string TaghumTL(Dictionary<string,string> TagParams)
        {
            return station.lowhumiditytoday.ToString();
        }

        private string TagThumTL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowhumiditytodaytime, "HH:mm", TagParams);
        }

        private string TagwindTM(Dictionary<string,string> TagParams)
        {
            return station.highwindtoday.ToString(cumulus.WindFormat);
        }

        private string TagTbeaufort(Dictionary<string,string> TagParams)
        {
            return "F" + cumulus.Beaufort(station.highwindtoday);
        }

        private string TagTbeaufortnumber(Dictionary<string,string> TagParams)
        {
            return cumulus.Beaufort(station.highwindtoday);
        }

        private string TagTbeaudesc(Dictionary<string,string> TagParams)
        {
            return cumulus.BeaufortDesc(station.highwindtoday);
        }

        private string TagTwindTM(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highwindtodaytime, "HH:mm", TagParams);
        }

        private string TagwgustTM(Dictionary<string,string> TagParams)
        {
            return station.highgusttoday.ToString(cumulus.WindFormat);
        }

        private string TagTwgustTM(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highgusttodaytime, "HH:mm", TagParams);
        }

        private string TagbearingTM(Dictionary<string,string> TagParams)
        {
            return station.highgustbearing.ToString();
        }

        private string TagrrateTM(Dictionary<string,string> TagParams)
        {
            return station.highraintoday.ToString(cumulus.RainFormat);
        }

        private string TagTrrateTM(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highraintodaytime, "HH:mm", TagParams);
        }

        private string TaghourlyrainTH(Dictionary<string,string> TagParams)
        {
            return station.highhourlyraintoday.ToString(cumulus.RainFormat);
        }

        private string TagThourlyrainTH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highhourlyraintodaytime, "HH:mm", TagParams);
        }

        private string TaghourlyrainYH(Dictionary<string,string> TagParams)
        {
            return station.highhourlyrainyesterday.ToString(cumulus.RainFormat);
        }

        private string TagThourlyrainYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highhourlyrainyesterdaytime, "HH:mm", TagParams);
        }

        private string TagSolarYH(Dictionary<string,string> TagParams)
        {
            return station.HighSolarYesterday.ToString("F0");
        }

        private string TagTsolarYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highsolaryesterdaytime, "HH:mm", TagParams);
        }

        private string TagUVYH(Dictionary<string,string> TagParams)
        {
            return station.HighUVYesterday.ToString(cumulus.UVFormat);
        }

        private string TagTUVYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highuvyesterdaytime, "HH:mm", TagParams);
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
            return station.RG11RainToday.ToString(cumulus.RainFormat);
        }

        private string TagRG11RainYest(Dictionary<string,string> TagParams)
        {
            return station.RG11RainYesterday.ToString(cumulus.RainFormat);
        }

        private string Tagcurrcond(Dictionary<string,string> TagParams)
        {
            return EncodeForWeb(GetCurrCondText());
        }

        private string Tagcurrcondenc(Dictionary<string,string> TagParams)
        {
            return EncodeForWeb(GetCurrCondText());
        }

        private string TagtempYH(Dictionary<string,string> TagParams)
        {
            return station.HighTempYesterday.ToString(cumulus.TempFormat);
        }

        private string TagTtempYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.hightempyesterdaytime, "HH:mm", TagParams);
        }

        private string TagtempYL(Dictionary<string,string> TagParams)
        {
            return station.LowTempYesterday.ToString(cumulus.TempFormat);
        }

        private string TagTtempYL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowtempyesterdaytime, "HH:mm", TagParams);
        }

        private string TagapptempYH(Dictionary<string,string> TagParams)
        {
            return station.HighAppTempYesterday.ToString(cumulus.TempFormat);
        }

        private string TagTapptempYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highapptempyesterdaytime, "HH:mm", TagParams);
        }

        private string TagapptempYL(Dictionary<string,string> TagParams)
        {
            return station.LowAppTempYesterday.ToString(cumulus.TempFormat);
        }

        private string TagTapptempYL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowapptempyesterdaytime, "HH:mm", TagParams);
        }

        private string TagdewpointYH(Dictionary<string,string> TagParams)
        {
            return station.HighDewpointYesterday.ToString(cumulus.TempFormat);
        }

        private string TagTdewpointYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.HighDewpointYesterdayTime, "HH:mm", TagParams);
        }

        private string TagdewpointYL(Dictionary<string,string> TagParams)
        {
            return station.LowDewpointYesterday.ToString(cumulus.TempFormat);
        }

        private string TagTdewpointYL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.LowDewpointYesterdayTime, "HH:mm", TagParams);
        }

        private string TagwchillYL(Dictionary<string,string> TagParams)
        {
            return station.lowwindchillyesterday.ToString(cumulus.TempFormat);
        }

        private string TagTwchillYL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowwindchillyesterdaytime, "HH:mm", TagParams);
        }

        private string TagpressYH(Dictionary<string,string> TagParams)
        {
            return station.highpressyesterday.ToString(cumulus.PressFormat);
        }

        private string TagTpressYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highpressyesterdaytime, "HH:mm", TagParams);
        }

        private string TagpressYL(Dictionary<string,string> TagParams)
        {
            return station.lowpressyesterday.ToString(cumulus.PressFormat);
        }

        private string TagTpressYL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowpressyesterdaytime, "HH:mm", TagParams);
        }

        private string TaghumYH(Dictionary<string,string> TagParams)
        {
            return station.highhumidityyesterday.ToString();
        }

        private string TagThumYH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highhumidityyesterdaytime, "HH:mm", TagParams);
        }

        private string TaghumYL(Dictionary<string,string> TagParams)
        {
            return station.lowhumidityyesterday.ToString();
        }

        private string TagThumYL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.lowhumidityyesterdaytime, "HH:mm", TagParams);
        }

        private string TagwindYM(Dictionary<string,string> TagParams)
        {
            return station.highwindyesterday.ToString(cumulus.WindFormat);
        }

        private string TagYbeaufort(Dictionary<string,string> TagParams)
        {
            return "F" + station.Beaufort(station.highwindyesterday);
        }

        private string TagYbeaufortnumber(Dictionary<string,string> TagParams)
        {
            return station.Beaufort(station.highwindyesterday).ToString();
        }

        private string TagYbeaudesc(Dictionary<string,string> TagParams)
        {
            return cumulus.BeaufortDesc(station.highwindyesterday);
        }

        private string TagTwindYM(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highwindyesterdaytime, "HH:mm", TagParams);
        }

        private string TagwgustYM(Dictionary<string,string> TagParams)
        {
            return station.highgustyesterday.ToString(cumulus.WindFormat);
        }

        private string TagTwgustYM(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highgustyesterdaytime, "HH:mm", TagParams);
        }

        private string TagbearingYM(Dictionary<string,string> TagParams)
        {
            return station.highgustbearingyesterday.ToString();
        }

        private string TagrrateYM(Dictionary<string,string> TagParams)
        {
            return station.highrainyesterday.ToString(cumulus.RainFormat);
        }

        private string TagTrrateYM(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.highrainyesterdaytime, "HH:mm", TagParams);
        }

        private string TagrfallY(Dictionary<string,string> TagParams)
        {
            return station.RainYesterday.ToString(cumulus.RainFormat);
        }

        // all time records
        private string TagtempH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_hightemp].value.ToString(cumulus.TempFormat);
        }

        private string TagTtempH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_hightemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagtempL(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_lowtemp].value.ToString(cumulus.TempFormat);
        }

        private string TagTtempL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_lowtemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagapptempH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highapptemp].value.ToString(cumulus.TempFormat);
        }

        private string TagTapptempH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highapptemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagapptempL(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_lowapptemp].value.ToString(cumulus.TempFormat);
        }

        private string TagTapptempL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_lowapptemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagdewpointH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highdewpoint].value.ToString(cumulus.TempFormat);
        }

        private string TagTdewpointH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highdewpoint].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagdewpointL(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_lowdewpoint].value.ToString(cumulus.TempFormat);
        }

        private string TagTdewpointL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_lowdewpoint].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagheatindexH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highheatindex].value.ToString(cumulus.TempFormat);
        }

        private string TagTheatindexH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highheatindex].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TaggustM(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highgust].value.ToString(cumulus.WindFormat);
        }

        private string TagTgustM(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highgust].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagwspeedH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highwind].value.ToString(cumulus.WindFormat);
        }

        private string TagTwspeedH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highwind].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagwchillH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_lowchill].value.ToString(cumulus.TempFormat);
        }

        private string TagTwchillH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_lowchill].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagrrateM(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highrainrate].value.ToString(cumulus.RainFormat);
        }

        private string TagTrrateM(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highrainrate].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagwindrunH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highwindrun].value.ToString(cumulus.WindRunFormat);
        }

        private string TagTwindrunH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highwindrun].timestamp, "o\\n dd MMMM yyyy", TagParams);
        }

        private string TagrfallH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_dailyrain].value.ToString(cumulus.RainFormat);
        }

        private string TagTrfallH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_dailyrain].timestamp, "o\\n dd MMMM yyyy", TagParams);
        }

        private string TagLongestDryPeriod(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_longestdryperiod].value.ToString("F0");
        }

        private string TagTLongestDryPeriod(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_longestdryperiod].timestamp, "\\to dd MMMM yyyy", TagParams);
        }

        private string TagLongestWetPeriod(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_longestwetperiod].value.ToString("F0");
        }

        private string TagTLongestWetPeriod(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_longestwetperiod].timestamp, "\\to dd MMMM yyyy", TagParams);
        }

        private string TagLowDailyTempRange(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_lowdailytemprange].value.ToString(cumulus.TempFormat);
        }

        private string TagHighDailyTempRange(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highdailytemprange].value.ToString(cumulus.TempFormat);
        }

        private string TagTLowDailyTempRange(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_lowdailytemprange].timestamp, "o\\n dd MMMM yyyy", TagParams);
        }

        private string TagTHighDailyTempRange(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highdailytemprange].timestamp, "o\\n dd MMMM yyyy", TagParams);
        }

        private string TagrfallhH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_hourlyrain].value.ToString(cumulus.RainFormat);
        }

        private string TagTrfallhH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_hourlyrain].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagrfallmH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_wetmonth].value.ToString(cumulus.RainFormat);
        }

        private string TagTrfallmH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_wetmonth].timestamp, "MMMM yyyy", TagParams);
        }

        private string TagpressH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highpress].value.ToString(cumulus.PressFormat);
        }

        private string TagTpressH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highpress].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagpressL(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_lowpress].value.ToString(cumulus.PressFormat);
        }

        private string TagTpressL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_lowpress].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TaghumH(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_highhumidity].value.ToString(cumulus.HumFormat);
        }

        private string TagThumH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highhumidity].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
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
            return station.alltimerecarray[WeatherStation.AT_highmintemp].value.ToString(cumulus.TempFormat);
        }

        private string TagTmintempH(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_highmintemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagmaxtempL(Dictionary<string,string> TagParams)
        {
            return station.alltimerecarray[WeatherStation.AT_lowmaxtemp].value.ToString(cumulus.TempFormat);
        }

        private string TagTmaxtempL(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.alltimerecarray[WeatherStation.AT_lowmaxtemp].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        // end of all-time records
        // month by month all time records
        private string TagByMonthTempH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_hightemp, month, cumulus.TempFormat);
        }
        
        private string TagByMonthTempHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_hightemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthTempL(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_lowtemp, month, cumulus.TempFormat);
        }

        private string TagByMonthTempLT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_lowtemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthAppTempH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highapptemp, month, cumulus.TempFormat);
        }

        private string TagByMonthAppTempHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highapptemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthAppTempL(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_lowapptemp, month, cumulus.TempFormat);
        }

        private string TagByMonthAppTempLT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_lowapptemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthDewPointH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highdewpoint, month, cumulus.TempFormat);
        }

        private string TagByMonthDewPointHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highdewpoint, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthDewPointL(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_lowdewpoint, month, cumulus.TempFormat);
        }

        private string TagByMonthDewPointLT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_lowdewpoint, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthHeatIndexH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highheatindex, month, cumulus.TempFormat);
        }

        private string TagByMonthHeatIndexHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highheatindex, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthGustH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highgust, month, cumulus.WindFormat);
        }

        private string TagByMonthGustHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highgust, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthWindH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highwind, month, cumulus.WindFormat);
        }

        private string TagByMonthWindHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highwind, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthWChillL(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_lowchill, month, cumulus.TempFormat);
        }

        private string TagByMonthWChillLT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_lowchill, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthRainRateH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highrainrate, month, cumulus.RainFormat);
        }

        private string TagByMonthRainRateHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highrainrate, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthWindRunH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highwindrun, month, cumulus.WindRunFormat);
        }

        private string TagByMonthWindRunHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highwindrun, month].timestamp, "o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthDailyRainH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_dailyrain, month, cumulus.RainFormat);
        }

        private string TagByMonthDailyRainHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_dailyrain, month].timestamp, "o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthLongestDryPeriod(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_longestdryperiod, month, "F0");
        }

        private string TagByMonthLongestDryPeriodT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_longestdryperiod, month].timestamp, "\\to dd MMMM yyyy", TagParams);
        }

        private string TagByMonthLongestWetPeriod(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_longestwetperiod, month, "F0");
        }

        private string TagByMonthLongestWetPeriodT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_longestwetperiod, month].timestamp, "\\to dd MMMM yyyy", TagParams);
        }

        private string TagByMonthLowDailyTempRange(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_lowdailytemprange, month, cumulus.TempFormat);
        }

        private string TagByMonthHighDailyTempRange(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highdailytemprange, month, cumulus.TempFormat);
        }

        private string TagByMonthLowDailyTempRangeT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_lowdailytemprange, month].timestamp, "o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthHighDailyTempRangeT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highdailytemprange, month].timestamp, "o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthHourlyRainH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_hourlyrain, month, cumulus.RainFormat);
        }

        private string TagByMonthHourlyRainHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_hourlyrain, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthMonthlyRainH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_wetmonth, month, cumulus.RainFormat);
        }

        private string TagByMonthMonthlyRainHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_wetmonth, month].timestamp, "MMMM yyyy", TagParams);
        }

        private string TagByMonthPressH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highpress, month, cumulus.PressFormat);
        }

        private string TagByMonthPressHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highpress, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthPressL(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_lowpress, month, cumulus.PressFormat);
        }

        private string TagByMonthPressLT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_lowpress, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthHumH(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highhumidity, month, cumulus.HumFormat);
        }

        private string TagByMonthHumHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highhumidity, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
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
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_highmintemp, month, cumulus.TempFormat);
        }

        private string TagByMonthMinTempHT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_highmintemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
        }

        private string TagByMonthMaxTempL(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetMonthlyAlltimeValueStr(WeatherStation.AT_lowmaxtemp, month, cumulus.TempFormat);
        }

        private string TagByMonthMaxTempLT(Dictionary<string,string> TagParams)
        {
            var month = GetMonthParam(TagParams);
            return GetFormattedDateTime(station.monthlyrecarray[WeatherStation.AT_lowmaxtemp, month].timestamp, "\\a\\t HH:mm o\\n dd MMMM yyyy", TagParams);
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
                    var rcstr = TagParams.Get("rc");
                    if (rcstr == null)
                    {
                        rcstr = "n";
                    }
                    var res = String.Format("{0:F"+dp+"}",cumulus.Latitude);
                    if (rcstr == "y")
                    {
                        res = ReplaceCommas(res);
                    }
                    return res;
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
                    var rcstr = TagParams.Get("rc");
                    if (rcstr == null)
                    {
                        rcstr = "n";
                    }
                    var res = String.Format("{0:F" + dp + "}", cumulus.Longitude);
                    if (rcstr == "y")
                    {
                        res = ReplaceCommas(res);
                    }
                    return res;
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
            return station.DataStopped ? "1" : "0";
        }

        private string Tagmoonrise(Dictionary<string,string> TagParams)
        {
            try
            {
                return GetFormattedDateTime(cumulus.MoonRiseTime, "HH:mm", TagParams);
            }
            catch (Exception)
            {

                return "-----";
            }
        }

        private string Tagmoonset(Dictionary<string,string> TagParams)
        {
            try
            {
                return GetFormattedDateTime(cumulus.MoonSetTime, "HH:mm", TagParams);
            }
            catch (Exception)
            {
                return "-----";
            }
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
            return ((int)cumulus.MoonPercent).ToString();
        }

        private string TagMoonPercentAbs(Dictionary<string,string> TagParams)
        {
            return ((int)Math.Abs(cumulus.MoonPercent)).ToString();
        }

        private string TagMoonAge(Dictionary<string,string> TagParams)
        {
            return ((int) cumulus.MoonAge).ToString();
        }

        private string TagLastRainTipISO(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.LastRainTip, TagParams);
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
            return station.ET.ToString(cumulus.ETFormat);
        }

        private string TagLight(Dictionary<string,string> TagParams)
        {
            return station.LightValue.ToString("F1");
        }

        private string TagUV(Dictionary<string,string> TagParams)
        {
            return station.UV.ToString(cumulus.UVFormat);
        }

        private string TagSolarRad(Dictionary<string,string> TagParams)
        {
            return ((int) station.SolarRad).ToString();
        }

        private string TagCurrentSolarMax(Dictionary<string,string> TagParams)
        {
            return ((int) station.CurrentSolarMax).ToString();
        }

        private string TagSunshineHours(Dictionary<string,string> TagParams)
        {
            return station.SunshineHours.ToString("F1");
        }

        private string TagTHWIndex(Dictionary<string,string> TagParams)
        {
            return station.THWIndex.ToString("F1");
        }

        private string TagTHSWIndex(Dictionary<string,string> TagParams)
        {
            return station.THSWIndex.ToString("F1");
        }

        private string TagChillHours(Dictionary<string,string> TagParams)
        {
            return station.ChillHours.ToString("F1");
        }

        private string TagYSunshineHours(Dictionary<string,string> TagParams)
        {
            return station.YestSunshineHours.ToString("F1");
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
            return station.ExtraTemp[1].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp2(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[2].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp3(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[3].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp4(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[4].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp5(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[5].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp6(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[6].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp7(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[7].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp8(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[8].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp9(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[9].ToString(cumulus.TempFormat);
        }

        private string TagExtraTemp10(Dictionary<string,string> TagParams)
        {
            return station.ExtraTemp[10].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP1(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[1].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP2(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[2].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP3(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[3].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP4(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[4].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP5(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[5].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP6(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[6].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP7(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[7].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP8(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[8].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP9(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[9].ToString(cumulus.TempFormat);
        }

        private string TagExtraDP10(Dictionary<string,string> TagParams)
        {
            return station.ExtraDewPoint[10].ToString(cumulus.TempFormat);
        }

        private string TagExtraHum1(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[1].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum2(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[2].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum3(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[3].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum4(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[4].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum5(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[5].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum6(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[6].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum7(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[7].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum8(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[8].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum9(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[9].ToString(cumulus.HumFormat);
        }

        private string TagExtraHum10(Dictionary<string,string> TagParams)
        {
            return station.ExtraHum[10].ToString(cumulus.HumFormat);
        }

        private string TagSoilTemp1(Dictionary<string,string> TagParams)
        {
            return station.SoilTemp1.ToString(cumulus.TempFormat);
        }

        private string TagSoilTemp2(Dictionary<string,string> TagParams)
        {
            return station.SoilTemp2.ToString(cumulus.TempFormat);
        }

        private string TagSoilTemp3(Dictionary<string,string> TagParams)
        {
            return station.SoilTemp3.ToString(cumulus.TempFormat);
        }

        private string TagSoilTemp4(Dictionary<string,string> TagParams)
        {
            return station.SoilTemp4.ToString(cumulus.TempFormat);
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

        private string TagLeafTemp1(Dictionary<string,string> TagParams)
        {
            return station.LeafTemp1.ToString(cumulus.TempFormat);
        }

        private string TagLeafTemp2(Dictionary<string,string> TagParams)
        {
            return station.LeafTemp2.ToString(cumulus.TempFormat);
        }

        private string TagLeafTemp3(Dictionary<string, string> TagParams)
        {
            return station.LeafTemp3.ToString(cumulus.TempFormat);
        }

        private string TagLeafTemp4(Dictionary<string, string> TagParams)
        {
            return station.LeafTemp4.ToString(cumulus.TempFormat);
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
        private string TagLowTempAlarm(Dictionary<string,string> TagParams)
        {
           return String.Empty;
        }

        private string TagHighTempAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagTempChangeUpAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagTempChangeDownAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagLowPressAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagHighPressAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagPressChangeUpAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagPressChangeDownAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagHighRainTodayAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagHighRainRateAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagHighWindGustAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        private string TagHighWindSpeedAlarm(Dictionary<string,string> TagParams)
        {
            return String.Empty;
        }

        // Monthly highs and lows - values
        private string TagMonthTempH(Dictionary<string,string> TagParams)
        {
            return station.HighTempThisMonth.ToString(cumulus.TempFormat);
        }

        private string TagMonthTempL(Dictionary<string,string> TagParams)
        {
            return station.LowTempThisMonth.ToString(cumulus.TempFormat);
        }

        private string TagMonthHeatIndexH(Dictionary<string,string> TagParams)
        {
            return station.HighHeatIndexThisMonth.ToString(cumulus.TempFormat);
        }

        private string TagMonthWChillL(Dictionary<string,string> TagParams)
        {
            return station.LowWindChillThisMonth.ToString(cumulus.TempFormat);
        }

        private string TagMonthAppTempH(Dictionary<string,string> TagParams)
        {
            return station.HighAppTempThisMonth.ToString(cumulus.TempFormat);
        }

        private string TagMonthAppTempL(Dictionary<string,string> TagParams)
        {
            return station.LowAppTempThisMonth.ToString(cumulus.TempFormat);
        }

        private string TagMonthDewPointH(Dictionary<string,string> TagParams)
        {
            return station.HighDewpointThisMonth.ToString(cumulus.TempFormat);
        }

        private string TagMonthDewPointL(Dictionary<string,string> TagParams)
        {
            return station.LowDewpointThisMonth.ToString(cumulus.TempFormat);
        }

        private string TagMonthMinTempH(Dictionary<string,string> TagParams)
        {
            if (station.HighMinTempThisMonth > -999)
            {
                return station.HighMinTempThisMonth.ToString(cumulus.TempFormat);
            }
            return "--";
        }

        private string TagMonthMaxTempL(Dictionary<string,string> TagParams)
        {
            if (station.LowMaxTempThisMonth < 999)
            {
                return station.LowMaxTempThisMonth.ToString(cumulus.TempFormat);
            }
            return "--";
        }

        private string TagMonthPressH(Dictionary<string,string> TagParams)
        {
            return station.HighPressThisMonth.ToString(cumulus.PressFormat);
        }

        private string TagMonthPressL(Dictionary<string,string> TagParams)
        {
            return station.LowPressThisMonth.ToString(cumulus.PressFormat);
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
            return station.HighGustThisMonth.ToString(cumulus.WindFormat);
        }

        private string TagMonthWindH(Dictionary<string,string> TagParams)
        {
            return station.HighWindThisMonth.ToString(cumulus.WindFormat);
        }

        private string TagMonthWindRunH(Dictionary<string,string> TagParams)
        {
            return station.HighDailyWindrunThisMonth.ToString(cumulus.WindRunFormat);
        }

        private string TagMonthRainRateH(Dictionary<string,string> TagParams)
        {
            return station.HighRainThisMonth.ToString(cumulus.RainFormat);
        }

        private string TagMonthHourlyRainH(Dictionary<string,string> TagParams)
        {
            return station.HighHourlyRainThisMonth.ToString(cumulus.RainFormat);
        }

        private string TagMonthDailyRainH(Dictionary<string,string> TagParams)
        {
            return station.HighDailyRainThisMonth.ToString(cumulus.RainFormat);
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
                return station.HighDailyTempRangeThisMonth.ToString(cumulus.TempFormat);
            }
            return "--";
        }

        private string TagMonthLowDailyTempRange(Dictionary<string,string> TagParams)
        {
            if (station.LowDailyTempRangeThisMonth < 999)
            {
                return station.LowDailyTempRangeThisMonth.ToString(cumulus.TempFormat);
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

        private string TagMonthDewPointHT(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.HighDewpointThisMonthTS, "t", TagParams);
        }

        private string TagMonthDewPointLT(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.LowDewpointThisMonthTS, "t", TagParams);
        }

        private string TagMonthMinTempHT(Dictionary<string,string> TagParams)
        {
            if (station.HighMinTempThisMonth > -999)
            {
                return GetFormattedDateTime(station.HighMinTempThisMonthTS, "t", TagParams);
            }
            return "---";
        }

        private string TagMonthMaxTempLT(Dictionary<string,string> TagParams)
        {
            if (station.LowMaxTempThisMonth < 999)
            {
                return GetFormattedDateTime(station.LowMaxTempThisMonthTS, "t", TagParams);
            }
            return "---";
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
            return station.HighTempThisYear.ToString(cumulus.TempFormat);
        }

        private string TagYearTempL(Dictionary<string,string> TagParams)
        {
            return station.LowTempThisYear.ToString(cumulus.TempFormat);
        }

        private string TagYearHeatIndexH(Dictionary<string,string> TagParams)
        {
            return station.HighHeatIndexThisYear.ToString(cumulus.TempFormat);
        }

        private string TagYearWChillL(Dictionary<string,string> TagParams)
        {
            return station.LowWindChillThisYear.ToString(cumulus.TempFormat);
        }

        private string TagYearAppTempH(Dictionary<string,string> TagParams)
        {
            return station.HighAppTempThisYear.ToString(cumulus.TempFormat);
        }

        private string TagYearAppTempL(Dictionary<string,string> TagParams)
        {
            return station.LowAppTempThisYear.ToString(cumulus.TempFormat);
        }

        private string TagYearDewPointH(Dictionary<string,string> TagParams)
        {
            return station.HighDewpointThisYear.ToString(cumulus.TempFormat);
        }

        private string TagYearDewPointL(Dictionary<string,string> TagParams)
        {
            return station.LowDewpointThisYear.ToString(cumulus.TempFormat);
        }

        private string TagYearMinTempH(Dictionary<string,string> TagParams)
        {
            if (station.HighMinTempThisYear > -999)
            {
                return station.HighMinTempThisYear.ToString(cumulus.TempFormat);
            }
            return "--";
        }

        private string TagYearMaxTempL(Dictionary<string,string> TagParams)
        {
            if (station.LowMaxTempThisYear < 999)
            {
                return station.LowMaxTempThisYear.ToString(cumulus.TempFormat);
            }
            return "--";
        }

        private string TagYearPressH(Dictionary<string,string> TagParams)
        {
            return station.HighPressThisYear.ToString(cumulus.PressFormat);
        }

        private string TagYearPressL(Dictionary<string,string> TagParams)
        {
            return station.LowPressThisYear.ToString(cumulus.PressFormat);
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
            return station.HighGustThisYear.ToString(cumulus.WindFormat);
        }

        private string TagYearWindH(Dictionary<string,string> TagParams)
        {
            return station.HighWindThisYear.ToString(cumulus.WindFormat);
        }

        private string TagYearWindRunH(Dictionary<string,string> TagParams)
        {
            return station.HighDailyWindrunThisYear.ToString(cumulus.WindRunFormat);
        }

        private string TagYearRainRateH(Dictionary<string,string> TagParams)
        {
            return station.HighRainThisYear.ToString(cumulus.RainFormat);
        }

        private string TagYearHourlyRainH(Dictionary<string,string> TagParams)
        {
            return station.HighHourlyRainThisYear.ToString(cumulus.RainFormat);
        }

        private string TagYearDailyRainH(Dictionary<string,string> TagParams)
        {
            return station.HighDailyRainThisYear.ToString(cumulus.RainFormat);
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
                return station.HighDailyTempRangeThisYear.ToString(cumulus.TempFormat);
            }
            return "--";
        }

        private string TagYearLowDailyTempRange(Dictionary<string,string> TagParams)
        {
            if (station.LowDailyTempRangeThisYear < 999)
            {
                return station.LowDailyTempRangeThisYear.ToString(cumulus.TempFormat);
            }
            return "--";
        }

        private string TagYearMonthlyRainH(Dictionary<string,string> TagParams)
        {
            return station.HighMonthlyRainThisYear.ToString(cumulus.RainFormat);
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

        private string TagYearDewPointHT(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.HighDewpointThisYearTS, "t", TagParams);
        }

        private string TagYearDewPointLT(Dictionary<string,string> TagParams)
        {
            return GetFormattedDateTime(station.LowDewpointThisYearTS, "t", TagParams);
        }

        private string TagYearMinTempHT(Dictionary<string,string> TagParams)
        {
            if (station.HighMinTempThisYear > -999)
            {
                return GetFormattedDateTime(station.HighMinTempThisYearTS, "t", TagParams);
            }
            return "---";
        }

        private string TagYearMaxTempLT(Dictionary<string,string> TagParams)
        {
            if (station.LowMaxTempThisYear < 999)
            {
                return GetFormattedDateTime(station.LowMaxTempThisYearTS, "t", TagParams);
            }
            return "---";
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
            if (station.HighDailyTempRangeThisYear < 999)
            {
                return GetFormattedDateTime(station.HighDailyTempRangeThisYearTS, "dd MMMM", TagParams);
            }
            return "------";
        }

        private string TagYearLowDailyTempRangeD(Dictionary<string,string> TagParams)
        {
            if (station.LowDailyTempRangeThisYear > -999)
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
            TimeSpan ts = DateTime.Now - Process.GetCurrentProcess().StartTime;
            return String.Format("{0} days {1} hours", ts.Days, ts.Hours);
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

        private string TagDavisTotalPacketsReceived(Dictionary<string,string> TagParams)
        {
            return station.DavisTotalPacketsReceived.ToString();
        }

        private string TagDavisTotalPacketsMissed(Dictionary<string,string> TagParams)
        {
            return station.DavisTotalPacketsMissed.ToString();
        }

        private string TagDavisNumberOfResynchs(Dictionary<string,string> TagParams)
        {
            return station.DavisNumberOfResynchs.ToString();
        }

        private string TagDavisMaxInARow(Dictionary<string,string> TagParams)
        {
            return station.DavisMaxInARow.ToString();
        }

        private string TagDavisNumCRCerrors(Dictionary<string,string> TagParams)
        {
            return station.DavisNumCRCerrors.ToString();
        }

        private string TagDavisFirmwareVersion(Dictionary<string,string> TagParams)
        {
            return station.DavisFirmwareVersion;
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
                return station.OutdoorTemperature.ToString(cumulus.TempFormat);
            }
            else
            {
                return result[0].OutsideTemp.ToString(cumulus.TempFormat);
            }
        }

        private string TagRecentWindSpeed(Dictionary<string,string> TagParams)
        {
            var recentTS = GetRecentTS(TagParams);

            var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

            if (result.Count == 0)
            {
                return station.WindAverage.ToString(cumulus.WindFormat);
            }
            else
            {
                return result[0].WindSpeed.ToString(cumulus.WindFormat);
            }
        }

        private string TagRecentWindGust(Dictionary<string,string> TagParams)
        {
            var recentTS = GetRecentTS(TagParams);

            var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

            if (result.Count == 0)
            {
                return station.RecentMaxGust.ToString(cumulus.WindFormat);
            }
            else
            {
                return result[0].WindGust.ToString(cumulus.WindFormat);
            }
        }

        private string TagRecentWindLatest(Dictionary<string,string> TagParams)
        {
            var recentTS = GetRecentTS(TagParams);

            var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

            if (result.Count == 0)
            {
                return station.WindLatest.ToString(cumulus.WindFormat);
            }
            else
            {
                return result[0].WindLatest.ToString(cumulus.WindFormat);
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
                return station.WindChill.ToString(cumulus.TempFormat);
            }
            else
            {
                return result[0].WindChill.ToString(cumulus.TempFormat);
            }
        }

        private string TagRecentDewPoint(Dictionary<string,string> TagParams)
        {
            var recentTS = GetRecentTS(TagParams);

            var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

            if (result.Count == 0)
            {
                return station.OutdoorDewpoint.ToString(cumulus.TempFormat);
            }
            else
            {
                return result[0].DewPoint.ToString(cumulus.TempFormat);
            }
        }

        private string TagRecentHeatIndex(Dictionary<string,string> TagParams)
        {
            var recentTS = GetRecentTS(TagParams);

            var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

            if (result.Count == 0)
            {
                return station.HeatIndex.ToString(cumulus.TempFormat);
            }
            else
            {
                return result[0].HeatIndex.ToString(cumulus.TempFormat);
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
                return station.Pressure.ToString(cumulus.PressFormat);
            }
            else
            {
                return result[0].Pressure.ToString(cumulus.PressFormat);
            }
        }

        private string TagRecentRainToday(Dictionary<string,string> TagParams)
        {
            var recentTS = GetRecentTS(TagParams);

            var result = station.RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", recentTS);

            if (result.Count == 0)
            {
                return station.RainToday.ToString(cumulus.RainFormat);
            }
            else
            {
                return result[0].RainToday.ToString(cumulus.RainFormat);
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
                return station.UV.ToString(cumulus.UVFormat);
            }
            else
            {
                return result[0].UV.ToString(cumulus.UVFormat);
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
            WebTagDictionary = new Dictionary<string, WebTagFunction>();

            WebTagDictionary.Add("time", TagTime);
            WebTagDictionary.Add("DaysSince30Dec1899", TagDaysSince30Dec1899);
            WebTagDictionary.Add("timeUTC", TagTimeUTC);
            WebTagDictionary.Add("timehhmmss", TagTimehhmmss);
            WebTagDictionary.Add("date", TagDate);
            WebTagDictionary.Add("yesterday", TagYesterday);
            WebTagDictionary.Add("metdate", TagMetDate);
            WebTagDictionary.Add("metdateyesterday", TagMetDateYesterday);
            WebTagDictionary.Add("day", TagDay);
            WebTagDictionary.Add("dayname", TagDayname);
            WebTagDictionary.Add("shortdayname", TagShortdayname);
            WebTagDictionary.Add("month", TagMonth);
            WebTagDictionary.Add("monthname", TagMonthname);
            WebTagDictionary.Add("shortmonthname", TagShortmonthname);
            WebTagDictionary.Add("year", TagYear);
            WebTagDictionary.Add("shortyear", TagShortyear);
            WebTagDictionary.Add("hour", TagHour);
            WebTagDictionary.Add("minute", TagMinute);
            WebTagDictionary.Add("forecast", Tagforecast);
            WebTagDictionary.Add("forecastnumber", Tagforecastnumber);
            WebTagDictionary.Add("cumulusforecast", Tagcumulusforecast);
            WebTagDictionary.Add("wsforecast", Tagwsforecast);
            WebTagDictionary.Add("forecastenc", Tagforecastenc);
            WebTagDictionary.Add("cumulusforecastenc", Tagcumulusforecastenc);
            WebTagDictionary.Add("wsforecastenc", Tagwsforecastenc);
            WebTagDictionary.Add("temp", Tagtemp);
            WebTagDictionary.Add("apptemp", Tagapptemp);
            WebTagDictionary.Add("temprange", Tagtemprange);
            WebTagDictionary.Add("temprangeY", TagtemprangeY);
            WebTagDictionary.Add("temptrend", Tagtemptrend);
            WebTagDictionary.Add("temptrendtext", Tagtemptrendtext);
            WebTagDictionary.Add("temptrendenglish", Tagtemptrendenglish);
            WebTagDictionary.Add("TempChangeLastHour", TagTempChangeLastHour);
            WebTagDictionary.Add("heatindex", Tagheatindex);
            WebTagDictionary.Add("avgtemp", Tagavgtemp);
            WebTagDictionary.Add("avgtempY", TagavgtempY);
            WebTagDictionary.Add("hum", Taghum);
            WebTagDictionary.Add("humidex", Taghumidex);
            WebTagDictionary.Add("press", Tagpress);
            WebTagDictionary.Add("altimeterpressure", Tagaltimeterpressure);
            WebTagDictionary.Add("presstrend", Tagpresstrend);
            WebTagDictionary.Add("presstrendenglish", Tagpresstrendenglish);
            WebTagDictionary.Add("cloudbase", Tagcloudbase);
            WebTagDictionary.Add("cloudbasevalue", Tagcloudbasevalue);
            WebTagDictionary.Add("cloudbaseunit", Tagcloudbaseunit);
            WebTagDictionary.Add("dew", Tagdew);
            WebTagDictionary.Add("wetbulb", Tagwetbulb);
            WebTagDictionary.Add("presstrendval", Tagpresstrendval);
            WebTagDictionary.Add("windrunY", TagwindrunY);
            WebTagDictionary.Add("domwindbearingY", TagdomwindbearingY);
            WebTagDictionary.Add("domwinddirY", TagdomwinddirY);
            WebTagDictionary.Add("tomorrowdaylength", Tagtomorrowdaylength);
            WebTagDictionary.Add("windrun", Tagwindrun);
            WebTagDictionary.Add("domwindbearing", Tagdomwindbearing);
            WebTagDictionary.Add("domwinddir", Tagdomwinddir);
            WebTagDictionary.Add("heatdegdays", Tagheatdegdays);
            WebTagDictionary.Add("heatdegdaysY", TagheatdegdaysY);
            WebTagDictionary.Add("cooldegdays", Tagcooldegdays);
            WebTagDictionary.Add("cooldegdaysY", TagcooldegdaysY);
            WebTagDictionary.Add("wlatest", Tagwlatest);
            WebTagDictionary.Add("wspeed", Tagwspeed);
            WebTagDictionary.Add("currentwdir", Tagcurrentwdir);
            WebTagDictionary.Add("wdir", Tagwdir);
            WebTagDictionary.Add("wgust", Tagwgust);
            WebTagDictionary.Add("wchill", Tagwchill);
            WebTagDictionary.Add("rrate", Tagrrate);
            WebTagDictionary.Add("StormRain", TagStormRain);
            WebTagDictionary.Add("StormRainStart", TagStormRainStart);
            WebTagDictionary.Add("bearing", Tagbearing);
            WebTagDictionary.Add("avgbearing", Tagavgbearing);
            WebTagDictionary.Add("BearingRangeFrom", TagBearingRangeFrom);
            WebTagDictionary.Add("BearingRangeTo", TagBearingRangeTo);
            WebTagDictionary.Add("BearingRangeFrom10", TagBearingRangeFrom10);
            WebTagDictionary.Add("BearingRangeTo10", TagBearingRangeTo10);
            WebTagDictionary.Add("beaufort", Tagbeaufort);
            WebTagDictionary.Add("beaufortnumber", Tagbeaufortnumber);
            WebTagDictionary.Add("beaudesc", Tagbeaudesc);
            WebTagDictionary.Add("Tbeaudesc", TagTbeaudesc);
            WebTagDictionary.Add("Ybeaudesc", TagYbeaudesc);
            WebTagDictionary.Add("wdirdata", Tagwdirdata);
            WebTagDictionary.Add("wspddata", Tagwspddata);
            WebTagDictionary.Add("WindSampleCount", TagWindSampleCount);
            WebTagDictionary.Add("WindRosePoints", TagWindRosePoints);
            WebTagDictionary.Add("WindRoseData", TagWindRoseData);
            WebTagDictionary.Add("nextwindindex", Tagnextwindindex);
            WebTagDictionary.Add("rfall", Tagrfall);
            WebTagDictionary.Add("ConsecutiveRainDays", TagConsecutiveRainDays);
            WebTagDictionary.Add("ConsecutiveDryDays", TagConsecutiveDryDays);
            WebTagDictionary.Add("rmidnight", Tagrmidnight);
            WebTagDictionary.Add("rmonth", Tagrmonth);
            WebTagDictionary.Add("rhour", Tagrhour);
            WebTagDictionary.Add("r24hour", Tagr24hour);
            WebTagDictionary.Add("ryear", Tagryear);
            WebTagDictionary.Add("inhum", Taginhum);
            WebTagDictionary.Add("intemp", Tagintemp);
            WebTagDictionary.Add("battery", Tagbattery);
            WebTagDictionary.Add("txbattery", Tagtxbattery);
            WebTagDictionary.Add("snowdepth", Tagsnowdepth);
            WebTagDictionary.Add("newrecord", Tagnewrecord);
            WebTagDictionary.Add("TempRecordSet", TagTempRecordSet);
            WebTagDictionary.Add("WindRecordSet", TagWindRecordSet);
            WebTagDictionary.Add("RainRecordSet", TagRainRecordSet);
            WebTagDictionary.Add("HumidityRecordSet", TagHumidityRecordSet);
            WebTagDictionary.Add("PressureRecordSet", TagPressureRecordSet);
            WebTagDictionary.Add("HighTempRecordSet", TagHighTempRecordSet);
            WebTagDictionary.Add("LowTempRecordSet", TagLowTempRecordSet);
            WebTagDictionary.Add("HighAppTempRecordSet", TagHighAppTempRecordSet);
            WebTagDictionary.Add("LowAppTempRecordSet", TagLowAppTempRecordSet);
            WebTagDictionary.Add("HighHeatIndexRecordSet", TagHighHeatIndexRecordSet);
            WebTagDictionary.Add("LowWindChillRecordSet", TagLowWindChillRecordSet);
            WebTagDictionary.Add("HighDewPointRecordSet", TagHighDewPointRecordSet);
            WebTagDictionary.Add("LowDewPointRecordSet", TagLowDewPointRecordSet);
            WebTagDictionary.Add("HighMinTempRecordSet", TagHighMinTempRecordSet);
            WebTagDictionary.Add("LowMaxTempRecordSet", TagLowMaxTempRecordSet);
            WebTagDictionary.Add("HighWindGustRecordSet", TagHighWindGustRecordSet);
            WebTagDictionary.Add("HighWindSpeedRecordSet", TagHighWindSpeedRecordSet);
            WebTagDictionary.Add("HighRainRateRecordSet", TagHighRainRateRecordSet);
            WebTagDictionary.Add("HighHourlyRainRecordSet", TagHighHourlyRainRecordSet);
            WebTagDictionary.Add("HighDailyRainRecordSet", TagHighDailyRainRecordSet);
            WebTagDictionary.Add("HighMonthlyRainRecordSet", TagHighMonthlyRainRecordSet);
            WebTagDictionary.Add("HighHumidityRecordSet", TagHighHumidityRecordSet);
            WebTagDictionary.Add("LowHumidityRecordSet", TagLowHumidityRecordSet);
            WebTagDictionary.Add("HighPressureRecordSet", TagHighPressureRecordSet);
            WebTagDictionary.Add("LowPressureRecordSet", TagLowPressureRecordSet);
            WebTagDictionary.Add("HighWindrunRecordSet", TagHighWindrunRecordSet);
            WebTagDictionary.Add("LongestDryPeriodRecordSet", TagLongestDryPeriodRecordSet);
            WebTagDictionary.Add("LongestWetPeriodRecordSet", TagLongestWetPeriodRecordSet);
            WebTagDictionary.Add("LowTempRangeRecordSet", TagLowTempRangeRecordSet);
            WebTagDictionary.Add("HighTempRangeRecordSet", TagHighTempRangeRecordSet);
            WebTagDictionary.Add("tempTH", TagtempTH);
            WebTagDictionary.Add("TtempTH", TagTtempTH);
            WebTagDictionary.Add("tempTL", TagtempTL);
            WebTagDictionary.Add("TtempTL", TagTtempTL);
            WebTagDictionary.Add("wchillTL", TagwchillTL);
            WebTagDictionary.Add("TwchillTL", TagTwchillTL);
            WebTagDictionary.Add("apptempTH", TagapptempTH);
            WebTagDictionary.Add("TapptempTH", TagTapptempTH);
            WebTagDictionary.Add("apptempTL", TagapptempTL);
            WebTagDictionary.Add("TapptempTL", TagTapptempTL);
            WebTagDictionary.Add("dewpointTH", TagdewpointTH);
            WebTagDictionary.Add("TdewpointTH", TagTdewpointTH);
            WebTagDictionary.Add("dewpointTL", TagdewpointTL);
            WebTagDictionary.Add("TdewpointTL", TagTdewpointTL);
            WebTagDictionary.Add("heatindexTH", TagheatindexTH);
            WebTagDictionary.Add("TheatindexTH", TagTheatindexTH);
            WebTagDictionary.Add("pressTH", TagpressTH);
            WebTagDictionary.Add("TpressTH", TagTpressTH);
            WebTagDictionary.Add("pressTL", TagpressTL);
            WebTagDictionary.Add("TpressTL", TagTpressTL);
            WebTagDictionary.Add("humTH", TaghumTH);
            WebTagDictionary.Add("ThumTH", TagThumTH);
            WebTagDictionary.Add("humTL", TaghumTL);
            WebTagDictionary.Add("ThumTL", TagThumTL);
            WebTagDictionary.Add("windTM", TagwindTM);
            WebTagDictionary.Add("Tbeaufort", TagTbeaufort);
            WebTagDictionary.Add("Tbeaufortnumber", TagTbeaufortnumber);
            WebTagDictionary.Add("TwindTM", TagTwindTM);
            WebTagDictionary.Add("wgustTM", TagwgustTM);
            WebTagDictionary.Add("TwgustTM", TagTwgustTM);
            WebTagDictionary.Add("bearingTM", TagbearingTM);
            WebTagDictionary.Add("rrateTM", TagrrateTM);
            WebTagDictionary.Add("TrrateTM", TagTrrateTM);
            WebTagDictionary.Add("hourlyrainTH", TaghourlyrainTH);
            WebTagDictionary.Add("ThourlyrainTH", TagThourlyrainTH);
            WebTagDictionary.Add("hourlyrainYH", TaghourlyrainYH);
            WebTagDictionary.Add("ThourlyrainYH", TagThourlyrainYH);
            WebTagDictionary.Add("solarTH", TagSolarTH);
            WebTagDictionary.Add("TsolarTH", TagTsolarTH);
            WebTagDictionary.Add("UVTH", TagUVTH);
            WebTagDictionary.Add("TUVTH", TagTUVTH);
            WebTagDictionary.Add("rollovertime", Tagrollovertime);
            WebTagDictionary.Add("currcond", Tagcurrcond);
            WebTagDictionary.Add("currcondenc", Tagcurrcondenc);
            WebTagDictionary.Add("tempYH", TagtempYH);
            WebTagDictionary.Add("TtempYH", TagTtempYH);
            WebTagDictionary.Add("tempYL", TagtempYL);
            WebTagDictionary.Add("TtempYL", TagTtempYL);
            WebTagDictionary.Add("wchillYL", TagwchillYL);
            WebTagDictionary.Add("TwchillYL", TagTwchillYL);
            WebTagDictionary.Add("apptempYH", TagapptempYH);
            WebTagDictionary.Add("TapptempYH", TagTapptempYH);
            WebTagDictionary.Add("apptempYL", TagapptempYL);
            WebTagDictionary.Add("TapptempYL", TagTapptempYL);
            WebTagDictionary.Add("dewpointYH", TagdewpointYH);
            WebTagDictionary.Add("TdewpointYH", TagTdewpointYH);
            WebTagDictionary.Add("dewpointYL", TagdewpointYL);
            WebTagDictionary.Add("TdewpointYL", TagTdewpointYL);
            WebTagDictionary.Add("heatindexYH", TagheatindexYH);
            WebTagDictionary.Add("TheatindexYH", TagTheatindexYH);
            WebTagDictionary.Add("pressYH", TagpressYH);
            WebTagDictionary.Add("TpressYH", TagTpressYH);
            WebTagDictionary.Add("pressYL", TagpressYL);
            WebTagDictionary.Add("TpressYL", TagTpressYL);
            WebTagDictionary.Add("humYH", TaghumYH);
            WebTagDictionary.Add("ThumYH", TagThumYH);
            WebTagDictionary.Add("humYL", TaghumYL);
            WebTagDictionary.Add("ThumYL", TagThumYL);
            WebTagDictionary.Add("windYM", TagwindYM);
            WebTagDictionary.Add("Ybeaufort", TagYbeaufort);
            WebTagDictionary.Add("Ybeaufortnumber", TagYbeaufortnumber);
            WebTagDictionary.Add("TwindYM", TagTwindYM);
            WebTagDictionary.Add("wgustYM", TagwgustYM);
            WebTagDictionary.Add("TwgustYM", TagTwgustYM);
            WebTagDictionary.Add("bearingYM", TagbearingYM);
            WebTagDictionary.Add("rrateYM", TagrrateYM);
            WebTagDictionary.Add("TrrateYM", TagTrrateYM);
            WebTagDictionary.Add("rfallY", TagrfallY);
            WebTagDictionary.Add("solarYH", TagSolarYH);
            WebTagDictionary.Add("TsolarYH", TagTsolarYH);
            WebTagDictionary.Add("UVYH", TagUVYH);
            WebTagDictionary.Add("TUVYH", TagTUVYH);
            WebTagDictionary.Add("tempH", TagtempH);
            WebTagDictionary.Add("TtempH", TagTtempH);
            WebTagDictionary.Add("tempL", TagtempL);
            WebTagDictionary.Add("TtempL", TagTtempL);
            WebTagDictionary.Add("apptempH", TagapptempH);
            WebTagDictionary.Add("TapptempH", TagTapptempH);
            WebTagDictionary.Add("apptempL", TagapptempL);
            WebTagDictionary.Add("TapptempL", TagTapptempL);
            WebTagDictionary.Add("dewpointH", TagdewpointH);
            WebTagDictionary.Add("TdewpointH", TagTdewpointH);
            WebTagDictionary.Add("dewpointL", TagdewpointL);
            WebTagDictionary.Add("TdewpointL", TagTdewpointL);
            WebTagDictionary.Add("heatindexH", TagheatindexH);
            WebTagDictionary.Add("TheatindexH", TagTheatindexH);
            WebTagDictionary.Add("gustM", TaggustM);
            WebTagDictionary.Add("TgustM", TagTgustM);
            WebTagDictionary.Add("wspeedH", TagwspeedH);
            WebTagDictionary.Add("TwspeedH", TagTwspeedH);
            WebTagDictionary.Add("windrunH", TagwindrunH);
            WebTagDictionary.Add("TwindrunH", TagTwindrunH);
            WebTagDictionary.Add("wchillH", TagwchillH);
            WebTagDictionary.Add("TwchillH", TagTwchillH);
            WebTagDictionary.Add("rrateM", TagrrateM);
            WebTagDictionary.Add("TrrateM", TagTrrateM);
            WebTagDictionary.Add("rfallH", TagrfallH);
            WebTagDictionary.Add("TrfallH", TagTrfallH);
            WebTagDictionary.Add("rfallhH", TagrfallhH);
            WebTagDictionary.Add("TrfallhH", TagTrfallhH);
            WebTagDictionary.Add("rfallmH", TagrfallmH);
            WebTagDictionary.Add("TrfallmH", TagTrfallmH);
            WebTagDictionary.Add("pressH", TagpressH);
            WebTagDictionary.Add("TpressH", TagTpressH);
            WebTagDictionary.Add("pressL", TagpressL);
            WebTagDictionary.Add("TpressL", TagTpressL);
            WebTagDictionary.Add("humH", TaghumH);
            WebTagDictionary.Add("ThumH", TagThumH);
            WebTagDictionary.Add("humL", TaghumL);
            WebTagDictionary.Add("ThumL", TagThumL);
            WebTagDictionary.Add("recordsbegandate", Tagrecordsbegandate);
            WebTagDictionary.Add("DaysSinceRecordsBegan", TagDaysSinceRecordsBegan);
            WebTagDictionary.Add("mintempH", TagmintempH);
            WebTagDictionary.Add("TmintempH", TagTmintempH);
            WebTagDictionary.Add("maxtempL", TagmaxtempL);
            WebTagDictionary.Add("TmaxtempL", TagTmaxtempL);
            WebTagDictionary.Add("LongestDryPeriod", TagLongestDryPeriod);
            WebTagDictionary.Add("TLongestDryPeriod", TagTLongestDryPeriod);
            WebTagDictionary.Add("LongestWetPeriod", TagLongestWetPeriod);
            WebTagDictionary.Add("TLongestWetPeriod", TagTLongestWetPeriod);
            WebTagDictionary.Add("LowDailyTempRange", TagLowDailyTempRange);
            WebTagDictionary.Add("TLowDailyTempRange", TagTLowDailyTempRange);
            WebTagDictionary.Add("HighDailyTempRange", TagHighDailyTempRange);
            WebTagDictionary.Add("THighDailyTempRange", TagTHighDailyTempRange);
            WebTagDictionary.Add("graphperiod", Taggraphperiod);
            WebTagDictionary.Add("stationtype", Tagstationtype);
            WebTagDictionary.Add("latitude", Taglatitude);
            WebTagDictionary.Add("longitude", Taglongitude);
            WebTagDictionary.Add("location", Taglocation);
            WebTagDictionary.Add("longlocation", Taglonglocation);
            WebTagDictionary.Add("sunrise", Tagsunrise);
            WebTagDictionary.Add("sunset", Tagsunset);
            WebTagDictionary.Add("daylength", Tagdaylength);
            WebTagDictionary.Add("dawn", Tagdawn);
            WebTagDictionary.Add("dusk", Tagdusk);
            WebTagDictionary.Add("daylightlength", Tagdaylightlength);
            WebTagDictionary.Add("isdaylight", Tagisdaylight);
            WebTagDictionary.Add("IsSunUp", TagIsSunUp);
            WebTagDictionary.Add("SensorContactLost", TagSensorContactLost);
            WebTagDictionary.Add("moonrise", Tagmoonrise);
            WebTagDictionary.Add("moonset", Tagmoonset);
            WebTagDictionary.Add("moonphase", Tagmoonphase);
            WebTagDictionary.Add("chillhours", TagChillHours);
            WebTagDictionary.Add("altitude", Tagaltitude);
            WebTagDictionary.Add("forum", Tagforum);
            WebTagDictionary.Add("webcam", Tagwebcam);
            WebTagDictionary.Add("tempunit", Tagtempunit);
            WebTagDictionary.Add("tempunitnodeg", Tagtempunitnodeg);
            WebTagDictionary.Add("windunit", Tagwindunit);
            WebTagDictionary.Add("windrununit", Tagwindrununit);
            WebTagDictionary.Add("pressunit", Tagpressunit);
            WebTagDictionary.Add("rainunit", Tagrainunit);
            WebTagDictionary.Add("interval", Taginterval);
            WebTagDictionary.Add("realtimeinterval", Tagrealtimeinterval);
            WebTagDictionary.Add("version", Tagversion);
            WebTagDictionary.Add("build", Tagbuild);
            WebTagDictionary.Add("update", Tagupdate);
            WebTagDictionary.Add("LastRainTipISO", TagLastRainTipISO);
            WebTagDictionary.Add("MinutesSinceLastRainTip", TagMinutesSinceLastRainTip);
            WebTagDictionary.Add("LastDataReadT", TagLastDataReadT);
            WebTagDictionary.Add("LatestNOAAMonthlyReport", TagLatestNOAAMonthlyReport);
            WebTagDictionary.Add("LatestNOAAYearlyReport", TagLatestNOAAYearlyReport);
            WebTagDictionary.Add("dailygraphperiod", Tagdailygraphperiod);
            WebTagDictionary.Add("RCtemp", TagRCtemp);
            WebTagDictionary.Add("RCtempTH", TagRCtempTH);
            WebTagDictionary.Add("RCtempTL", TagRCtempTL);
            WebTagDictionary.Add("RCintemp", TagRCintemp);
            WebTagDictionary.Add("RCdew", TagRCdew);
            WebTagDictionary.Add("RCheatindex", TagRCheatindex);
            WebTagDictionary.Add("RCwchill", TagRCwchill);
            WebTagDictionary.Add("RChum", TagRChum);
            WebTagDictionary.Add("RCinhum", TagRCinhum);
            WebTagDictionary.Add("RCrfall", TagRCrfall);
            WebTagDictionary.Add("RCrrate", TagRCrrate);
            WebTagDictionary.Add("RCrrateTM", TagRCrrateTM);
            WebTagDictionary.Add("RCwgust", TagRCwgust);
            WebTagDictionary.Add("RCwlatest", TagRCwlatest);
            WebTagDictionary.Add("RCwspeed", TagRCwspeed);
            WebTagDictionary.Add("RCwgustTM", TagRCwgustTM);
            WebTagDictionary.Add("RCpress", TagRCpress);
            WebTagDictionary.Add("RCpressTH", TagRCpressTH);
            WebTagDictionary.Add("RCpressTL", TagRCpressTL);
            WebTagDictionary.Add("RCdewpointTH", TagRCdewpointTH);
            WebTagDictionary.Add("RCdewpointTL", TagRCdewpointTL);
            WebTagDictionary.Add("RCwchillTL", TagRCwchillTL);
            WebTagDictionary.Add("RCheatindexTH", TagRCheatindexTH);
            WebTagDictionary.Add("RCapptempTH", TagRCapptempTH);
            WebTagDictionary.Add("RCapptempTL", TagRCapptempTL);
            WebTagDictionary.Add("ET", TagET);
            WebTagDictionary.Add("UV", TagUV);
            WebTagDictionary.Add("SolarRad", TagSolarRad);
            WebTagDictionary.Add("Light", TagLight);
            WebTagDictionary.Add("CurrentSolarMax", TagCurrentSolarMax);
            WebTagDictionary.Add("SunshineHours", TagSunshineHours);
            WebTagDictionary.Add("YSunshineHours", TagYSunshineHours);
            WebTagDictionary.Add("IsSunny", TagIsSunny);
            WebTagDictionary.Add("IsRaining", TagIsRaining);
            WebTagDictionary.Add("IsFreezing", TagIsFreezing);
            WebTagDictionary.Add("THWindex", TagTHWIndex);
            WebTagDictionary.Add("THSWindex", TagTHSWIndex);
            WebTagDictionary.Add("ExtraTemp1", TagExtraTemp1);
            WebTagDictionary.Add("ExtraTemp2", TagExtraTemp2);
            WebTagDictionary.Add("ExtraTemp3", TagExtraTemp3);
            WebTagDictionary.Add("ExtraTemp4", TagExtraTemp4);
            WebTagDictionary.Add("ExtraTemp5", TagExtraTemp5);
            WebTagDictionary.Add("ExtraTemp6", TagExtraTemp6);
            WebTagDictionary.Add("ExtraTemp7", TagExtraTemp7);
            WebTagDictionary.Add("ExtraTemp8", TagExtraTemp8);
            WebTagDictionary.Add("ExtraTemp9", TagExtraTemp9);
            WebTagDictionary.Add("ExtraTemp10", TagExtraTemp10);
            WebTagDictionary.Add("ExtraDP1", TagExtraDP1);
            WebTagDictionary.Add("ExtraDP2", TagExtraDP2);
            WebTagDictionary.Add("ExtraDP3", TagExtraDP3);
            WebTagDictionary.Add("ExtraDP4", TagExtraDP4);
            WebTagDictionary.Add("ExtraDP5", TagExtraDP5);
            WebTagDictionary.Add("ExtraDP6", TagExtraDP6);
            WebTagDictionary.Add("ExtraDP7", TagExtraDP7);
            WebTagDictionary.Add("ExtraDP8", TagExtraDP8);
            WebTagDictionary.Add("ExtraDP9", TagExtraDP3);
            WebTagDictionary.Add("ExtraDP10", TagExtraDP10);
            WebTagDictionary.Add("ExtraHum1", TagExtraHum1);
            WebTagDictionary.Add("ExtraHum2", TagExtraHum2);
            WebTagDictionary.Add("ExtraHum3", TagExtraHum3);
            WebTagDictionary.Add("ExtraHum4", TagExtraHum4);
            WebTagDictionary.Add("ExtraHum5", TagExtraHum5);
            WebTagDictionary.Add("ExtraHum6", TagExtraHum6);
            WebTagDictionary.Add("ExtraHum7", TagExtraHum7);
            WebTagDictionary.Add("ExtraHum8", TagExtraHum8);
            WebTagDictionary.Add("ExtraHum9", TagExtraHum9);
            WebTagDictionary.Add("ExtraHum10", TagExtraHum10);
            WebTagDictionary.Add("SoilTemp1", TagSoilTemp1);
            WebTagDictionary.Add("SoilTemp2", TagSoilTemp2);
            WebTagDictionary.Add("SoilTemp3", TagSoilTemp3);
            WebTagDictionary.Add("SoilTemp4", TagSoilTemp4);
            WebTagDictionary.Add("SoilMoisture1", TagSoilMoisture1);
            WebTagDictionary.Add("SoilMoisture2", TagSoilMoisture2);
            WebTagDictionary.Add("SoilMoisture3", TagSoilMoisture3);
            WebTagDictionary.Add("SoilMoisture4", TagSoilMoisture4);
            WebTagDictionary.Add("LeafTemp1", TagLeafTemp1);
            WebTagDictionary.Add("LeafTemp2", TagLeafTemp2);
            WebTagDictionary.Add("LeafTemp3", TagLeafTemp3);
            WebTagDictionary.Add("LeafTemp4", TagLeafTemp4);
            WebTagDictionary.Add("LeafWetness1", TagLeafWetness1);
            WebTagDictionary.Add("LeafWetness2", TagLeafWetness2);
            WebTagDictionary.Add("LeafWetness3", TagLeafWetness3);
            WebTagDictionary.Add("LeafWetness4", TagLeafWetness4);
            WebTagDictionary.Add("LowTempAlarm", TagLowTempAlarm);
            WebTagDictionary.Add("HighTempAlarm", TagHighTempAlarm);
            WebTagDictionary.Add("TempChangeUpAlarm", TagTempChangeUpAlarm);
            WebTagDictionary.Add("TempChangeDownAlarm", TagTempChangeDownAlarm);
            WebTagDictionary.Add("LowPressAlarm", TagLowPressAlarm);
            WebTagDictionary.Add("HighPressAlarm", TagHighPressAlarm);
            WebTagDictionary.Add("PressChangeUpAlarm", TagPressChangeUpAlarm);
            WebTagDictionary.Add("PressChangeDownAlarm", TagPressChangeDownAlarm);
            WebTagDictionary.Add("HighRainTodayAlarm", TagHighRainTodayAlarm);
            WebTagDictionary.Add("HighRainRateAlarm", TagHighRainRateAlarm);
            WebTagDictionary.Add("HighWindGustAlarm", TagHighWindGustAlarm);
            WebTagDictionary.Add("HighWindSpeedAlarm", TagHighWindSpeedAlarm);
            WebTagDictionary.Add("RG11RainToday", TagRG11RainToday);
            WebTagDictionary.Add("RG11RainYest", TagRG11RainYest);
            // Monthly highs and lows - values
            WebTagDictionary.Add("MonthTempH", TagMonthTempH);
            WebTagDictionary.Add("MonthTempL", TagMonthTempL);
            WebTagDictionary.Add("MonthHeatIndexH", TagMonthHeatIndexH);
            WebTagDictionary.Add("MonthWChillL", TagMonthWChillL);
            WebTagDictionary.Add("MonthAppTempH", TagMonthAppTempH);
            WebTagDictionary.Add("MonthAppTempL", TagMonthAppTempL);
            WebTagDictionary.Add("MonthMinTempH", TagMonthMinTempH);
            WebTagDictionary.Add("MonthMaxTempL", TagMonthMaxTempL);
            WebTagDictionary.Add("MonthPressH", TagMonthPressH);
            WebTagDictionary.Add("MonthPressL", TagMonthPressL);
            WebTagDictionary.Add("MonthHumH", TagMonthHumH);
            WebTagDictionary.Add("MonthHumL", TagMonthHumL);
            WebTagDictionary.Add("MonthGustH", TagMonthGustH);
            WebTagDictionary.Add("MonthWindH", TagMonthWindH);
            WebTagDictionary.Add("MonthRainRateH", TagMonthRainRateH);
            WebTagDictionary.Add("MonthHourlyRainH", TagMonthHourlyRainH);
            WebTagDictionary.Add("MonthDailyRainH", TagMonthDailyRainH);
            WebTagDictionary.Add("MonthDewPointH", TagMonthDewPointH);
            WebTagDictionary.Add("MonthDewPointL", TagMonthDewPointL);
            WebTagDictionary.Add("MonthWindRunH", TagMonthWindRunH);
            WebTagDictionary.Add("MonthLongestDryPeriod", TagMonthLongestDryPeriod);
            WebTagDictionary.Add("MonthLongestWetPeriod", TagMonthLongestWetPeriod);
            WebTagDictionary.Add("MonthHighDailyTempRange", TagMonthHighDailyTempRange);
            WebTagDictionary.Add("MonthLowDailyTempRange", TagMonthLowDailyTempRange);
            // This year"s highs and lows - times
            WebTagDictionary.Add("MonthTempHT", TagMonthTempHT);
            WebTagDictionary.Add("MonthTempLT", TagMonthTempLT);
            WebTagDictionary.Add("MonthHeatIndexHT", TagMonthHeatIndexHT);
            WebTagDictionary.Add("MonthWChillLT", TagMonthWChillLT);
            WebTagDictionary.Add("MonthAppTempHT", TagMonthAppTempHT);
            WebTagDictionary.Add("MonthAppTempLT", TagMonthAppTempLT);
            WebTagDictionary.Add("MonthPressHT", TagMonthPressHT);
            WebTagDictionary.Add("MonthPressLT", TagMonthPressLT);
            WebTagDictionary.Add("MonthHumHT", TagMonthHumHT);
            WebTagDictionary.Add("MonthHumLT", TagMonthHumLT);
            WebTagDictionary.Add("MonthGustHT", TagMonthGustHT);
            WebTagDictionary.Add("MonthWindHT", TagMonthWindHT);
            WebTagDictionary.Add("MonthRainRateHT", TagMonthRainRateHT);
            WebTagDictionary.Add("MonthHourlyRainHT", TagMonthHourlyRainHT);
            WebTagDictionary.Add("MonthDewPointHT", TagMonthDewPointHT);
            WebTagDictionary.Add("MonthDewPointLT", TagMonthDewPointLT);
            // This month"s highs and lows - dates
            WebTagDictionary.Add("MonthTempHD", TagMonthTempHD);
            WebTagDictionary.Add("MonthTempLD", TagMonthTempLD);
            WebTagDictionary.Add("MonthHeatIndexHD", TagMonthHeatIndexHD);
            WebTagDictionary.Add("MonthWChillLD", TagMonthWChillLD);
            WebTagDictionary.Add("MonthAppTempHD", TagMonthAppTempHD);
            WebTagDictionary.Add("MonthAppTempLD", TagMonthAppTempLD);
            WebTagDictionary.Add("MonthMinTempHD", TagMonthMinTempHD);
            WebTagDictionary.Add("MonthMaxTempLD", TagMonthMaxTempLD);
            WebTagDictionary.Add("MonthPressHD", TagMonthPressHD);
            WebTagDictionary.Add("MonthPressLD", TagMonthPressLD);
            WebTagDictionary.Add("MonthHumHD", TagMonthHumHD);
            WebTagDictionary.Add("MonthHumLD", TagMonthHumLD);
            WebTagDictionary.Add("MonthGustHD", TagMonthGustHD);
            WebTagDictionary.Add("MonthWindHD", TagMonthWindHD);
            WebTagDictionary.Add("MonthRainRateHD", TagMonthRainRateHD);
            WebTagDictionary.Add("MonthHourlyRainHD", TagMonthHourlyRainHD);
            WebTagDictionary.Add("MonthDailyRainHD", TagMonthDailyRainHD);
            WebTagDictionary.Add("MonthDewPointHD", TagMonthDewPointHD);
            WebTagDictionary.Add("MonthDewPointLD", TagMonthDewPointLD);
            WebTagDictionary.Add("MonthWindRunHD", TagMonthWindRunHD);
            WebTagDictionary.Add("MonthLongestDryPeriodD", TagMonthLongestDryPeriodD);
            WebTagDictionary.Add("MonthLongestWetPeriodD", TagMonthLongestWetPeriodD);
            WebTagDictionary.Add("MonthHighDailyTempRangeD", TagMonthHighDailyTempRangeD);
            WebTagDictionary.Add("MonthLowDailyTempRangeD", TagMonthLowDailyTempRangeD);
            // This Year"s highs and lows - values
            WebTagDictionary.Add("YearTempH", TagYearTempH);
            WebTagDictionary.Add("YearTempL", TagYearTempL);
            WebTagDictionary.Add("YearHeatIndexH", TagYearHeatIndexH);
            WebTagDictionary.Add("YearWChillL", TagYearWChillL);
            WebTagDictionary.Add("YearAppTempH", TagYearAppTempH);
            WebTagDictionary.Add("YearAppTempL", TagYearAppTempL);
            WebTagDictionary.Add("YearMinTempH", TagYearMinTempH);
            WebTagDictionary.Add("YearMaxTempL", TagYearMaxTempL);
            WebTagDictionary.Add("YearPressH", TagYearPressH);
            WebTagDictionary.Add("YearPressL", TagYearPressL);
            WebTagDictionary.Add("YearHumH", TagYearHumH);
            WebTagDictionary.Add("YearHumL", TagYearHumL);
            WebTagDictionary.Add("YearGustH", TagYearGustH);
            WebTagDictionary.Add("YearWindH", TagYearWindH);
            WebTagDictionary.Add("YearRainRateH", TagYearRainRateH);
            WebTagDictionary.Add("YearHourlyRainH", TagYearHourlyRainH);
            WebTagDictionary.Add("YearDailyRainH", TagYearDailyRainH);
            WebTagDictionary.Add("YearMonthlyRainH", TagYearMonthlyRainH);
            WebTagDictionary.Add("YearDewPointH", TagYearDewPointH);
            WebTagDictionary.Add("YearDewPointL", TagYearDewPointL);
            WebTagDictionary.Add("YearWindRunH", TagYearWindRunH);
            WebTagDictionary.Add("YearLongestDryPeriod", TagYearLongestDryPeriod);
            WebTagDictionary.Add("YearLongestWetPeriod", TagYearLongestWetPeriod);
            WebTagDictionary.Add("YearHighDailyTempRange", TagYearHighDailyTempRange);
            WebTagDictionary.Add("YearLowDailyTempRange", TagYearLowDailyTempRange);
            // This years"s highs and lows - times
            WebTagDictionary.Add("YearTempHT", TagYearTempHT);
            WebTagDictionary.Add("YearTempLT", TagYearTempLT);
            WebTagDictionary.Add("YearHeatIndexHT", TagYearHeatIndexHT);
            WebTagDictionary.Add("YearWChillLT", TagYearWChillLT);
            WebTagDictionary.Add("YearAppTempHT", TagYearAppTempHT);
            WebTagDictionary.Add("YearAppTempLT", TagYearAppTempLT);
            WebTagDictionary.Add("YearPressHT", TagYearPressHT);
            WebTagDictionary.Add("YearPressLT", TagYearPressLT);
            WebTagDictionary.Add("YearHumHT", TagYearHumHT);
            WebTagDictionary.Add("YearHumLT", TagYearHumLT);
            WebTagDictionary.Add("YearGustHT", TagYearGustHT);
            WebTagDictionary.Add("YearWindHT", TagYearWindHT);
            WebTagDictionary.Add("YearRainRateHT", TagYearRainRateHT);
            WebTagDictionary.Add("YearHourlyRainHT", TagYearHourlyRainHT);
            WebTagDictionary.Add("YearDewPointHT", TagYearDewPointHT);
            WebTagDictionary.Add("YearDewPointLT", TagYearDewPointLT);
            // Yearly highs and lows - dates
            WebTagDictionary.Add("YearTempHD", TagYearTempHD);
            WebTagDictionary.Add("YearTempLD", TagYearTempLD);
            WebTagDictionary.Add("YearHeatIndexHD", TagYearHeatIndexHD);
            WebTagDictionary.Add("YearWChillLD", TagYearWChillLD);
            WebTagDictionary.Add("YearAppTempHD", TagYearAppTempHD);
            WebTagDictionary.Add("YearAppTempLD", TagYearAppTempLD);
            WebTagDictionary.Add("YearMinTempHD", TagYearMinTempHD);
            WebTagDictionary.Add("YearMaxTempLD", TagYearMaxTempLD);
            WebTagDictionary.Add("YearPressHD", TagYearPressHD);
            WebTagDictionary.Add("YearPressLD", TagYearPressLD);
            WebTagDictionary.Add("YearHumHD", TagYearHumHD);
            WebTagDictionary.Add("YearHumLD", TagYearHumLD);
            WebTagDictionary.Add("YearGustHD", TagYearGustHD);
            WebTagDictionary.Add("YearWindHD", TagYearWindHD);
            WebTagDictionary.Add("YearRainRateHD", TagYearRainRateHD);
            WebTagDictionary.Add("YearHourlyRainHD", TagYearHourlyRainHD);
            WebTagDictionary.Add("YearDailyRainHD", TagYearDailyRainHD);
            WebTagDictionary.Add("YearMonthlyRainHD", TagYearMonthlyRainHD);
            WebTagDictionary.Add("YearDewPointHD", TagYearDewPointHD);
            WebTagDictionary.Add("YearDewPointLD", TagYearDewPointLD);
            WebTagDictionary.Add("YearWindRunHD", TagYearWindRunHD);
            WebTagDictionary.Add("YearLongestDryPeriodD", TagYearLongestDryPeriodD);
            WebTagDictionary.Add("YearLongestWetPeriodD", TagYearLongestWetPeriodD);
            WebTagDictionary.Add("YearHighDailyTempRangeD", TagYearHighDailyTempRangeD);
            WebTagDictionary.Add("YearLowDailyTempRangeD", TagYearLowDailyTempRangeD);
            // misc
            WebTagDictionary.Add("LatestError", TagLatestError);
            WebTagDictionary.Add("LatestErrorDate", TagLatestErrorDate);
            WebTagDictionary.Add("LatestErrorTime", TagLatestErrorTime);
            WebTagDictionary.Add("ErrorLight", Tagerrorlight);
            WebTagDictionary.Add("MoonPercent", TagMoonPercent);
            WebTagDictionary.Add("MoonPercentAbs", TagMoonPercentAbs);
            WebTagDictionary.Add("MoonAge", TagMoonAge);
            WebTagDictionary.Add("OsVersion", TagOsVersion);
            WebTagDictionary.Add("OsLanguage", TagOsLanguage);
            WebTagDictionary.Add("SystemUpTime", TagSystemUpTime);
            WebTagDictionary.Add("ProgramUpTime", TagProgramUpTime);
            WebTagDictionary.Add("CpuName", TagCpuName);
            WebTagDictionary.Add("CpuCount", TagCpuCount);
            WebTagDictionary.Add("MemoryStatus", TagMemoryStatus);
            WebTagDictionary.Add("DisplayMode", TagDisplayModeString);
            WebTagDictionary.Add("AllocatedMemory", TagAllocatedMemory);
            WebTagDictionary.Add("DiskSize", TagDiskSize);
            WebTagDictionary.Add("DiskFree", TagDiskFree);
            WebTagDictionary.Add("DavisTotalPacketsReceived", TagDavisTotalPacketsReceived);
            WebTagDictionary.Add("DavisTotalPacketsMissed", TagDavisTotalPacketsMissed);
            WebTagDictionary.Add("DavisNumberOfResynchs", TagDavisNumberOfResynchs);
            WebTagDictionary.Add("DavisMaxInARow", TagDavisMaxInARow);
            WebTagDictionary.Add("DavisNumCRCerrors", TagDavisNumCRCerrors);
            WebTagDictionary.Add("DavisFirmwareVersion", TagDavisFirmwareVersion);
            WebTagDictionary.Add("DataStopped", TagDataStopped);
            // Recent history
            WebTagDictionary.Add("RecentOutsideTemp", TagRecentOutsideTemp);
            WebTagDictionary.Add("RecentWindSpeed", TagRecentWindSpeed);
            WebTagDictionary.Add("RecentWindGust", TagRecentWindGust);
            WebTagDictionary.Add("RecentWindLatest", TagRecentWindLatest);
            WebTagDictionary.Add("RecentWindDir", TagRecentWindDir);
            WebTagDictionary.Add("RecentWindAvgDir", TagRecentWindAvgDir);
            WebTagDictionary.Add("RecentWindChill", TagRecentWindChill);
            WebTagDictionary.Add("RecentDewPoint", TagRecentDewPoint);
            WebTagDictionary.Add("RecentHeatIndex", TagRecentHeatIndex);
            WebTagDictionary.Add("RecentHumidity", TagRecentHumidity);
            WebTagDictionary.Add("RecentPressure", TagRecentPressure);
            WebTagDictionary.Add("RecentRainToday", TagRecentRainToday);
            WebTagDictionary.Add("RecentSolarRad", TagRecentSolarRad);
            WebTagDictionary.Add("RecentUV", TagRecentUV);
            WebTagDictionary.Add("RecentTS", TagRecentTS);
            // Recent history with comma decimals replaced
            WebTagDictionary.Add("RCRecentOutsideTemp", TagRCRecentOutsideTemp);
            WebTagDictionary.Add("RCRecentWindSpeed", TagRCRecentWindSpeed);
            WebTagDictionary.Add("RCRecentWindGust", TagRCRecentWindGust);
            WebTagDictionary.Add("RCRecentWindLatest", TagRCRecentWindLatest);
            WebTagDictionary.Add("RCRecentWindChill", TagRCRecentWindChill);
            WebTagDictionary.Add("RCRecentDewPoint", TagRCRecentDewPoint);
            WebTagDictionary.Add("RCRecentHeatIndex", TagRCRecentHeatIndex);
            WebTagDictionary.Add("RCRecentPressure", TagRCRecentPressure);
            WebTagDictionary.Add("RCRecentRainToday", TagRCRecentRainToday);
            WebTagDictionary.Add("RCRecentUV", TagRCRecentUV);
            // Month-by-month highs and lows - values
            WebTagDictionary.Add("ByMonthTempH", TagByMonthTempH);
            WebTagDictionary.Add("ByMonthTempL", TagByMonthTempL);
            WebTagDictionary.Add("ByMonthAppTempH", TagByMonthAppTempH);
            WebTagDictionary.Add("ByMonthAppTempL", TagByMonthAppTempL);
            WebTagDictionary.Add("ByMonthDewPointH", TagByMonthDewPointH);
            WebTagDictionary.Add("ByMonthDewPointL", TagByMonthDewPointL);
            WebTagDictionary.Add("ByMonthHeatIndexH", TagByMonthHeatIndexH);
            WebTagDictionary.Add("ByMonthGustH", TagByMonthGustH);
            WebTagDictionary.Add("ByMonthWindH", TagByMonthWindH);
            WebTagDictionary.Add("ByMonthWindRunH", TagByMonthWindRunH);
            WebTagDictionary.Add("ByMonthWChillL", TagByMonthWChillL);
            WebTagDictionary.Add("ByMonthRainRateH", TagByMonthRainRateH);
            WebTagDictionary.Add("ByMonthDailyRainH", TagByMonthDailyRainH);
            WebTagDictionary.Add("ByMonthHourlyRainH", TagByMonthHourlyRainH);
            WebTagDictionary.Add("ByMonthMonthlyRainH", TagByMonthMonthlyRainH);
            WebTagDictionary.Add("ByMonthPressH", TagByMonthPressH);
            WebTagDictionary.Add("ByMonthPressL", TagByMonthPressL);
            WebTagDictionary.Add("ByMonthHumH", TagByMonthHumH);
            WebTagDictionary.Add("ByMonthHumL", TagByMonthHumL);
            WebTagDictionary.Add("ByMonthMinTempH", TagByMonthMinTempH);
            WebTagDictionary.Add("ByMonthMaxTempL", TagByMonthMaxTempL);
            WebTagDictionary.Add("ByMonthLongestDryPeriod", TagByMonthLongestDryPeriod);
            WebTagDictionary.Add("ByMonthLongestWetPeriod", TagByMonthLongestWetPeriod);
            WebTagDictionary.Add("ByMonthLowDailyTempRange", TagByMonthLowDailyTempRange);
            WebTagDictionary.Add("ByMonthHighDailyTempRange", TagByMonthHighDailyTempRange);
            // Month-by-month highs and lows - timestamps
            WebTagDictionary.Add("ByMonthTempHT", TagByMonthTempHT);
            WebTagDictionary.Add("ByMonthTempLT", TagByMonthTempLT);
            WebTagDictionary.Add("ByMonthAppTempHT", TagByMonthAppTempHT);
            WebTagDictionary.Add("ByMonthAppTempLT", TagByMonthAppTempLT);
            WebTagDictionary.Add("ByMonthDewPointHT", TagByMonthDewPointHT);
            WebTagDictionary.Add("ByMonthDewPointLT", TagByMonthDewPointLT);
            WebTagDictionary.Add("ByMonthHeatIndexHT", TagByMonthHeatIndexHT);
            WebTagDictionary.Add("ByMonthGustHT", TagByMonthGustHT);
            WebTagDictionary.Add("ByMonthWindHT", TagByMonthWindHT);
            WebTagDictionary.Add("ByMonthWindRunHT", TagByMonthWindRunHT);
            WebTagDictionary.Add("ByMonthWChillLT", TagByMonthWChillLT);
            WebTagDictionary.Add("ByMonthRainRateHT", TagByMonthRainRateHT);
            WebTagDictionary.Add("ByMonthDailyRainHT", TagByMonthDailyRainHT);
            WebTagDictionary.Add("ByMonthHourlyRainHT", TagByMonthHourlyRainHT);
            WebTagDictionary.Add("ByMonthMonthlyRainHT", TagByMonthMonthlyRainHT);
            WebTagDictionary.Add("ByMonthPressHT", TagByMonthPressHT);
            WebTagDictionary.Add("ByMonthPressLT", TagByMonthPressLT);
            WebTagDictionary.Add("ByMonthHumHT", TagByMonthHumHT);
            WebTagDictionary.Add("ByMonthHumLT", TagByMonthHumLT);
            WebTagDictionary.Add("ByMonthMinTempHT", TagByMonthMinTempHT);
            WebTagDictionary.Add("ByMonthMaxTempLT", TagByMonthMaxTempLT);
            WebTagDictionary.Add("ByMonthLongestDryPeriodT", TagByMonthLongestDryPeriodT);
            WebTagDictionary.Add("ByMonthLongestWetPeriodT", TagByMonthLongestWetPeriodT);
            WebTagDictionary.Add("ByMonthLowDailyTempRangeT", TagByMonthLowDailyTempRangeT);
            WebTagDictionary.Add("ByMonthHighDailyTempRangeT", TagByMonthHighDailyTempRangeT);

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

        private static string Utf16ToUtf8(string utf16String)
        {
            /**************************************************************
             * Every .NET string will store text with the UTF16 encoding, *
             * known as Encoding.Unicode. Other encodings may exist as    *
             * Byte-Array or incorrectly stored with the UTF16 encoding.  *
             *                                                            *
             * UTF8 = 1 bytes per char                                    *
             *    ["100" for the ansi 'd']                                *
             *    ["206" and "186" for the russian 'κ']                   *
             *                                                            *
             * UTF16 = 2 bytes per char                                   *
             *    ["100, 0" for the ansi 'd']                             *
             *    ["186, 3" for the russian 'κ']                          *
             *                                                            *
             * UTF8 inside UTF16                                          *
             *    ["100, 0" for the ansi 'd']                             *
             *    ["206, 0" and "186, 0" for the russian 'κ']             *
             *                                                            *
             * We can use the convert encoding function to convert an     *
             * UTF16 Byte-Array to an UTF8 Byte-Array. When we use UTF8   *
             * encoding to string method now, we will get a UTF16 string. *
             *                                                            *
             * So we imitate UTF16 by filling the second byte of a char   *
             * with a 0 byte (binary 0) while creating the string.        *
             **************************************************************/

            // Storage for the UTF8 string
            string utf8String = String.Empty;

            // Get UTF16 bytes and convert UTF16 bytes to UTF8 bytes
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(utf16String);
            byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);

            // Fill UTF8 bytes inside UTF8 string
            for (int i = 0; i < utf8Bytes.Length; i++)
            {
                // Because char always saves 2 bytes, fill char with 0
                byte[] utf8Container = new byte[2] { utf8Bytes[i], 0 };
                utf8String += BitConverter.ToChar(utf8Container, 0);
            }

            // Return UTF8
            return utf8String;
        }
    } // end WebTags class

    //Jan 2010 - www.haiders.net
    //An Extension Method to Get a Dictionary Item by Key
    //If the Key does not exist, null is returned for reference types
    //For Value types, default value is returned (default(TValue))
    public static class DictionaryExtension
    {
        public static tvalue Get<tkey, tvalue>(this Dictionary<tkey, tvalue> dictionary, tkey key)
        {
            tvalue value = default(tvalue);
            dictionary.TryGetValue(key, out value);
            return value;
        }
    }
}