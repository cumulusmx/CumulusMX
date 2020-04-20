using System.Collections.Generic;
using System.Threading.Tasks;
using LinqToTwitter;
using TwitterDataReporter;

namespace TwitterDataReporterTest
{
    public class TwitterContextTest : ITwitterContext
    {
        public List<Tweet> Tweets { get; } = new List<Tweet>();
        public string Tag { get; set; }

        public void Dispose()
        {
            Tweets.Clear();
        }

        public async Task<Status> TweetAsync(string status)
        {
            return await TweetAsync(status,default(decimal),default(decimal),default(bool));
        }

        public async Task<Status> TweetAsync(string status, string attachmentUrl)
        {
            return await TweetAsync(status, default(decimal), default(decimal), default(bool));
        }

        public async Task<Status> TweetAsync(string status, decimal latitude, decimal longitude)
        {
            return await TweetAsync(status, latitude, longitude, default(bool));
        }

        public async Task<Status> TweetAsync(string status, decimal latitude, decimal longitude, bool displayCoordinates)
        {
            Tweets.Add(new Tweet()
                {
                    Status = status, Latitude = latitude, Longitude = longitude,
                    DisplayCoordinates = displayCoordinates
                }
            );

            var result = new Status() { StatusID = 12345, Text = status, CreatedAt = System.DateTime.Now, User = new User() {Name = "Test User" } };
            return result;
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