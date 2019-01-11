using System;
using System.Collections.Generic;
using System.Text;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace CumulusMX.Web.Controllers
{
    public abstract class ControllerBase : WebApiController
    {
        public const string RelativePath = "/api/";

        public ControllerBase(IHttpContext context) : base(context)
        {
        }


        protected bool HandleError(Exception ex, int statusCode)
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
