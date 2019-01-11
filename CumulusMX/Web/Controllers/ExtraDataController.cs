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
    public class ExtraDataController : ControllerBase
    {
        private readonly IDataProvider dataProvider;

        public ExtraDataController(IDataProvider dataProvider, IHttpContext context) : base(context)
        {
            this.dataProvider = dataProvider;
        }

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
                        return this.JsonResponse(dataProvider.GetExtraTemp());
                    case "hum.json":
                        return this.JsonResponse(dataProvider.GetExtraHum());
                    case "dew.json":
                        return this.JsonResponse(dataProvider.GetExtraDew());
                    case "soiltemp.json":
                        return this.JsonResponse(dataProvider.GetSoilTemp());
                    case "soilmoisture.json":
                        return this.JsonResponse(dataProvider.GetSoilMoisture());
                    case "leaf.json":
                        return this.JsonResponse(dataProvider.GetLeaf());
                    case "leaf4.json":
                        return this.JsonResponse(dataProvider.GetLeaf4());

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
