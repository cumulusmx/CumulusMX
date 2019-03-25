using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Autofac;
using CumulusMX.Configuration;
using CumulusMX.Data;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;
using CumulusMX.Stations;
using CumulusMX.Web;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;

namespace CumulusMX
{
    public class CumulusService : ServiceBase
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string appGuid = "57190d2e-7e45-4efb-8c09-06a176cef3f3";
        private Mutex appMutex;

        private readonly int _httpPort;
        private readonly string _appDir;
        private readonly string _contentRootDir;
        private readonly IniFile _iniFile;

        private readonly CumulusConfiguration _config;
        private readonly CumulusWebService _webService;
        private readonly ExtensionLoader _extensionLoader;
        private readonly Dictionary<Type,Type[]> _extensions;
        private readonly List<IWeatherStation> _stations;
        private readonly List<IDataReporter> _reporters;

        public CumulusService(int httpPort, string appDir, string contentRootDir)
        {
            this._httpPort = httpPort;
            this._appDir = appDir;
            this._contentRootDir = contentRootDir;
            _iniFile = new IniFile("Cumulus.ini");
            this._config = new CumulusConfiguration(_iniFile);
            this._webService = new CumulusWebService(httpPort, contentRootDir);

            var extensionLoaderSettings = new ExtensionLoaderSettings() { Path = Path.Combine(appDir, "Extensions") };
            this._extensionLoader = new ExtensionLoader(extensionLoaderSettings);
            _extensions = _extensionLoader.GetExtensions();

            _stations = new List<IWeatherStation>();
            _reporters = new List<IDataReporter>();
        }

        protected override void OnStart(string[] args)
        {
            log.Info("========================== Cumulus MX starting ==========================");
            log.Info($"Command line    : {Environment.CommandLine}");
            log.Info($"Cumulus version : {typeof(CumulusService).Assembly.GetName().Version.ToString()}");
            log.Info($"Platform        : {Environment.OSVersion.Platform.ToString()}");
            log.Info($"OS version      : {Environment.OSVersion.ToString()}");
            log.Info($"Current culture : {CultureInfo.CurrentCulture.DisplayName}");

            if (_config.WarnMultiple)
            {
                appMutex = new Mutex(false, "Global\\" + appGuid);

                if (!appMutex.WaitOne(0, false))
                {
                    log.Error("Cumulus is already running - terminating");
                    Environment.Exit(0);
                }
            }

            var wrappedLogger = new LogWrapper(log);
            AutofacWrapper.Instance.Builder.RegisterInstance(wrappedLogger).As<ILogger>();
            var dataStatistics = new WeatherDataStatistics();
            AutofacWrapper.Instance.Builder.RegisterInstance(dataStatistics).As<IWeatherDataStatistics>();
            AutofacWrapper.Instance.Builder.RegisterInstance(_config).As<CumulusConfiguration>();
            AutofacWrapper.Instance.Builder.RegisterType(typeof(DataReporterSettingsGeneric));

            foreach (var service in _extensions)
            {
                if (service.Value.Contains(typeof(IWeatherStation)))
                {
                    IWeatherStation theStation = (IWeatherStation) AutofacWrapper.Instance.Scope.Resolve(service.Key);
                    if (theStation.Enabled)
                    {
                        _stations.Add(theStation);
                        log.Info($"Initialising weather station {theStation.Identifier}.");
                        theStation.Initialise();
                    }
                    else
                    {
                        log.Debug($"Weather station {theStation.Identifier} found - but disabled.");
                    }
                }

                if (service.Value.Contains(typeof(IDataReporter)))
                {
                    IDataReporter theReporter = (IDataReporter)AutofacWrapper.Instance.Scope.Resolve(service.Key);
                    if (theReporter.Enabled)
                    {
                        _reporters.Add(theReporter);
                        log.Info($"Initialising reporter {theReporter.Identifier}.");
                        theReporter.Initialise();
                    }
                    else
                    {
                        log.Debug($"Reporter {theReporter.Identifier} found - but disabled.");
                    }
                }
            }

//            _webService.Start();
            foreach (var weatherStation in _stations)
            {
                weatherStation.Start();
            }

            foreach (var dataReporter in _reporters)
            {
                dataReporter.Start();
            }
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
