using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BedrockLauncher.Accounts;
using Xunit;

namespace BedrockLauncher.Linux.Tests
{
    public class AccountTests
    {
        /// <summary>Plays Microsoft identity + Xbox Live: one pending poll, then success.</summary>
        private sealed class FakeServices : HttpMessageHandler
        {
            public readonly List<string> Calls = new();
            private int polls;
            public string XstsResponse = "{\"Token\":\"xsts\",\"DisplayClaims\":{\"xui\":[{\"gtg\":\"Steve2026\",\"xid\":\"2535400000000001\",\"uhs\":\"123\"}]}}";
            public HttpStatusCode XstsStatus = HttpStatusCode.OK;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri.ToString();
                string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                Calls.Add(url + " " + body);
                (HttpStatusCode, string) reply = url switch
                {
                    _ when url.EndsWith("/devicecode") => (HttpStatusCode.OK, "{\"device_code\":\"DC\",\"user_code\":\"ABCD-1234\",\"verification_uri\":\"https://microsoft.com/link\",\"expires_in\":900,\"interval\":1,\"message\":\"go\"}"),
                    _ when url.EndsWith("/token") && body.Contains("device_code") && polls++ == 0 => (HttpStatusCode.BadRequest, "{\"error\":\"authorization_pending\"}"),
                    _ when url.EndsWith("/token") => (HttpStatusCode.OK, "{\"access_token\":\"AT\",\"refresh_token\":\"RT2\",\"expires_in\":3600}"),
                    _ when url.Contains("user.auth.xboxlive.com") => (HttpStatusCode.OK, "{\"Token\":\"usertoken\",\"DisplayClaims\":{\"xui\":[{\"uhs\":\"123\"}]}}"),
                    _ when url.Contains("xsts.auth.xboxlive.com") => (XstsStatus, XstsResponse),
                    _ => (HttpStatusCode.NotFound, "{}"),
                };
                return new HttpResponseMessage(reply.Item1) { Content = new StringContent(reply.Item2) };
            }
        }

        [Fact]
        public async Task Device_code_sign_in_goes_through_microsoft_and_xbox_live()
        {
            var services = new FakeServices();
            var auth = new XboxAuthenticator(new HttpClient(services));
            DeviceCodePrompt shown = null;

            StoredAccount account = await auth.SignInAsync("11111111-2222-3333-4444-555555555555", p => shown = p, CancellationToken.None);

            Assert.Equal("ABCD-1234", shown.UserCode);
            Assert.Equal("https://microsoft.com/link", shown.VerificationUri);
            Assert.Equal("Steve2026", account.Gamertag);
            Assert.Equal("2535400000000001", account.Xuid);
            Assert.Equal("RT2", account.RefreshToken);
            Assert.Contains(services.Calls, c => c.Contains("scope=XboxLive.signin+offline_access"));
            Assert.Contains(services.Calls, c => c.Contains("user.auth.xboxlive.com") && c.Contains("\"RpsTicket\":\"d=AT\""));
            Assert.Contains(services.Calls, c => c.Contains("xsts.auth.xboxlive.com") && c.Contains("\"UserTokens\":[\"usertoken\"]"));
        }

        [Fact]
        public async Task Xsts_errors_are_explained()
        {
            var services = new FakeServices { XstsStatus = HttpStatusCode.Unauthorized, XstsResponse = "{\"XErr\":2148916233}" };
            var auth = new XboxAuthenticator(new HttpClient(services));
            var ex = await Assert.ThrowsAsync<XboxAccountException>(() =>
                auth.RefreshAsync(new StoredAccount { ClientId = "id", RefreshToken = "RT" }));
            Assert.Contains("no Xbox profile", ex.Message);
        }

        [Fact]
        public async Task A_missing_client_id_explains_the_azure_setup()
        {
            var auth = new XboxAuthenticator(new HttpClient(new FakeServices()));
            var ex = await Assert.ThrowsAsync<XboxAccountException>(() => auth.SignInAsync("", null, CancellationToken.None));
            Assert.Contains("Azure", ex.Message);
        }
    
        [Fact]
        public async Task Browser_sign_in_comes_back_on_localhost_with_pkce()
        {
            var services = new FakeServices();
            var auth = new XboxAuthenticator(new HttpClient(services));
            BrowserSignInPrompt shown = null;
            using var browser = new HttpClient();

            StoredAccount account = await auth.SignInWithBrowserAsync("11111111-2222-3333-4444-555555555555", prompt =>
            {
                shown = prompt;
                var query = System.Web.HttpUtility.ParseQueryString(new Uri(prompt.AuthorizeUrl).Query);
                // What Microsoft does after the user signs in: redirect to the loopback address with the code.
                _ = Task.Run(async () =>
                {
                    await browser.GetAsync(prompt.RedirectUri + "/favicon.ico");
                    await browser.GetAsync($"{prompt.RedirectUri}/?code=AUTHCODE&state={query["state"]}");
                });
            }, CancellationToken.None);

            var authorize = System.Web.HttpUtility.ParseQueryString(new Uri(shown.AuthorizeUrl).Query);
            Assert.StartsWith("https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?", shown.AuthorizeUrl);
            Assert.Equal("S256", authorize["code_challenge_method"]);
            Assert.Equal(shown.RedirectUri, authorize["redirect_uri"]);
            Assert.Matches(@"^http://localhost:\d+$", shown.RedirectUri);
            Assert.Equal("XboxLive.signin offline_access", authorize["scope"]);
            Assert.Equal("Steve2026", account.Gamertag);

            string tokenCall = services.Calls.Single(c => c.Contains("/token "));
            var form = System.Web.HttpUtility.ParseQueryString(tokenCall.Substring(tokenCall.IndexOf(' ') + 1));
            Assert.Equal("authorization_code", form["grant_type"]);
            Assert.Equal("AUTHCODE", form["code"]);
            Assert.Equal(shown.RedirectUri, form["redirect_uri"]);
            // The verifier must hash to the challenge sent to /authorize.
            string challenge = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(form["code_verifier"])))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Assert.Equal(authorize["code_challenge"], challenge);
        }

        [Fact]
        public async Task Browser_sign_in_rejects_a_foreign_state_and_reports_a_decline()
        {
            var auth = new XboxAuthenticator(new HttpClient(new FakeServices()));
            using var browser = new HttpClient();

            var wrong = await Assert.ThrowsAsync<XboxAccountException>(() => auth.SignInWithBrowserAsync("id", p =>
                _ = Task.Run(() => browser.GetAsync($"{p.RedirectUri}/?code=X&state=forged")), CancellationToken.None));
            Assert.Contains("state", wrong.Message);

            var declined = await Assert.ThrowsAsync<XboxAccountException>(() => auth.SignInWithBrowserAsync("id", p =>
                _ = Task.Run(() => browser.GetAsync($"{p.RedirectUri}/?error=access_denied")), CancellationToken.None));
            Assert.Contains("declined", declined.Message);
        }

        [Fact]
        public async Task Browser_sign_in_can_be_cancelled()
        {
            var auth = new XboxAuthenticator(new HttpClient(new FakeServices()));
            using var cancel = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => auth.SignInWithBrowserAsync("id", _ => cancel.CancelAfter(200), cancel.Token));
        }
    }
}
