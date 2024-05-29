using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;


namespace CumulusMX
{
	public static class Api
	{
		internal static WeatherStation Station { get; set; }
		internal static Cumulus cumulus {  get; set; }
		public static ProgramSettings programSettings { get; set; }
		internal static StationSettings stationSettings { get; set; }
		public static InternetSettings internetSettings { get; set; }
		public static ThirdPartySettings thirdpartySettings { get; set; }
		public static ExtraSensorSettings extraSensorSettings { get; set; }
		public static CalibrationSettings calibrationSettings { get; set; }
		public static NOAASettings noaaSettings { get; set; }
		public static MysqlSettings mySqlSettings { get; set; }
		public static MqttSettings mqttSettings { get; set; }
		public static CustomLogs customLogs { get; set; }
		internal static HttpFiles httpFiles {  get; set; }
		public static Wizard wizard { get; set; }
		internal static LangSettings langSettings {  get; set; }
		internal static DisplaySettings displaySettings {  get; set; }
		internal static AlarmSettings alarmSettings {  get; set; }
		internal static AlarmUserSettings alarmUserSettings {  get; set; }
		internal static DataEditor dataEditor {  get; set; }
		internal static ApiTagProcessor tagProcessor { get; set; }
		internal static HttpStationWund stationWund { get; set; }
		internal static HttpStationEcowitt stationEcowitt { get; set; }
		internal static HttpStationEcowitt stationEcowittExtra { get; set; }
		internal static HttpStationAmbient stationAmbient {  get; set; }
		internal static HttpStationAmbient stationAmbientExtra {  get; set; }
		internal static JsonStation stationJson { get; set; }
		private static readonly char[] separator = [':'];


		// Get/Post Edit data
		public class EditController : WebApiController
		{
			[Route(HttpVerbs.Get, "/edit/{req}")]
			public async Task GetEditData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "raintodayeditdata.json":
							await writer.WriteAsync(dataEditor.GetRainTodayEditData());
							break;
						case "raintoday":
							await writer.WriteAsync(dataEditor.EditRainToday(HttpContext));
							break;
						case "currentcond.json":
							await writer.WriteAsync(dataEditor.GetCurrentCond());
							break;
						case "alltimerecords.json":
							await writer.WriteAsync(dataEditor.GetAllTimeRecData());
							break;
						case "alltimerecordsdayfile.json":
							await writer.WriteAsync(dataEditor.GetRecordsDayFile("alltime"));
							break;
						case "alltimerecordslogfile.json":
							await writer.WriteAsync(dataEditor.GetRecordsLogFile("alltime"));
							break;
						case "monthlyrecords.json":
							await writer.WriteAsync(dataEditor.GetMonthlyRecData());
							break;
						case "monthlyrecordsdayfile.json":
							await writer.WriteAsync(dataEditor.GetMonthlyRecDayFile());
							break;
						case "monthlyrecordslogfile.json":
							await writer.WriteAsync(dataEditor.GetMonthlyRecLogFile());
							break;
						case "thismonthrecords.json":
							await writer.WriteAsync(dataEditor.GetThisMonthRecData());
							break;
						case "thismonthrecordsdayfile.json":
							await writer.WriteAsync(dataEditor.GetRecordsDayFile("thismonth"));
							break;
						case "thismonthrecordslogfile.json":
							await writer.WriteAsync(dataEditor.GetRecordsLogFile("thismonth"));
							break;
						case "thisyearrecords.json":
							await writer.WriteAsync(dataEditor.GetThisYearRecData());
							break;
						case "thisyearrecordsdayfile.json":
							await writer.WriteAsync(dataEditor.GetRecordsDayFile("thisyear"));
							break;
						case "thisyearrecordslogfile.json":
							await writer.WriteAsync(dataEditor.GetRecordsLogFile("thisyear"));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/edit: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Post, "/edit/{req}")]
			public async Task PostEditData(string req)
			{
				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					if (!(await Authenticate(HttpContext)))
					{
						return;
					}

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					string res;
					switch (req)
					{
						case "raintodayeditdata.json":
							await writer.WriteAsync(dataEditor.GetRainTodayEditData());
							break;
						case "raintoday":
							await writer.WriteAsync(dataEditor.EditRainToday(HttpContext));
							break;
						case "diarydata":
							await writer.WriteAsync(dataEditor.EditDiary(HttpContext));
							break;
						case "diarydelete":
							await writer.WriteAsync(dataEditor.DeleteDiary(HttpContext));
							break;
						case "currcond":
							await writer.WriteAsync(dataEditor.EditCurrentCond(HttpContext));
							break;
						case "alltime":
							res = dataEditor.EditAllTimeRecs(HttpContext);
							if (res != "Success")
							{
								Response.StatusCode = 500;
							}
							await writer.WriteAsync(res);
							break;
						case "monthly":
							res = dataEditor.EditMonthlyRecs(HttpContext);
							if (res != "Success")
							{
								Response.StatusCode = 500;
							}
							await writer.WriteAsync(res);
							break;
						case "thismonth":
							res = dataEditor.EditThisMonthRecs(HttpContext);
							if (res != "Success")
							{
								Response.StatusCode = 500;
							}
							await writer.WriteAsync(res);
							break;
						case "thisyear":
							res = dataEditor.EditThisYearRecs(HttpContext);
							if (res != "Success")
							{
								Response.StatusCode = 500;
							}
							await writer.WriteAsync(res);
							break;
						case "dayfile":
							await writer.WriteAsync(dataEditor.EditDayFile(HttpContext));
							break;
						case "datalogs":
							await writer.WriteAsync(dataEditor.EditDatalog(HttpContext));
							break;
						case "mysqlcache":
							await writer.WriteAsync(dataEditor.EditMySqlCache(HttpContext));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/edit: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}


		// Get log and diary Data
		public class DataController : WebApiController
		{
			[Route(HttpVerbs.Get, "/data/{req}")]
			public async Task GetData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					var date = query["date"];
					var from = query["from"];
					var to = query["to"];
					var draw = query["draw"];
					int start = Convert.ToInt32(query["start"]);
					int length = Convert.ToInt32(query["length"]);
					string search = query["search[value]"];

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "dayfile":
							await writer.WriteAsync(Station.GetDayfile(draw, start, length, search));
							break;
						case "logfile":
							await writer.WriteAsync(Station.GetLogfile(from, to, draw, start, length, search, false));
							break;
						case "extralogfile":
							await writer.WriteAsync(Station.GetLogfile(from, to, draw, start, length, search, true));
							break;
						case "currentdata":
							await writer.WriteAsync(Station.GetCurrentData());
							break;
						case "diarydata":
							await writer.WriteAsync(Station.GetDiaryData(date));
							break;
						case "diarysummary":
							await writer.WriteAsync(Station.GetDiarySummary());
							break;
						case "mysqlcache.json":
							if (await Authenticate(HttpContext))
							{
								await writer.WriteAsync(Station.GetCachedSqlCommands(draw, start, length, search));
							}
							break;
						case "errorlog.json":
							if (await Authenticate(HttpContext))
							{
								await writer.WriteAsync(Cumulus.GetErrorLog());
							}
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/data: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}

		// Get/Post Tag body data
		public class TagController : WebApiController
		{
			[Route(HttpVerbs.Post, "/tags/{req}")]
			public async Task PostTags(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "process.txt":
							Response.ContentType = "text/plain";
							await writer.WriteAsync(tagProcessor.ProcessText(HttpContext.Request));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/tags: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"500\",\"Description\":\"{ex.Message}\"}}");
				}
			}

