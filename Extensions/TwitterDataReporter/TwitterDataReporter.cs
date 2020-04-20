using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Autofac;
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

        public override string Identifier => "Twitter";

        public TwitterDataReporter(ILogger logger, TwitterReporterSettings settings, IWeatherDataStatistics data) : base(logger, settings, data)
        {
            ReportInterval = TwitterSettings.ReportInterval;
        }

        private TwitterReporterSettings TwitterSettings => (TwitterReporterSettings)Settings;
        public AutofacWrapper DependencyInjection { private get; set; } = AutofacWrapper.Instance;

        private const string UNKNOWN_STRING = "Unknown";

        private IAuthorizer _auth;
        private string _oauthToken = UNKNOWN_STRING;
        private string _oauthTokenSecret;
        private Task _getTokenTask;
        private Dictionary<string, object> _extraRenderParameters;
        private string _template;

        public override void Initialise()
        {
            //TODO: Need to register the standard IAuthorizer and ICredentialStore implementation.  Not currently anywhere to do this.
            _auth = DependencyInjection.Scope.Resolve<IAuthorizer>();
            var credentials = DependencyInjection.Scope.Resolve<ICredentialStore>();
            credentials.ConsumerKey = TwitterSettings.ConsumerKey;
            credentials.ConsumerSecret = TwitterSettings.ConsumerSecret;
            if (credentials is XAuthCredentials xac)
            {
                xac.UserName = TwitterSettings.Username;
                xac.Password = TwitterSettings.Password;
            }

            _auth.CredentialStore = credentials;

            _getTokenTask = Task.Run(new Action(GetToken));

            var filename = Path.Combine(_extensionPath, TwitterSettings.TemplateFilename ?? string.Empty);
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
                if (_getTokenTask == null || _getTokenTask.IsCompleted)
                {
                    _getTokenTask = Task.Run(new Action(GetToken)); 
                }
                _getTokenTask?.Wait();
            }

            using (ITwitterContext twitterCtx = DependencyInjection.Scope.Resolve<ITwitterContext>())
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
                        var latitude = decimal.Parse(Settings.GetValue("Latitude", "0"));
                        var longitude = decimal.Parse(Settings.GetValue("Longitude", "0"));
                        tweet = await twitterCtx.TweetAsync(status, latitude, longitude );
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

        private async void GetToken()
        {
            _log.Debug("Obtaining Twitter tokens");
            await _auth.AuthorizeAsync();

            _oauthToken = _auth.CredentialStore.OAuthToken;
            _oauthTokenSecret = _auth.CredentialStore.OAuthTokenSecret;

            _log.Debug("Tokens obtained");
        }
    }
}
