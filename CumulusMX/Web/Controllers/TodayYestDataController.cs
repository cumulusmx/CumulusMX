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
    public class TodayYestDataController : ControllerBase
    {
        private readonly IDataProvider dataProvider;

        public TodayYestDataController(IDataProvider dataProvider, IHttpContext context) : base(context)
        {
            this.dataProvider = dataProvider;
        }

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
                        return this.JsonResponse(dataProvider.GetTodayYestTemp());
                    case "hum.json":
                        return this.JsonResponse(dataProvider.GetTodayYestHum());
                    case "rain.json":
                        return this.JsonResponse(dataProvider.GetTodayYestRain());
                    case "wind.json":
                        return this.JsonResponse(dataProvider.GetTodayYestWind());
                    case "pressure.json":
                        return this.JsonResponse(dataProvider.GetTodayYestPressure());
                    case "solar.json":
                        return this.JsonResponse(dataProvider.GetTodayYestSolar());

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
