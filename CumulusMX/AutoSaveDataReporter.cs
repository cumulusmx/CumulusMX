using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AwekasDataReporter;
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
        }

        public override void Initialise()
        {

        }

        public override async void DoReport(IWeatherDataStatistics currentData)
        {
            currentData.Save();
        }

    }
}
