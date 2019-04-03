using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace CumulusMX.Common
{
    public delegate string WebTagFunction(Dictionary<string, string> tagParams);


    //    public void InitialiseWebtags()
    //    {
    //        // create the web tag dictionary
    //        WebTagDictionary = new Dictionary<string, WebTagFunction>();

    //        WebTagDictionary.Add("time", TagTime);
    //        WebTagDictionary.Add("DaysSince30Dec1899", TagDaysSince30Dec1899); //TODO
    //        WebTagDictionary.Add("timeUTC", TagTimeUTC);
    //        WebTagDictionary.Add("timehhmmss", TagTimehhmmss);
    //        WebTagDictionary.Add("date", TagDate);
    //        WebTagDictionary.Add("yesterday", TagYesterday);
    //        WebTagDictionary.Add("metdate", TagMetDate);
    //        WebTagDictionary.Add("metdateyesterday", TagMetDateYesterday);
    //        WebTagDictionary.Add("day", TagDay);
    //        WebTagDictionary.Add("dayname", TagDayname);
    //        WebTagDictionary.Add("shortdayname", TagShortdayname);
    //        WebTagDictionary.Add("month", TagMonth);
    //        WebTagDictionary.Add("monthname", TagMonthname);
    //        WebTagDictionary.Add("shortmonthname", TagShortmonthname);
    //        WebTagDictionary.Add("year", TagYear);
    //        WebTagDictionary.Add("shortyear", TagShortyear);
    //        WebTagDictionary.Add("hour", TagHour);
    //        WebTagDictionary.Add("minute", TagMinute);
    //        WebTagDictionary.Add("forecast", Tagforecast); //TODO
    //        WebTagDictionary.Add("forecastnumber", Tagforecastnumber); //TODO
    //        WebTagDictionary.Add("cumulusforecast", Tagcumulusforecast); //TODO
    //        WebTagDictionary.Add("wsforecast", Tagwsforecast); //TODO
    //        WebTagDictionary.Add("forecastenc", Tagforecastenc); //TODO
    //        WebTagDictionary.Add("cumulusforecastenc", Tagcumulusforecastenc); //TODO
    //        WebTagDictionary.Add("wsforecastenc", Tagwsforecastenc); //TODO
    //        WebTagDictionary.Add("temp", Tagtemp);
    //        WebTagDictionary.Add("apptemp", Tagapptemp);
    //        WebTagDictionary.Add("temprange", Tagtemprange);
    //        WebTagDictionary.Add("temprangeY", TagtemprangeY);
    //        WebTagDictionary.Add("temptrend", Tagtemptrend); //TODO
    //        WebTagDictionary.Add("temptrendtext", Tagtemptrendtext); //TODO
    //        WebTagDictionary.Add("temptrendenglish", Tagtemptrendenglish); //TODO
    //        WebTagDictionary.Add("TempChangeLastHour", TagTempChangeLastHour);
    //        WebTagDictionary.Add("heatindex", Tagheatindex);
    //        WebTagDictionary.Add("avgtemp", Tagavgtemp);
    //        WebTagDictionary.Add("avgtempY", TagavgtempY);
    //        WebTagDictionary.Add("hum", Taghum);
    //        WebTagDictionary.Add("humidex", Taghumidex);
    //        WebTagDictionary.Add("press", Tagpress);
    //        WebTagDictionary.Add("altimeterpressure", Tagaltimeterpressure);
    //        WebTagDictionary.Add("presstrend", Tagpresstrend);
    //        WebTagDictionary.Add("presstrendenglish", Tagpresstrendenglish);
    //        WebTagDictionary.Add("cloudbase", Tagcloudbase); //TODO
    //        WebTagDictionary.Add("cloudbasevalue", Tagcloudbasevalue); //TODO
    //        WebTagDictionary.Add("cloudbaseunit", Tagcloudbaseunit); //TODO
    //        WebTagDictionary.Add("dew", Tagdew);
    //        WebTagDictionary.Add("wetbulb", Tagwetbulb);
    //        WebTagDictionary.Add("presstrendval", Tagpresstrendval);
    //        WebTagDictionary.Add("windrunY", TagwindrunY);
    //        WebTagDictionary.Add("domwindbearingY", TagdomwindbearingY); //TODO
    //        WebTagDictionary.Add("domwinddirY", TagdomwinddirY); //TODO
    //        WebTagDictionary.Add("tomorrowdaylength", Tagtomorrowdaylength); //TODO
    //        WebTagDictionary.Add("windrun", Tagwindrun);
    //        WebTagDictionary.Add("domwindbearing", Tagdomwindbearing); //TODO
    //        WebTagDictionary.Add("domwinddir", Tagdomwinddir);
    //        WebTagDictionary.Add("heatdegdays", Tagheatdegdays);
    //        WebTagDictionary.Add("heatdegdaysY", TagheatdegdaysY);
    //        WebTagDictionary.Add("cooldegdays", Tagcooldegdays);
    //        WebTagDictionary.Add("cooldegdaysY", TagcooldegdaysY);
    //        WebTagDictionary.Add("wlatest", Tagwlatest);
    //        WebTagDictionary.Add("wspeed", Tagwspeed);
    //        WebTagDictionary.Add("currentwdir", Tagcurrentwdir);
    //        WebTagDictionary.Add("wdir", Tagwdir);
    //        WebTagDictionary.Add("wgust", Tagwgust);
    //        WebTagDictionary.Add("wchill", Tagwchill);
    //        WebTagDictionary.Add("rrate", Tagrrate);
    //        WebTagDictionary.Add("StormRain", TagStormRain); //TODO
    //        WebTagDictionary.Add("StormRainStart", TagStormRainStart); //TODO
    //        WebTagDictionary.Add("bearing", Tagbearing);
    //        WebTagDictionary.Add("avgbearing", Tagavgbearing);
    //        WebTagDictionary.Add("BearingRangeFrom", TagBearingRangeFrom);
    //        WebTagDictionary.Add("BearingRangeTo", TagBearingRangeTo);
    //        WebTagDictionary.Add("BearingRangeFrom10", TagBearingRangeFrom10);
    //        WebTagDictionary.Add("BearingRangeTo10", TagBearingRangeTo10);
    //        WebTagDictionary.Add("beaufort", Tagbeaufort); //TODO
    //        WebTagDictionary.Add("beaufortnumber", Tagbeaufortnumber); //TODO
    //        WebTagDictionary.Add("beaudesc", Tagbeaudesc); //TODO
    //        WebTagDictionary.Add("Tbeaudesc", TagTbeaudesc);
    //        WebTagDictionary.Add("Ybeaudesc", TagYbeaudesc);
    //        WebTagDictionary.Add("wdirdata", Tagwdirdata);
    //        WebTagDictionary.Add("wspddata", Tagwspddata);
    //        WebTagDictionary.Add("WindSampleCount", TagWindSampleCount);
    //        WebTagDictionary.Add("WindRosePoints", TagWindRosePoints);
    //        WebTagDictionary.Add("WindRoseData", TagWindRoseData);
    //        WebTagDictionary.Add("nextwindindex", Tagnextwindindex);
    //        WebTagDictionary.Add("rfall", Tagrfall);
    //        WebTagDictionary.Add("ConsecutiveRainDays", TagConsecutiveRainDays);
    //        WebTagDictionary.Add("ConsecutiveDryDays", TagConsecutiveDryDays);
    //        WebTagDictionary.Add("rmidnight", Tagrmidnight);
    //        WebTagDictionary.Add("rmonth", Tagrmonth);
    //        WebTagDictionary.Add("rhour", Tagrhour);
    //        WebTagDictionary.Add("r24hour", Tagr24hour);
    //        WebTagDictionary.Add("ryear", Tagryear);
    //        WebTagDictionary.Add("inhum", Taginhum);
    //        WebTagDictionary.Add("intemp", Tagintemp);
    //        WebTagDictionary.Add("battery", Tagbattery); //TODO
    //        WebTagDictionary.Add("txbattery", Tagtxbattery); //TODO
    //        WebTagDictionary.Add("snowdepth", Tagsnowdepth); //TODO
    //        WebTagDictionary.Add("newrecord", Tagnewrecord);
    //        WebTagDictionary.Add("TempRecordSet", TagTempRecordSet);
    //        WebTagDictionary.Add("WindRecordSet", TagWindRecordSet);
    //        WebTagDictionary.Add("RainRecordSet", TagRainRecordSet);
    //        WebTagDictionary.Add("HumidityRecordSet", TagHumidityRecordSet);
    //        WebTagDictionary.Add("PressureRecordSet", TagPressureRecordSet);
    //        WebTagDictionary.Add("HighTempRecordSet", TagHighTempRecordSet);
    //        WebTagDictionary.Add("LowTempRecordSet", TagLowTempRecordSet);
    //        WebTagDictionary.Add("HighAppTempRecordSet", TagHighAppTempRecordSet);
    //        WebTagDictionary.Add("LowAppTempRecordSet", TagLowAppTempRecordSet);
    //        WebTagDictionary.Add("HighHeatIndexRecordSet", TagHighHeatIndexRecordSet);
    //        WebTagDictionary.Add("LowWindChillRecordSet", TagLowWindChillRecordSet);
    //        WebTagDictionary.Add("HighDewPointRecordSet", TagHighDewPointRecordSet);
    //        WebTagDictionary.Add("LowDewPointRecordSet", TagLowDewPointRecordSet);
    //        WebTagDictionary.Add("HighMinTempRecordSet", TagHighMinTempRecordSet);
    //        WebTagDictionary.Add("LowMaxTempRecordSet", TagLowMaxTempRecordSet);
    //        WebTagDictionary.Add("HighWindGustRecordSet", TagHighWindGustRecordSet);
    //        WebTagDictionary.Add("HighWindSpeedRecordSet", TagHighWindSpeedRecordSet);
    //        WebTagDictionary.Add("HighRainRateRecordSet", TagHighRainRateRecordSet);
    //        WebTagDictionary.Add("HighHourlyRainRecordSet", TagHighHourlyRainRecordSet);
    //        WebTagDictionary.Add("HighDailyRainRecordSet", TagHighDailyRainRecordSet);
    //        WebTagDictionary.Add("HighMonthlyRainRecordSet", TagHighMonthlyRainRecordSet);
    //        WebTagDictionary.Add("HighHumidityRecordSet", TagHighHumidityRecordSet);
    //        WebTagDictionary.Add("LowHumidityRecordSet", TagLowHumidityRecordSet);
    //        WebTagDictionary.Add("HighPressureRecordSet", TagHighPressureRecordSet);
    //        WebTagDictionary.Add("LowPressureRecordSet", TagLowPressureRecordSet);
    //        WebTagDictionary.Add("HighWindrunRecordSet", TagHighWindrunRecordSet);
    //        WebTagDictionary.Add("LongestDryPeriodRecordSet", TagLongestDryPeriodRecordSet);
    //        WebTagDictionary.Add("LongestWetPeriodRecordSet", TagLongestWetPeriodRecordSet);
    //        WebTagDictionary.Add("LowTempRangeRecordSet", TagLowTempRangeRecordSet);
    //        WebTagDictionary.Add("HighTempRangeRecordSet", TagHighTempRangeRecordSet);
    //        WebTagDictionary.Add("tempTH", TagtempTH);
    //        WebTagDictionary.Add("TtempTH", TagTtempTH);
    //        WebTagDictionary.Add("tempTL", TagtempTL);
    //        WebTagDictionary.Add("TtempTL", TagTtempTL);
    //        WebTagDictionary.Add("wchillTL", TagwchillTL);
    //        WebTagDictionary.Add("TwchillTL", TagTwchillTL);
    //        WebTagDictionary.Add("apptempTH", TagapptempTH);
    //        WebTagDictionary.Add("TapptempTH", TagTapptempTH);
    //        WebTagDictionary.Add("apptempTL", TagapptempTL);
    //        WebTagDictionary.Add("TapptempTL", TagTapptempTL);
    //        WebTagDictionary.Add("dewpointTH", TagdewpointTH);
    //        WebTagDictionary.Add("TdewpointTH", TagTdewpointTH);
    //        WebTagDictionary.Add("dewpointTL", TagdewpointTL);
    //        WebTagDictionary.Add("TdewpointTL", TagTdewpointTL);
    //        WebTagDictionary.Add("heatindexTH", TagheatindexTH);
    //        WebTagDictionary.Add("TheatindexTH", TagTheatindexTH);
    //        WebTagDictionary.Add("pressTH", TagpressTH);
    //        WebTagDictionary.Add("TpressTH", TagTpressTH);
    //        WebTagDictionary.Add("pressTL", TagpressTL);
    //        WebTagDictionary.Add("TpressTL", TagTpressTL);
    //        WebTagDictionary.Add("humTH", TaghumTH);
    //        WebTagDictionary.Add("ThumTH", TagThumTH);
    //        WebTagDictionary.Add("humTL", TaghumTL);
    //        WebTagDictionary.Add("ThumTL", TagThumTL);
    //        WebTagDictionary.Add("windTM", TagwindTM);
    //        WebTagDictionary.Add("Tbeaufort", TagTbeaufort); //TODO
    //        WebTagDictionary.Add("Tbeaufortnumber", TagTbeaufortnumber); //TODO
    //        WebTagDictionary.Add("TwindTM", TagTwindTM);
    //        WebTagDictionary.Add("wgustTM", TagwgustTM);
    //        WebTagDictionary.Add("TwgustTM", TagTwgustTM);
    //        WebTagDictionary.Add("bearingTM", TagbearingTM);
    //        WebTagDictionary.Add("rrateTM", TagrrateTM);
    //        WebTagDictionary.Add("TrrateTM", TagTrrateTM);
    //        WebTagDictionary.Add("hourlyrainTH", TaghourlyrainTH);
    //        WebTagDictionary.Add("ThourlyrainTH", TagThourlyrainTH);
    //        WebTagDictionary.Add("hourlyrainYH", TaghourlyrainYH);
    //        WebTagDictionary.Add("ThourlyrainYH", TagThourlyrainYH);
    //        WebTagDictionary.Add("solarTH", TagSolarTH);
    //        WebTagDictionary.Add("TsolarTH", TagTsolarTH);
    //        WebTagDictionary.Add("UVTH", TagUVTH);
    //        WebTagDictionary.Add("TUVTH", TagTUVTH);
    //        WebTagDictionary.Add("rollovertime", Tagrollovertime); //TODO
    //        WebTagDictionary.Add("currcond", Tagcurrcond);
    //        WebTagDictionary.Add("currcondenc", Tagcurrcondenc);
    //        WebTagDictionary.Add("tempYH", TagtempYH);
    //        WebTagDictionary.Add("TtempYH", TagTtempYH);
    //        WebTagDictionary.Add("tempYL", TagtempYL);
    //        WebTagDictionary.Add("TtempYL", TagTtempYL);
    //        WebTagDictionary.Add("wchillYL", TagwchillYL);
    //        WebTagDictionary.Add("TwchillYL", TagTwchillYL);
    //        WebTagDictionary.Add("apptempYH", TagapptempYH);
    //        WebTagDictionary.Add("TapptempYH", TagTapptempYH);
    //        WebTagDictionary.Add("apptempYL", TagapptempYL);
    //        WebTagDictionary.Add("TapptempYL", TagTapptempYL);
    //        WebTagDictionary.Add("dewpointYH", TagdewpointYH);
    //        WebTagDictionary.Add("TdewpointYH", TagTdewpointYH);
    //        WebTagDictionary.Add("dewpointYL", TagdewpointYL);
    //        WebTagDictionary.Add("TdewpointYL", TagTdewpointYL);
    //        WebTagDictionary.Add("heatindexYH", TagheatindexYH);
    //        WebTagDictionary.Add("TheatindexYH", TagTheatindexYH);
    //        WebTagDictionary.Add("pressYH", TagpressYH);
    //        WebTagDictionary.Add("TpressYH", TagTpressYH);
    //        WebTagDictionary.Add("pressYL", TagpressYL);
    //        WebTagDictionary.Add("TpressYL", TagTpressYL);
    //        WebTagDictionary.Add("humYH", TaghumYH);
    //        WebTagDictionary.Add("ThumYH", TagThumYH);
    //        WebTagDictionary.Add("humYL", TaghumYL);
    //        WebTagDictionary.Add("ThumYL", TagThumYL);
    //        WebTagDictionary.Add("windYM", TagwindYM);
    //        WebTagDictionary.Add("Ybeaufort", TagYbeaufort);
    //        WebTagDictionary.Add("Ybeaufortnumber", TagYbeaufortnumber);
    //        WebTagDictionary.Add("TwindYM", TagTwindYM);
    //        WebTagDictionary.Add("wgustYM", TagwgustYM);
    //        WebTagDictionary.Add("TwgustYM", TagTwgustYM);
    //        WebTagDictionary.Add("bearingYM", TagbearingYM);
    //        WebTagDictionary.Add("rrateYM", TagrrateYM);
    //        WebTagDictionary.Add("TrrateYM", TagTrrateYM);
    //        WebTagDictionary.Add("rfallY", TagrfallY);
    //        WebTagDictionary.Add("solarYH", TagSolarYH);
    //        WebTagDictionary.Add("TsolarYH", TagTsolarYH);
    //        WebTagDictionary.Add("UVYH", TagUVYH);
    //        WebTagDictionary.Add("TUVYH", TagTUVYH);
    //        WebTagDictionary.Add("tempH", TagtempH);
    //        WebTagDictionary.Add("TtempH", TagTtempH);
    //        WebTagDictionary.Add("tempL", TagtempL);
    //        WebTagDictionary.Add("TtempL", TagTtempL);
    //        WebTagDictionary.Add("apptempH", TagapptempH);
    //        WebTagDictionary.Add("TapptempH", TagTapptempH);
    //        WebTagDictionary.Add("apptempL", TagapptempL);
    //        WebTagDictionary.Add("TapptempL", TagTapptempL);
    //        WebTagDictionary.Add("dewpointH", TagdewpointH);
    //        WebTagDictionary.Add("TdewpointH", TagTdewpointH);
    //        WebTagDictionary.Add("dewpointL", TagdewpointL);
    //        WebTagDictionary.Add("TdewpointL", TagTdewpointL);
    //        WebTagDictionary.Add("heatindexH", TagheatindexH);
    //        WebTagDictionary.Add("TheatindexH", TagTheatindexH);
    //        WebTagDictionary.Add("gustM", TaggustM);
    //        WebTagDictionary.Add("TgustM", TagTgustM);
    //        WebTagDictionary.Add("wspeedH", TagwspeedH);
    //        WebTagDictionary.Add("TwspeedH", TagTwspeedH);
    //        WebTagDictionary.Add("windrunH", TagwindrunH);
    //        WebTagDictionary.Add("TwindrunH", TagTwindrunH);
    //        WebTagDictionary.Add("wchillH", TagwchillH);
    //        WebTagDictionary.Add("TwchillH", TagTwchillH);
    //        WebTagDictionary.Add("rrateM", TagrrateM);
    //        WebTagDictionary.Add("TrrateM", TagTrrateM);
    //        WebTagDictionary.Add("rfallH", TagrfallH);
    //        WebTagDictionary.Add("TrfallH", TagTrfallH);
    //        WebTagDictionary.Add("rfallhH", TagrfallhH);
    //        WebTagDictionary.Add("TrfallhH", TagTrfallhH);
    //        WebTagDictionary.Add("rfallmH", TagrfallmH);
    //        WebTagDictionary.Add("TrfallmH", TagTrfallmH);
    //        WebTagDictionary.Add("pressH", TagpressH);
    //        WebTagDictionary.Add("TpressH", TagTpressH);
    //        WebTagDictionary.Add("pressL", TagpressL);
    //        WebTagDictionary.Add("TpressL", TagTpressL);
    //        WebTagDictionary.Add("humH", TaghumH);
    //        WebTagDictionary.Add("ThumH", TagThumH);
    //        WebTagDictionary.Add("humL", TaghumL);
    //        WebTagDictionary.Add("ThumL", TagThumL);
    //        WebTagDictionary.Add("recordsbegandate", Tagrecordsbegandate); 
    //        WebTagDictionary.Add("DaysSinceRecordsBegan", TagDaysSinceRecordsBegan);
    //        WebTagDictionary.Add("mintempH", TagmintempH);
    //        WebTagDictionary.Add("TmintempH", TagTmintempH);
    //        WebTagDictionary.Add("maxtempL", TagmaxtempL);
    //        WebTagDictionary.Add("TmaxtempL", TagTmaxtempL);
    //        WebTagDictionary.Add("LongestDryPeriod", TagLongestDryPeriod);
    //        WebTagDictionary.Add("TLongestDryPeriod", TagTLongestDryPeriod);
    //        WebTagDictionary.Add("LongestWetPeriod", TagLongestWetPeriod);
    //        WebTagDictionary.Add("TLongestWetPeriod", TagTLongestWetPeriod);
    //        WebTagDictionary.Add("LowDailyTempRange", TagLowDailyTempRange);
    //        WebTagDictionary.Add("TLowDailyTempRange", TagTLowDailyTempRange);
    //        WebTagDictionary.Add("HighDailyTempRange", TagHighDailyTempRange);
    //        WebTagDictionary.Add("THighDailyTempRange", TagTHighDailyTempRange);
    //        WebTagDictionary.Add("graphperiod", Taggraphperiod); //TODO
    //        WebTagDictionary.Add("stationtype", Tagstationtype); //TODO
    //        WebTagDictionary.Add("latitude", Taglatitude); 
    //        WebTagDictionary.Add("longitude", Taglongitude); 
    //        WebTagDictionary.Add("location", Taglocation); //TODO
    //        WebTagDictionary.Add("longlocation", Taglonglocation); //TODO
    //        WebTagDictionary.Add("sunrise", Tagsunrise); 
    //        WebTagDictionary.Add("sunset", Tagsunset); 
    //        WebTagDictionary.Add("daylength", Tagdaylength);
    //        WebTagDictionary.Add("dawn", Tagdawn); //TODO
    //        WebTagDictionary.Add("dusk", Tagdusk); //TODO
    //        WebTagDictionary.Add("daylightlength", Tagdaylightlength);
    //        WebTagDictionary.Add("isdaylight", Tagisdaylight); 
    //        WebTagDictionary.Add("IsSunUp", TagIsSunUp); 
    //        WebTagDictionary.Add("SensorContactLost", TagSensorContactLost); //TODO
    //        WebTagDictionary.Add("moonrise", Tagmoonrise); 
    //        WebTagDictionary.Add("moonset", Tagmoonset); 
    //        WebTagDictionary.Add("moonphase", Tagmoonphase);
    //        WebTagDictionary.Add("chillhours", TagChillHours); //TODO
    //        WebTagDictionary.Add("altitude", Tagaltitude); //TODO
    //        WebTagDictionary.Add("forum", Tagforum); //TODO
    //        WebTagDictionary.Add("webcam", Tagwebcam); //TODO
    //        WebTagDictionary.Add("tempunit", Tagtempunit); //TODO
    //        WebTagDictionary.Add("tempunitnodeg", Tagtempunitnodeg);
    //        WebTagDictionary.Add("windunit", Tagwindunit); //TODO
    //        WebTagDictionary.Add("windrununit", Tagwindrununit);
    //        WebTagDictionary.Add("pressunit", Tagpressunit); //TODO
    //        WebTagDictionary.Add("rainunit", Tagrainunit); //TODO
    //        WebTagDictionary.Add("interval", Taginterval); //TODO
    //        WebTagDictionary.Add("realtimeinterval", Tagrealtimeinterval); //TODO
    //        WebTagDictionary.Add("version", Tagversion); 
    //        WebTagDictionary.Add("build", Tagbuild); 
    //        WebTagDictionary.Add("update", Tagupdate); 
    //        WebTagDictionary.Add("LastRainTipISO", TagLastRainTipISO);
    //        WebTagDictionary.Add("MinutesSinceLastRainTip", TagMinutesSinceLastRainTip);
    //        WebTagDictionary.Add("LastDataReadT", TagLastDataReadT);
    //        WebTagDictionary.Add("LatestNOAAMonthlyReport", TagLatestNOAAMonthlyReport);
    //        WebTagDictionary.Add("LatestNOAAYearlyReport", TagLatestNOAAYearlyReport);
    //        WebTagDictionary.Add("dailygraphperiod", Tagdailygraphperiod);
    //        WebTagDictionary.Add("RCtemp", TagRCtemp);
    //        WebTagDictionary.Add("RCtempTH", TagRCtempTH);
    //        WebTagDictionary.Add("RCtempTL", TagRCtempTL);
    //        WebTagDictionary.Add("RCintemp", TagRCintemp);
    //        WebTagDictionary.Add("RCdew", TagRCdew);
    //        WebTagDictionary.Add("RCheatindex", TagRCheatindex);
    //        WebTagDictionary.Add("RCwchill", tagParams => ReplaceCommas(Tagwchill(tagParams)));
    //        WebTagDictionary.Add("RChum", tagParams => ReplaceCommas(Taghum(tagParams)));
    //        WebTagDictionary.Add("RCinhum", tagParams => ReplaceCommas(Taginhum(tagParams)));
    //        WebTagDictionary.Add("RCrfall", tagParams => ReplaceCommas(Tagrfall(tagParams)));
    //        WebTagDictionary.Add("RCrrate", TagRCrrate);
    //        WebTagDictionary.Add("RCrrateTM", TagRCrrateTM);
    //        WebTagDictionary.Add("RCwgust", TagRCwgust);
    //        WebTagDictionary.Add("RCwlatest", TagRCwlatest);
    //        WebTagDictionary.Add("RCwspeed", TagRCwspeed);
    //        WebTagDictionary.Add("RCwgustTM", TagRCwgustTM);
    //        WebTagDictionary.Add("RCpress", TagRCpress);
    //        WebTagDictionary.Add("RCpressTH", TagRCpressTH);
    //        WebTagDictionary.Add("RCpressTL", TagRCpressTL);
    //        WebTagDictionary.Add("RCdewpointTH", TagRCdewpointTH);
    //        WebTagDictionary.Add("RCdewpointTL", TagRCdewpointTL);
    //        WebTagDictionary.Add("RCwchillTL", TagRCwchillTL);
    //        WebTagDictionary.Add("RCheatindexTH", TagRCheatindexTH);
    //        WebTagDictionary.Add("RCapptempTH", TagRCapptempTH);
    //        WebTagDictionary.Add("RCapptempTL", TagRCapptempTL);
    //        WebTagDictionary.Add("ET", TagET);
    //        WebTagDictionary.Add("UV", TagUV);
    //        WebTagDictionary.Add("SolarRad", TagSolarRad);
    //        WebTagDictionary.Add("Light", TagLight);
    //        WebTagDictionary.Add("CurrentSolarMax", TagCurrentSolarMax); //TODO
    //        WebTagDictionary.Add("SunshineHours", TagSunshineHours);
    //        WebTagDictionary.Add("YSunshineHours", TagYSunshineHours);
    //        WebTagDictionary.Add("IsSunny", TagIsSunny); //TODO
    //        WebTagDictionary.Add("IsRaining", TagIsRaining); //TODO
    //        WebTagDictionary.Add("IsFreezing", TagIsFreezing); //TODO
    //        WebTagDictionary.Add("THWindex", TagTHWIndex);
    //        WebTagDictionary.Add("THSWindex", TagTHSWIndex);
    //        WebTagDictionary.Add("ExtraTemp1", TagExtraTemp1);
    //        WebTagDictionary.Add("ExtraTemp2", TagExtraTemp2);
    //        WebTagDictionary.Add("ExtraTemp3", TagExtraTemp3);
    //        WebTagDictionary.Add("ExtraTemp4", TagExtraTemp4);
    //        WebTagDictionary.Add("ExtraTemp5", TagExtraTemp5);
    //        WebTagDictionary.Add("ExtraTemp6", TagExtraTemp6);
    //        WebTagDictionary.Add("ExtraTemp7", TagExtraTemp7);
    //        WebTagDictionary.Add("ExtraTemp8", TagExtraTemp8);
    //        WebTagDictionary.Add("ExtraTemp9", TagExtraTemp9);
    //        WebTagDictionary.Add("ExtraTemp10", TagExtraTemp10);
    //        WebTagDictionary.Add("ExtraDP1", TagExtraDP1);
    //        WebTagDictionary.Add("ExtraDP2", TagExtraDP2);
    //        WebTagDictionary.Add("ExtraDP3", TagExtraDP3);
    //        WebTagDictionary.Add("ExtraDP4", TagExtraDP4);
    //        WebTagDictionary.Add("ExtraDP5", TagExtraDP5);
    //        WebTagDictionary.Add("ExtraDP6", TagExtraDP6);
    //        WebTagDictionary.Add("ExtraDP7", TagExtraDP7);
    //        WebTagDictionary.Add("ExtraDP8", TagExtraDP8);
    //        WebTagDictionary.Add("ExtraDP9", TagExtraDP3);
    //        WebTagDictionary.Add("ExtraDP10", TagExtraDP10);
    //        WebTagDictionary.Add("ExtraHum1", TagExtraHum1);
    //        WebTagDictionary.Add("ExtraHum2", TagExtraHum2);
    //        WebTagDictionary.Add("ExtraHum3", TagExtraHum3);
    //        WebTagDictionary.Add("ExtraHum4", TagExtraHum4);
    //        WebTagDictionary.Add("ExtraHum5", TagExtraHum5);
    //        WebTagDictionary.Add("ExtraHum6", TagExtraHum6);
    //        WebTagDictionary.Add("ExtraHum7", TagExtraHum7);
    //        WebTagDictionary.Add("ExtraHum8", TagExtraHum8);
    //        WebTagDictionary.Add("ExtraHum9", TagExtraHum9);
    //        WebTagDictionary.Add("ExtraHum10", TagExtraHum10);
    //        WebTagDictionary.Add("SoilTemp1", TagSoilTemp1);
    //        WebTagDictionary.Add("SoilTemp2", TagSoilTemp2);
    //        WebTagDictionary.Add("SoilTemp3", TagSoilTemp3);
    //        WebTagDictionary.Add("SoilTemp4", TagSoilTemp4);
    //        WebTagDictionary.Add("SoilMoisture1", TagSoilMoisture1);
    //        WebTagDictionary.Add("SoilMoisture2", TagSoilMoisture2);
    //        WebTagDictionary.Add("SoilMoisture3", TagSoilMoisture3);
    //        WebTagDictionary.Add("SoilMoisture4", tagParams => station.SoilMoisture4.ToString());
    //        WebTagDictionary.Add("LeafTemp1", tagParams => station.LeafTemp1.ToString(cumulus.TempFormat));
    //        WebTagDictionary.Add("LeafTemp2", tagParams => station.LeafTemp2.ToString(cumulus.TempFormat));
    //        WebTagDictionary.Add("LeafTemp3", tagParams => station.LeafTemp3.ToString(cumulus.TempFormat));
    //        WebTagDictionary.Add("LeafTemp4", tagParams => station.LeafTemp4.ToString(cumulus.TempFormat));
    //        WebTagDictionary.Add("LeafWetness1", TagLeafWetness1);
    //        WebTagDictionary.Add("LeafWetness2", TagLeafWetness2);
    //        WebTagDictionary.Add("LeafWetness3", TagLeafWetness3);
    //        WebTagDictionary.Add("LeafWetness4", TagLeafWetness4);
    //        WebTagDictionary.Add("LowTempAlarm", TagLowTempAlarm);
    //        WebTagDictionary.Add("HighTempAlarm", TagHighTempAlarm);
    //        WebTagDictionary.Add("TempChangeUpAlarm", TagTempChangeUpAlarm);
    //        WebTagDictionary.Add("TempChangeDownAlarm", TagTempChangeDownAlarm);
    //        WebTagDictionary.Add("LowPressAlarm", TagLowPressAlarm);
    //        WebTagDictionary.Add("HighPressAlarm", TagHighPressAlarm);
    //        WebTagDictionary.Add("PressChangeUpAlarm", TagPressChangeUpAlarm);
    //        WebTagDictionary.Add("PressChangeDownAlarm", TagPressChangeDownAlarm);
    //        WebTagDictionary.Add("HighRainTodayAlarm", TagHighRainTodayAlarm);
    //        WebTagDictionary.Add("HighRainRateAlarm", TagHighRainRateAlarm);
    //        WebTagDictionary.Add("HighWindGustAlarm", TagHighWindGustAlarm);
    //        WebTagDictionary.Add("HighWindSpeedAlarm", TagHighWindSpeedAlarm);
    //        WebTagDictionary.Add("RG11RainToday", TagRG11RainToday);
    //        WebTagDictionary.Add("RG11RainYest", TagRG11RainYest);
    //        // Monthly highs and lows - values
    //        WebTagDictionary.Add("MonthTempH", TagMonthTempH);
    //        WebTagDictionary.Add("MonthTempL", TagMonthTempL);
    //        WebTagDictionary.Add("MonthHeatIndexH", TagMonthHeatIndexH);
    //        WebTagDictionary.Add("MonthWChillL", TagMonthWChillL);
    //        WebTagDictionary.Add("MonthAppTempH", TagMonthAppTempH);
    //        WebTagDictionary.Add("MonthAppTempL", TagMonthAppTempL);
    //        WebTagDictionary.Add("MonthMinTempH", TagMonthMinTempH);
    //        WebTagDictionary.Add("MonthMaxTempL", TagMonthMaxTempL);
    //        WebTagDictionary.Add("MonthPressH", TagMonthPressH);
    //        WebTagDictionary.Add("MonthPressL", TagMonthPressL);
    //        WebTagDictionary.Add("MonthHumH", TagMonthHumH);
    //        WebTagDictionary.Add("MonthHumL", TagMonthHumL);
    //        WebTagDictionary.Add("MonthGustH", TagMonthGustH);
    //        WebTagDictionary.Add("MonthWindH", TagMonthWindH);
    //        WebTagDictionary.Add("MonthRainRateH", TagMonthRainRateH);
    //        WebTagDictionary.Add("MonthHourlyRainH", TagMonthHourlyRainH);
    //        WebTagDictionary.Add("MonthDailyRainH", TagMonthDailyRainH);
    //        WebTagDictionary.Add("MonthDewPointH", TagMonthDewPointH);
    //        WebTagDictionary.Add("MonthDewPointL", TagMonthDewPointL);
    //        WebTagDictionary.Add("MonthWindRunH", TagMonthWindRunH);
    //        WebTagDictionary.Add("MonthLongestDryPeriod", TagMonthLongestDryPeriod);
    //        WebTagDictionary.Add("MonthLongestWetPeriod", TagMonthLongestWetPeriod);
    //        WebTagDictionary.Add("MonthHighDailyTempRange", TagMonthHighDailyTempRange);
    //        WebTagDictionary.Add("MonthLowDailyTempRange", TagMonthLowDailyTempRange);
    //        // This year"s highs and lows - times
    //        WebTagDictionary.Add("MonthTempHT", TagMonthTempHT);
    //        WebTagDictionary.Add("MonthTempLT", TagMonthTempLT);
    //        WebTagDictionary.Add("MonthHeatIndexHT", TagMonthHeatIndexHT);
    //        WebTagDictionary.Add("MonthWChillLT", TagMonthWChillLT);
    //        WebTagDictionary.Add("MonthAppTempHT", TagMonthAppTempHT);
    //        WebTagDictionary.Add("MonthAppTempLT", TagMonthAppTempLT);
    //        WebTagDictionary.Add("MonthPressHT", TagMonthPressHT);
    //        WebTagDictionary.Add("MonthPressLT", TagMonthPressLT);
    //        WebTagDictionary.Add("MonthHumHT", TagMonthHumHT);
    //        WebTagDictionary.Add("MonthHumLT", TagMonthHumLT);
    //        WebTagDictionary.Add("MonthGustHT", TagMonthGustHT);
    //        WebTagDictionary.Add("MonthWindHT", TagMonthWindHT);
    //        WebTagDictionary.Add("MonthRainRateHT", TagMonthRainRateHT);
    //        WebTagDictionary.Add("MonthHourlyRainHT", TagMonthHourlyRainHT);
    //        WebTagDictionary.Add("MonthDewPointHT", TagMonthDewPointHT);
    //        WebTagDictionary.Add("MonthDewPointLT", TagMonthDewPointLT);
    //        // This month"s highs and lows - dates
    //        WebTagDictionary.Add("MonthTempHD", tagParams => GetFormattedDateTime(station.HighTempThisMonthTS, "dd MMMM", tagParams));
    //        WebTagDictionary.Add("MonthTempLD", tagParams => GetFormattedDateTime(station.LowTempThisMonthTS, "dd MMMM", tagParams));
    //        WebTagDictionary.Add("MonthHeatIndexHD", tagParams => GetFormattedDateTime(station.HighHeatIndexThisMonthTS, "dd MMMM", tagParams));
    //        WebTagDictionary.Add("MonthWChillLD", TagMonthWChillLD);
    //        WebTagDictionary.Add("MonthAppTempHD", TagMonthAppTempHD);
    //        WebTagDictionary.Add("MonthAppTempLD", TagMonthAppTempLD);
    //        WebTagDictionary.Add("MonthMinTempHD", TagMonthMinTempHD);
    //        WebTagDictionary.Add("MonthMaxTempLD", TagMonthMaxTempLD);
    //        WebTagDictionary.Add("MonthPressHD", TagMonthPressHD);
    //        WebTagDictionary.Add("MonthPressLD", TagMonthPressLD);
    //        WebTagDictionary.Add("MonthHumHD", TagMonthHumHD);
    //        WebTagDictionary.Add("MonthHumLD", TagMonthHumLD);
    //        WebTagDictionary.Add("MonthGustHD", TagMonthGustHD);
    //        WebTagDictionary.Add("MonthWindHD", TagMonthWindHD);
    //        WebTagDictionary.Add("MonthRainRateHD", TagMonthRainRateHD);
    //        WebTagDictionary.Add("MonthHourlyRainHD", TagMonthHourlyRainHD);
    //        WebTagDictionary.Add("MonthDailyRainHD", TagMonthDailyRainHD);
    //        WebTagDictionary.Add("MonthDewPointHD", TagMonthDewPointHD);
    //        WebTagDictionary.Add("MonthDewPointLD", TagMonthDewPointLD);
    //        WebTagDictionary.Add("MonthWindRunHD", TagMonthWindRunHD);
    //        WebTagDictionary.Add("MonthLongestDryPeriodD", TagMonthLongestDryPeriodD);
    //        WebTagDictionary.Add("MonthLongestWetPeriodD", TagMonthLongestWetPeriodD);
    //        WebTagDictionary.Add("MonthHighDailyTempRangeD", TagMonthHighDailyTempRangeD);
    //        WebTagDictionary.Add("MonthLowDailyTempRangeD", TagMonthLowDailyTempRangeD);
    //        // This Year"s highs and lows - values
    //        WebTagDictionary.Add("YearTempH", TagYearTempH);
    //        WebTagDictionary.Add("YearTempL", TagYearTempL);
    //        WebTagDictionary.Add("YearHeatIndexH", TagYearHeatIndexH);
    //        WebTagDictionary.Add("YearWChillL", TagYearWChillL);
    //        WebTagDictionary.Add("YearAppTempH", TagYearAppTempH);
    //        WebTagDictionary.Add("YearAppTempL", TagYearAppTempL);
    //        WebTagDictionary.Add("YearMinTempH", TagYearMinTempH);
    //        WebTagDictionary.Add("YearMaxTempL", TagYearMaxTempL);
    //        WebTagDictionary.Add("YearPressH", TagYearPressH);
    //        WebTagDictionary.Add("YearPressL", TagYearPressL);
    //        WebTagDictionary.Add("YearHumH", TagYearHumH);
    //        WebTagDictionary.Add("YearHumL", TagYearHumL);
    //        WebTagDictionary.Add("YearGustH", TagYearGustH);
    //        WebTagDictionary.Add("YearWindH", TagYearWindH);
    //        WebTagDictionary.Add("YearRainRateH", TagYearRainRateH);
    //        WebTagDictionary.Add("YearHourlyRainH", TagYearHourlyRainH);
    //        WebTagDictionary.Add("YearDailyRainH", TagYearDailyRainH);
    //        WebTagDictionary.Add("YearMonthlyRainH", TagYearMonthlyRainH);
    //        WebTagDictionary.Add("YearDewPointH", TagYearDewPointH);
    //        WebTagDictionary.Add("YearDewPointL", TagYearDewPointL);
    //        WebTagDictionary.Add("YearWindRunH", TagYearWindRunH);
    //        WebTagDictionary.Add("YearLongestDryPeriod", TagYearLongestDryPeriod);
    //        WebTagDictionary.Add("YearLongestWetPeriod", TagYearLongestWetPeriod);
    //        WebTagDictionary.Add("YearHighDailyTempRange", TagYearHighDailyTempRange);
    //        WebTagDictionary.Add("YearLowDailyTempRange", TagYearLowDailyTempRange);
    //        // This years"s highs and lows - times
    //        WebTagDictionary.Add("YearTempHT", TagYearTempHT);
    //        WebTagDictionary.Add("YearTempLT", TagYearTempLT);
    //        WebTagDictionary.Add("YearHeatIndexHT", TagYearHeatIndexHT);
    //        WebTagDictionary.Add("YearWChillLT", TagYearWChillLT);
    //        WebTagDictionary.Add("YearAppTempHT", TagYearAppTempHT);
    //        WebTagDictionary.Add("YearAppTempLT", TagYearAppTempLT);
    //        WebTagDictionary.Add("YearPressHT", tagParams => GetFormattedDateTime(station.HighPressThisYearTS, "t", tagParams));
    //        WebTagDictionary.Add("YearPressLT", tagParams => GetFormattedDateTime(station.LowPressThisYearTS, "t", tagParams));
    //        WebTagDictionary.Add("YearHumHT", TagYearHumHT);
    //        WebTagDictionary.Add("YearHumLT", TagYearHumLT);
    //        WebTagDictionary.Add("YearGustHT", TagYearGustHT);
    //        WebTagDictionary.Add("YearWindHT", TagYearWindHT);
    //        WebTagDictionary.Add("YearRainRateHT", TagYearRainRateHT);
    //        WebTagDictionary.Add("YearHourlyRainHT", TagYearHourlyRainHT);
    //        WebTagDictionary.Add("YearDewPointHT", tagParams => GetFormattedDateTime(station.HighDewpointThisYearTS, "t", tagParams));
    //        WebTagDictionary.Add("YearDewPointLT", tagParams => GetFormattedDateTime(station.LowDewpointThisYearTS, "t", tagParams));
    //        // Yearly highs and lows - dates
    //        WebTagDictionary.Add("YearTempHD", TagYearTempHD);
    //        WebTagDictionary.Add("YearTempLD", TagYearTempLD);
    //        WebTagDictionary.Add("YearHeatIndexHD", TagYearHeatIndexHD);
    //        WebTagDictionary.Add("YearWChillLD", TagYearWChillLD);
    //        WebTagDictionary.Add("YearAppTempHD", TagYearAppTempHD);
    //        WebTagDictionary.Add("YearAppTempLD", TagYearAppTempLD);
    //        WebTagDictionary.Add("YearMinTempHD", TagYearMinTempHD);
    //        WebTagDictionary.Add("YearMaxTempLD", TagYearMaxTempLD);
    //        WebTagDictionary.Add("YearPressHD", TagYearPressHD);
    //        WebTagDictionary.Add("YearPressLD", TagYearPressLD);
    //        WebTagDictionary.Add("YearHumHD", TagYearHumHD);
    //        WebTagDictionary.Add("YearHumLD", TagYearHumLD);
    //        WebTagDictionary.Add("YearGustHD", TagYearGustHD);
    //        WebTagDictionary.Add("YearWindHD", TagYearWindHD);
    //        WebTagDictionary.Add("YearRainRateHD", TagYearRainRateHD);
    //        WebTagDictionary.Add("YearHourlyRainHD", TagYearHourlyRainHD);
    //        WebTagDictionary.Add("YearDailyRainHD", TagYearDailyRainHD);
    //        WebTagDictionary.Add("YearMonthlyRainHD", TagYearMonthlyRainHD);
    //        WebTagDictionary.Add("YearDewPointHD", TagYearDewPointHD);
    //        WebTagDictionary.Add("YearDewPointLD", TagYearDewPointLD);
    //        WebTagDictionary.Add("YearWindRunHD", TagYearWindRunHD);
    //        WebTagDictionary.Add("YearLongestDryPeriodD", TagYearLongestDryPeriodD);
    //        WebTagDictionary.Add("YearLongestWetPeriodD", TagYearLongestWetPeriodD);
    //        WebTagDictionary.Add("YearHighDailyTempRangeD", TagYearHighDailyTempRangeD);
    //        WebTagDictionary.Add("YearLowDailyTempRangeD", TagYearLowDailyTempRangeD);
    //        // misc
    //        WebTagDictionary.Add("LatestError", TagLatestError);
    //        WebTagDictionary.Add("LatestErrorDate", TagLatestErrorDate); 
    //        WebTagDictionary.Add("LatestErrorTime", TagLatestErrorTime); 
    //        WebTagDictionary.Add("ErrorLight", Tagerrorlight);
    //        WebTagDictionary.Add("MoonPercent", TagMoonPercent);
    //        WebTagDictionary.Add("MoonPercentAbs", TagMoonPercentAbs);
    //        WebTagDictionary.Add("MoonAge", TagMoonAge); 
    //        WebTagDictionary.Add("OsVersion", TagOsVersion); 
    //        WebTagDictionary.Add("OsLanguage", TagOsLanguage); 
    //        WebTagDictionary.Add("SystemUpTime", TagSystemUpTime); 
    //        WebTagDictionary.Add("ProgramUpTime", TagProgramUpTime); 
    //        WebTagDictionary.Add("CpuName", TagCpuName); 
    //        WebTagDictionary.Add("CpuCount", TagCpuCount); 
    //        WebTagDictionary.Add("MemoryStatus", TagMemoryStatus); 
    //        WebTagDictionary.Add("DisplayMode", TagDisplayModeString); 
    //        WebTagDictionary.Add("AllocatedMemory", TagAllocatedMemory); 
    //        WebTagDictionary.Add("DiskSize", TagDiskSize); 
    //        WebTagDictionary.Add("DiskFree", TagDiskFree); 
    //        WebTagDictionary.Add("DavisTotalPacketsReceived", TagDavisTotalPacketsReceived); //TODO
    //        WebTagDictionary.Add("DavisTotalPacketsMissed", TagDavisTotalPacketsMissed); //TODO
    //        WebTagDictionary.Add("DavisNumberOfResynchs", TagDavisNumberOfResynchs); //TODO
    //        WebTagDictionary.Add("DavisMaxInARow", TagDavisMaxInARow); //TODO
    //        WebTagDictionary.Add("DavisNumCRCerrors", TagDavisNumCRCerrors); //TODO
    //        WebTagDictionary.Add("DavisFirmwareVersion", TagDavisFirmwareVersion); //TODO
    //        WebTagDictionary.Add("DataStopped", TagDataStopped); //TODO
    //        // Recent history
    //        WebTagDictionary.Add("RecentOutsideTemp", TagRecentOutsideTemp);
    //        WebTagDictionary.Add("RecentWindSpeed", TagRecentWindSpeed);
    //        WebTagDictionary.Add("RecentWindGust", TagRecentWindGust);
    //        WebTagDictionary.Add("RecentWindLatest", TagRecentWindLatest);
    //        WebTagDictionary.Add("RecentWindDir", TagRecentWindDir);
    //        WebTagDictionary.Add("RecentWindAvgDir", TagRecentWindAvgDir);
    //        WebTagDictionary.Add("RecentWindChill", TagRecentWindChill);
    //        WebTagDictionary.Add("RecentDewPoint", TagRecentDewPoint);
    //        WebTagDictionary.Add("RecentHeatIndex", TagRecentHeatIndex);
    //        WebTagDictionary.Add("RecentHumidity", TagRecentHumidity);
    //        WebTagDictionary.Add("RecentPressure", TagRecentPressure);
    //        WebTagDictionary.Add("RecentRainToday", TagRecentRainToday);
    //        WebTagDictionary.Add("RecentSolarRad", TagRecentSolarRad);
    //        WebTagDictionary.Add("RecentUV", TagRecentUV);
    //        WebTagDictionary.Add("RecentTS", TagRecentTS);
    //        // Recent history with comma decimals replaced
    //        WebTagDictionary.Add("RCRecentOutsideTemp", TagRCRecentOutsideTemp);
    //        WebTagDictionary.Add("RCRecentWindSpeed", TagRCRecentWindSpeed);
    //        WebTagDictionary.Add("RCRecentWindGust", TagRCRecentWindGust);
    //        WebTagDictionary.Add("RCRecentWindLatest", TagRCRecentWindLatest);
    //        WebTagDictionary.Add("RCRecentWindChill", TagRCRecentWindChill);
    //        WebTagDictionary.Add("RCRecentDewPoint", TagRCRecentDewPoint);
    //        WebTagDictionary.Add("RCRecentHeatIndex", TagRCRecentHeatIndex);
    //        WebTagDictionary.Add("RCRecentPressure", TagRCRecentPressure);
    //        WebTagDictionary.Add("RCRecentRainToday", TagRCRecentRainToday);
    //        WebTagDictionary.Add("RCRecentUV", TagRCRecentUV);
    //        // Month-by-month highs and lows - values
    //        WebTagDictionary.Add("ByMonthTempH", TagByMonthTempH);
    //        WebTagDictionary.Add("ByMonthTempL", TagByMonthTempL);
    //        WebTagDictionary.Add("ByMonthAppTempH", TagByMonthAppTempH);
    //        WebTagDictionary.Add("ByMonthAppTempL", TagByMonthAppTempL);
    //        WebTagDictionary.Add("ByMonthDewPointH", TagByMonthDewPointH);
    //        WebTagDictionary.Add("ByMonthDewPointL", TagByMonthDewPointL);
    //        WebTagDictionary.Add("ByMonthHeatIndexH", TagByMonthHeatIndexH);
    //        WebTagDictionary.Add("ByMonthGustH", TagByMonthGustH);
    //        WebTagDictionary.Add("ByMonthWindH", TagByMonthWindH);
    //        WebTagDictionary.Add("ByMonthWindRunH", TagByMonthWindRunH);
    //        WebTagDictionary.Add("ByMonthWChillL", TagByMonthWChillL);
    //        WebTagDictionary.Add("ByMonthRainRateH", TagByMonthRainRateH);
    //        WebTagDictionary.Add("ByMonthDailyRainH", TagByMonthDailyRainH);
    //        WebTagDictionary.Add("ByMonthHourlyRainH", TagByMonthHourlyRainH);
    //        WebTagDictionary.Add("ByMonthMonthlyRainH", TagByMonthMonthlyRainH);
    //        WebTagDictionary.Add("ByMonthPressH", TagByMonthPressH);
    //        WebTagDictionary.Add("ByMonthPressL", TagByMonthPressL);
    //        WebTagDictionary.Add("ByMonthHumH", TagByMonthHumH);
    //        WebTagDictionary.Add("ByMonthHumL", TagByMonthHumL);
    //        WebTagDictionary.Add("ByMonthMinTempH", TagByMonthMinTempH);
    //        WebTagDictionary.Add("ByMonthMaxTempL", TagByMonthMaxTempL);
    //        WebTagDictionary.Add("ByMonthLongestDryPeriod", TagByMonthLongestDryPeriod);
    //        WebTagDictionary.Add("ByMonthLongestWetPeriod", TagByMonthLongestWetPeriod);
    //        WebTagDictionary.Add("ByMonthLowDailyTempRange", TagByMonthLowDailyTempRange);
    //        WebTagDictionary.Add("ByMonthHighDailyTempRange", TagByMonthHighDailyTempRange);
    //        // Month-by-month highs and lows - timestamps
    //        WebTagDictionary.Add("ByMonthTempHT", TagByMonthTempHT);
    //        WebTagDictionary.Add("ByMonthTempLT", TagByMonthTempLT);
    //        WebTagDictionary.Add("ByMonthAppTempHT", TagByMonthAppTempHT);
    //        WebTagDictionary.Add("ByMonthAppTempLT", TagByMonthAppTempLT);
    //        WebTagDictionary.Add("ByMonthDewPointHT", TagByMonthDewPointHT);
    //        WebTagDictionary.Add("ByMonthDewPointLT", TagByMonthDewPointLT);
    //        WebTagDictionary.Add("ByMonthHeatIndexHT", TagByMonthHeatIndexHT);
    //        WebTagDictionary.Add("ByMonthGustHT", TagByMonthGustHT);
    //        WebTagDictionary.Add("ByMonthWindHT", TagByMonthWindHT);
    //        WebTagDictionary.Add("ByMonthWindRunHT", TagByMonthWindRunHT);
    //        WebTagDictionary.Add("ByMonthWChillLT", TagByMonthWChillLT);
    //        WebTagDictionary.Add("ByMonthRainRateHT", TagByMonthRainRateHT);
    //        WebTagDictionary.Add("ByMonthDailyRainHT", TagByMonthDailyRainHT);
    //        WebTagDictionary.Add("ByMonthHourlyRainHT", TagByMonthHourlyRainHT);
    //        WebTagDictionary.Add("ByMonthMonthlyRainHT", TagByMonthMonthlyRainHT);
    //        WebTagDictionary.Add("ByMonthPressHT", TagByMonthPressHT);
    //        WebTagDictionary.Add("ByMonthPressLT", TagByMonthPressLT);
    //        WebTagDictionary.Add("ByMonthHumHT", TagByMonthHumHT);
    //        WebTagDictionary.Add("ByMonthHumLT", TagByMonthHumLT);
    //        WebTagDictionary.Add("ByMonthMinTempHT", TagByMonthMinTempHT);
    //        WebTagDictionary.Add("ByMonthMaxTempLT", TagByMonthMaxTempLT);
    //        WebTagDictionary.Add("ByMonthLongestDryPeriodT", TagByMonthLongestDryPeriodT);
    //        WebTagDictionary.Add("ByMonthLongestWetPeriodT", TagByMonthLongestWetPeriodT);
    //        WebTagDictionary.Add("ByMonthLowDailyTempRangeT", TagByMonthLowDailyTempRangeT);
    //        WebTagDictionary.Add("ByMonthHighDailyTempRangeT", TagByMonthHighDailyTempRangeT);

    //        cumulus.LogMessage(WebTagDictionary.Count + " web tags initialised");

    //        if (cumulus.ListWebTags)
    //        {
    //            using (StreamWriter file = new StreamWriter(cumulus.WebTagFile))
    //            {
    //                foreach (var pair in WebTagDictionary)
    //                {
    //                    file.WriteLine(pair.Key);
    //                }
    //            }
    //        }
    //    }
    //}
}