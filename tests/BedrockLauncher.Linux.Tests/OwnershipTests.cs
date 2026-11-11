using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BedrockLauncher.Accounts;
using BedrockLauncher.Handlers;
using BedrockLauncher.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BedrockLauncher.Linux.Tests
{
    public class OwnershipTests
    {
        private const string Catalog = "{\"Product\":{\"DisplaySkuAvailabilities\":[{\"Sku\":{\"Properties\":{\"Packages\":[" +
            "{\"ContentId\":\"xbox-one\",\"PlatformDependencies\":[{\"PlatformName\":\"Windows.Xbox\"}]}," +
            "{\"ContentId\":\"5ad6d2c0-desktop\",\"PlatformDependencies\":[{\"PlatformName\":\"Windows.Desktop\"}]}]}},\"Availabilities\":[]}]}}";

        /// <summary>Plays XSTS, Minecraft services and the Store display catalog.</summary>
        private sealed class FakeServices : HttpMessageHandler
        {
            public readonly List<string> Calls = new();
            public HttpStatusCode LoginStatus = HttpStatusCode.OK;
            public string Entitlements = "{\"items\":[]}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri.ToString();
                string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                Calls.Add($"{url} {request.Headers.Authorization} {body}");
                (HttpStatusCode, string) reply = url switch
                {
                    _ when url.Contains("xsts.auth.xboxlive.com") => (HttpStatusCode.OK, "{\"Token\":\"mcxsts\",\"DisplayClaims\":{\"xui\":[{\"uhs\":\"777\"}]}}"),
                    _ when url.EndsWith("/authentication/login_with_xbox") => (LoginStatus, LoginStatus == HttpStatusCode.OK ? "{\"access_token\":\"MCAT\"}" : "{\"error\":\"Invalid app registration\"}"),
                    _ when url.EndsWith("/entitlements/mcstore") => (HttpStatusCode.OK, Entitlements),
                    _ when url.Contains("displaycatalog.mp.microsoft.com") => (HttpStatusCode.OK, Catalog),
                    _ => (HttpStatusCode.NotFound, "{}"),
                };
                return new HttpResponseMessage(reply.Item1) { Content = new StringContent(reply.Item2) };
            }
        }

        /// <summary>Stands in for xodus-cli: records the command and answers like "xodus-cli license" would.</summary>
        private sealed class FakeXodus : IProcessRunner
        {
            public LaunchCommand Last;
            public int ExitCode;
            public string Output = "";

            public Task<int> RunAsync(LaunchCommand command, Action<string> log, CancellationToken cancellationToken = default)
            {
                Last = command;
                Directory.CreateDirectory(command.Arguments[2]);
                File.WriteAllText(Path.Combine(command.Arguments[2], "key.cik"), "secret");
                foreach (string line in Output.Split('\n')) log?.Invoke(line);
                return Task.FromResult(ExitCode);
            }

            public Process Start(LaunchCommand command, Action<string> log) => throw new NotSupportedException();
        }

        [Fact]
        public async Task Bedrock_entitlement_from_minecraft_services_means_owned()
        {
            var services = new FakeServices { Entitlements = "{\"items\":[{\"name\":\"product_minecraft_bedrock\"},{\"name\":\"game_minecraft_bedrock\"}]}" };
            var checker = new MinecraftOwnershipChecker(new HttpClient(services));

            OwnershipResult result = await checker.CheckAsync("usertoken");

            Assert.Equal(OwnershipState.Owned, result.State);
            Assert.Equal("minecraftservices", result.Source);
            Assert.Contains(services.Calls, c => c.Contains("xsts.auth.xboxlive.com") && c.Contains("rp://api.minecraftservices.com/") && c.Contains("\"UserTokens\":[\"usertoken\"]"));
            Assert.Contains(services.Calls, c => c.Contains("login_with_xbox") && c.Contains("XBL3.0 x=777;mcxsts"));
            Assert.Contains(services.Calls, c => c.Contains("/entitlements/mcstore") && c.Contains("Bearer MCAT"));
        }

        [Fact]
        public async Task Java_only_entitlements_mean_not_owned()
        {
            var services = new FakeServices { Entitlements = "{\"items\":[{\"name\":\"product_minecraft\"},{\"name\":\"game_minecraft\"}]}" };
            OwnershipResult result = await new MinecraftOwnershipChecker(new HttpClient(services)).CheckAsync("usertoken");
            Assert.Equal(OwnershipState.NotOwned, result.State);
        }

        [Fact]
        public async Task An_unapproved_azure_app_is_unverified_without_xodus()
        {
            var services = new FakeServices { LoginStatus = HttpStatusCode.Forbidden };
            OwnershipResult result = await new MinecraftOwnershipChecker(new HttpClient(services)).CheckAsync("usertoken");
            Assert.Equal(OwnershipState.Unverified, result.State);
            Assert.Equal("app-not-approved", result.Detail);
        }

        [Fact]
        public async Task Xodus_store_license_confirms_ownership_and_its_keys_are_deleted()
        {
            var services = new FakeServices { LoginStatus = HttpStatusCode.Forbidden };
            var xodus = new FakeXodus { ExitCode = 0 };
            var checker = new MinecraftOwnershipChecker(new HttpClient(services), xodus, () => "/opt/xodus-cli", () => true);

            OwnershipResult result = await checker.CheckAsync("usertoken");

            Assert.Equal(OwnershipState.Owned, result.State);
            Assert.Equal("xodus", result.Source);
            Assert.Equal("/opt/xodus-cli", xodus.Last.FileName);
            Assert.Equal(new[] { "license", "5ad6d2c0-desktop" }, xodus.Last.Arguments.GetRange(0, 2));
            Assert.False(Directory.Exists(xodus.Last.Arguments[2]));
        }

        [Fact]
        public async Task Xodus_not_entitled_overrides_an_inconclusive_answer()
        {
            var services = new FakeServices { LoginStatus = HttpStatusCode.Forbidden };
            var xodus = new FakeXodus { ExitCode = 1, Output = "requesting license\nnot entitled to this content: No license" };
            var checker = new MinecraftOwnershipChecker(new HttpClient(services), xodus, () => "/opt/xodus-cli", () => true);

            OwnershipResult result = await checker.CheckAsync("usertoken");

            Assert.Equal(OwnershipState.NotOwned, result.State);
            Assert.Equal("xodus", result.Source);
        }

        [Fact]
        public async Task Xodus_is_not_asked_when_it_is_signed_out()
        {
            var services = new FakeServices();
            var xodus = new FakeXodus();
            var checker = new MinecraftOwnershipChecker(new HttpClient(services), xodus, () => "/opt/xodus-cli", () => false);

            OwnershipResult result = await checker.CheckAsync("usertoken");

            Assert.Equal(OwnershipState.NotOwned, result.State);
            Assert.Null(xodus.Last);
        }

        [Fact]
        public void The_desktop_package_content_id_is_picked_from_the_catalog()
        {
            Assert.Equal("5ad6d2c0-desktop", MinecraftOwnershipChecker.FindDesktopContentId(JObject.Parse(Catalog)));
            Assert.Null(MinecraftOwnershipChecker.FindDesktopContentId(new JObject()));
        }
    }

    /// <summary>The download gate: signed in AND owning Minecraft Bedrock.</summary>
    [Collection("App singletons")]
    public class AccountGateTests : IDisposable
    {
        private readonly TempDir dir = new();

        public AccountGateTests()
        {
            PathHandler.RootOverride = dir.Sub("home");
            Environment.SetEnvironmentVariable("BL_MSA_CLIENT_ID", "11111111-2222-3333-4444-555555555555");
            Environment.SetEnvironmentVariable("BL_SKIP_ACCOUNT_CHECK", null);
        }

        public void Dispose()
        {
            PathHandler.RootOverride = null;
            Environment.SetEnvironmentVariable("BL_MSA_CLIENT_ID", null);
            dir.Dispose();
        }

        private static (AccountManager, FakeServicesForGate) Manager(string entitlements, HttpStatusCode login = HttpStatusCode.OK)
        {
            var services = new FakeServicesForGate { Entitlements = entitlements, LoginStatus = login };
            var http = new HttpClient(services);
            return (new AccountManager(new XboxAuthenticator(http), new MinecraftOwnershipChecker(http)), services);
        }

        [Fact]
        public async Task Signed_out_accounts_may_not_download()
        {
            var (manager, _) = Manager("{\"items\":[]}");
            Assert.False(manager.MayDownload);
            Assert.Equal("Linux_AccountRequired", manager.DownloadBlockReason);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task Owners_may_download_and_the_answer_is_stored()
        {
            var (manager, _) = Manager("{\"items\":[{\"name\":\"game_minecraft_bedrock\"}]}");
            await manager.SignInAsync(null, CancellationToken.None);
            Assert.True(manager.MayDownload);
            Assert.Null(manager.DownloadBlockReason);
            string stored = File.ReadAllText(AccountManager.StorePath);
            Assert.Equal((int)OwnershipState.Owned, (int)JObject.Parse(stored)["Ownership"]["State"]);
            Assert.DoesNotContain("usertoken", stored);
        }

        [Fact]
        public async Task Signed_in_non_owners_may_not_download()
        {
            var (manager, _) = Manager("{\"items\":[{\"name\":\"game_minecraft\"}]}");
            await manager.SignInAsync(null, CancellationToken.None);
            Assert.True(manager.IsSignedIn);
            Assert.False(manager.MayDownload);
            Assert.Equal("Linux_NotOwned", manager.DownloadBlockReason);
        }

        [Fact]
        public async Task An_unapproved_app_explains_how_to_verify_with_xodus()
        {
            var (manager, _) = Manager("{}", HttpStatusCode.Forbidden);
            await manager.SignInAsync(null, CancellationToken.None);
            Assert.False(manager.MayDownload);
            Assert.Equal("Linux_OwnershipAppNotApproved", manager.DownloadBlockReason);
        }

        internal sealed class FakeServicesForGate : HttpMessageHandler
        {
            public HttpStatusCode LoginStatus = HttpStatusCode.OK;
            public string Entitlements;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri.ToString();
                (HttpStatusCode, string) reply = url switch
                {
                    _ when url.EndsWith("/devicecode") => (HttpStatusCode.OK, "{\"device_code\":\"DC\",\"user_code\":\"C\",\"verification_uri\":\"https://microsoft.com/link\",\"expires_in\":900,\"interval\":1}"),
                    _ when url.EndsWith("/token") => (HttpStatusCode.OK, "{\"access_token\":\"AT\",\"refresh_token\":\"RT\"}"),
                    _ when url.Contains("user.auth.xboxlive.com") => (HttpStatusCode.OK, "{\"Token\":\"usertoken\"}"),
                    _ when url.Contains("xsts.auth.xboxlive.com") => (HttpStatusCode.OK, "{\"Token\":\"x\",\"DisplayClaims\":{\"xui\":[{\"gtg\":\"Steve\",\"xid\":\"1\",\"uhs\":\"2\"}]}}"),
                    _ when url.EndsWith("/authentication/login_with_xbox") => (LoginStatus, "{\"access_token\":\"MCAT\"}"),
                    _ when url.EndsWith("/entitlements/mcstore") => (HttpStatusCode.OK, Entitlements),
                    _ => (HttpStatusCode.NotFound, "{}"),
                };
                return Task.FromResult(new HttpResponseMessage(reply.Item1) { Content = new StringContent(reply.Item2) });
            }
        }
    }
}
