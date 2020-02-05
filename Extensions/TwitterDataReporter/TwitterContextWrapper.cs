using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CumulusMX.Extensions;
using LinqToTwitter;
using NotSupportedException = System.NotSupportedException;

namespace TwitterDataReporter
{
    public class TwitterContextWrapper : ITwitterContext, IDisposable, IPassive
    {
        private TwitterContext _context;

        public TwitterContextWrapper(IAuthorizer authorizer)
        {
            _context = new TwitterContext(authorizer);
        }
        public TwitterContextWrapper(ITwitterExecute execute)
        {
            _context = new TwitterContext(execute);
        }

        public TwitterContextWrapper(TwitterContext context)
        {
            _context = context;
        }

        public Task<AccountActivity> AddAccountActivitySubscriptionAsync(ulong webhookID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.AddAccountActivitySubscriptionAsync(webhookID, cancelToken);
        }

        public Task<AccountActivity> AddAccountActivityWebhookAsync(string url, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.AddAccountActivityWebhookAsync(url, cancelToken);
        }

        public Task<AccountActivity> SendAccountActivityCrcAsync(ulong webhookID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.SendAccountActivityCrcAsync(webhookID, cancelToken);
        }

        public Task<AccountActivity> DeleteAccountActivitySubscriptionAsync(ulong webhookID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteAccountActivitySubscriptionAsync(webhookID, cancelToken);
        }

        public Task<AccountActivity> DeleteAccountActivityWebhookAsync(ulong webhookID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteAccountActivityWebhookAsync(webhookID, cancelToken);
        }

        public Task<User> UpdateAccountColorsAsync(string background, string text, string link, string sidebarFill, string sidebarBorder,
            bool skipStatus)
        {
            throw new NotSupportedException("Method deprecated.");
        }

        public Task<User> UpdateAccountColorsAsync(string background, string text, string link, string sidebarFill, string sidebarBorder,
            bool includeEntities, bool skipStatus, CancellationToken cancelToken = new CancellationToken())
        {
            throw new NotSupportedException("Method deprecated.");
        }

        public Task<User> UpdateAccountImageAsync(byte[] image, string fileName, string imageType, bool skipStatus,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateAccountImageAsync(image, fileName, imageType, skipStatus, cancelToken);
        }

        public Task<User> UpdateAccountImageAsync(byte[] image, string fileName, string imageType, bool includeEntities, bool skipStatus,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateAccountImageAsync(image, fileName, imageType, includeEntities, skipStatus, cancelToken);
        }

        public Task<User> UpdateAccountBackgroundImageAsync(byte[] image, string fileName, string imageType, bool tile, bool includeEntities,
            bool skipStatus, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateAccountBackgroundImageAsync(image, fileName, imageType, tile, includeEntities, skipStatus, cancelToken);
        }

        public Task<User> UpdateAccountBackgroundImageAsync(ulong mediaID, string fileName, string imageType, bool tile,
            bool includeEntities, bool skipStatus, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateAccountBackgroundImageAsync(mediaID, fileName, imageType, tile, includeEntities, skipStatus, cancelToken);
        }

        public Task<User> UpdateAccountBackgroundImageAsync(byte[] image, ulong mediaID, string fileName, string imageType, bool tile,
            bool includeEntities, bool skipStatus, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateAccountBackgroundImageAsync(image, mediaID, fileName, imageType, tile, includeEntities, skipStatus, cancelToken);
        }

        public Task<User> UpdateAccountProfileAsync(string name, string url, string location, string description, bool skipStatus,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateAccountProfileAsync(name, url, location, description, skipStatus, cancelToken);
        }

        public Task<User> UpdateAccountProfileAsync(string name, string url, string location, string description, bool includeEntities,
            bool skipStatus, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateAccountProfileAsync(name, url, location, description, includeEntities, skipStatus, cancelToken);
        }

        public Task<Account> UpdateAccountSettingsAsync(int? trendLocationWoeid, bool? sleepTimeEnabled, int? startSleepTime, int? endSleepTime,
            string timeZone, string lang, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateAccountSettingsAsync(trendLocationWoeid, sleepTimeEnabled, startSleepTime, endSleepTime, timeZone, lang, cancelToken);
        }

