using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BedrockLauncher.Accounts
{
    /// <summary>What the user has to do to finish a device code sign-in.</summary>
    public sealed class DeviceCodePrompt
    {
        public string UserCode { get; init; }
        public string VerificationUri { get; init; }
        public string Message { get; init; }
        public DateTime ExpiresAt { get; init; }
    }

    public sealed class XboxAccountException : Exception
    {
        public XboxAccountException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>The signed-in account as stored on disk (refresh token only; the short-lived tokens are re-derived).</summary>
    public sealed class StoredAccount
    {
        public string Gamertag { get; set; }
        public string Xuid { get; set; }
        public string UserHash { get; set; }
        public string RefreshToken { get; set; }
        public string ClientId { get; set; }
        public DateTime SignedInAt { get; set; }
        /// <summary>Last Minecraft Bedrock ownership answer, kept so an offline start still knows it.</summary>
        public OwnershipResult Ownership { get; set; }

        /// <summary>user.auth.xboxlive.com token of this session (short-lived, never written to disk).</summary>
        [JsonIgnore] public string XboxUserToken { get; set; }
    }

    /// <summary>
    /// Microsoft account + Xbox Live sign-in, the same chain Minecraft uses:
    /// Microsoft identity platform (device code flow, consumer accounts, scope XboxLive.signin)
    /// -> user.auth.xboxlive.com (user token) -> xsts.auth.xboxlive.com (XSTS token, gamertag, XUID).
    /// The OAuth client is the launcher's own Azure app registration (Settings > Accounts).
    /// </summary>
    public sealed class XboxAuthenticator
    {
        public const string Scope = "XboxLive.signin offline_access";
        private const string Authority = "https://login.microsoftonline.com/consumers/oauth2/v2.0";

        private readonly HttpClient http;

        public XboxAuthenticator(HttpClient http = null)
        {
            this.http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>Full interactive sign-in: shows the code through <paramref name="prompt"/> and waits for the user.</summary>
        public async Task<StoredAccount> SignInAsync(string clientId, Action<DeviceCodePrompt> prompt, CancellationToken cancellationToken)
        {
            RequireClientId(clientId);
            JObject code = await PostFormAsync($"{Authority}/devicecode", new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = Scope,
            }, cancellationToken);

            prompt?.Invoke(new DeviceCodePrompt
            {
                UserCode = (string)code["user_code"],
                VerificationUri = (string)code["verification_uri"],
                Message = (string)code["message"],
                ExpiresAt = DateTime.UtcNow.AddSeconds((int?)code["expires_in"] ?? 900),
            });

            int interval = Math.Max(1, (int?)code["interval"] ?? 5);
            DateTime expires = DateTime.UtcNow.AddSeconds((int?)code["expires_in"] ?? 900);
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);
                if (DateTime.UtcNow > expires) throw new XboxAccountException("The sign-in code expired. Start the sign-in again.");

                JObject token = await PostFormAsync($"{Authority}/token", new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = clientId,
                    ["device_code"] = (string)code["device_code"],
                }, cancellationToken, allowError: true);

                string error = (string)token["error"];
                if (error == null) return await CompleteAsync(clientId, token, cancellationToken);
                if (error == "authorization_pending") continue;
                if (error == "slow_down") { interval += 5; continue; }
                if (error == "authorization_declined") throw new XboxAccountException("The sign-in was declined.");
                if (error == "expired_token") throw new XboxAccountException("The sign-in code expired. Start the sign-in again.");
                throw new XboxAccountException($"Microsoft sign-in failed: {error} {(string)token["error_description"]}");
            }
        }

        /// <summary>Silent sign-in from a stored refresh token (launcher start).</summary>
        public async Task<StoredAccount> RefreshAsync(StoredAccount stored, CancellationToken cancellationToken = default)
        {
            RequireClientId(stored?.ClientId);
            JObject token = await PostFormAsync($"{Authority}/token", new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = stored.ClientId,
                ["refresh_token"] = stored.RefreshToken,
                ["scope"] = Scope,
            }, cancellationToken);
            return await CompleteAsync(stored.ClientId, token, cancellationToken);
        }

        private async Task<StoredAccount> CompleteAsync(string clientId, JObject msaToken, CancellationToken cancellationToken)
        {
            string accessToken = (string)msaToken["access_token"];

            JObject user = await PostJsonAsync("https://user.auth.xboxlive.com/user/authenticate", new JObject
            {
                ["Properties"] = new JObject
                {
                    ["AuthMethod"] = "RPS",
                    ["SiteName"] = "user.auth.xboxlive.com",
                    ["RpsTicket"] = "d=" + accessToken,
                },
                ["RelyingParty"] = "http://auth.xboxlive.com",
                ["TokenType"] = "JWT",
            }, cancellationToken);

            JObject xsts = await PostJsonAsync("https://xsts.auth.xboxlive.com/xsts/authorize", new JObject
            {
                ["Properties"] = new JObject
                {
                    ["SandboxId"] = "RETAIL",
                    ["UserTokens"] = new JArray((string)user["Token"]),
                },
                ["RelyingParty"] = "http://xboxlive.com",
                ["TokenType"] = "JWT",
            }, cancellationToken);

            JToken claims = xsts["DisplayClaims"]?["xui"]?[0];
            return new StoredAccount
            {
                Gamertag = (string)claims?["gtg"],
                Xuid = (string)claims?["xid"],
                UserHash = (string)claims?["uhs"],
                RefreshToken = (string)msaToken["refresh_token"],
                ClientId = clientId,
                SignedInAt = DateTime.UtcNow,
                XboxUserToken = (string)user["Token"],
            };
        }

        /// <summary>XSTS refuses some accounts with an XErr code; these are the documented ones.</summary>
        public static string DescribeXErr(long xerr) => xerr switch
        {
            2148916227 => "This account is banned from Xbox Live.",
            2148916233 => "This Microsoft account has no Xbox profile yet. Sign in once at xbox.com to create one, then try again.",
            2148916235 => "Xbox Live is not available in this account's country or region.",
            2148916236 or 2148916237 => "This account needs adult verification (South Korea) at xbox.com.",
            2148916238 => "This is a child account: an adult has to add it to a Microsoft family group first.",
            _ => $"Xbox Live refused the sign-in (XErr {xerr}).",
        };

        private static void RequireClientId(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new XboxAccountException("No Microsoft client ID is set. Register an app in Azure (personal Microsoft accounts, public client flows on) and enter its Application (client) ID in Settings > Accounts.");
        }

        private async Task<JObject> PostFormAsync(string url, Dictionary<string, string> form, CancellationToken cancellationToken, bool allowError = false)
        {
            using var response = await http.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            JObject json = ParseObject(body);
            if (!response.IsSuccessStatusCode && !(allowError && json?["error"] != null))
                throw new XboxAccountException($"Microsoft sign-in failed ({(int)response.StatusCode}): {(string)json?["error_description"] ?? body}");
            return json ?? new JObject();
        }

        private async Task<JObject> PostJsonAsync(string url, JObject payload, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("x-xbl-contract-version", "1");
            using var response = await http.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            JObject json = ParseObject(body);
            if (!response.IsSuccessStatusCode)
            {
                long? xerr = (long?)json?["XErr"];
                throw new XboxAccountException(xerr != null ? DescribeXErr(xerr.Value) : $"Xbox Live sign-in failed ({(int)response.StatusCode}).");
            }
            return json;
        }

        private static JObject ParseObject(string body)
        {
            try { return string.IsNullOrWhiteSpace(body) ? null : JObject.Parse(body); }
            catch (JsonException) { return null; }
        }
    }
}
