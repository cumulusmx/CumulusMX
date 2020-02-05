using System.Collections.Generic;
using System.Threading.Tasks;
using LinqToTwitter;
using TwitterDataReporter;

namespace TwitterDataReporterTest
{
    public class TwitterContextTest : ITwitterContext
    {
        public List<Tweet> Tweets { get; } = new List<Tweet>();

        public void Dispose()
        {
            Tweets.Clear();
        }

        public Task<Status> TweetAsync(string status)
        {
            return TweetAsync(status,default(decimal),default(decimal),default(bool));
        }

        public Task<Status> TweetAsync(string status, string attachmentUrl)
        {
            return TweetAsync(status, default(decimal), default(decimal), default(bool));
        }

        public Task<Status> TweetAsync(string status, decimal latitude, decimal longitude)
        {
            return TweetAsync(status, latitude, longitude, default(bool));
        }

        public Task<Status> TweetAsync(string status, decimal latitude, decimal longitude, bool displayCoordinates)
        {
            return new Task<Status>(() =>
            {
                Tweets.Add(new Tweet()
                    {
                        Status = status, Latitude = latitude, Longitude = longitude,
                        DisplayCoordinates = displayCoordinates
                    }
                );

                return new Status();
            });
        }

        public struct Tweet
        {
            public string Status;
            public decimal Latitude;
            public decimal Longitude;
            public bool DisplayCoordinates;
        }
    }
}