        public Task<Account> UpdateDeliveryDeviceAsync(DeviceType device, bool? includeEntitites,
            CancellationToken cancelToken = new CancellationToken())
        {
            throw new NotSupportedException("Method deprecated.");
        }

        public Task<User> UpdateProfileBannerAsync(byte[] banner, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateProfileBannerAsync(banner, cancelToken);
        }

        public Task<User> UpdateProfileBannerAsync(byte[] banner, int width, int height, int offsetLeft, int offsetTop,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateProfileBannerAsync(banner, width, height, offsetLeft, offsetTop, cancelToken);
        }

        public Task<User> RemoveProfileBannerAsync(CancellationToken cancelToken = new CancellationToken())
        {
            return _context.RemoveProfileBannerAsync(cancelToken);
        }

        public Task<User> CreateBlockAsync(ulong userID, string screenName, bool skipStatus)
        {
            return _context.CreateBlockAsync(userID, screenName, skipStatus);
        }

        public Task<User> CreateBlockAsync(ulong userID, string screenName, bool includeEntities, bool skipStatus,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.CreateBlockAsync(userID, screenName, includeEntities, skipStatus, cancelToken);
        }

        public Task<User> DestroyBlockAsync(ulong userID, string screenName, bool skipStatus,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DestroyBlockAsync(userID, screenName, skipStatus, cancelToken);
        }

        public Task<User> DestroyBlockAsync(ulong userID, string screenName, bool includeEntities, bool skipStatus,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DestroyBlockAsync(userID, screenName, includeEntities, skipStatus, cancelToken);
        }

        public Task<DirectMessageEvents> NewDirectMessageEventAsync(ulong recipientID, string text, ulong mediaId,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.NewDirectMessageEventAsync(recipientID, text, mediaId, cancelToken);
        }

        public Task<DirectMessageEvents> NewDirectMessageEventAsync(ulong recipientID, string text, double latitude, double longitude,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.NewDirectMessageEventAsync(recipientID, text, latitude, longitude, cancelToken);
        }

        public Task<DirectMessageEvents> NewDirectMessageEventAsync(ulong recipientID, string text, string placeID,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.NewDirectMessageEventAsync(recipientID, text, placeID, cancelToken);
        }

        public Task<DirectMessageEvents> NewDirectMessageEventAsync(ulong recipientID, string text,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.NewDirectMessageEventAsync(recipientID, text, cancelToken);
        }

        public Task<DirectMessageEvents> RequestQuickReplyOptionsAsync(ulong recipientID, string text, IEnumerable<QuickReplyOption> options,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.RequestQuickReplyOptionsAsync(recipientID, text, options, cancelToken);
        }

        public Task<DirectMessageEvents> RequestButtonChoiceAsync(ulong recipientID, string text, IEnumerable<CallToAction> callToActions,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.RequestButtonChoiceAsync(recipientID, text, callToActions, cancelToken);
        }

        public Task DeleteDirectMessageEventAsync(ulong directMessageID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteDirectMessageEventAsync(directMessageID, cancelToken);
        }

        public Task MarkReadAsync(ulong lastReadEventID, ulong recipientID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.MarkReadAsync(lastReadEventID, recipientID, cancelToken);
        }

        public Task IndicateTypingAsync(ulong recipientID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.IndicateTypingAsync(recipientID, cancelToken);
        }

        public Task<DirectMessage> NewDirectMessageAsync(string screenName, string text, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.NewDirectMessageAsync(screenName, text, cancelToken);
        }

        public Task<DirectMessage> NewDirectMessageAsync(ulong userID, string text, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.NewDirectMessageAsync(userID, text, cancelToken);
        }

        public Task<DirectMessage> DestroyDirectMessageAsync(ulong id, bool includeEntites, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DestroyDirectMessageAsync(id, includeEntites, cancelToken);
        }

