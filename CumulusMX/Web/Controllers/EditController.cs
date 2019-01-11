using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace CumulusMX.Web.Controllers
{
    public class EditController : ControllerBase
    {
        private readonly DataEditor dataEditor;

        public EditController(DataEditor dataEditor, IHttpContext context) : base(context)
        {
            this.dataEditor = dataEditor;
        }

        [WebApiHandler(HttpVerbs.Get, RelativePath + "edit/*")]
        public bool EditDataGet()
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
                }

                throw new KeyNotFoundException("Key Not Found: " + lastSegment);
            }
            catch (Exception ex)
            {
                return HandleError(ex, 404);
            }

        }

        [WebApiHandler(HttpVerbs.Post, RelativePath + "edit/*")]
        public bool EditDataPost()
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
