using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace TwitterDataReporter
{
    public class TwitterDataReporter : IDataReporter
    {
        private ILogger _logger;
        public string ServiceName => "Twitter Data Reporter Service";

        public IDataReporterSettings Settings { get; private set; }
        public void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public string Identifier => "TBC"; //TODO

        public TwitterDataReporter()
        {
            //TODO: Implement
        }

        public void Initialise(ILogger logger, ISettings settings)
        {
            _logger = logger;
            Settings = settings as IDataReporterSettings;
        }

        /*
        internal async void UpdateTwitter()
        {
            LogDebugMessage("Starting Twitter update");
            var auth = new XAuthAuthorizer
            {
                CredentialStore =
                               new XAuthCredentials { ConsumerKey = twitterKey, ConsumerSecret = twitterSecret, UserName = Twitteruser, Password = TwitterPW }
            };

            if (TwitterOauthToken == "unknown")
            {
                // need to get tokens using xauth
                LogDebugMessage("Obtaining Twitter tokens");
                await auth.AuthorizeAsync();

                TwitterOauthToken = auth.CredentialStore.OAuthToken;
                TwitterOauthTokenSecret = auth.CredentialStore.OAuthTokenSecret;
                //LogDebugMessage("Token=" + TwitterOauthToken);
                //LogDebugMessage("TokenSecret=" + TwitterOauthTokenSecret);
                LogDebugMessage("Tokens obtained");
            }
            else
            {
                auth.CredentialStore.OAuthToken = TwitterOauthToken;
                auth.CredentialStore.OAuthTokenSecret = TwitterOauthTokenSecret;
            }

            using (var twitterCtx = new TwitterContext(auth))
            {
                string status;

                if (File.Exists(TwitterTxtFile))
                {
                    // use twitter.txt file
                    LogDebugMessage("Using twitter.txt file");
                    var twitterTokenParser = new TokenParser();
                    var utf8WithoutBom = new System.Text.UTF8Encoding(false);
                    var encoding = utf8WithoutBom;
                    twitterTokenParser.encoding = encoding;
                    twitterTokenParser.SourceFile = TwitterTxtFile;
                    twitterTokenParser.OnToken += TokenParserOnToken;
                    status = twitterTokenParser.ToString();
                }
                else
                {
                    // default message
                    status = "Wind " + station.WindAverage.ToString(WindFormat) + " " + WindUnitText + " " + station.AvgBearingText;
                    status += ". Barometer " + station.Pressure.ToString(PressFormat) + " " + PressUnitText + ", " + station.Presstrendstr;
                    status += ". Temperature " + station.OutdoorTemperature.ToString(TempFormat) + " " + TempUnitText;
                    status += ". Rain today " + station.RainToday.ToString(RainFormat) + RainUnitText;
                    status += ". Humidity " + station.OutdoorHumidity + "%";
                }

                LogDebugMessage("Updating Twitter: " + status);

                Status tweet;

                try
                {
                    if (TwitterSendLocation)
                    {
                        tweet = await twitterCtx.TweetAsync(status, (decimal)Latitude, (decimal)Longitude);
                    }
                    else
                    {
                        tweet = await twitterCtx.TweetAsync(status);
                    }

                    if (tweet == null)
                    {
                        LogDebugMessage("Null Twitter response");
                    }
                    else
                    {
                        LogDebugMessage("Status returned: " + "(" + tweet.StatusID + ")" + "[" + tweet.User.Name + "]" + tweet.User.Name + ", " + tweet.Text + ", " +
                                        tweet.CreatedAt + "\n");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage("UpdateTwitter: " + ex.Message);
                }
                //if (tweet != null)
                //    Console.WriteLine("Status returned: " + "(" + tweet.StatusID + ")" + tweet.User.Name + ", " + tweet.Text + "\n");
            }
        }
        */

    }
}
