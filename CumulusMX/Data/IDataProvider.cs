using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Data
{
    public interface IDataProvider
    {
        object GetDayfile(string draw, int start, int length);
        object GetLogfile(string month, string draw, int start, int length, bool v);
        object GetCurrentData();
        object GetDiaryData(string date);
        object GetGraphConfig();
        object GetTempGraphData();
        object GetTempGraphDataD3();
        object GetWindGraphData();
        object GetWindGraphDataD3();
        object GetRainGraphData();
        object GetRainGraphDataD3();
        object GetPressGraphData();
        object GetPressGraphDataD3();
        object GetWindDirGraphData();
        object GetWindDirGraphDataD3();
        object GetHumGraphData();
        object GetHumGraphDataD3();
        object GetSolarGraphData();
        object GetSolarGraphDataD3();
        object GetDailyRainGraphData();
        object GetSunHoursGraphData();
        object GetDailyTempGraphData();
        object GetExtraTemp();
        object GetTodayYestTemp();
        object GetUnits();
        object GetExtraHum();
        object GetTempRecords();
        object GetRainRecords();
        object GetWindRecords();
        object GetExtraDew();
        object GetTodayYestHum();
        object GetPressRecords();
        object GetHumRecords();
        object GetSoilTemp();
        object GetThisYearRainRecords();
        object GetTodayYestRain();
        object GetSoilMoisture();
        object GetThisYearWindRecords();
        object GetThisYearPressRecords();
        object GetLeaf();
        object GetThisYearHumRecords();
        object GetTodayYestWind();
        object GetThisYearTempRecords();
        object GetLeaf4();
        object GetThisMonthRainRecords();
        object GetThisMonthWindRecords();
        object GetTodayYestPressure();
        object GetThisMonthPressRecords();
        object GetThisMonthHumRecords();
        object GetThisMonthTempRecords();
        object GetTodayYestSolar();
        object GetMonthlyRainRecords(int month);
        object GetMonthlyWindRecords(int month);
        object GetMonthlyPressRecords(int month);
        object GetMonthlyHumRecords(int month);
        object GetMonthlyTempRecords(int month);
    }
}
