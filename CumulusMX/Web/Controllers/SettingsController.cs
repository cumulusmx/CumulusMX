using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Cumulus4.Data;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace Cumulus4.Web.Controllers
{
    public class SettingsController : ControllerBase
    {
        private readonly IDataProvider dataProvider;

        public SettingsController(IDataProvider dataProvider, IHttpContext context) : base(context)
        {
            this.dataProvider = dataProvider;
        }

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
                }

                throw new KeyNotFoundException("Key Not Found: " + lastSegment);
            }
            catch (Exception ex)
            {
                return HandleError(ex, 404);
            }

        }




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
                }

                throw new KeyNotFoundException("Key Not Found: " + lastSegment);
            }
            catch (Exception ex)
            {
                return HandleError(ex, 404);
            }

        }

    }
}
