using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BedrockLauncher.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BedrockLauncher.Accounts
{
    public enum OwnershipState
    {
        /// <summary>Not checked yet.</summary>
        Unknown,
        /// <summary>The account owns Minecraft Bedrock Edition for Windows.</summary>
        Owned,
        /// <summary>A licensing service answered that the account does not own it.</summary>
        NotOwned,
        /// <summary>No service could give an answer (service refused the app, offline, Xodus not set up).</summary>
        Unverified,
    }

    public sealed class OwnershipResult
    {
        public OwnershipState State { get; set; }
        /// <summary>"minecraftservices" or "xodus": which service gave the answer.</summary>
        public string Source { get; set; }
        public string Detail { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

        public static OwnershipResult Of(OwnershipState state, string source, string detail = null) =>
            new OwnershipResult { State = state, Source = source, Detail = detail };

        public override string ToString() => $"{State} ({Source}{(string.IsNullOrEmpty(Detail) ? "" : ": " + Detail)})";
    }

    /// <summary>
    /// Checks that the signed-in account owns Minecraft for Windows (Bedrock), with two real licensing sources:
    ///
    /// 1. Minecraft services (what the official Minecraft Launcher uses): XSTS for rp://api.minecraftservices.com/
    ///    -> /authentication/login_with_xbox -> /entitlements/mcstore. An account that owns Bedrock on PC has the
    ///    "product_minecraft_bedrock" / "game_minecraft_bedrock" entitlements. Mojang only accepts Azure apps it has
    ///    approved; an unapproved app gets 403 and the result is <see cref="OwnershipState.Unverified"/>.
    ///
    /// 2. The Microsoft Store license itself, through Xodus ("xodus-cli license &lt;content id&gt;"), which asks
    ///    licensing.mp.microsoft.com for the game's content license with the Xodus account and fails with
    ///    "not entitled" when the account has none. Used when (1) does not say "owned".
    /// </summary>
    public sealed class MinecraftOwnershipChecker
    {
        /// <summary>Store ID of "Minecraft for Windows" (Bedrock Edition, PC).</summary>
        public const string BedrockStoreId = "9NBLGGH2JHXJ";
        public const string MinecraftServicesRelyingParty = "rp://api.minecraftservices.com/";
        public const string MinecraftServices = "https://api.minecraftservices.com";
        public static readonly string[] BedrockEntitlements = { "product_minecraft_bedrock", "game_minecraft_bedrock" };

        private readonly HttpClient http;
        private readonly IProcessRunner runner;
        private readonly Func<string> xodusCli;
        private readonly Func<bool> xodusSignedIn;

        public MinecraftOwnershipChecker(HttpClient http = null, IProcessRunner runner = null, Func<string> xodusCli = null, Func<bool> xodusSignedIn = null)
        {
            this.http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            this.runner = runner ?? ProcessRunner.Default;
            this.xodusCli = xodusCli ?? (() => null);
            this.xodusSignedIn = xodusSignedIn ?? (() => false);
        }

        /// <param name="xboxUserToken">user.auth.xboxlive.com token of the signed-in account (null skips source 1).</param>
        public async Task<OwnershipResult> CheckAsync(string xboxUserToken, CancellationToken cancellationToken = default)
        {
            OwnershipResult services = xboxUserToken == null
                ? OwnershipResult.Of(OwnershipState.Unverified, "minecraftservices", "no Xbox token")
                : await CheckMinecraftServicesAsync(xboxUserToken, cancellationToken);
            Trace.WriteLine($"Ownership (Minecraft services): {services}");
            if (services.State == OwnershipState.Owned) return services;

            string cli = xodusCli();
            if (cli == null || !xodusSignedIn()) return services;

            OwnershipResult store = await CheckStoreLicenseAsync(cli, cancellationToken);
            Trace.WriteLine($"Ownership (Store license via Xodus): {store}");
            // The Store license is the authoritative answer whenever it gives one.
            return store.State == OwnershipState.Unverified ? services : store;
        }

        public async Task<OwnershipResult> CheckMinecraftServicesAsync(string xboxUserToken, CancellationToken cancellationToken)
        {
            const string source = "minecraftservices";
            try
            {
                var xsts = await PostJsonAsync("https://xsts.auth.xboxlive.com/xsts/authorize", new JObject
                {
                    ["Properties"] = new JObject { ["SandboxId"] = "RETAIL", ["UserTokens"] = new JArray(xboxUserToken) },
                    ["RelyingParty"] = MinecraftServicesRelyingParty,
                    ["TokenType"] = "JWT",
                }, cancellationToken);
                if (xsts.Status != HttpStatusCode.OK)
                {
                    long? xerr = (long?)xsts.Body?["XErr"];
                    return OwnershipResult.Of(OwnershipState.Unverified, source,
                        xerr != null ? XboxAuthenticator.DescribeXErr(xerr.Value) : $"XSTS {(int)xsts.Status}");
                }
                string uhs = (string)xsts.Body["DisplayClaims"]?["xui"]?[0]?["uhs"];
                string token = (string)xsts.Body["Token"];

                var login = await PostJsonAsync($"{MinecraftServices}/authentication/login_with_xbox",
                    new JObject { ["identityToken"] = $"XBL3.0 x={uhs};{token}" }, cancellationToken);
                if (login.Status == HttpStatusCode.Forbidden || login.Status == HttpStatusCode.Unauthorized)
                    return OwnershipResult.Of(OwnershipState.Unverified, source, "app-not-approved");
                if (login.Status != HttpStatusCode.OK)
                    return OwnershipResult.Of(OwnershipState.Unverified, source, $"login_with_xbox {(int)login.Status}");

                string accessToken = (string)login.Body["access_token"];
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{MinecraftServices}/entitlements/mcstore");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await http.SendAsync(request, cancellationToken);
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return OwnershipResult.Of(OwnershipState.Unverified, source, $"entitlements {(int)response.StatusCode}");

                var names = (Parse(body)?["items"] as JArray ?? new JArray())
                    .Select(i => (string)i["name"]).Where(n => n != null).ToList();
                bool owned = names.Any(n => BedrockEntitlements.Contains(n, StringComparer.OrdinalIgnoreCase));
                return OwnershipResult.Of(owned ? OwnershipState.Owned : OwnershipState.NotOwned, source,
                    names.Count == 0 ? "no entitlements" : string.Join(",", names));
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return OwnershipResult.Of(OwnershipState.Unverified, source, "offline: " + ex.Message);
            }
        }

        /// <summary>Asks for the game's Store content license through Xodus; the keys it writes are deleted at once.</summary>
        public async Task<OwnershipResult> CheckStoreLicenseAsync(string cli, CancellationToken cancellationToken)
        {
            const string source = "xodus";
            string contentId;
            try
            {
                contentId = await GetContentIdAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested || ex is JsonException)
            {
                return OwnershipResult.Of(OwnershipState.Unverified, source, "catalog: " + ex.Message);
            }
            if (contentId == null) return OwnershipResult.Of(OwnershipState.Unverified, source, "no content id in the catalog");

            string keys = Path.Combine(Path.GetTempPath(), "bl-license-" + Guid.NewGuid().ToString("N"));
            var output = new StringBuilder();
            try
            {
                int code = await runner.RunAsync(Xodus.License(cli, contentId, keys), line => { lock (output) output.AppendLine(line); }, cancellationToken);
                string text = output.ToString();
                if (code == 0) return OwnershipResult.Of(OwnershipState.Owned, source, contentId);
                if (text.Contains("not entitled", StringComparison.OrdinalIgnoreCase))
                    return OwnershipResult.Of(OwnershipState.NotOwned, source, LastLine(text));
                return OwnershipResult.Of(OwnershipState.Unverified, source, LastLine(text) ?? $"exit {code}");
            }
            finally
            {
                try { if (Directory.Exists(keys)) Directory.Delete(keys, true); }
                catch (IOException ex) { Trace.WriteLine($"Could not delete {keys}: {ex.Message}"); }
            }
        }

        /// <summary>The Windows.Desktop package content ID of Minecraft for Windows, from the Store display catalog.</summary>
        public async Task<string> GetContentIdAsync(CancellationToken cancellationToken)
        {
            string url = $"https://displaycatalog.mp.microsoft.com/v7.0/products/{BedrockStoreId}?market=US&languages=en,neutral";
            string body = await http.GetStringAsync(url, cancellationToken);
            return FindDesktopContentId(JObject.Parse(body));
        }

        public static string FindDesktopContentId(JObject catalog)
        {
            foreach (JToken availability in catalog?["Product"]?["DisplaySkuAvailabilities"] as JArray ?? new JArray())
                foreach (JToken package in availability["Sku"]?["Properties"]?["Packages"] as JArray ?? new JArray())
                {
                    bool desktop = (package["PlatformDependencies"] as JArray ?? new JArray())
                        .Any(d => (string)d["PlatformName"] == "Windows.Desktop");
                    string id = (string)package["ContentId"];
                    if (desktop && !string.IsNullOrEmpty(id)) return id;
                }
            return null;
        }

        private static string LastLine(string text) =>
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();

        private async Task<(HttpStatusCode Status, JObject Body)> PostJsonAsync(string url, JObject payload, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("x-xbl-contract-version", "1");
            using var response = await http.SendAsync(request, cancellationToken);
            return (response.StatusCode, Parse(await response.Content.ReadAsStringAsync(cancellationToken)) ?? new JObject());
        }

        private static JObject Parse(string body)
        {
            try { return string.IsNullOrWhiteSpace(body) ? null : JObject.Parse(body); }
            catch (JsonException) { return null; }
        }
    }
}
