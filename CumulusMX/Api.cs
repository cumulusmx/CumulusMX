using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Swan;

namespace CumulusMX
{
	public static class Api
	{
		private const string RelativePath = "/api/";
		internal static WeatherStation Station;
		public static StationSettings stationSettings;
		public static InternetSettings internetSettings;
		public static CalibrationSettings calibrationSettings;
		public static NOAASettings noaaSettings;
		public static MysqlSettings mySqlSettings;
		internal static AlarmSettings alarmSettings;
		internal static DataEditor dataEditor;

		public static string Utf16ToUtf8(string utf16String)
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
			server.Module<WebApiModule>().RegisterController<GetSettingsController>();
			server.Module<WebApiModule>().RegisterController<SetSettingsController>();
			server.Module<WebApiModule>().RegisterController<EditControllerGet>();
			server.Module<WebApiModule>().RegisterController<EditControllerPost>();
			server.Module<WebApiModule>().RegisterController<ReportsController>();
		}

		public class EditControllerGet : WebApiController
		{
			public EditControllerGet(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "edit/*")]
			public bool EditData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					string lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "raintodayeditdata.json":
							return this.JsonResponse(dataEditor.GetRainTodayEditData());

						case "raintoday":
							return this.JsonResponse(dataEditor.EditRainToday(this));

						case "currentcond.json":
							return this.JsonResponse(dataEditor.GetCurrentCond());

						case "alltimerecords.json":
							return this.JsonResponse(dataEditor.GetAllTimeRecData());

						case "alltimerecordsdayfile.json":
							return this.JsonResponse(dataEditor.GetAllTimeRecDayFile());

						case "alltimerecordslogfile.json":
							return this.JsonResponse(dataEditor.GetAllTimeRecLogFile());

						case "monthlyrecords.json":
							return this.JsonResponse(dataEditor.GetMonthlyRecData());

						case "monthlyrecordsdayfile.json":
							return this.JsonResponse(dataEditor.GetMonthlyRecDayFile());

