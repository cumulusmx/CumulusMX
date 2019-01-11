using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using CumulusMX.Data;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace CumulusMX.Web.Controllers
{
    public class DataController : ControllerBase
    {
        private readonly IDataProvider dataProvider;

        public DataController(IDataProvider dataProvider, IHttpContext context) : base(context)
        {
            this.dataProvider = dataProvider;
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
                var month = query["month"];
                var draw = query["draw"];
                int start = Convert.ToInt32(query["start"]);
                int length = Convert.ToInt32(query["length"]);

                switch (lastSegment)
                {
                    case "dayfile":
                        return this.JsonResponse(dataProvider.GetDayfile(draw, start, length));
                    case "logfile":
                        return this.JsonResponse(dataProvider.GetLogfile(month, draw, start, length, false));
                    case "extralogfile":
                        return this.JsonResponse(dataProvider.GetLogfile(month, draw, start, length, true));
                    case "currentdata":
                        return this.JsonResponse(dataProvider.GetCurrentData());
                    case "diary":
                        return this.JsonResponse(dataProvider.GetDiaryData(date));

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
