using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cumulus4.Web.Controllers;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace Cumulus4.Web
{
    public class CumulusWebService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly WebServer _webServer;

        public CumulusWebService(int webPort, string contentRootDir)
        {
            Unosquare.Swan.Terminal.Settings.DisplayLoggingMessageType = Unosquare.Swan.LogMessageType.None;

            _webServer = new WebServer(webPort, RoutingStrategy.Wildcard);
            var htmlRootPath = Path.Combine(contentRootDir, "interface");

            log.Debug("HTML root path = " + htmlRootPath);

            _webServer.RegisterModule(new StaticFilesModule(htmlRootPath));
            _webServer.Module<StaticFilesModule>().UseRamCache = true;

            _webServer.RegisterModule(new WebApiModule());
            _webServer.Module<WebApiModule>().RegisterController<GraphDataController>(x => new GraphDataController(x));
            _webServer.Module<WebApiModule>().RegisterController<DataController>();
            _webServer.Module<WebApiModule>().RegisterController<RecordsController>();
            _webServer.Module<WebApiModule>().RegisterController<TodayYestDataController>();
            _webServer.Module<WebApiModule>().RegisterController<ExtraDataController>();
            _webServer.Module<WebApiModule>().RegisterController<SettingsController>();
            _webServer.Module<WebApiModule>().RegisterController<EditController>();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }
    }
}
