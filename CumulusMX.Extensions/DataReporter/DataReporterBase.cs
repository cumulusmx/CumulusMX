using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CumulusMX.Extensions.Station;

namespace CumulusMX.Extensions.DataReporter
{
    public abstract class DataReporterBase : IDataReporter
    {
        protected readonly ILogger _log;
        private readonly IWeatherDataStatistics _weatherStatistics;

        public DataReporterBase(ILogger logger, DataReporterSettingsBase settings, IWeatherDataStatistics weatherStatistics)
        {
            _log = logger;
            _weatherStatistics = weatherStatistics;
            _cts = new CancellationTokenSource();

            Settings = settings;
            Enabled = settings.IsEnabled;
            ReportInterval = settings.ReportInterval;

            _extensionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
        
        public abstract string Identifier { get; }
        public abstract void Initialise();

        public abstract string ServiceName { get; }
        public IDataReporterSettings Settings { get; }
        public bool Enabled { get; }
        public int ReportInterval { get; protected set; }
        public abstract void DoReport(IWeatherDataStatistics currentData);

        protected Task _backgroundTask;
        protected CancellationTokenSource _cts;
        protected string _extensionPath;

        public void Start()
        {
            _log.Info($"Starting {Identifier} data reporter background task");
            _backgroundTask = Task.Factory.StartNew(() =>
                    PostUpdates(_cts.Token, _weatherStatistics)
                , _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private async void PostUpdates(CancellationToken ct, IWeatherDataStatistics weatherStatistics)
        {
            _log.Info($"Starting publication loop for {Identifier}.");

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    DoReport(weatherStatistics);
                    await Task.Delay(ReportInterval);
                }
            }
            // Catch the ThreadAbortException
            catch (ThreadAbortException)
            {
            }
        }

        public void Stop()
        {
            _log.Info($"Ceasing publication loop for {Identifier}.");

            _cts?.Cancel();

            _log.Info("Waiting for background task to complete");
            _backgroundTask.Wait();
        }
    }
}
