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
    public class AwekasDataReporter : IDataReporter
    {
        private ILogger _logger;
        private bool _updating;

        public string ServiceName => "Awekas Data Reporter Service";

         public IDataReporterSettings Settings { get; private set; }

        public string Identifier => "TBC"; //TODO

        private readonly HttpClient _httpClient;

        public AwekasDataReporter()
        {
            var awekasHttpHandler = new HttpClientHandler();
            _httpClient = new HttpClient(awekasHttpHandler);
        }

        public void Initialise(ILogger logger, ISettings settings)
        {
            _logger = logger;
            Settings = settings as IDataReporterSettings;
        }

        public async void DoReport(IWeatherDataStatistics currentData)
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

                _logger.Debug(LogURL);

                var response = await _httpClient.GetAsync(url);
                var responseBodyAsText = await response.Content.ReadAsStringAsync();
                _logger.Info("Awekas Response: " + response.StatusCode + ": " + responseBodyAsText);
            }
            catch (Exception ex)
            {
                _logger.Info("Awekas update: " + ex.Message);
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
                 _logger
             ) {Timestamp = timestamp};
             return renderer.Render();
        }


    }
}
