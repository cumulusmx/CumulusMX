using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Autofac;
using CumulusMX.Configuration;
using CumulusMX.Data;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;
using CumulusMX.Common;
using CumulusMX.Web;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace CumulusMX
{
    public class CumulusService : BackgroundService
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
            AutofacWrapper.Instance.Builder.RegisterType(typeof(AutoSaveDataReporter));
            AutofacWrapper.Instance.Builder.RegisterType(typeof(AutoSaveSettings));

            _stations = new List<IWeatherStation>();
            _reporters = new List<IDataReporter>();
        }

        protected void OnStart(string[] args)
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
            var dataFile = _iniFile.GetValue("System", "DataFile", "WeatherData.json");
            WeatherDataStatistics dataStatistics = WeatherDataStatistics.TryLoad(dataFile);
            AutofacWrapper.Instance.Builder.RegisterInstance(dataStatistics).As<IWeatherDataStatistics>();
            AutofacWrapper.Instance.Builder.RegisterInstance(_iniFile).As<IConfigurationProvider>();
            AutofacWrapper.Instance.Builder.RegisterType<MeteoLib>().Named<object>("MeteoLib");

            var stations = _iniFile.GetSection("Stations");

            foreach (var station in stations)
            {
                IWeatherStation theStation = AutofacWrapper.Instance.Scope.ResolveKeyed<IWeatherStation>
                    (
                    station.Value.AsString
                    );
                theStation.ConfigurationSettings.ConfigurationSectionName = $"Stations:{station.Key}";
                if (theStation.Enabled)
                {
                    log.Info($"Initialising weather station {theStation.Identifier}.");
                    theStation.Initialise();
                    if (theStation.Enabled)
                        _stations.Add(theStation);
                }
                else
                {
                    log.Debug($"Configuraton for weather station {station.Key} found - but disabled.");
                }
            }

            var calculations = _iniFile.GetSection("Calculations");
            foreach (var calculation in calculations)
            {
                Dictionary<string,Setting> calcDetails = calculation.Value.AsSection;
                object methodClass = AutofacWrapper.Instance.Scope.ResolveKeyed<object>(calcDetails["Class"].AsString);
                var method = methodClass.GetType().GetMethod(calcDetails["Method"].AsString);
                var returnType = method.ReturnType;
                dataStatistics.DefineStatistic(calcDetails["Name"].AsString, returnType);
                dataStatistics.DefineCalculation(calcDetails["Name"].AsString, calcDetails["Inputs"].AsList,
                    method);
            }

            var dayStatistics = _iniFile.GetSection("DayStatistics");
            foreach (var dayStat in dayStatistics)
            {
                Dictionary<string, Setting> calcDetails = dayStat.Value.AsSection;
                var dayStatLambda = calcDetails["Lambda"].AsString;
                dataStatistics.DefineDayStatistic(calcDetails["Name"].AsString, calcDetails["Input"].AsString, dayStatLambda);
            }

            var reporters = _iniFile.GetSection("Reporters");
            foreach (var reporter in reporters)
            {
                IDataReporter theReporter = (IDataReporter)AutofacWrapper.Instance.Scope.ResolveNamed<IDataReporter>
                    (
                    reporter.Value.AsString,
                    new NamedParameter("configurationSectionName", "Reporters:{station.Key}")
                    );
                if (theReporter.Enabled)
                {
                    log.Info($"Initialising reporter {reporter.Key}.");
                    theReporter.Initialise();

                    if (theReporter.Enabled)
                        _reporters.Add(theReporter);
                }
                else
                {
                    log.Debug($"Configuration for reporter {theReporter.Identifier} found - but disabled.");
                }
            }

            var autoSave = (IDataReporter)AutofacWrapper.Instance.Scope.Resolve(typeof(AutoSaveDataReporter));
            _reporters.Add(autoSave);

            if (!_stations.Any())
            {
                log.Error("No weather stations enabled. Aborting.");
                return;
            }

            //_webService.Start();

            foreach (var weatherStation in _stations)
            {
                weatherStation.Start();
            }

            foreach (var dataReporter in _reporters)
            {
                dataReporter.Start();
            }
        }

        public void Start()
        {
            var token = new CancellationToken();
            ExecuteAsync(token).Wait();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            OnStart(null);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }
        }
    }
}