			[Route(HttpVerbs.Get, "/tags/{req}")]
			public async Task GetTags(string req)
			{
				try
				{
					Response.ContentType = "application/json";

					if (Station == null)
					{
						Response.StatusCode = 503;
						using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
						await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
						return;
					}

					using (var writer = HttpContext.OpenResponseText(new UTF8Encoding(false)))
					{
						switch (req)
						{
							case "process.json":
								await writer.WriteAsync(tagProcessor.ProcessJson(Request));
								break;
							default:
								Response.StatusCode = 404;
								break;
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/tags: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"500\",\"Description\":\"{ex.Message}\"}}");
				}
			}
		}

		// Get recent/daily graph data
		public class GraphDataController : WebApiController
		{
			[Route(HttpVerbs.Get, "/graphdata/{req}")]
			public async Task GetGraphData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				var incremental = false;
				DateTime? start = null;
				DateTime? end = null;

				if (Request.QueryString.AllKeys.Contains("start") && long.TryParse(Request.QueryString.Get("start"), out long ts))
				{
					start = Utils.FromUnixTime(ts);
					if (!Request.QueryString.AllKeys.Contains("end"))
						incremental = true;
				}

				if (Request.QueryString.AllKeys.Contains("end") && long.TryParse(Request.QueryString.Get("end"), out ts))
				{
					end = Utils.FromUnixTime(ts);
					if (end > DateTime.Now)
						end = DateTime.Now;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						// recent data
						case "tempdata.json":
							await writer.WriteAsync(Station.GetTempGraphData(incremental, true, start));
							break;
						case "winddata.json":
							await writer.WriteAsync(Station.GetWindGraphData(incremental, start));
							break;
						case "raindata.json":
							await writer.WriteAsync(Station.GetRainGraphData(incremental, start));
							break;
						case "pressdata.json":
							await writer.WriteAsync(Station.GetPressGraphData(incremental, start));
							break;
						case "wdirdata.json":
							await writer.WriteAsync(Station.GetWindDirGraphData(incremental, start));
							break;
						case "humdata.json":
							await writer.WriteAsync(Station.GetHumGraphData(incremental, true, start));
							break;
						case "solardata.json":
							await writer.WriteAsync(Station.GetSolarGraphData(incremental, true, start));
							break;
						case "airqualitydata.json":
							await writer.WriteAsync(Station.GetAqGraphData(incremental, start));
							break;
						case "extratemp.json":
							await writer.WriteAsync(Station.GetExtraTempGraphData(incremental, true, start));
							break;
						case "extrahum.json":
							await writer.WriteAsync(Station.GetExtraHumGraphData(incremental, true, start));
							break;
						case "extradew.json":
							await writer.WriteAsync(Station.GetExtraDewPointGraphData(incremental, true, start));
							break;
						case "soiltemp.json":
							await writer.WriteAsync(Station.GetSoilTempGraphData(incremental, true, start));
							break;
						case "soilmoist.json":
							await writer.WriteAsync(Station.GetSoilMoistGraphData(incremental, true, start));
							break;
						case "leafwetness.json":
							await writer.WriteAsync(Station.GetLeafWetnessGraphData(incremental, true, start));
							break;
						case "usertemp.json":
							await writer.WriteAsync(Station.GetUserTempGraphData(incremental, true, start));
							break;
						case "co2sensor.json":
							await writer.WriteAsync(Station.GetCo2SensorGraphData(incremental, true, start));
							break;

						// daily data
						case "dailyrain.json":
							await writer.WriteAsync(Station.GetDailyRainGraphData());
							break;
						case "sunhours.json":
							await writer.WriteAsync(Station.GetSunHoursGraphData(true));
							break;
						case "dailytemp.json":
							await writer.WriteAsync(Station.GetDailyTempGraphData(true));
							break;
						case "units.json":
							await writer.WriteAsync(Station.GetUnits());
							break;

						// interval data
						case "intvtemp.json":
							await writer.WriteAsync(Station.GetIntervalTempGraphData(true, start, end));
							break;
						case "intvwind.json":
							await writer.WriteAsync(Station.GetIntervalWindGraphData(start, end));
							break;
						case "intvrain.json":
							await writer.WriteAsync(Station.GetIntervalRainGraphData(start, end));
							break;
						case "intvpress.json":
							await writer.WriteAsync(Station.GetIntervalPressGraphData(start, end));
							break;
						case "intvhum.json":
							await writer.WriteAsync(Station.GetIntervalHumGraphData(true, start, end));
							break;
						case "intvsolar.json":
							await writer.WriteAsync(Station.GetIntervalSolarGraphData(true, start, end));
							break;
						case "intvairquality.json":
							await writer.WriteAsync(Station.GetAqGraphData(false, start, end));
							break;
						case "intvextratemp.json":
							await writer.WriteAsync(Station.GetExtraTempGraphData(false, true, start, end));
							break;
						case "intvextrahum.json":
							await writer.WriteAsync(Station.GetExtraHumGraphData(false, true, start, end));
							break;
						case "intvextradew.json":
							await writer.WriteAsync(Station.GetExtraDewPointGraphData(false, true, start, end));
							break;
						case "intvsoiltemp.json":
							await writer.WriteAsync(Station.GetSoilTempGraphData(false, true, start, end));
							break;
						case "intvsoilmoist.json":
							await writer.WriteAsync(Station.GetSoilMoistGraphData(false, true, start, end));
							break;
						case "intvleafwetness.json":
							await writer.WriteAsync(Station.GetLeafWetnessGraphData(false, true, start, end));
							break;
						case "intvusertemp.json":
							await writer.WriteAsync(Station.GetUserTempGraphData(false, true, start, end));
							break;
						case "intvco2sensor.json":
							await writer.WriteAsync(Station.GetCo2SensorGraphData(false, true, start, end));
							break;

						// config data
						case "graphconfig.json":
							await writer.WriteAsync(Station.GetGraphConfig(true));
							break;
						case "availabledata.json":
							await writer.WriteAsync(Station.GetAvailGraphData(true));
							break;
						case "selectachart.json":
							await writer.WriteAsync(Station.GetSelectaChartOptions());
							break;
						case "selectaperiod.json":
							await writer.WriteAsync(Station.GetSelectaPeriodOptions());
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/graphdata: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Post, "/graphdata/{req}")]
			public async Task SetGraphData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "selectachart.json":
							await writer.WriteAsync(stationSettings.SetSelectaChartOptions(HttpContext));
							break;
						case "selectaperiod.json":
							await writer.WriteAsync(stationSettings.SetSelectaPeriodOptions(HttpContext));
							break;
						default:
							Response.StatusCode = 404;
							break;

					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/graphdata: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Get, "/dailygraphdata/{req}")]
			public async Task GetDailyGraphData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "tempdata.json":
							await writer.WriteAsync(Station.GetAllDailyTempGraphData(true));
							break;
						case "winddata.json":
							await writer.WriteAsync(Station.GetAllDailyWindGraphData());
							break;
						case "raindata.json":
							await writer.WriteAsync(Station.GetAllDailyRainGraphData());
							break;
						case "pressdata.json":
							await writer.WriteAsync(Station.GetAllDailyPressGraphData());
							break;
						case "wdirdata.json":
							await writer.WriteAsync(Station.GetAllDailyWindDirGraphData());
							break;
						case "humdata.json":
							await writer.WriteAsync(Station.GetAllDailyHumGraphData());
							break;
						case "solardata.json":
							await writer.WriteAsync(Station.GetAllDailySolarGraphData(true));
							break;
						case "degdaydata.json":
							await writer.WriteAsync(Station.GetAllDegreeDaysGraphData(true));
							break;
						case "tempsumdata.json":
							await writer.WriteAsync(Station.GetAllTempSumGraphData(true));
							break;
						case "units.json":
							await writer.WriteAsync(Station.GetUnits());
							break;
						case "graphconfig.json":
							await writer.WriteAsync(Station.GetGraphConfig(true));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/dailygraphdata: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}

		// Get Records data
		public class RecordsController : WebApiController
		{
			[Route(HttpVerbs.Get, "/records/alltime/{req}")]
			public async Task GetAlltimeData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync("{}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "temperature.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetTempRecords()));
							break;
						case "humidity.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetHumRecords()));
							break;
						case "pressure.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetPressRecords()));
							break;
						case "wind.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetWindRecords()));
							break;
						case "rain.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetRainRecords()));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/records/alltime: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Get, "/records/month/{mon}/{req}")]
			public async Task GetMonthlyRecordData(string mon, string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					int month = Convert.ToInt32(mon);

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					if (month < 1 || month > 12)
					{
						await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"406\",\"Description\":\"Month value is out of range\"}}");
						Response.StatusCode = 406;
					}

					switch (req)
					{
						case "temperature.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetMonthlyTempRecords(month)));
							break;
						case "humidity.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetMonthlyHumRecords(month)));
							break;
						case "pressure.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetMonthlyPressRecords(month)));
							break;
						case "wind.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetMonthlyWindRecords(month)));
							break;
						case "rain.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetMonthlyRainRecords(month)));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/records/month: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Get, "/records/thismonth/{req}")]
			public async Task GetThisMonthRecordData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "temperature.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisMonthTempRecords()));
							break;
						case "humidity.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisMonthHumRecords()));
							break;
						case "pressure.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisMonthPressRecords()));
							break;
						case "wind.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisMonthWindRecords()));
							break;
						case "rain.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisMonthRainRecords()));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/records/thismonth: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Get, "/records/thisyear/{req}")]
			public async Task GetThisYearRecordData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync("{}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "temperature.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisYearTempRecords()));
							break;
						case "humidity.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisYearHumRecords()));
							break;
						case "pressure.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisYearPressRecords()));
							break;
						case "wind.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisYearWindRecords()));
							break;
						case "rain.json":
							await writer.WriteAsync(EscapeUnicode(Station.GetThisYearRainRecords()));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/records/thisyear: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Get, "/records/thisperiod")]
			public async Task GetThisPeriodRecordData()
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync("{}");
					return;
				}

				try
				{
					int startday, startmonth, startyear;
					int endday, endmonth, endyear;

					var query = HttpUtility.ParseQueryString(Request.Url.Query);

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					if (query.AllKeys.Contains("startdate"))
					{
						// we expect "yyyy-mm-dd"
						var start = query["startdate"].Split('-');

						if (!Int32.TryParse(start[0], out startyear) || startyear < 2000 || startyear > 2050)
						{
							await writer.WriteAsync("Invalid start year supplied: " + startyear);
							Response.StatusCode = 406;
							return;
						}

						if (!Int32.TryParse(start[1], out startmonth) || startmonth < 1 || startmonth > 12)
						{
							await writer.WriteAsync("Invalid start month supplied: " + startmonth);
							Response.StatusCode = 406;
							return;
						}

						if (!Int32.TryParse(start[2], out startday) || startday < 1 || startday > 31)
						{
							await writer.WriteAsync("Invalid start day supplied: " + startday);
							Response.StatusCode = 406;
							return;
						}
					}
					else
					{
						await writer.WriteAsync("No start date supplied: ");
						Response.StatusCode = 406;
						return;
					}

					if (query.AllKeys.Contains("enddate"))
					{
						// we expect "yyyy-mm-dd"
						var end = query["enddate"].Split('-');

						if (!Int32.TryParse(end[0], out endyear) || endyear < 2000 || endyear > 2050)
						{
							await writer.WriteAsync("Invalid end year supplied: " + endyear);
							Response.StatusCode = 406;
							return;
						}

						if (!Int32.TryParse(end[1], out endmonth) || endmonth < 1 || endmonth > 12)
						{
							await writer.WriteAsync("Invalid end month supplied: " + endmonth);
							Response.StatusCode = 406;
							return;
						}

						if (!Int32.TryParse(end[2], out endday) || endday < 1 || endday > 31)
						{
							await writer.WriteAsync("Invalid end day supplied: " + endday);
							Response.StatusCode = 406;
							return;
						}
					}
					else
					{
						await writer.WriteAsync("No start date supplied: ");
						Response.StatusCode = 406;
						return;
					}

					var startDate = new DateTime(startyear, startmonth, startday, 0, 0, 0, DateTimeKind.Local);
					var endDate = new DateTime(endyear, endmonth, endday, 0, 0, 0, DateTimeKind.Local);


					await writer.WriteAsync(EscapeUnicode(dataEditor.GetRecordsDayFile("thisperiod", startDate, endDate)));
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/records/thisperiod: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			private static string EscapeUnicode(string input)
			{
				StringBuilder sb = new StringBuilder(input.Length);
				foreach (char ch in input)
				{
					if (ch <= 0x7f)
						sb.Append(ch);
					else
						sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int) ch);
				}
				return sb.ToString();
			}
		}

		// Get today/yesterday data
		public class TodayYestDataController : WebApiController
		{
			[Route(HttpVerbs.Get, "/todayyest/{req}")]
			public async Task GetYesterdayData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "temp.json":
							await writer.WriteAsync(Station.GetTodayYestTemp());
							break;
						case "hum.json":
							await writer.WriteAsync(Station.GetTodayYestHum());
							break;
						case "rain.json":
							await writer.WriteAsync(Station.GetTodayYestRain());
							break;
						case "wind.json":
							await writer.WriteAsync(Station.GetTodayYestWind());
							break;
						case "pressure.json":
							await writer.WriteAsync(Station.GetTodayYestPressure());
							break;
						case "solar.json":
							await writer.WriteAsync(Station.GetTodayYestSolar());
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/todayyest: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}

		// Get Extra data
		public class ExtraDataController : WebApiController
		{
			[Route(HttpVerbs.Get, "/extra/{req}")]
			public async Task GetExtraData(string req)
			{
				Response.ContentType = "application/json";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync($"{{\"Title\":\"Unexpected Error\",\"ErrorCode\":\"503\",\"Description\":\"The station is not running\"}}");
					return;
				}

				try
				{
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "temp.json":
							await writer.WriteAsync(Station.GetExtraTemp());
							break;
						case "hum.json":
							await writer.WriteAsync(Station.GetExtraHum());
							break;
						case "dew.json":
							await writer.WriteAsync(Station.GetExtraDew());
							break;
						case "soiltemp.json":
							await writer.WriteAsync(Station.GetSoilTemp());
							break;
						case "soilmoisture.json":
							await writer.WriteAsync(Station.GetSoilMoisture());
							break;
						case "leaf8.json":
							await writer.WriteAsync(Station.GetLeaf8(true));
							break;
						case "airqual.json":
							await writer.WriteAsync(Station.GetAirQuality(true));
							break;
						case "lightning.json":
							await writer.WriteAsync(Station.GetLightning());
							break;
						case "usertemp.json":
							await writer.WriteAsync(Station.GetUserTemp());
							break;

						case "airLinkCountsOut.json":
							await writer.WriteAsync(Station.GetAirLinkCountsOut());
							break;
						case "airLinkAqiOut.json":
							await writer.WriteAsync(Station.GetAirLinkAqiOut());
							break;
						case "airLinkPctOut.json":
							await writer.WriteAsync(Station.GetAirLinkPctOut());
							break;
						case "airLinkCountsIn.json":
							await writer.WriteAsync(Station.GetAirLinkCountsIn());
							break;
						case "airLinkAqiIn.json":
							await writer.WriteAsync(Station.GetAirLinkAqiIn());
							break;
						case "airLinkPctIn.json":
							await writer.WriteAsync(Station.GetAirLinkPctIn());
							break;

						case "co2sensor.json":
							await writer.WriteAsync(Station.GetCO2sensor(true));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/extra: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}

		// Get/Post settings data
		public class SettingsController : WebApiController
		{
			[Route(HttpVerbs.Get, "/settings/{req}")]
			public async Task SettingsGet(string req)
			{
				try
				{
					Response.ContentType = "application/json";

					if (!(await Authenticate(HttpContext)))
					{
						return;
					}

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "programdata.json":
							await writer.WriteAsync(programSettings.GetAlpacaFormData());
							break;
						case "stationdata.json":
							await writer.WriteAsync(stationSettings.GetAlpacaFormData());
							break;
						case "internetdata.json":
							await writer.WriteAsync(internetSettings.GetAlpacaFormData());
							break;
						case "thirdpartydata.json":
							await writer.WriteAsync(thirdpartySettings.GetAlpacaFormData());
							break;
						case "extrasensordata.json":
							await writer.WriteAsync(extraSensorSettings.GetAlpacaFormData());
							break;
						case "extrawebfiles.json":
							await writer.WriteAsync(internetSettings.GetExtraWebFilesData());
							break;
						case "calibrationdata.json":
							await writer.WriteAsync(calibrationSettings.GetAlpacaFormData());
							break;
						case "langdata.json":
							await writer.WriteAsync(langSettings.GetAlpacaFormData());
							break;
						case "noaadata.json":
							await writer.WriteAsync(noaaSettings.GetAlpacaFormData());
							break;
						case "mysqldata.json":
							await writer.WriteAsync(mySqlSettings.GetAlpacaFormData());
							break;
						case "alarms.json":
							await writer.WriteAsync(alarmSettings.GetAlarmSettings());
							break;
						case "useralarms.json":
							await writer.WriteAsync(alarmUserSettings.GetAlpacaFormData());
							break;
						case "wizard.json":
							await writer.WriteAsync(wizard.GetAlpacaFormData());
							break;
						case "customlogsintvl.json":
							await writer.WriteAsync(customLogs.GetAlpacaFormDataIntvl());
							break;
						case "customlogsdaily.json":
							await writer.WriteAsync(customLogs.GetAlpacaFormDataDaily());
							break;
						case "displayoptions.json":
							await writer.WriteAsync(displaySettings.GetAlpacaFormData());
							break;
						case "httpfiles.json":
							await writer.WriteAsync(httpFiles.GetAlpacaFormData());
							break;
						case "mqttdata.json":
							await writer.WriteAsync(mqttSettings.GetAlpacaFormData());
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/settings: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Post, "/setsettings/{req}")]
			public async Task SettingsSet(string req)
			{
				try
				{
					Response.ContentType = "application/json";

					if (!(await Authenticate(HttpContext)))
					{
						return;
					}

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "updateprogramconfig.json":
							await writer.WriteAsync(programSettings.UpdateConfig(HttpContext));
							break;
						case "updatestationconfig.json":
							await writer.WriteAsync(stationSettings.UpdateConfig(HttpContext));
							break;
						case "updateinternetconfig.json":
							await writer.WriteAsync(internetSettings.UpdateConfig(HttpContext));
							break;
						case "updatethirdpartyconfig.json":
							await writer.WriteAsync(thirdpartySettings.UpdateConfig(HttpContext));
							break;
						case "updateextrasensorconfig.json":
							await writer.WriteAsync(extraSensorSettings.UpdateConfig(HttpContext));
							break;
						case "updatecalibrationconfig.json":
							await writer.WriteAsync(calibrationSettings.UpdateConfig(HttpContext));
							break;
						case "updatenoaaconfig.json":
							await writer.WriteAsync(noaaSettings.UpdateConfig(HttpContext));
							break;
						case "updateextrawebfiles.html":
							await writer.WriteAsync(internetSettings.UpdateExtraWebFiles(HttpContext));
							break;
						case "updatemysqlconfig.json":
							await writer.WriteAsync(mySqlSettings.UpdateConfig(HttpContext));
							break;
						case "createmonthlysql.json":
							await writer.WriteAsync(mySqlSettings.CreateMonthlySQL());
							break;
						case "createdayfilesql.json":
							await writer.WriteAsync(mySqlSettings.CreateDayfileSQL());
							break;
						case "createrealtimesql.json":
							await writer.WriteAsync(mySqlSettings.CreateRealtimeSQL());
							break;
						case "updatemonthlysql.json":
							await writer.WriteAsync(mySqlSettings.UpdateMonthlySQL());
							break;
						case "updatedayfilesql.json":
							await writer.WriteAsync(mySqlSettings.UpdateDayfileSQL());
							break;
						case "updaterealtimesql.json":
							await writer.WriteAsync(mySqlSettings.UpdateRealtimeSQL());
							break;
						case "updatealarmconfig.json":
							await writer.WriteAsync(alarmSettings.UpdateAlarmSettings(HttpContext));
							break;
						case "updateuseralarms.json":
							await writer.WriteAsync(alarmUserSettings.UpdateConfig(HttpContext));
							break;
						case "testemail.json":
							await writer.WriteAsync(alarmSettings.TestEmail(HttpContext));
							break;
						case "wizard.json":
							await writer.WriteAsync(wizard.UpdateConfig(HttpContext));
							break;
						case "updatecustomlogsintvl.json":
							await writer.WriteAsync(customLogs.UpdateConfigIntvl(HttpContext));
							break;
						case "updatecustomlogsdaily.json":
							await writer.WriteAsync(customLogs.UpdateConfigDaily(HttpContext));
							break;
						case "updatedisplay.json":
							await writer.WriteAsync(displaySettings.UpdateConfig(HttpContext));
							break;
						case "updatelanguage.json":
							await writer.WriteAsync(langSettings.UpdateConfig(HttpContext));
							break;
						case "updatehttpfiles.json":
							await writer.WriteAsync(httpFiles.UpdateConfig(HttpContext));
							break;
						case "updatemqttconfig.json":
							await writer.WriteAsync(mqttSettings.UpdateConfig(HttpContext));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/setsettings: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}

		// Get reports data
		public class ReportsController : WebApiController
		{
			[Route(HttpVerbs.Get, "/reports/{req}")]
			public async Task GetData(string req)
			{
				NOAAReports noaarpts = new NOAAReports(cumulus, Station);
				try
				{
					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					int month, year;

					Response.ContentType = "text/plain";

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));

					if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
					{
						await writer.WriteAsync("Invalid year supplied: " + year);
						Response.StatusCode = 406;
						return;
					}

					switch (req)
					{
						case "noaayear":
							await writer.WriteAsync(noaarpts.GetNoaaYearReport(year));
							break;
						case "noaamonth":
							if (!Int32.TryParse(query["month"], out month) || month < 1 || month > 12)
							{
								await writer.WriteAsync("Invalid month supplied: " + month);
								Response.StatusCode = 406;
								return;
							}
							await writer.WriteAsync(noaarpts.GetNoaaMonthReport(year, month));
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/reports: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Get, "/genreports/{req}")]
			public async Task GenReports(string req)
			{
				NOAAReports noaarpts = new NOAAReports(cumulus, Station);
				try
				{
					if (!(await Authenticate(HttpContext)))
					{
						return;
					}

					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					int month, year;
					Response.ContentType = "text/plain";

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "noaayear":
							if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
							{
								await writer.WriteAsync("Invalid year supplied: " + year);
								Response.StatusCode = 406;
								return;
							}
							await writer.WriteAsync(noaarpts.GenerateNoaaYearReport(year));
							break;
						case "noaamonth":
							if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
							{
								await writer.WriteAsync("Invalid year supplied: " + year);
								Response.StatusCode = 406;
								return;
							}
							if (!Int32.TryParse(query["month"], out month) || month < 1 || month > 12)
							{
								await writer.WriteAsync("Invalid month supplied: " + month);
								Response.StatusCode = 406;
								return;
							}
							await writer.WriteAsync(noaarpts.GenerateNoaaMonthReport(year, month));
							break;
						case "all":
							await writer.WriteAsync(noaarpts.GenerateMissing());
							break;
						default:
							Response.StatusCode = 404;
							throw new ArgumentException("Unknown request: " + req);
					}
				}
				catch (Exception ex)
				{
					Response.StatusCode = 500;
					cumulus.LogErrorMessage($"api/genreports: Unexpected Error, ErrorCode: {ex.GetType().Name}, Description: \"{ex.Message}\"");
				}
			}

			[Route(HttpVerbs.Get, "/uploadreport/{req}")]
			public async Task UploadReport(string req)
			{
				try
				{
					NOAAReports noaarpts = new NOAAReports(cumulus, Station);

					if (!(await Authenticate(HttpContext)))
					{
						return;
					}

					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					int month, year;
					Response.ContentType = "text/plain";

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "noaayear":
							if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
							{
								await writer.WriteAsync("Invalid year supplied: " + year);
								Response.StatusCode = 406;
								return;
							}
							await writer.WriteAsync(noaarpts.UploadNoaaReport(year));
							break;
						case "noaamonth":
							if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
							{
								await writer.WriteAsync("Invalid year supplied: " + year);
								Response.StatusCode = 406;
								return;
							}
							if (!Int32.TryParse(query["month"], out month) || month < 1 || month > 12)
							{
								await writer.WriteAsync("Invalid month supplied: " + month);
								Response.StatusCode = 406;
								return;
							}
							await writer.WriteAsync(noaarpts.UploadNoaaReport(year, month));
							break;
						default:
							Response.StatusCode = 404;
							throw new ArgumentException("Unknown request: " + req);
					}
				}
				catch (Exception ex)
				{
					Response.StatusCode = 500;
					cumulus.LogErrorMessage($"api/uploadreport: Unexpected Error, ErrorCode: {ex.GetType().Name}, Description: \"{ex.Message}\"");
				}
			}
		}

		// HTTP Station
		public class HttpStation : WebApiController
		{
			[Route(HttpVerbs.Post, "/{req}")]
			public async Task PostStation(string req)
			{
				try
				{
					Response.ContentType = "application/json";

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "ecowitt":
							if (stationEcowitt != null)
							{
								await writer.WriteAsync(stationEcowitt.ProcessData(HttpContext, true));
							}
							else
							{
								Response.StatusCode = 503;
								await writer.WriteAsync("{\"Error\":\"HTTP Station (Ecowitt) is not running}\"");
							}
							break;
						case "ecowittextra":
							if (stationEcowittExtra != null)
							{
								await writer.WriteAsync(stationEcowittExtra.ProcessData(HttpContext, false));
							}
							else
							{
								Response.StatusCode = 503;
								await writer.WriteAsync("{\"Error\":\"HTTP Station (Ecowitt) is not running}\"");
							}
							break;
						case "json":
							if (stationJson != null)
							{
								await writer.WriteAsync(stationJson.ReceiveDataFromApi(HttpContext, false));
							}
							else
							{
								Response.StatusCode = 503;
								await writer.WriteAsync("{\"Error\":\"JSON Station is not running}\"");
							}
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/httpstation: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Get, "/{req}")]
			public async Task GetStation(string req)
			{
				try
				{
					Response.ContentType = "application/json";

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));

					switch (req)
					{
						case "wunderground":
							if (stationWund != null)
							{
								await writer.WriteAsync(stationWund.ProcessData(HttpContext));
							}
							else
							{
								Response.StatusCode = 503;
								await writer.WriteAsync("HTTP Station (Wunderground) is not running");
							}
							break;

						case "ambient":
							if (stationAmbient != null)
							{
								await writer.WriteAsync(stationAmbient.ProcessData(HttpContext, true));
							}
							else
							{
								Response.StatusCode = 503;
								await writer.WriteAsync("HTTP Station (Ambient) is not running");
							}
							break;

						case "ambientextra":
							if (stationAmbientExtra != null)
							{
								await writer.WriteAsync(stationAmbient.ProcessData(HttpContext, false));
							}
							else
							{
								Response.StatusCode = 503;
								await writer.WriteAsync("HTTP Station (Ambient) is not running");
							}
							break;
						case "ecowitt":
							if (stationEcowitt != null)
							{
								await writer.WriteAsync(stationEcowitt.ProcessData(HttpContext, true));
							}
							else
							{
								Response.StatusCode = 503;
								await writer.WriteAsync("{\"Error\":\"HTTP Station (Ecowitt) is not running}\"");
							}
							break;
						case "ecowittextra":
							if (stationEcowittExtra != null)
							{
								await writer.WriteAsync(stationEcowittExtra.ProcessData(HttpContext, false));
							}
							else
							{
								Response.StatusCode = 503;
								await writer.WriteAsync("{\"Error\":\"HTTP Station (Ecowitt) is not running}\"");
							}
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/httpstation: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}

		// Utilities
		public class UtilsController : WebApiController
		{
			[Route(HttpVerbs.Get, "/utils/{req}")]
			public async Task GetUtilData(string req)
			{
				Response.ContentType = "plain/text";

				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync("The station is not running");
					return;
				}

				try
				{
					if (!(await Authenticate(HttpContext)))
					{
						return;
					}

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "reloaddayfile":
							await writer.WriteAsync(Station.LoadDayFile());
							break;
						case "purgemysql":
							var cnt = cumulus.MySqlFailedList.Count;
							cumulus.MySqlFailedList.Clear();
							_ = Station.RecentDataDb.Execute("DELETE FROM SqlCache");
							string msg;
							if (cnt == 0)
							{
								msg = "The MySQL cache is already empty!";
							}
							else
							{
								msg = $"Cached MySQL queue cleared of {cnt} commands";
							}
							await writer.WriteAsync(msg);
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/utils: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}

			[Route(HttpVerbs.Post, "/utils/{req}")]
			public async Task PostUtilsData(string req)
			{
				if (Station == null)
				{
					Response.StatusCode = 503;
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync("The station is not running");
					return;
				}

				try
				{
					if (!(await Authenticate(HttpContext)))
					{
						return;
					}

					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "ftpnow.json":
							await writer.WriteAsync(stationSettings.UploadNow(HttpContext));
							break;
						case "clearerrorlog.json":
							await writer.WriteAsync(Cumulus.ClearErrorLog());
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/edit: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}

		// Info
		public class InfoController : WebApiController
		{
			[Route(HttpVerbs.Get, "/info/{req}")]
			public async Task InfoGet(string req)
			{
				try
				{
					Response.ContentType = "application/json";
					using var writer = HttpContext.OpenResponseText(new UTF8Encoding(false));
					switch (req)
					{
						case "wsport.json":
							await writer.WriteAsync(stationSettings.GetWSport());
							break;
						case "version.json":
							await writer.WriteAsync(stationSettings.GetVersion());
							break;
						case "dateformat.txt":
							Response.ContentType = "text/plain";
							await writer.WriteAsync(CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
							break;
						case "csvseparator.txt":
							Response.ContentType = "text/plain";
							await writer.WriteAsync(',');
							break;
						case "alarms.json":
							await writer.WriteAsync(alarmSettings.GetAlarmInfo());
							break;
						case "units.json":
							await writer.WriteAsync(Station.GetUnits());
							break;
						default:
							Response.StatusCode = 404;
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"api/info: Unexpected Error, Description: \"{ex.Message}\"");
					Response.StatusCode = 500;
				}
			}
		}
		private static async Task<bool> Authenticate(IHttpContext context)
		{
			string authorization = context.Request.Headers["Authorization"];
			string userInfo;
			string username;
			string password;

			if (cumulus.ProgramOptions.SecureSettings)
			{
				if (authorization != null)
				{
					byte[] tempConverted = Convert.FromBase64String(authorization.Replace("Basic ", "").Trim());
					userInfo = Encoding.UTF8.GetString(tempConverted);
					string[] usernamePassword = userInfo.Split(separator);
					username = usernamePassword[0] ?? string.Empty;
					password = usernamePassword[1] ?? string.Empty;

					if (username == cumulus.ProgramOptions.SettingsUsername && password == cumulus.ProgramOptions.SettingsPassword)
					{
						return true;
					}
					else
					{
						context.Response.StatusCode = 401;
						context.Response.ContentType = "application/json";
						context.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"My Realm\"");
						using var writer = context.OpenResponseText(new UTF8Encoding(false));
						await writer.WriteAsync("{\"Title\":\"Authentication required\",\"ErrorCode\":\"Authentication required\",\"Description\":\"You must authenticate\"}");

						return false;
					}
				}
				else
				{
					context.Response.StatusCode = 401;
					context.Response.ContentType = "application/json";
					context.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"My Realm\"");
					using var writer = context.OpenResponseText(new UTF8Encoding(false));
					await writer.WriteAsync("{\"Title\":\"Authentication required\",\"ErrorCode\":\"Authentication required\",\"Description\":\"You must authenticate\"}");

					return false;
				}
			}

			return true;
		}
	}
}
