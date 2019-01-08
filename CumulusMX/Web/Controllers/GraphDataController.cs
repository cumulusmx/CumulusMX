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
    public class GraphDataController : ControllerBase
    {
        private readonly IDataProvider dataProvider;

        public GraphDataController(IDataProvider dataProvider, IHttpContext context) : base(context)
        {
            this.dataProvider = dataProvider;
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
                        return this.JsonResponse(dataProvider.GetTempGraphData());
                    case "tempdatad3.json":
                        return this.JsonResponse(dataProvider.GetTempGraphDataD3());
                    case "winddata.json":
                        return this.JsonResponse(dataProvider.GetWindGraphData());
                    case "winddatad3.json":
                        return this.JsonResponse(dataProvider.GetWindGraphDataD3());
                    case "raindata.json":
                        return this.JsonResponse(dataProvider.GetRainGraphData());
                    case "raindatad3.json":
                        return this.JsonResponse(dataProvider.GetRainGraphDataD3());
                    case "pressdata.json":
                        return this.JsonResponse(dataProvider.GetPressGraphData());
                    case "pressdatad3.json":
                        return this.JsonResponse(dataProvider.GetPressGraphDataD3());
                    case "wdirdata.json":
                        return this.JsonResponse(dataProvider.GetWindDirGraphData());
                    case "wdirdatad3.json":
                        return this.JsonResponse(dataProvider.GetWindDirGraphDataD3());
                    case "humdata.json":
                        return this.JsonResponse(dataProvider.GetHumGraphData());
                    case "humdatad3.json":
                        return this.JsonResponse(dataProvider.GetHumGraphDataD3());
                    case "solardata.json":
                        return this.JsonResponse(dataProvider.GetSolarGraphData());
                    case "solardatad3.json":
                        return this.JsonResponse(dataProvider.GetSolarGraphDataD3());
                    case "dailyrain.json":
                        return this.JsonResponse(dataProvider.GetDailyRainGraphData());
                    case "sunhours.json":
                        return this.JsonResponse(dataProvider.GetSunHoursGraphData());
                    case "dailytemp.json":
                        return this.JsonResponse(dataProvider.GetDailyTempGraphData());
                    case "units.json":
                        return this.JsonResponse(dataProvider.GetUnits());
                    case "graphconfig.json":
                        return this.JsonResponse(dataProvider.GetGraphConfig());
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
