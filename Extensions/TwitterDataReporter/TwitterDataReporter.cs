using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CumulusMX.Common;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;
using LinqToTwitter;

namespace TwitterDataReporter
{
    public class TwitterDataReporter : DataReporterBase
    {
        public override string ServiceName => "Twitter Data Reporter Service";

        public override string Identifier => "Twitter"; //TODO

        public TwitterDataReporter(ILogger logger, TwitterReporterSettings settings, IWeatherDataStatistics data) : base(logger, settings, data)
        {
            ReportInterval = TwitterSettings.ReportInterval;
        }

        private TwitterReporterSettings TwitterSettings => (TwitterReporterSettings)Settings;

        private const string UNKNOWN_STRING = "Unknwon";

        private XAuthAuthorizer _auth;
        private string _oauthToken = UNKNOWN_STRING;
        private string _oauthTokenSecret;
        private Task _getTokenTask;
        private Dictionary<string, object> _extraRenderParameters;
        private string _template;

        public override void Initialise()
        {
            _auth = new XAuthAuthorizer
            {
                CredentialStore =
                    new XAuthCredentials
                    {
                        ConsumerKey = TwitterSettings.ConsumerKey,
                        ConsumerSecret = TwitterSettings.ConsumerSecret,
                        UserName = TwitterSettings.Username,
                        Password = TwitterSettings.Password
                    }
            };

            _getTokenTask = GetToken();

            var filename = Path.Combine(_extensionPath, TwitterSettings.TemplateFilename);
            if (File.Exists(filename))
            {
                _template = File.ReadAllText(filename);
            }
            else
            {
                _template =
                    @"Wind [data.WindSpeed.DayAverage;format=""{ 0:F0}""] [data.WindBearing.DayAverage.Degrees;format=""{ 0:F0}""]. Barometer [data.Pressure.Latest] [data.PressureTrend.Latest]";
                //, " + station.Presstrendstr;
                //status += ". Temperature " + station.OutdoorTemperature.ToString(TempFormat) + " " + TempUnitText;
                //status += ". Rain today " + station.RainToday.ToString(RainFormat) + RainUnitText;
                //status += ". Humidity " + station.OutdoorHumidity + "%";

            }

            _extraRenderParameters = new Dictionary<string, object>();

        }

        public override async void DoReport(IWeatherDataStatistics currentData)
        {
            _log.Debug("Starting Twitter update");

            var data = currentData;

            if (_oauthToken == UNKNOWN_STRING)
            {
                if (_getTokenTask.IsCompleted)
                    _getTokenTask = GetToken();

                _getTokenTask.Wait();
            }

            using (var twitterCtx = new TwitterContext(_auth))
            {
                var renderer = new TemplateRenderer
                    (
                        new StringReader(_template),
                        data,
                        Settings,
                        _extraRenderParameters,
                        _log
                    )
                    { Timestamp = data.Time };

                string status = renderer.Render();

                _log.Debug("Updating Twitter: " + status);

                Status tweet;

                try
                {
                    if (TwitterSettings.SendLocation)
                    {
                        tweet = await twitterCtx.TweetAsync(status, decimal.Parse(Settings.GetValue("Latitude","0")), decimal.Parse(Settings.GetValue("Longitude","0")));
                    }
                    else
                    {
                        tweet = await twitterCtx.TweetAsync(status);
                    }

                    if (tweet == null)
                    {
                        _log.Warn("Null Twitter response");
                    }
                    else
                    {
                        _log.Debug($"Status returned: ({tweet.StatusID})[{tweet.User.Name}]  {tweet.Text}, {tweet.CreatedAt}");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("UpdateTwitter: " + ex.Message);
                }
                //if (tweet != null)
                //    Console.WriteLine("Status returned: " + "(" + tweet.StatusID + ")" + tweet.User.Name + ", " + tweet.Text + "\n");
            }
        }

        private async Task GetToken()
        {
            _log.Debug("Obtaining Twitter tokens");
            await _auth.AuthorizeAsync();

            _oauthToken = _auth.CredentialStore.OAuthToken;
            _oauthTokenSecret = _auth.CredentialStore.OAuthTokenSecret;

            _log.Debug("Tokens obtained");
        }
    }
}
