using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using CumulusMX.Configuration;
using CumulusMX.Extensions;
using CumulusMX.Web;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;

namespace CumulusMX
{
    public class CumulusService : ServiceBase
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string appGuid = "57190d2e-7e45-4efb-8c09-06a176cef3f3";
        private Mutex appMutex;

        private readonly int _httpport;
        private readonly string _appDir;
        private readonly string _contentRootDir;
        private readonly CumulusConfiguration _config;
        private readonly CumulusWebService _webService;
        private readonly ExtensionLoader _extensionLoader;

        public CumulusService(int httpPort, string appDir, string contentRootDir)
        {
            this._httpport = httpPort;
            this._appDir = appDir;
            this._contentRootDir = contentRootDir;
            var iniFile = new IniFile("Cumulus.ini");
            this._config = new CumulusConfiguration(iniFile);
            this._webService = new CumulusWebService(httpPort, contentRootDir);

            var extensionLoaderSettings = new ExtensionLoaderSettings() { Path = Path.Combine(appDir, "Extensions") };
            this._extensionLoader = new ExtensionLoader(extensionLoaderSettings);
            _extensionLoader.GetExtensions();
        }

        protected override void OnStart(string[] args)
        {
            log.Info("========================== Cumulus MX starting ==========================");
            log.Info($"Command line: {Environment.CommandLine}");
            log.Info($"Cumulus version: {typeof(CumulusService).Assembly.GetName().Version.ToString()}");
            log.Info($"Platform: {Environment.OSVersion.Platform.ToString()}");
            log.Info($"OS version: {Environment.OSVersion.ToString()}");
            log.Info($"Current culture: {CultureInfo.CurrentCulture.DisplayName}");

            if (_config.WarnMultiple)
            {
                appMutex = new Mutex(false, "Global\\" + appGuid);

                if (!appMutex.WaitOne(0, false))
                {
                    log.Error("Cumulus is already running - terminating");
                    Environment.Exit(0);
                }
            }



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
