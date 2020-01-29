using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace CumulusMX
{
    public class AutoSaveDataReporter : DataReporterBase
    {
        private bool _updating;

        public override string ServiceName => "Service to Save Data Locally";

        public override string Identifier => "AutoSave"; 

        public AutoSaveDataReporter(ILogger logger, AutoSaveSettings settings, IWeatherDataStatistics data) : base(logger, settings, data)
        {
            ReportInterval = 30000;
        }

        public override void Initialise()
        {
            //Note: Never called for this reporter
        }

        public override async void DoReport(IWeatherDataStatistics currentData)
        {
            var timer = new Stopwatch();
            timer.Start();
            currentData.Save();
            timer.Stop();
            _log.Debug($"Updating saved data took {timer.ElapsedMilliseconds}ms.");
        }

    }
}
