using System;
using System.Threading.Tasks;
using LinqToTwitter;

namespace TwitterDataReporter
{
    public interface ITwitterContext : IDisposable
    {
        Task<Status> TweetAsync(string status);
        Task<Status> TweetAsync(string status, string attachmentUrl);
        Task<Status> TweetAsync(string status, decimal latitude, decimal longitude);
        Task<Status> TweetAsync(string status, decimal latitude, decimal longitude, bool displayCoordinates);
    }
}