        public Task<Status> CreateFavoriteAsync(ulong id)
        {
            return _context.CreateFavoriteAsync(id);
        }

        public Task<Status> CreateFavoriteAsync(ulong id, bool includeEntities, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.CreateFavoriteAsync(id, includeEntities, cancelToken);
        }

        public Task<Status> DestroyFavoriteAsync(ulong id, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DestroyFavoriteAsync(id, cancelToken);
        }

        public Task<Status> DestroyFavoriteAsync(ulong id, bool includeEntities, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DestroyFavoriteAsync(id, includeEntities, cancelToken);
        }

        public Task<User> CreateFriendshipAsync(ulong userID, bool follow, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.CreateFriendshipAsync(userID, follow, cancelToken);
        }

        public Task<User> CreateFriendshipAsync(string screenName, bool follow, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.CreateFriendshipAsync(screenName, follow, cancelToken);
        }

        public Task<User> DestroyFriendshipAsync(ulong userID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DestroyFriendshipAsync(userID, cancelToken);
        }

        public Task<User> DestroyFriendshipAsync(string screenName, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DestroyFriendshipAsync(screenName, cancelToken);
        }

        public Task<Friendship> UpdateFriendshipSettingsAsync(string screenName, bool retweets, bool device,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateFriendshipSettingsAsync(screenName, retweets, device, cancelToken);
        }

        public Task<Friendship> UpdateFriendshipSettingsAsync(ulong userID, bool retweets, bool device,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateFriendshipSettingsAsync(userID, retweets, device, cancelToken);
        }

