using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace AwekasDataReporter
{
    public class AwekasDataReporter : DataReporterBase
    {
        private bool _updating;

        public override string ServiceName => "Awekas Data Reporter Service";

        private DataReporterSettingsGeneric _localSettings;
        
        public override string Identifier => "TBC"; //TODO

        private readonly HttpClient _httpClient;

        public AwekasDataReporter(ILogger logger, AwekasSettings settings, IWeatherDataStatistics data) : base(logger,settings,data)
        {
            var awekasHttpHandler = new HttpClientHandler();
            _httpClient = new HttpClient(awekasHttpHandler);

            _localSettings = settings;
        }

        public override void Initialise()
        {

        }

        public override async void DoReport(IWeatherDataStatistics currentData)
        {
            await UpdateAwekas(currentData, DateTime.Now);
        }

        private async Task UpdateAwekas(IWeatherDataStatistics currentData, DateTime timestamp)
        {
            if (_updating) return;

            _updating = true;

            try
            {
                string url = GetUrl(currentData, timestamp, out var passwordString);

                string starredPasswordString = "<password>";

                string LogURL = url.Replace(passwordString, starredPasswordString);

                _log.Debug(LogURL);

                var response = await _httpClient.GetAsync(url);
                var responseBodyAsText = await response.Content.ReadAsStringAsync();
                _log.Info("Awekas Response: " + response.StatusCode + ": " + responseBodyAsText);
            }
            catch (Exception ex)
            {
                _log.Info("Awekas update: " + ex.Message);
            }
            finally
            {
                _updating = false;
            }
        }

         private string GetUrl(IWeatherDataStatistics data, DateTime timestamp, out string passwordString)
         {
             byte[] hashPassword;

             // password is sent as MD5 hash
             using (MD5 md5 = MD5.Create())
             {
                 hashPassword = md5.ComputeHash(Encoding.ASCII.GetBytes(Settings.GetValue("AwekasPW", string.Empty)));
             }

             passwordString = hashPassword.ToHexadecimalString();

             int pressureTrend;

             double threeHourlyPressureChangeMb = 0;

             threeHourlyPressureChangeMb = data.Pressure.ThreeHourChange.Millibars;

             if (threeHourlyPressureChangeMb > 6) pressureTrend = 2;
             else if (threeHourlyPressureChangeMb > 3.5) pressureTrend = 2;
             else if (threeHourlyPressureChangeMb > 1.5) pressureTrend = 1;
             else if (threeHourlyPressureChangeMb > 0.1) pressureTrend = 1;
             else if (threeHourlyPressureChangeMb > -0.1) pressureTrend = 0;
             else if (threeHourlyPressureChangeMb > -1.5) pressureTrend = -1;
             else if (threeHourlyPressureChangeMb > -3.5) pressureTrend = -1;
             else if (threeHourlyPressureChangeMb > -6) pressureTrend = -2;
             else
                 pressureTrend = -2;

             var extraRenderParameters = new Dictionary<string, object>()
             {
                 {"passwordString", passwordString},
                 {"pressureTrend", pressureTrend},
                 {"IsFineOffset", false} //TODO: fill value
             };

             var renderer = new TemplateRenderer
             (
                 "AwekasUrl.StringTemplate",
                 data,
                 Settings,
                 extraRenderParameters,
                 _log
             ) {Timestamp = timestamp};
             return renderer.Render();
        }


    }
}
