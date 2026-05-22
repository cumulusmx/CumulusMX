using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		// The Today/Yesterday data is in the form:
		// Name, today value + units, today time, yesterday value + units, yesterday time
		// It's used to automatically populate a DataTables table in the browser interface
		public string GetTodayYestTemp()
		{
			var json = new StringBuilder("{\"data\":[", 2048);
			var sepStr = "\",\"";
			var closeStr = "\"],";
			var tempUnitStr = "&nbsp;&deg;" + cumulus.Units.TempText[1].ToString() + sepStr;

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiTemp"]}\",\"");
			json.Append(DailyHighLow.Today.HighTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.HighTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.HighTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["LoTemp"]}\",\"");
			json.Append(DailyHighLow.Today.LowTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.LowTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.LowTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.LowTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["TempRange"]}\",\"");
			json.Append((DailyHighLow.Today.HighTemp - DailyHighLow.Today.LowTemp).ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\",\"");
			json.Append((DailyHighLow.Yest.HighTemp - DailyHighLow.Yest.LowTemp).ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["AvgTemp"]}\",\"");
			json.Append((MetData.TempTotalToday / tempsamplestoday).ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\",\"");
			json.Append(MetData.YestAvgTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\"],");


			json.Append($"[\"{cumulus.Trans.DataCaptions["HiAppTemp"]}\",\"");
			json.Append(DailyHighLow.Today.HighAppTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.HighAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighAppTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.HighAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["LoAppTemp"]}\",\"");
			json.Append(DailyHighLow.Today.LowAppTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.LowAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.LowAppTemp.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.LowAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiFeelsLike"]}\",\"");
			json.Append(DailyHighLow.Today.HighFeelsLike.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.HighFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighFeelsLike.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.HighFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["LoFeelsLike"]}\",\"");
			json.Append(DailyHighLow.Today.LowFeelsLike.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.LowFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.LowFeelsLike.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.LowFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiHumidex"]}\",\"");
			json.Append(DailyHighLow.Today.HighHumidex.ToFixedLocal(cumulus.TempFormat));
			json.Append("\",\"");
			json.Append(DailyHighLow.Today.HighHumidexTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighHumidex.ToFixedLocal(cumulus.TempFormat));
			json.Append("\",\"");
			json.Append(DailyHighLow.Yest.HighHumidexTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiDewPnt"]}\",\"");
			json.Append(DailyHighLow.Today.HighDewPoint.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.HighDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighDewPoint.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.HighDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["LoDewPnt"]}\",\"");
			json.Append(DailyHighLow.Today.LowDewPoint.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.LowDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.LowDewPoint.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.LowDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["LoWindChill"]}\",\"");
			json.Append(DailyHighLow.Today.LowWindChill.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.LowWindChillTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.LowWindChill.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.LowWindChillTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiHeatInd"]}\",\"");
			json.Append(DailyHighLow.Today.HighHeatIndex.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Today.HighHeatIndexTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighHeatIndex.ToFixedLocal(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(DailyHighLow.Yest.HighHeatIndexTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestHum()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";
			var unitStr = "&nbsp;%" + sepStr;

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiHum"]}\",\"");
			json.Append(DailyHighLow.Today.HighHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(DailyHighLow.Today.HighHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(DailyHighLow.Yest.HighHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["LoHum"]}\",\"");
			json.Append(DailyHighLow.Today.LowHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(DailyHighLow.Today.LowHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.LowHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(DailyHighLow.Yest.LowHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestRain()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";
			var unitStr = "&nbsp;" + cumulus.Units.RainText;

			json.Append($"[\"{cumulus.Trans.DataCaptions["TotalRain"]}\",\"");
			json.Append(MetData.RainToday.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(MetData.RainYesterday.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiRainRate"]}\",\"");
			json.Append(DailyHighLow.Today.HighRainRate.ToString(cumulus.RainFormat));
			json.Append(unitStr + "/hr");
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.HighRainRateTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighRainRate.ToString(cumulus.RainFormat));
			json.Append(unitStr + "/hr");
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighRainRateTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiHourlyRain"]}\",\"");
			json.Append(DailyHighLow.Today.HighHourlyRain.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.HighHourlyRainTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighHourlyRain.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighHourlyRainTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["Hi24hRain"]}\",\"");
			json.Append(DailyHighLow.Today.HighRain24h.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.HighRain24hTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighRain24h.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighRain24hTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");


			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestWind()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiGust"]}\",\"");
			json.Append(DailyHighLow.Today.HighGust.ToString(cumulus.WindFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.HighGustTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighGust.ToString(cumulus.WindFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighGustTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiWindSpeed"]}\",\"");
			json.Append(DailyHighLow.Today.HighWind.ToString(cumulus.WindAvgFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.HighWindTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighWind.ToString(cumulus.WindAvgFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighWindTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["WindRun"]}\",\"");
			json.Append(MetData.WindRunToday.ToString(cumulus.WindRunFormat));
			json.Append("&nbsp;" + cumulus.Units.WindRunText);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(MetData.YesterdayWindRun.ToString(cumulus.WindRunFormat));
			json.Append("&nbsp;" + cumulus.Units.WindRunText);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["DomDir"]}\",\"");
			json.Append(MetData.DominantWindBearing.ToString("F0"));
			json.Append("&nbsp;&deg;&nbsp;" + CompassPoint(MetData.DominantWindBearing));
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(MetData.YestDominantWindBearing.ToString("F0"));
			json.Append("&nbsp;&deg;&nbsp;" + CompassPoint(MetData.YestDominantWindBearing));
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestPressure()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";
			var unitStr = "&nbsp;" + cumulus.Units.PressText;

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiPress"]}\",\"");
			json.Append(DailyHighLow.Today.HighPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.HighPressTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighPressTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["LoPress"]}\",\"");
			json.Append(DailyHighLow.Today.LowPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.LowPressTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.LowPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.LowPressTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestSolar()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiSolar"]}\",\"");
			json.Append(DailyHighLow.Today.HighSolar.ToString("F0"));
			json.Append("&nbsp;W/m<sup>2</sup>");
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.HighSolarTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighSolar.ToString("F0"));
			json.Append("&nbsp;W/m<sup>2</sup>");
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighSolarTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["SunshineHrs"]}\",\"");
			json.Append(MetData.SunshineHours.ToString(cumulus.SunFormat));
			json.Append("&nbsp;hrs");
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(MetData.YestSunshineHours.ToString(cumulus.SunFormat));
			json.Append("&nbsp;hrs");
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append("\"],");

			json.Append($"[\"{cumulus.Trans.DataCaptions["HiUV"]}\",\"");
			json.Append(DailyHighLow.Today.HighUv.ToString("F1"));
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(DailyHighLow.Today.HighUvTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighUv.ToString("F1"));
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(DailyHighLow.Yest.HighUvTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}
	}
}