						case "monthlyrecordslogfile.json":
							return this.JsonResponse(dataEditor.GetMonthlyRecLogFile());

					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}

			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class EditControllerPost : WebApiController
		{
			public EditControllerPost(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Post, RelativePath + "edit/*")]
			public bool EditData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					string lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "raintodayeditdata.json":
							return this.JsonResponse(dataEditor.GetRainTodayEditData());

						case "raintoday":
							return this.JsonResponse(dataEditor.EditRainToday(this));

						case "diarydata":
							return this.JsonResponse(dataEditor.EditDiary(this));

						case "diarydelete":
							return this.JsonResponse(dataEditor.DeleteDiary(this));

						case "currcond":
							return this.JsonResponse(dataEditor.EditCurrentCond(this));

						case "alltime":
							return this.JsonResponse(dataEditor.EditAllTimeRecs(this));

						case "monthly":
							return this.JsonResponse(dataEditor.EditMonthlyRecs(this));

					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}

			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class DataController : WebApiController
		{
			public DataController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "data/*")]
			public bool GetData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					var date = query["date"];
					var year = query["year"];
					var month = query["month"];
					var draw = query["draw"];
					int start = Convert.ToInt32(query["start"]);
					int length = Convert.ToInt32(query["length"]);

					switch (lastSegment)
					{
						case "dayfile":
							return this.JsonResponse(Station.GetDayfile(draw,start,length));
						case "logfile":
							return this.JsonResponse(Station.GetLogfile(month,draw,start,length,false));
						case "extralogfile":
							return this.JsonResponse(Station.GetLogfile(month, draw, start, length, true));
						case "currentdata":
							return this.JsonResponse(Station.GetCurrentData());
						case "diarydata":
							return this.JsonResponse(Station.GetDiaryData(date));
						case "diarysummary":
							//return this.JsonResponse(Station.GetDiarySummary(year, month));
							return this.JsonResponse(Station.GetDiarySummary());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class GraphDataController : WebApiController
		{
			public GraphDataController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "graphdata/*")]
			public bool GetGraphData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "tempdata.json":
							return this.JsonResponse(Station.GetTempGraphData());
						case "tempdatad3.json":
							return this.JsonResponse(Station.GetTempGraphDataD3());
						case "winddata.json":
							return this.JsonResponse(Station.GetWindGraphData());
						case "winddatad3.json":
							return this.JsonResponse(Station.GetWindGraphDataD3());
						case "raindata.json":
							return this.JsonResponse(Station.GetRainGraphData());
						case "raindatad3.json":
							return this.JsonResponse(Station.GetRainGraphDataD3());
						case "pressdata.json":
							return this.JsonResponse(Station.GetPressGraphData());
						case "pressdatad3.json":
							return this.JsonResponse(Station.GetPressGraphDataD3());
						case "wdirdata.json":
							return this.JsonResponse(Station.GetWindDirGraphData());
						case "wdirdatad3.json":
							return this.JsonResponse(Station.GetWindDirGraphDataD3());
						case "humdata.json":
							return this.JsonResponse(Station.GetHumGraphData());
						case "humdatad3.json":
							return this.JsonResponse(Station.GetHumGraphDataD3());
						case "solardata.json":
							return this.JsonResponse(Station.GetSolarGraphData());
						case "solardatad3.json":
							return this.JsonResponse(Station.GetSolarGraphDataD3());
						case "dailyrain.json":
							return this.JsonResponse(Station.GetDailyRainGraphData());
						case "sunhours.json":
							return this.JsonResponse(Station.GetSunHoursGraphData());
						case "dailytemp.json":
							return this.JsonResponse(Station.GetDailyTempGraphData());
						case "units.json":
							return this.JsonResponse(Station.GetUnits());
						case "graphconfig.json":
							return this.JsonResponse(Station.GetGraphConfig());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class RecordsController : WebApiController
		{
			public RecordsController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "records/alltime/*")]
			public bool GetAlltimeData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temperature.json":
							return this.JsonResponse(EscapeUnicode(Station.GetTempRecords()));
						case "humidity.json":
							return this.JsonResponse(EscapeUnicode(Station.GetHumRecords()));
						case "pressure.json":
							return this.JsonResponse(EscapeUnicode(Station.GetPressRecords()));
						case "wind.json":
							return this.JsonResponse(EscapeUnicode(Station.GetWindRecords()));
						case "rain.json":
							return this.JsonResponse(EscapeUnicode(Station.GetRainRecords()));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "records/month/*")]
			public bool GetMonthlyRecordData()
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
							return this.JsonResponse(EscapeUnicode(Station.GetMonthlyTempRecords(month)));
						case "humidity.json":
							return this.JsonResponse(EscapeUnicode(Station.GetMonthlyHumRecords(month)));
						case "pressure.json":
							return this.JsonResponse(EscapeUnicode(Station.GetMonthlyPressRecords(month)));
						case "wind.json":
							return this.JsonResponse(EscapeUnicode(Station.GetMonthlyWindRecords(month)));
						case "rain.json":
							return this.JsonResponse(EscapeUnicode(Station.GetMonthlyRainRecords(month)));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "records/thismonth/*")]
			public bool GetThisMonthRecordData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temperature.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisMonthTempRecords()));
						case "humidity.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisMonthHumRecords()));
						case "pressure.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisMonthPressRecords()));
						case "wind.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisMonthWindRecords()));
						case "rain.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisMonthRainRecords()));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "records/thisyear/*")]
			public bool GetThisYearRecordData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temperature.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisYearTempRecords()));
						case "humidity.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisYearHumRecords()));
						case "pressure.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisYearPressRecords()));
						case "wind.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisYearWindRecords()));
						case "rain.json":
							return this.JsonResponse(EscapeUnicode(Station.GetThisYearRainRecords()));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class TodayYestDataController : WebApiController
		{
			public TodayYestDataController(IHttpContext context) : base(context) {}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "todayyest/*")]
			public bool GetYesterdayData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temp.json":
							return this.JsonResponse(Station.GetTodayYestTemp());
						case "hum.json":
							return this.JsonResponse(Station.GetTodayYestHum());
						case "rain.json":
							return this.JsonResponse(Station.GetTodayYestRain());
						case "wind.json":
							return this.JsonResponse(Station.GetTodayYestWind());
						case "pressure.json":
							return this.JsonResponse(Station.GetTodayYestPressure());
						case "solar.json":
							return this.JsonResponse(Station.GetTodayYestSolar());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class ExtraDataController : WebApiController
		{
			public ExtraDataController(IHttpContext context) : base(context) { }

			[WebApiHandler(HttpVerbs.Get, RelativePath + "extra/*")]
			public bool GetExtraData()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "temp.json":
							return this.JsonResponse(Station.GetExtraTemp());
						case "hum.json":
							return this.JsonResponse(Station.GetExtraHum());
						case "dew.json":
							return this.JsonResponse(Station.GetExtraDew());
						case "soiltemp.json":
							return this.JsonResponse(Station.GetSoilTemp());
						case "soilmoisture.json":
							return this.JsonResponse(Station.GetSoilMoisture());
						case "leaf.json":
							return this.JsonResponse(Station.GetLeaf());
						case "leaf4.json":
							return this.JsonResponse(Station.GetLeaf4());
						case "airqual.json":
							return this.JsonResponse(Station.GetAirQuality());
						case "lightning.json":
							return this.JsonResponse(Station.GetLightning());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class SetSettingsController : WebApiController
		{
			public SetSettingsController(IHttpContext context) : base(context) { }

			[WebApiHandler(HttpVerbs.Post, RelativePath + "setsettings/*")]
			public bool SettingsSet()
			{
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					switch (lastSegment)
					{
						case "updatestationconfig.json":
							return this.JsonResponse(stationSettings.UpdateStationConfig(this));
						case "updateinternetconfig.json":
							return this.JsonResponse(internetSettings.UpdateInternetConfig(this));
						case "updatecalibrationconfig.json":
							return this.JsonResponse(calibrationSettings.UpdateCalibrationConfig(this));
						case "updatenoaaconfig.json":
							return this.JsonResponse(noaaSettings.UpdateNoaaConfig(this));
						case "updateextrawebfiles.html":
							return this.JsonResponse(internetSettings.UpdateExtraWebFiles(this));
						case "updatemysqlconfig.json":
							return this.JsonResponse(mySqlSettings.UpdateMysqlConfig(this));
						case "createmonthlysql.json":
							return this.JsonResponse(mySqlSettings.CreateMonthlySQL(this));
						case "createdayfilesql.json":
							return this.JsonResponse(mySqlSettings.CreateDayfileSQL(this));
						case "createrealtimesql.json":
							return this.JsonResponse(mySqlSettings.CreateRealtimeSQL(this));
						case "updatealarmconfig.json":
							return this.JsonResponse(alarmSettings.UpdateAlarmSettings(this));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class GetSettingsController : WebApiController
		{
			public GetSettingsController(IHttpContext context) : base(context) { }

			[WebApiHandler(HttpVerbs.Get, RelativePath + "settings/*")]
			public bool SettingsGet()
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
						case "stationdata.json":
							return this.JsonResponse(stationSettings.GetStationAlpacaFormData());
						case "stationoptions.json":
							return this.JsonResponse(stationSettings.GetStationAlpacaFormOptions());
						case "stationschema.json":
							return this.JsonResponse(stationSettings.GetStationAlpacaFormSchema());

						case "internetdata.json":
							return this.JsonResponse(internetSettings.GetInternetAlpacaFormData());
						case "internetoptions.json":
							return this.JsonResponse(internetSettings.GetInternetAlpacaFormOptions());
						case "internetschema.json":
							return this.JsonResponse(internetSettings.GetInternetAlpacaFormSchema());

						case "extrawebfiles.json":
							return this.JsonResponse(internetSettings.GetExtraWebFilesData());

						case "calibrationdata.json":
							return this.JsonResponse(calibrationSettings.GetCalibrationAlpacaFormData());
						case "calibrationoptions.json":
							return this.JsonResponse(calibrationSettings.GetCalibrationAlpacaFormOptions());
						case "calibrationschema.json":
							return this.JsonResponse(calibrationSettings.GetCalibrationAlpacaFormSchema());

						case "noaadata.json":
							return this.JsonResponse(noaaSettings.GetNoaaAlpacaFormData());
						case "noaaoptions.json":
							return this.JsonResponse(noaaSettings.GetNoaaAlpacaFormOptions());
						case "noaaschema.json":
							return this.JsonResponse(noaaSettings.GetNoaaAlpacaFormSchema());

						case "wsport.json":
							return this.JsonResponse(stationSettings.GetWSport());
						case "version.json":
							return this.JsonResponse(stationSettings.GetVersion());

						case "mysqldata.json":
							return this.JsonResponse(mySqlSettings.GetMySqlAlpacaFormData());
						case "mysqloptions.json":
							return this.JsonResponse(mySqlSettings.GetMySqAlpacaFormOptions());
						case "mysqlschema.json":
							return this.JsonResponse(mySqlSettings.GetMySqAlpacaFormSchema());

						case "alarms.json":
							return this.JsonResponse(alarmSettings.GetAlarmSettings());
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}

		public class ReportsController : WebApiController
		{
			public ReportsController(IHttpContext context) : base(context)
			{
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "reports/*")]
			public bool GetData()
			{
				NOAAReports noaarpts = new NOAAReports(Program.cumulus);
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					int month, year;

					if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
						return this.JsonResponse("Invalid year supplied: " + year);

					switch (lastSegment)
					{
						case "noaayear":
							return this.JsonResponse(noaarpts.GetNoaaYearReport(year));
						case "noaamonth":
							if (!Int32.TryParse(query["month"], out month) || month < 1 || month > 12)
								return this.JsonResponse("Invalid month supplied: " + month);
							return this.JsonResponse(noaarpts.GetNoaaMonthReport(year, month));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch (Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			[WebApiHandler(HttpVerbs.Get, RelativePath + "genreports/*")]
			public bool GenReports()
			{
				NOAAReports noaarpts = new NOAAReports(Program.cumulus);
				try
				{
					// read the last segment of the URL to determine what data the caller wants
					var lastSegment = Request.Url.Segments.Last();

					var query = HttpUtility.ParseQueryString(Request.Url.Query);
					int month, year;

					if (!Int32.TryParse(query["year"], out year) || year < 2000 || year > 2050)
						return this.JsonResponse("Invalid year supplied: " + year);

					switch (lastSegment)
					{
						case "noaayear":
							return this.JsonResponse(noaarpts.GenerateNoaaYearReport(year));
						case "noaamonth":
							if (!Int32.TryParse(query["month"], out month) || month < 1 || month > 12)
								return this.JsonResponse("Invalid month supplied: " + month);
							return this.JsonResponse(noaarpts.GenerateNoaaMonthReport(year, month));
					}

					throw new KeyNotFoundException("Key Not Found: " + lastSegment);
				}
				catch(Exception ex)
				{
					return HandleError(ex, 404);
				}
			}

			private bool HandleError(Exception ex, int statusCode)
			{
				var errorResponse = new
				{
					Title = "Unexpected Error",
					ErrorCode = ex.GetType().Name,
					Description = ex.Message,
				};

				this.Response.StatusCode = statusCode;
				return this.JsonResponse(errorResponse);
			}
		}
	}
}
