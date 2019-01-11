using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceProcess;
using System.Text;
using CumulusMX.Configuration;
using CumulusMX.Web;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;

namespace CumulusMX
{
    public class CumulusService : ServiceBase
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly int _httpport;
        private readonly string _appDir;
        private readonly string _contentRootDir;
        private readonly CumulusConfiguration _config;
        private readonly CumulusWebService _webService;

        public CumulusService(int httpPort, string appDir, string contentRootDir)
        {
            this._httpport = httpPort;
            this._appDir = appDir;
            this._contentRootDir = contentRootDir;
            var iniFile = new IniFile("Cumulus.ini");
            this._config = new CumulusConfiguration(iniFile);
            this._webService = new CumulusWebService(httpPort, contentRootDir);
        }

        protected override void OnStart(string[] args)
        {
            log.Info("========================== Cumulus MX starting ==========================");
            log.Info($"Command line: {Environment.CommandLine}");
            log.Info($"Cumulus version: {typeof(CumulusService).Assembly.GetName().Version.ToString()}");
            log.Info($"Platform: {Environment.OSVersion.Platform.ToString()}");
            log.Info($"OS version: {Environment.OSVersion.ToString()}");
            log.Info($"Current culture: {CultureInfo.CurrentCulture.DisplayName}");

            _webService.Start();
        }

        protected override void OnStop()
        {

        }


        public void Start()
        {
            OnStart(null);
        }

    }
}
