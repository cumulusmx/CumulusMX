using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace CumulusMX
{
	public static class Api
	{
		private const string RelativePath = "/api/";
		internal static WeatherStation Station;
		public static ProgramSettings programSettings;
		internal static StationSettings stationSettings;
		public static InternetSettings internetSettings;
		public static ExtraSensorSettings extraSensorSettings;
		public static CalibrationSettings calibrationSettings;
		public static NOAASettings noaaSettings;
		public static MysqlSettings mySqlSettings;
		internal static AlarmSettings alarmSettings;
		internal static DataEditor dataEditor;
		internal static ApiTagProcessor tagProcessor;


		private static string EscapeUnicode(string input)
		{
			StringBuilder sb = new StringBuilder(input.Length);
			foreach (char ch in input)
			{
				if (ch <= 0x7f)
					sb.Append(ch);
				else
					sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)ch);
			}
			return sb.ToString();
		}

		public static void Setup(WebServer server)
		{
			server.RegisterModule(new WebApiModule());

			server.Module<WebApiModule>().RegisterController<GraphDataController>();
			server.Module<WebApiModule>().RegisterController<DataController>();
			server.Module<WebApiModule>().RegisterController<RecordsController>();
			server.Module<WebApiModule>().RegisterController<TodayYestDataController>();
			server.Module<WebApiModule>().RegisterController<ExtraDataController>();
			server.Module<WebApiModule>().RegisterController<SettingsController>();
			server.Module<WebApiModule>().RegisterController<EditController>();
			server.Module<WebApiModule>().RegisterController<ReportsController>();
			server.Module<WebApiModule>().RegisterController<TagController>();
		}

		// Get/Post Edit data
		public class EditController : WebApiController
		{
			public EditController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "edit/*")]
			public async Task<bool> GetEditData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					string lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "raintodayeditdata.json":
							return await this.JsonResponseAsync(dataEditor.GetRainTodayEditData());

						case "raintoday":
							return await this.JsonResponseAsync(dataEditor.EditRainToday(this));

						case "currentcond.json":
							return await this.JsonResponseAsync(dataEditor.GetCurrentCond());

						case "alltimerecords.json":
							return await this.JsonResponseAsync(dataEditor.GetAllTimeRecData());

						case "alltimerecordsdayfile.json":
							return await this.JsonResponseAsync(dataEditor.GetRecordsDayFile("alltime"));

						case "alltimerecordslogfile.json":
							return await this.JsonResponseAsync(dataEditor.GetRecordsLogFile("alltime"));

						case "monthlyrecords.json":
							return await this.JsonResponseAsync(dataEditor.GetMonthlyRecData());

						case "monthlyrecordsdayfile.json":
							return await this.JsonResponseAsync(dataEditor.GetMonthlyRecDayFile());

						case "monthlyrecordslogfile.json":
							return await this.JsonResponseAsync(dataEditor.GetMonthlyRecLogFile());

						case "thismonthrecords.json":
							return await this.JsonResponseAsync(dataEditor.GetThisMonthRecData());

						case "thismonthrecordsdayfile.json":
							return await this.JsonResponseAsync(dataEditor.GetRecordsDayFile("thismonth"));

						case "thismonthrecordslogfile.json":
							return await this.JsonResponseAsync(dataEditor.GetRecordsLogFile("thismonth"));

						case "thisyearrecords.json":
							return await this.JsonResponseAsync(dataEditor.GetThisYearRecData());

						case "thisyearrecordsdayfile.json":
							return await this.JsonResponseAsync(dataEditor.GetRecordsDayFile("thisyear"));

						case "thisyearrecordslogfile.json":
							return await this.JsonResponseAsync(dataEditor.GetRecordsLogFile("thisyear"));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Post, RelativePath + "edit/*")]
			public async Task<bool> PostEditData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					string lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "raintodayeditdata.json":
							return await this.JsonResponseAsync(dataEditor.GetRainTodayEditData());

						case "raintoday":
							return await this.JsonResponseAsync(dataEditor.EditRainToday(this));

						case "diarydata":
							return await this.JsonResponseAsync(dataEditor.EditDiary(this));

						case "diarydelete":
							return await this.JsonResponseAsync(dataEditor.DeleteDiary(this));

						case "currcond":
							return await this.JsonResponseAsync(dataEditor.EditCurrentCond(this));

						case "alltime":
							return await this.JsonResponseAsync(dataEditor.EditAllTimeRecs(this));

						case "monthly":
							return await this.JsonResponseAsync(dataEditor.EditMonthlyRecs(this));

						case "thismonth":
							return await this.JsonResponseAsync(dataEditor.EditThisMonthRecs(this));

						case "thisyear":
							return await this.JsonResponseAsync(dataEditor.EditThisYearRecs(this));

						case "dayfile":
							return await this.JsonResponseAsync(dataEditor.EditDayFile(this));

						case "datalogs":
							return await this.JsonResponseAsync(dataEditor.EditDatalog(this));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}


		// Get log and diary Data
		public class DataController : WebApiController
		{
			public DataController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "data/*")]
			public async Task<bool> GetData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					var date = query["date"];
					var month = query["month"];
					var draw = query["draw"];
					int start = Convert.ToInt32(query["start"]);
					int length = Convert.ToInt32(query["length"]);

					switch (lastSegment)
					{
						case "dayfile":
							return await this.JsonResponseAsync(Station.GetDayfile(draw,start,length));
						case "logfile":
							return await this.JsonResponseAsync(Station.GetLogfile(month,draw,start,length,false));
						case "extralogfile":
							return await this.JsonResponseAsync(Station.GetLogfile(month, draw, start, length, true));
						case "currentdata":
							return await this.JsonResponseAsync(Station.GetCurrentData());
						case "diarydata":
							return await this.JsonResponseAsync(Station.GetDiaryData(date));
						case "diarysummary":
							//return await this.JsonResponseAsync(Station.GetDiarySummary(year, month));
							return await this.JsonResponseAsync(Station.GetDiarySummary());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}

		// Get/Post Tag body data
		public class TagController : WebApiController
		{
			public TagController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Post, RelativePath + "tags/*")]
			public async Task<bool> PostTags()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					string lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "process.txt":
							return await this.StringResponseAsync(tagProcessor.ProcessText(this));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "tags/*")]
			public async Task<bool> GetTags()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					string lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "process.json":
							return await this.JsonResponseAsync(tagProcessor.ProcessJson(Request.Url.Query));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}

		// Get recent/daily graph data
		public class GraphDataController : WebApiController
		{
			public GraphDataController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "graphdata/*")]
			public async Task<bool> GetGraphData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "tempdata.json":
							return await this.JsonResponseAsync(Station.GetTempGraphData());
						case "winddata.json":
							return await this.JsonResponseAsync(Station.GetWindGraphData());
						case "raindata.json":
							return await this.JsonResponseAsync(Station.GetRainGraphData());
						case "pressdata.json":
							return await this.JsonResponseAsync(Station.GetPressGraphData());
						case "wdirdata.json":
							return await this.JsonResponseAsync(Station.GetWindDirGraphData());
						case "humdata.json":
							return await this.JsonResponseAsync(Station.GetHumGraphData());
						case "solardata.json":
							return await this.JsonResponseAsync(Station.GetSolarGraphData());
						case "dailyrain.json":
							return await this.JsonResponseAsync(Station.GetDailyRainGraphData());
						case "sunhours.json":
							return await this.JsonResponseAsync(Station.GetSunHoursGraphData());
						case "dailytemp.json":
							return await this.JsonResponseAsync(Station.GetDailyTempGraphData());
						case "units.json":
							return await this.JsonResponseAsync(Station.GetUnits());
						case "graphconfig.json":
							return await this.JsonResponseAsync(Station.GetGraphConfig());
						case "airqualitydata.json":
							return await this.JsonResponseAsync(Station.GetAqGraphData());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "dailygraphdata/*")]
			public async Task<bool> GetDailyGraphData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "tempdata.json":
							return await this.JsonResponseAsync(Station.GetAllDailyTempGraphData());
						case "winddata.json":
							return await this.JsonResponseAsync(Station.GetAllDailyWindGraphData());
						case "raindata.json":
							return await this.JsonResponseAsync(Station.GetAllDailyRainGraphData());
						case "pressdata.json":
							return await this.JsonResponseAsync(Station.GetAllDailyPressGraphData());
						//case "wdirdata.json":
						//	return await this.JsonResponseAsync(Station.GetAllDailyWindDirGraphData());
						case "humdata.json":
							return await this.JsonResponseAsync(Station.GetAllDailyHumGraphData());
						case "solardata.json":
							return await this.JsonResponseAsync(Station.GetAllDailySolarGraphData());
						case "units.json":
							return await this.JsonResponseAsync(Station.GetUnits());
						case "graphconfig.json":
							return await this.JsonResponseAsync(Station.GetGraphConfig());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}

		// Get Records data
		public class RecordsController : WebApiController
		{
			public RecordsController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "records/alltime/*")]
			public async Task<bool> GetAlltimeData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temperature.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetTempRecords()));
						case "humidity.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetHumRecords()));
						case "pressure.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetPressRecords()));
						case "wind.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetWindRecords()));
						case "rain.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetRainRecords()));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "records/month/*")]
			public async Task<bool> GetMonthlyRecordData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();
					// Get penultimate segment and trim off traling slash. This gives the required month
					int month = Convert.ToInt32(Request.Url.Segments[Request.Url.Segments.Length - 2].TrimEnd('/'));

					switch (lastSegment)
					{
						case "temperature.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetMonthlyTempRecords(month)));
						case "humidity.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetMonthlyHumRecords(month)));
						case "pressure.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetMonthlyPressRecords(month)));
						case "wind.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetMonthlyWindRecords(month)));
						case "rain.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetMonthlyRainRecords(month)));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "records/thismonth/*")]
			public async Task<bool> GetThisMonthRecordData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temperature.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisMonthTempRecords()));
						case "humidity.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisMonthHumRecords()));
						case "pressure.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisMonthPressRecords()));
						case "wind.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisMonthWindRecords()));
						case "rain.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisMonthRainRecords()));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "records/thisyear/*")]
			public async Task<bool> GetThisYearRecordData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temperature.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisYearTempRecords()));
						case "humidity.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisYearHumRecords()));
						case "pressure.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisYearPressRecords()));
						case "wind.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisYearWindRecords()));
						case "rain.json":
							return await this.JsonResponseAsync(EscapeUnicode(Station.GetThisYearRainRecords()));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}

		// Get today/yesterday data
		public class TodayYestDataController : WebApiController
		{
			public TodayYestDataController(IHttpContext context) : base(context) {}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "todayyest/*")]
			public async Task<bool> GetYesterdayData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temp.json":
							return await this.JsonResponseAsync(Station.GetTodayYestTemp());
						case "hum.json":
							return await this.JsonResponseAsync(Station.GetTodayYestHum());
						case "rain.json":
							return await this.JsonResponseAsync(Station.GetTodayYestRain());
						case "wind.json":
							return await this.JsonResponseAsync(Station.GetTodayYestWind());
						case "pressure.json":
							return await this.JsonResponseAsync(Station.GetTodayYestPressure());
						case "solar.json":
							return await this.JsonResponseAsync(Station.GetTodayYestSolar());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}

		// Get Extra data
		public class ExtraDataController : WebApiController
		{
			public ExtraDataController(IHttpContext context) : base(context) { }

			[WebApiHandler(HttpVerbs.Get, RelativePath + "extra/*")]
			public async Task<bool> GetExtraData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temp.json":
							return await this.JsonResponseAsync(Station.GetExtraTemp());
						case "hum.json":
							return await this.JsonResponseAsync(Station.GetExtraHum());
						case "dew.json":
							return await this.JsonResponseAsync(Station.GetExtraDew());
						case "soiltemp.json":
							return await this.JsonResponseAsync(Station.GetSoilTemp());
						case "soilmoisture.json":
							return await this.JsonResponseAsync(Station.GetSoilMoisture());
						case "leaf.json":
							return await this.JsonResponseAsync(Station.GetLeaf());
						case "leaf4.json":
							return await this.JsonResponseAsync(Station.GetLeaf4());
						case "airqual.json":
							return await this.JsonResponseAsync(Station.GetAirQuality());
						case "lightning.json":
							return await this.JsonResponseAsync(Station.GetLightning());
						case "usertemp.json":
							return await this.JsonResponseAsync(Station.GetUserTemp());

						case "airLinkCountsOut.json":
							return await this.JsonResponseAsync(Station.GetAirLinkCountsOut());
						case "airLinkAqiOut.json":
							return await this.JsonResponseAsync(Station.GetAirLinkAqiOut());
						case "airLinkPctOut.json":
							return await this.JsonResponseAsync(Station.GetAirLinkPctOut());
						case "airLinkCountsIn.json":
							return await this.JsonResponseAsync(Station.GetAirLinkCountsIn());
						case "airLinkAqiIn.json":
							return await this.JsonResponseAsync(Station.GetAirLinkAqiIn());
						case "airLinkPctIn.json":
							return await this.JsonResponseAsync(Station.GetAirLinkPctIn());

					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}

		// Get/Post settings data
		public class SettingsController : WebApiController
		{
			public SettingsController(IHttpContext context) : base(context) { }

			[WebApiHandler(HttpVerbs.Get, RelativePath + "settings/*")]
			public async Task<bool> SettingsGet()
			{
				/* string authorization = context.Request.Headers["Authorization"];
				 string userInfo;
				 string username = "";
				 string password = "";
				 if (authorization != null)
				 {
					 byte[] tempConverted = Convert.FromBase64String(authorization.Replace("Basic ", "").Trim());
					 userInfo = System.Text.Encoding.UTF8.GetString(tempConverted);
					 string[] usernamePassword = userInfo.Split(new string[] {":"}, StringSplitOptions.RemoveEmptyEntries);
					 username = usernamePassword[0];
					 password = usernamePassword[1];
					 Console.WriteLine("username = "+username+" password = "+password);
				 }
				 else
				 {
					 var errorResponse = new
					 {
						 Title = "Authentication required",
						 ErrorCode = "Authentication required",
						 Description = "You must authenticate",
					 };

					 context.Response.StatusCode = 401;
					 return context.JsonResponse(errorResponse);
				 }*/

				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "programdata.json":
							return await this.JsonResponseAsync(programSettings.GetProgramAlpacaFormData());
						case "programoptions.json":
							return await this.JsonResponseAsync(programSettings.GetProgramAlpacaFormOptions());
						case "programschema.json":
							return await this.JsonResponseAsync(programSettings.GetProgramAlpacaFormSchema());

						case "stationdata.json":
							return await this.JsonResponseAsync(stationSettings.GetStationAlpacaFormData());
						case "stationoptions.json":
							return await this.JsonResponseAsync(stationSettings.GetStationAlpacaFormOptions());
						case "stationschema.json":
							return await this.JsonResponseAsync(stationSettings.GetStationAlpacaFormSchema());

						case "internetdata.json":
							return await this.JsonResponseAsync(internetSettings.GetInternetAlpacaFormData());
						case "internetoptions.json":
							return await this.JsonResponseAsync(internetSettings.GetInternetAlpacaFormOptions());
						case "internetschema.json":
							return await this.JsonResponseAsync(internetSettings.GetInternetAlpacaFormSchema());

						case "extrasensordata.json":
							return await this.JsonResponseAsync(extraSensorSettings.GetExtraSensorAlpacaFormData());
						case "extrasensoroptions.json":
							return await this.JsonResponseAsync(extraSensorSettings.GetExtraSensorAlpacaFormOptions());
						case "extrasensorschema.json":
							return await this.JsonResponseAsync(extraSensorSettings.GetExtraSensorAlpacaFormSchema());
						case "extrawebfiles.json":
							return await this.JsonResponseAsync(internetSettings.GetExtraWebFilesData());

						case "calibrationdata.json":
							return await this.JsonResponseAsync(calibrationSettings.GetCalibrationAlpacaFormData());
						case "calibrationoptions.json":
							return await this.JsonResponseAsync(calibrationSettings.GetCalibrationAlpacaFormOptions());
						case "calibrationschema.json":
							return await this.JsonResponseAsync(calibrationSettings.GetCalibrationAlpacaFormSchema());

						case "noaadata.json":
							return await this.JsonResponseAsync(noaaSettings.GetNoaaAlpacaFormData());
						case "noaaoptions.json":
							return await this.JsonResponseAsync(noaaSettings.GetNoaaAlpacaFormOptions());
						case "noaaschema.json":
							return await this.JsonResponseAsync(noaaSettings.GetNoaaAlpacaFormSchema());

						case "wsport.json":
							return await this.JsonResponseAsync(stationSettings.GetWSport());
						case "version.json":
							return await this.JsonResponseAsync(stationSettings.GetVersion());

						case "mysqldata.json":
							return await this.JsonResponseAsync(mySqlSettings.GetMySqlAlpacaFormData());
						case "mysqloptions.json":
							return await this.JsonResponseAsync(mySqlSettings.GetMySqAlpacaFormOptions());
						case "mysqlschema.json":
							return await this.JsonResponseAsync(mySqlSettings.GetMySqAlpacaFormSchema());

						case "alarms.json":
							return await this.JsonResponseAsync(alarmSettings.GetAlarmSettings());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Post, RelativePath + "setsettings/*")]
			public async Task<bool> SettingsSet()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "updateprogramconfig.json":
							return await this.JsonResponseAsync(programSettings.UpdateProgramConfig(this));

						case "updatestationconfig.json":
							return await this.JsonResponseAsync(stationSettings.UpdateStationConfig(this));
						case "updateinternetconfig.json":
							return await this.JsonResponseAsync(internetSettings.UpdateInternetConfig(this));
						case "updateextrasensorconfig.json":
							return await this.JsonResponseAsync(extraSensorSettings.UpdateExtraSensorConfig(this));
						case "updatecalibrationconfig.json":
							return await this.JsonResponseAsync(calibrationSettings.UpdateCalibrationConfig(this));
						case "updatenoaaconfig.json":
							return await this.JsonResponseAsync(noaaSettings.UpdateNoaaConfig(this));
						case "updateextrawebfiles.html":
							return await this.JsonResponseAsync(internetSettings.UpdateExtraWebFiles(this));
						case "updatemysqlconfig.json":
							return await this.JsonResponseAsync(mySqlSettings.UpdateMysqlConfig(this));
						case "createmonthlysql.json":
							return await this.JsonResponseAsync(mySqlSettings.CreateMonthlySQL(this));
						case "createdayfilesql.json":
							return await this.JsonResponseAsync(mySqlSettings.CreateDayfileSQL(this));
						case "createrealtimesql.json":
							return await this.JsonResponseAsync(mySqlSettings.CreateRealtimeSQL(this));
						case "updatealarmconfig.json":
							return await this.JsonResponseAsync(alarmSettings.UpdateAlarmSettings(this));
						case "ftpnow.json":
							return await this.JsonResponseAsync(stationSettings.FtpNow(this));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}

		// Get reports data
		public class ReportsController : WebApiController
		{
			public ReportsController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "reports/*")]
			public async Task<bool> GetData()
			{
				NOAAReports noaarpts = new NOAAReports(Program.cumulus);
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					int month, year;

					if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
						return await this.JsonResponseAsync("Invalid year supplied: " + year);

					switch (lastSegment)
					{
						case "noaayear":
							return await this.JsonResponseAsync(noaarpts.GetNoaaYearReport(year));
						case "noaamonth":
							if (!Int32.TryParse(query["month"], out month) || month < 1 || month > 12)
								return await this.JsonResponseAsync("Invalid month supplied: " + month);
							return await this.JsonResponseAsync(noaarpts.GetNoaaMonthReport(year, month));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "genreports/*")]
			public async Task<bool> GenReports()
			{
				NOAAReports noaarpts = new NOAAReports(Program.cumulus);
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					int month, year;

					if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
						return await this.JsonResponseAsync("Invalid year supplied: " + year);

					switch (lastSegment)
					{
						case "noaayear":
							return await this.JsonResponseAsync(noaarpts.GenerateNoaaYearReport(year));
						case "noaamonth":
							if (!Int32.TryParse(query["month"], out month) || month < 1 || month > 12)
								return await this.JsonResponseAsync("Invalid month supplied: " + month);
							return await this.JsonResponseAsync(noaarpts.GenerateNoaaMonthReport(year, month));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch(Exception ex)
				{
					return await HandleError(ex, 404);
				}
			}

			private async Task<bool> HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return await this.JsonResponseAsync(errorResponse);
			}
		}
	}
}
