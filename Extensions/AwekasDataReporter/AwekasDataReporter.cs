using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Antlr.Runtime;
using Antlr4.StringTemplate;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;
using CumulusMX.Common.StringTemplate;
using UnitsNet;

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

            string url = GetUrl(currentData, timestamp, out var passwordString);

            string starredPasswordString = "<password>";

            string LogURL = url.Replace(passwordString, starredPasswordString);

            _logger.Debug(LogURL);

            try
            {
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
                hashPassword = md5.ComputeHash(Encoding.ASCII.GetBytes(Settings.GetValue("AwekasPW",string.Empty)));
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

            var templateGroup = new TemplateGroup();
            templateGroup.RegisterRenderer(typeof(double),new DefaultNumberRenderer("{0:F1}"));
            templateGroup.RegisterRenderer(typeof(DateTime),new DateRenderer());
            var templateString = File.ReadAllText("AwekasUrl.StringTemplate");
            var template = new Template(templateGroup,templateString);
            template.Add("Settings", Settings);
            template.Add("passwordString", passwordString);
            template.Add("data", data);
            template.Add("timestamp", DateTime.Now);
            template.Add("pressureTrend", pressureTrend);
            template.Add("IsFineOffset", false); //TODO: fill value
            template.Add("Version","4"); //TODO: Lookup version

            return template.Render(CultureInfo.InvariantCulture);
        }


    }
}
