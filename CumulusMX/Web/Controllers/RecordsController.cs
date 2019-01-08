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
    public class RecordsController : ControllerBase
    {
        private readonly IDataProvider dataProvider;

        public RecordsController(IDataProvider dataProvider, IHttpContext context) : base(context)
        {
            this.dataProvider = dataProvider;
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
                        return this.JsonResponse(dataProvider.GetTempRecords());
                    case "humidity.json":
                        return this.JsonResponse(dataProvider.GetHumRecords());
                    case "pressure.json":
                        return this.JsonResponse(dataProvider.GetPressRecords());
                    case "wind.json":
                        return this.JsonResponse(dataProvider.GetWindRecords());
                    case "rain.json":
                        return this.JsonResponse(dataProvider.GetRainRecords());

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
                        return this.JsonResponse(dataProvider.GetMonthlyTempRecords(month));
                    case "humidity.json":
                        return this.JsonResponse(dataProvider.GetMonthlyHumRecords(month));
                    case "pressure.json":
                        return this.JsonResponse(dataProvider.GetMonthlyPressRecords(month));
                    case "wind.json":
                        return this.JsonResponse(dataProvider.GetMonthlyWindRecords(month));
                    case "rain.json":
                        return this.JsonResponse(dataProvider.GetMonthlyRainRecords(month));

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
                        return this.JsonResponse(dataProvider.GetThisMonthTempRecords());
                    case "humidity.json":
                        return this.JsonResponse(dataProvider.GetThisMonthHumRecords());
                    case "pressure.json":
                        return this.JsonResponse(dataProvider.GetThisMonthPressRecords());
                    case "wind.json":
                        return this.JsonResponse(dataProvider.GetThisMonthWindRecords());
                    case "rain.json":
                        return this.JsonResponse(dataProvider.GetThisMonthRainRecords());

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
                        return this.JsonResponse(dataProvider.GetThisYearTempRecords());
                    case "humidity.json":
                        return this.JsonResponse(dataProvider.GetThisYearHumRecords());
                    case "pressure.json":
                        return this.JsonResponse(dataProvider.GetThisYearPressRecords());
                    case "wind.json":
                        return this.JsonResponse(dataProvider.GetThisYearWindRecords());
                    case "rain.json":
                        return this.JsonResponse(dataProvider.GetThisYearRainRecords());

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