        public Task<object> ExecuteAsync<T>(Expression expression, bool isEnumerable) where T : class
        {
            return _context.ExecuteAsync<T>(expression, isEnumerable);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        public Task<List> CreateListAsync(string listName, string mode, string description,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.CreateListAsync(listName, mode, description, cancelToken);
        }

        public Task<List> UpdateListAsync(ulong listID, string slug, string name, ulong ownerID, string ownerScreenName, string mode,
            string description, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateListAsync(listID, slug, name, ownerID, ownerScreenName, mode, description, cancelToken);
        }

        public Task<List> DeleteListAsync(ulong listID, string slug, ulong ownerID, string ownerScreenName,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteListAsync(listID, slug, ownerID, ownerScreenName, cancelToken);
        }

        public Task<List> AddMemberToListAsync(ulong userID, ulong listID, string slug, ulong ownerID, string ownerScreenName,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.AddMemberToListAsync(userID, listID, slug, ownerID, ownerScreenName, cancelToken);
        }

        public Task<List> AddMemberToListAsync(string screenName, ulong listID, string slug, ulong ownerID, string ownerScreenName,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.AddMemberToListAsync(screenName, listID, slug, ownerID, ownerScreenName, cancelToken);
        }

        public Task<List> AddMemberRangeToListAsync(ulong listID, string slug, ulong ownerID, string ownerScreenName, List<string> screenNames,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.AddMemberRangeToListAsync(listID, slug, ownerID, ownerScreenName, screenNames, cancelToken);
        }

        public Task<List> AddMemberRangeToListAsync(ulong listID, string slug, ulong ownerID, string ownerScreenName, List<ulong> userIDs,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.AddMemberRangeToListAsync(listID, slug, ownerID, ownerScreenName, userIDs, cancelToken);
        }

        public Task<List> DeleteMemberFromListAsync(ulong userID, string screenName, ulong listID, string slug, ulong ownerID,
            string ownerScreenName, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteMemberFromListAsync(userID, screenName, listID, slug, ownerID, ownerScreenName, cancelToken);
        }

        public Task<List> SubscribeToListAsync(ulong listID, string slug, ulong ownerID, string ownerScreenName,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.SubscribeToListAsync(listID, slug, ownerID, ownerScreenName, cancelToken);
        }

        public Task<List> UnsubscribeFromListAsync(ulong listID, string slug, ulong ownerID, string ownerScreenName,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UnsubscribeFromListAsync(listID, slug, ownerID, ownerScreenName, cancelToken);
        }

        public Task<List> DeleteMemberRangeFromListAsync(ulong listID, string slug, List<ulong> userIDs, ulong ownerID, string ownerScreenName,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteMemberRangeFromListAsync(listID, slug, userIDs, ownerID, ownerScreenName, cancelToken);
        }

        public Task<List> DeleteMemberRangeFromListAsync(ulong listID, string slug, List<string> screenNames, ulong ownerID, string ownerScreenName,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteMemberRangeFromListAsync(listID, slug, screenNames, ownerID, ownerScreenName, cancelToken);
        }

        public Task<Media> UploadMediaAsync(byte[] media, string mediaType, string mediaCategory, bool shared = false,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UploadMediaAsync(media, mediaType, mediaCategory, shared, cancelToken);
        }

        public Task<Media> UploadMediaAsync(byte[] media, string mediaType, IEnumerable<ulong> additionalOwners, string mediaCategory,
            bool shared = false, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UploadMediaAsync(media, mediaType, additionalOwners, mediaCategory, shared, cancelToken);
        }

        public Task CreateMediaMetadataAsync(ulong mediaID, string altText, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.CreateMediaMetadataAsync(mediaID, altText, cancelToken);
        }

        public Task<User> MuteAsync(string screenName, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.MuteAsync(screenName, cancelToken);
        }

        public Task<User> MuteAsync(ulong userID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.MuteAsync(userID, cancelToken);
        }

        public Task<User> UnMuteAsync(string screenName, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UnMuteAsync(screenName, cancelToken);
        }

        public Task<User> UnMuteAsync(ulong userID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UnMuteAsync(userID, cancelToken);
        }

        public Task<string> ExecuteRawAsync(string queryString, Dictionary<string, string> parameters,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.ExecuteRawAsync(queryString, parameters, cancelToken);
        }

        public Task<SavedSearch> CreateSavedSearchAsync(string query, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.CreateSavedSearchAsync(query, cancelToken);
        }

        public Task<SavedSearch> DestroySavedSearchAsync(ulong id, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DestroySavedSearchAsync(id, cancelToken);
        }

        public Task<Status> ReplyAsync(ulong tweetID, string status)
        {
            return _context.ReplyAsync(tweetID, status);
        }

        public Task<Status> ReplyAsync(ulong tweetID, string status, bool autoPopulateReplyMetadata, IEnumerable<ulong> excludeReplyUserIds,
            string attachmentUrl)
        {
            return _context.ReplyAsync(tweetID, status, autoPopulateReplyMetadata, excludeReplyUserIds, attachmentUrl);
        }

        public Task<Status> ReplyAsync(ulong tweetID, string status, decimal latitude, decimal longitude, bool displayCoordinates)
        {
            return _context.ReplyAsync(tweetID, status, latitude, longitude, displayCoordinates);
        }

        public Task<Status> ReplyAsync(ulong tweetID, string status, decimal latitude, decimal longitude, string placeID, bool trimUser)
        {
            return _context.ReplyAsync(tweetID, status, latitude, longitude, placeID, trimUser);
        }

        public Task<Status> ReplyAsync(ulong tweetID, string status, string placeID, bool displayCoordinates, bool trimUser)
        {
            return _context.ReplyAsync(tweetID, status, placeID, displayCoordinates, trimUser);
        }

        public Task<Status> ReplyAsync(ulong tweetID, string status, decimal latitude, decimal longitude, string placeID,
            bool displayCoordinates, bool trimUser)
        {
            return _context.ReplyAsync(tweetID, status, latitude, longitude, placeID, displayCoordinates, trimUser);
        }

        public Task<Status> ReplyAsync(ulong tweetID, string status, IEnumerable<ulong> mediaIds)
        {
            return _context.ReplyAsync(tweetID, status, mediaIds);
        }

        public Task<Status> ReplyAsync(ulong tweetID, string status, decimal latitude = decimal.MaxValue,
            decimal longitude = decimal.MaxValue, string placeID = null, bool displayCoordinates = false,
            bool trimUser = false, IEnumerable<ulong> mediaIds = null, bool autoPopulateReplyMetadata = false,
            IEnumerable<ulong> excludeReplyUserIds = null, string attachmentUrl = null)
        {
            return _context.ReplyAsync(tweetID, status, latitude, longitude, placeID, displayCoordinates, trimUser, mediaIds, autoPopulateReplyMetadata, excludeReplyUserIds, attachmentUrl);
        }

        public Task<Status> TweetAsync(string status)
        {
            return _context.TweetAsync(status);
        }

        public Task<Status> TweetAsync(string status, string attachmentUrl)
        {
            return _context.TweetAsync(status, attachmentUrl);
        }

        public Task<Status> TweetAsync(string status, decimal latitude, decimal longitude)
        {
            return _context.TweetAsync(status, latitude, longitude);
        }

        public Task<Status> TweetAsync(string status, decimal latitude, decimal longitude, bool displayCoordinates)
        {
            return _context.TweetAsync(status, latitude, longitude, displayCoordinates);
        }

        public Task<Status> TweetAsync(string status, decimal latitude, decimal longitude, string placeID, bool trimUser)
        {
            return _context.TweetAsync(status, latitude, longitude, placeID, trimUser);
        }

        public Task<Status> TweetAsync(string status, decimal latitude, decimal longitude, string placeID, bool displayCoordinates,
            bool trimUser)
        {
            return _context.TweetAsync(status, latitude, longitude, placeID, displayCoordinates, trimUser);
        }

        public Task<Status> TweetAsync(string status, string placeID, bool displayCoordinates, bool trimUser)
        {
            return _context.TweetAsync(status, placeID, displayCoordinates, trimUser);
        }

        public Task<Status> TweetAsync(string status, IEnumerable<ulong> mediaIds)
        {
            return _context.TweetAsync(status, mediaIds);
        }

        public Task<Status> TweetAsync(string status, decimal latitude, decimal longitude, string placeID, bool displayCoordinates,
            bool trimUser, IEnumerable<ulong> mediaIds, string attachmentUrl = null)
        {
            return _context.TweetAsync(status, latitude, longitude, placeID, displayCoordinates, trimUser, mediaIds, attachmentUrl);
        }

        public Task<Status> DeleteTweetAsync(ulong tweetID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteTweetAsync(tweetID, cancelToken);
        }

        public Task<Status> RetweetAsync(ulong tweetID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.RetweetAsync(tweetID, cancelToken);
        }

        public Task<ControlStream> AddSiteStreamUserAsync(List<ulong> userIDs, string streamID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.AddSiteStreamUserAsync(userIDs, streamID, cancelToken);
        }

        public Task<ControlStream> RemoveSiteStreamUserAsync(List<ulong> userIDs, string streamID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.RemoveSiteStreamUserAsync(userIDs, streamID, cancelToken);
        }

        public Task<User> ReportSpamAsync(ulong userID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.ReportSpamAsync(userID, cancelToken);
        }

        public Task<User> ReportSpamAsync(string screenName, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.ReportSpamAsync(screenName, cancelToken);
        }

        public Task<WelcomeMessage> NewWelcomeMessageAsync(string name, string text, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.NewWelcomeMessageAsync(name, text, cancelToken);
        }

        public Task<WelcomeMessage> UpdateWelcomeMessageAsync(ulong welcomeMessageID, string name, string text,
            CancellationToken cancelToken = new CancellationToken())
        {
            return _context.UpdateWelcomeMessageAsync(welcomeMessageID, name, text, cancelToken);
        }

        public Task DeleteWelcomeMessageAsync(ulong welcomeMessageID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteWelcomeMessageAsync(welcomeMessageID, cancelToken);
        }

        public Task<WelcomeMessage> NewWelcomeMessageRuleAsync(ulong welcomeMessageID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.NewWelcomeMessageRuleAsync(welcomeMessageID, cancelToken);
        }

        public Task DeleteWelcomeMessageRuleAsync(ulong welcomeMessageRuleID, CancellationToken cancelToken = new CancellationToken())
        {
            return _context.DeleteWelcomeMessageRuleAsync(welcomeMessageRuleID, cancelToken);
        }

        public string BaseUrl
        {
            get => _context.BaseUrl;
            set => _context.BaseUrl = value;
        }

        public string VineUrl
        {
            get => _context.VineUrl;
            set => _context.VineUrl = value;
        }

        public string UploadUrl
        {
            get => _context.UploadUrl;
            set => _context.UploadUrl = value;
        }

        public string StreamingUrl
        {
            get => _context.StreamingUrl;
            set => _context.StreamingUrl = value;
        }

        public string UserStreamUrl
        {
            get => _context.UserStreamUrl;
            set => _context.UserStreamUrl = value;
        }

        public string SiteStreamUrl
        {
            get => _context.SiteStreamUrl;
            set => _context.SiteStreamUrl = value;
        }

        public TextWriter Log
        {
            get => _context.Log;
            set => _context.Log = value;
        }

        public string RawResult
        {
            get => _context.RawResult;
            set => _context.RawResult = value;
        }

        public bool ExcludeRawJson
        {
            get => _context.ExcludeRawJson;
            set => _context.ExcludeRawJson = value;
        }

        public string UserAgent
        {
            get => _context.UserAgent;
            set => _context.UserAgent = value;
        }

        public int ReadWriteTimeout
        {
            get => _context.ReadWriteTimeout;
            set => _context.ReadWriteTimeout = value;
        }

        public int Timeout
        {
            get => _context.Timeout;
            set => _context.Timeout = value;
        }

        public IAuthorizer Authorizer
        {
            get => _context.Authorizer;
            set => _context.Authorizer = value;
        }

        public IWebProxy Proxy
        {
            get => _context.Proxy;
            set => _context.Proxy = value;
        }

        public Uri LastUrl => _context.LastUrl;

        public IDictionary<string, string> ResponseHeaders => _context.ResponseHeaders;

        public int RateLimitCurrent => _context.RateLimitCurrent;

        public int RateLimitRemaining => _context.RateLimitRemaining;

        public int RateLimitReset => _context.RateLimitReset;

        public int RetryAfter => _context.RetryAfter;

        public int MediaRateLimitCurrent => _context.MediaRateLimitCurrent;

        public int MediaRateLimitRemaining => _context.MediaRateLimitRemaining;

        public int MediaRateLimitReset => _context.MediaRateLimitReset;

        public DateTime? TwitterDate => _context.TwitterDate;

        public TwitterQueryable<Account> Account => _context.Account;

        public TwitterQueryable<AccountActivity> AccountActivity => _context.AccountActivity;

        public TwitterQueryable<Blocks> Blocks => _context.Blocks;

        public TwitterQueryable<ControlStream> ControlStream => _context.ControlStream;

        public TwitterQueryable<DirectMessage> DirectMessage => _context.DirectMessage;

        public TwitterQueryable<DirectMessageEvents> DirectMessageEvents => _context.DirectMessageEvents;

        public TwitterQueryable<Favorites> Favorites => _context.Favorites;

        public TwitterQueryable<Friendship> Friendship => _context.Friendship;

        public TwitterQueryable<Geo> Geo => _context.Geo;

        public TwitterQueryable<Help> Help => _context.Help;

        public TwitterQueryable<Media> Media => _context.Media;

        public TwitterQueryable<Mute> Mute => _context.Mute;

        public TwitterQueryable<List> List => _context.List;

        public TwitterQueryable<Raw> RawQuery => _context.RawQuery;

        public TwitterQueryable<SavedSearch> SavedSearch => _context.SavedSearch;

        public TwitterQueryable<Search> Search => _context.Search;

        public TwitterQueryable<Status> Status => _context.Status;

        public TwitterQueryable<Streaming> Streaming => _context.Streaming;

        public TwitterQueryable<Trend> Trends => _context.Trends;

        public TwitterQueryable<User> User => _context.User;

        public TwitterQueryable<Vine> Vine => _context.Vine;

        public TwitterQueryable<WelcomeMessage> WelcomeMessage => _context.WelcomeMessage;
    }
}
