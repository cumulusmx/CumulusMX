using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LinqToTwitter;

namespace TwitterDataReporterTest
{
    public class XAuthoriserTest : IAuthorizer
    {
        public Task AuthorizeAsync()
        {
            return new Task(() => { });
        }

        public string GetAuthorizationString(string method, string oauthUrl, IDictionary<string, string> parameters)
        {
            return string.Empty;
        }

        public string UserAgent { get; set; }
        public ICredentialStore CredentialStore { get; set; }
        public IWebProxy Proxy { get; set; }
        public bool SupportsCompression { get; set; }
    }

    public class XCredentialsTest : ICredentialStore
    {
        public bool HasAllCredentials()
        {
            return true;
        }

        public Task LoadAsync()
        {
            return new Task(() => {});
        }

        public Task StoreAsync()
        {
            return new Task(() => { });
        }

        public Task ClearAsync()
        {
            return new Task(() => { });
        }

        public string ConsumerKey { get; set; }
        public string ConsumerSecret { get; set; }
        public string OAuthToken { get; set; }
        public string OAuthTokenSecret { get; set; }
        public string ScreenName { get; set; }
        public ulong UserID { get; set; }
    }
}
