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
        }

        public class EditControllerGet : WebApiController
        {
            [WebApiHandler(HttpVerbs.Get, RelativePath + "edit/*")]
            public bool EditData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();
                                        
                    switch (lastSegment)
                    {
                        case "raintodayeditdata.json":
                            return context.JsonResponse(dataEditor.GetRainTodayEditData());
                        
                        case "raintoday":
                            return context.JsonResponse(dataEditor.EditRainToday(context));                        
                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class EditControllerPost : WebApiController
        {
            [WebApiHandler(HttpVerbs.Post, RelativePath + "edit/*")]
            public bool EditData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();

                    switch (lastSegment)
                    {
                        case "raintodayeditdata.json":
                            return context.JsonResponse(dataEditor.GetRainTodayEditData());

                        case "raintoday":
                            return context.JsonResponse(dataEditor.EditRainToday(context));
                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class DataController : WebApiController
        {
            [WebApiHandler(HttpVerbs.Get, RelativePath + "data/*")]
            public bool GetData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();

                    var query = HttpUtility.ParseQueryString(context.Request.Url.Query);
                    var date = query["date"];
                    var month = query["month"];                    
                    var draw = query["draw"];
                    int start = Convert.ToInt32(query["start"]);
                    int length = Convert.ToInt32(query["length"]);

                    switch (lastSegment)
                    {
                        case "dayfile":
                            return context.JsonResponse(Station.GetDayfile(draw,start,length));
                        case "logfile":
                            return context.JsonResponse(Station.GetLogfile(month,draw,start,length,false));
                        case "extralogfile":
                            return context.JsonResponse(Station.GetLogfile(month, draw, start, length, true));
                        case "currentdata":
                            return context.JsonResponse(Station.GetCurrentData());
                        case "diary":
                            return context.JsonResponse(Station.GetDiaryData(date));
                            
                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class GraphDataController : WebApiController
        {
            [WebApiHandler(HttpVerbs.Get, RelativePath + "graphdata/*")]
            public bool GetGraphData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();
                    
                    switch (lastSegment)
                    {
                        case "tempdata.json":
                            return context.JsonResponse(Station.GetTempGraphData());
                        case "tempdatad3.json":
                            return context.JsonResponse(Station.GetTempGraphDataD3());
                        case "winddata.json":
                            return context.JsonResponse(Station.GetWindGraphData());
                        case "winddatad3.json":
                            return context.JsonResponse(Station.GetWindGraphDataD3());
                        case "raindata.json":
                            return context.JsonResponse(Station.GetRainGraphData());
                        case "raindatad3.json":
                            return context.JsonResponse(Station.GetRainGraphDataD3());
                        case "pressdata.json":
                            return context.JsonResponse(Station.GetPressGraphData());
                        case "pressdatad3.json":
                            return context.JsonResponse(Station.GetPressGraphDataD3());
                        case "wdirdata.json":
                            return context.JsonResponse(Station.GetWindDirGraphData());
                        case "wdirdatad3.json":
                            return context.JsonResponse(Station.GetWindDirGraphDataD3());
                        case "humdata.json":
                            return context.JsonResponse(Station.GetHumGraphData());
                        case "humdatad3.json":
                            return context.JsonResponse(Station.GetHumGraphDataD3());
                        case "solardata.json":
                            return context.JsonResponse(Station.GetSolarGraphData());
                        case "solardatad3.json":
                            return context.JsonResponse(Station.GetSolarGraphDataD3());
                        case "dailyrain.json":
                            return context.JsonResponse(Station.GetDailyRainGraphData());
                        case "sunhours.json":
                            return context.JsonResponse(Station.GetSunHoursGraphData());
                        case "dailytemp.json":
                            return context.JsonResponse(Station.GetDailyTempGraphData());
                        case "units.json":
                            return context.JsonResponse(Station.GetUnits());
                        case "graphconfig.json":
                            return context.JsonResponse(Station.GetGraphConfig());
                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }
                
            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class RecordsController : WebApiController
        {
            [WebApiHandler(HttpVerbs.Get, RelativePath + "records/alltime/*")]
            public bool GetAlltimeData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();

                    switch (lastSegment)
                    {
                        case "temperature.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetTempRecords()));
                        case "humidity.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetHumRecords()));
                        case "pressure.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetPressRecords()));
                        case "wind.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetWindRecords()));
                        case "rain.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetRainRecords()));
                        
                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            [WebApiHandler(HttpVerbs.Get, RelativePath + "records/month/*")]
            public bool GetMonthlyRecordData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();
                    // Get penultimate segment and trim off traling slash. This gives the required month
                    int month = Convert.ToInt32(context.Request.Url.Segments[context.Request.Url.Segments.Length - 2].TrimEnd('/'));

                    switch (lastSegment)
                    {
                        case "temperature.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetMonthlyTempRecords(month)));
                        case "humidity.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetMonthlyHumRecords(month)));
                        case "pressure.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetMonthlyPressRecords(month)));
                        case "wind.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetMonthlyWindRecords(month)));
                        case "rain.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetMonthlyRainRecords(month)));

                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            [WebApiHandler(HttpVerbs.Get, RelativePath + "records/thismonth/*")]
            public bool GetThisMonthRecordData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();
                    
                    switch (lastSegment)
                    {
                        case "temperature.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisMonthTempRecords()));
                        case "humidity.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisMonthHumRecords()));
                        case "pressure.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisMonthPressRecords()));
                        case "wind.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisMonthWindRecords()));
                        case "rain.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisMonthRainRecords()));

                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            [WebApiHandler(HttpVerbs.Get, RelativePath + "records/thisyear/*")]
            public bool GetThisYearRecordData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();

                    switch (lastSegment)
                    {
                        case "temperature.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisYearTempRecords()));
                        case "humidity.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisYearHumRecords()));
                        case "pressure.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisYearPressRecords()));
                        case "wind.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisYearWindRecords()));
                        case "rain.json":
                            return context.JsonResponse(EscapeUnicode(Station.GetThisYearRainRecords()));

                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class TodayYestDataController : WebApiController
        {
            [WebApiHandler(HttpVerbs.Get, RelativePath + "todayyest/*")]
            public bool GetYesterdayData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();

                    switch (lastSegment)
                    {
                        case "temp.json":
                            return context.JsonResponse(Station.GetTodayYestTemp());
                        case "hum.json":
                            return context.JsonResponse(Station.GetTodayYestHum());
                        case "rain.json":
                            return context.JsonResponse(Station.GetTodayYestRain());
                        case "wind.json":
                            return context.JsonResponse(Station.GetTodayYestWind());
                        case "pressure.json":
                            return context.JsonResponse(Station.GetTodayYestPressure());
                        case "solar.json":
                            return context.JsonResponse(Station.GetTodayYestSolar());

                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class ExtraDataController : WebApiController
        {
            [WebApiHandler(HttpVerbs.Get, RelativePath + "extra/*")]
            public bool GetExtraData(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();

                    switch (lastSegment)
                    {
                        case "temp.json":
                            return context.JsonResponse(Station.GetExtraTemp());
                        case "hum.json":
                            return context.JsonResponse(Station.GetExtraHum());
                        case "dew.json":
                            return context.JsonResponse(Station.GetExtraDew());
                        case "soiltemp.json":
                            return context.JsonResponse(Station.GetSoilTemp());
                        case "soilmoisture.json":
                            return context.JsonResponse(Station.GetSoilMoisture());
                        case "leaf.json":
                            return context.JsonResponse(Station.GetLeaf());
                        case "leaf4.json":
                            return context.JsonResponse(Station.GetLeaf4());

                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class SetSettingsController : WebApiController
        {
            [WebApiHandler(HttpVerbs.Post, RelativePath + "setsettings/*")]
            public bool SettingsSet(WebServer server, HttpListenerContext context)
            {
                try
                {
                    // read the last segment of the URL to determine what data the caller wants
                    var lastSegment = context.Request.Url.Segments.Last();
                    
                    switch (lastSegment)
                    {
                       
                        case "updatestationconfig.json":
                            return context.JsonResponse(stationSettings.UpdateStationConfig(context));
                        case "updateinternetconfig.json":
                            return context.JsonResponse(internetSettings.UpdateInternetConfig(context));
                        case "updatecalibrationconfig.json":
                            return context.JsonResponse(calibrationSettings.UpdateCalibrationConfig(context));
                        case "updatenoaaconfig.json":
                            return context.JsonResponse(noaaSettings.UpdateNoaaConfig(context));
                        case "updateextrawebfiles.html":
                            return context.JsonResponse(internetSettings.UpdateExtraWebFiles(context));
                        case "updatemysqlconfig.json":
                            return context.JsonResponse(mySqlSettings.UpdateMysqlConfig(context));
                        case "createmonthlysql.json":
                            return context.JsonResponse(mySqlSettings.CreateMonthlySQL(context));
                        case "createdayfilesql.json":
                            return context.JsonResponse(mySqlSettings.CreateDayfileSQL(context));
                        case "createrealtimesql.json":
                            return context.JsonResponse(mySqlSettings.CreateRealtimeSQL(context));
                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class GetSettingsController : WebApiController
        {
            
            [WebApiHandler(HttpVerbs.Get, RelativePath + "settings/*")]
            public bool SettingsGet(WebServer server, HttpListenerContext context)
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
                    var lastSegment = context.Request.Url.Segments.Last();
                    
                    switch (lastSegment)
                    {
                        case "stationdata.json":
                            return context.JsonResponse(stationSettings.GetStationAlpacaFormData());
                        case "stationoptions.json":
                            return context.JsonResponse(stationSettings.GetStationAlpacaFormOptions());
                        case "stationschema.json":
                            return context.JsonResponse(stationSettings.GetStationAlpacaFormSchema());

                        case "internetdata.json":
                            return context.JsonResponse(internetSettings.GetInternetAlpacaFormData());
                        case "internetoptions.json":
                            return context.JsonResponse(internetSettings.GetInternetAlpacaFormOptions());
                        case "internetschema.json":
                            return context.JsonResponse(internetSettings.GetInternetAlpacaFormSchema());

                        case "extrawebfiles.json":
                            return context.JsonResponse(internetSettings.GetExtraWebFilesData());

                        case "calibrationdata.json":
                            return context.JsonResponse(calibrationSettings.GetCalibrationAlpacaFormData());
                        case "calibrationoptions.json":
                            return context.JsonResponse(calibrationSettings.GetCalibrationAlpacaFormOptions());
                        case "calibrationschema.json":
                            return context.JsonResponse(calibrationSettings.GetCalibrationAlpacaFormSchema());

                        case "noaadata.json":
                            return context.JsonResponse(noaaSettings.GetNoaaAlpacaFormData());
                        case "noaaoptions.json":
                            return context.JsonResponse(noaaSettings.GetNoaaAlpacaFormOptions());
                        case "noaaschema.json":
                            return context.JsonResponse(noaaSettings.GetNoaaAlpacaFormSchema());

                        case "wsport.json":
                            return context.JsonResponse(stationSettings.GetWSport());
                        case "version.json":
                            return context.JsonResponse(stationSettings.GetVersion());

                        case "mysqldata.json":
                            return context.JsonResponse(mySqlSettings.GetMySqlAlpacaFormData());
                        case "mysqloptions.json":
                            return context.JsonResponse(mySqlSettings.GetMySqAlpacaFormOptions());
                        case "mysqlschema.json":
                            return context.JsonResponse(mySqlSettings.GetMySqAlpacaFormSchema());
                    }

                    throw new KeyNotFoundException("Key Not Found: " + lastSegment);
                }
                catch (Exception ex)
                {
                    return HandleError(context, ex, 404);
                }

            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode)
            {
                var errorResponse = new
                {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }
    }
}
