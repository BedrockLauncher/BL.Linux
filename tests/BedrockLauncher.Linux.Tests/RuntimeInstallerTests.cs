using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BedrockLauncher.Runtime;
using Xunit;

namespace BedrockLauncher.Linux.Tests
{
    public class RuntimeInstallerTests : IDisposable
    {
        private readonly TempDir dir = new();

        public void Dispose() => dir.Dispose();

        private string Archive(string folder, string file, string content)
        {
            string src = dir.Sub("src", folder);
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, file), content);
            string archive = dir.Sub("src", folder + ".tar.xz");
            Process.Start("tar", new[] { "-cJf", archive, "-C", dir.Sub("src"), folder }).WaitForExit();
            return archive;
        }

        private RuntimeInstaller Installer(HttpMessageHandler server) =>
            new RuntimeInstaller(dir.Sub("runtimes"), dir.Sub("downloads"), new HttpClient(server));

        [Fact]
        public async Task A_package_is_verified_unpacked_and_recorded()
        {
            var server = new PackageHandlerTests.RuntimeServerStub(Archive("xodus", "xodus-cli", "v1"), "xodus", "Xodus");
            var installer = Installer(server);
            RuntimeManifest manifest = await installer.GetManifestAsync();
            RuntimePackage xodus = manifest.Xodus;
            Assert.NotNull(xodus);
            Assert.False(installer.IsUpToDate(xodus));

            long last = 0;
            await installer.InstallAsync(xodus, (done, total) => last = done, null, CancellationToken.None);

            Assert.Equal("v1", File.ReadAllText(dir.Sub("runtimes", "xodus", "xodus-cli")));
            Assert.True(installer.IsUpToDate(xodus));
            Assert.Equal(xodus.Size, last);
            Assert.Empty(Directory.GetFiles(dir.Sub("downloads")));
            Assert.Equal(dir.Sub("runtimes", "xodus", "xodus-cli"), Xodus.Locate(dir.Sub("runtimes"), null));
        }

        [Fact]
        public async Task A_damaged_download_is_rejected_and_the_installed_copy_stays()
        {
            Directory.CreateDirectory(dir.Sub("runtimes", "xodus"));
            File.WriteAllText(dir.Sub("runtimes", "xodus", "xodus-cli"), "old");
            var server = new PackageHandlerTests.RuntimeServerStub(Archive("xodus", "xodus-cli", "new"), "xodus", "Xodus", sha256: new string('0', 64));
            var installer = Installer(server);
            RuntimePackage xodus = (await installer.GetManifestAsync()).Xodus;

            var ex = await Assert.ThrowsAsync<RuntimeInstallException>(() => installer.InstallAsync(xodus, null, null, CancellationToken.None));
            Assert.Contains("SHA-256", ex.Message);
            Assert.Equal("old", File.ReadAllText(dir.Sub("runtimes", "xodus", "xodus-cli")));
            Assert.Empty(Directory.GetFiles(dir.Sub("downloads")));
        }

        [Fact]
        public async Task An_update_replaces_the_previous_version()
        {
            var installer = Installer(new PackageHandlerTests.RuntimeServerStub(Archive("winegdk", "bl-runtime.json", "{\"kind\":\"WineGdk\"}"), "winegdk", "WineGdk"));
            RuntimePackage winegdk = (await installer.GetManifestAsync()).For(GameRuntimeKind.WineGdk);
            Directory.CreateDirectory(dir.Sub("runtimes", "winegdk", "stale"));

            await installer.InstallAsync(winegdk, null, null, CancellationToken.None);

            Assert.False(Directory.Exists(dir.Sub("runtimes", "winegdk", "stale")));
            Assert.True(File.Exists(dir.Sub("runtimes", "winegdk", "bl-runtime.json")));
            Assert.Empty(Directory.GetDirectories(dir.Sub("runtimes"), ".staging-*"));
        }

        [Fact]
        public async Task Unsafe_folder_names_and_unreachable_lists_are_errors()
        {
            var installer = Installer(new PackageHandlerTests.RuntimeServerStub(Archive("xodus", "a", "b"), "xodus", "Xodus"));
            var bad = new RuntimePackage { Id = "x", Folder = "../etc", Url = "https://example.invalid/x.tar.xz", Sha256 = "00" };
            await Assert.ThrowsAsync<RuntimeInstallException>(() => installer.InstallAsync(bad, null, null, CancellationToken.None));

            var offline = new RuntimeInstaller(dir.Sub("r"), dir.Sub("d"), new HttpClient(new OfflineHandler()));
            await Assert.ThrowsAsync<RuntimeInstallException>(() => offline.GetManifestAsync());
        }

        private sealed class OfflineHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                throw new HttpRequestException("network unreachable");
        }
    
        /// <summary>No BL.Linux release yet: runtimes.json is 404, GitHub's API lists the GDK-Proton release.</summary>
        private sealed class UnpublishedServer : HttpMessageHandler
        {
            private readonly byte[] archive;
            private readonly string digest;
            public UnpublishedServer(string archivePath, bool withDigest)
            {
                archive = File.ReadAllBytes(archivePath);
                digest = withDigest ? "\"digest\":\"sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(archive)).ToLowerInvariant() + "\"," : "";
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri.ToString();
                if (url.EndsWith("runtimes.json")) return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
                if (url == RuntimeInstaller.GdkProtonLatestApi)
                {
                    Assert.Contains("BL.Linux", request.Headers.UserAgent.ToString());
                    string json = "{\"tag_name\":\"release10-32\",\"assets\":[{\"name\":\"GDK-Proton10-32.sha512sum\"},{\"name\":\"GDK-Proton10-32.tar.gz\"," + digest +
                        "\"size\":" + archive.Length + ",\"browser_download_url\":\"https://github.com/Weather-OS/GDK-Proton/releases/download/release10-32/GDK-Proton10-32.tar.gz\"}]}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) });
                }
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(archive) });
            }
        }

        private string GdkProtonArchive()
        {
            string src = dir.Sub("gp", "GDK-Proton10-32");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "proton"), "#!/bin/sh\n");
            string archive = dir.Sub("gp", "GDK-Proton10-32.tar.gz");
            Process.Start("tar", new[] { "-czf", archive, "-C", dir.Sub("gp"), "GDK-Proton10-32" }).WaitForExit();
            return archive;
        }

        [Fact]
        public async Task Without_the_bl_linux_release_gdk_proton_comes_from_its_own_release()
        {
            var installer = Installer(new UnpublishedServer(GdkProtonArchive(), withDigest: true));
            RuntimeManifest manifest = await installer.GetManifestAsync();

            Assert.Contains("Actions > Runtimes", manifest.Problem);
            Assert.Null(manifest.For(GameRuntimeKind.WineUwp));
            RuntimePackage gdkProton = manifest.For(GameRuntimeKind.GdkProton);
            Assert.Equal("release10-32", gdkProton.Version);
            Assert.Equal("GDK-Proton10-32", gdkProton.Folder);
            Assert.NotNull(gdkProton.Sha256);

            await installer.InstallAsync(gdkProton, null, null, CancellationToken.None);
            Assert.True(installer.IsUpToDate(gdkProton));
            GameRuntime runtime = new RuntimeLocator(dir.Sub("runtimes"), home: dir.Path).Resolve(GameRuntimeKind.GdkProton, null);
            Assert.Equal(dir.Sub("runtimes", "GDK-Proton10-32"), runtime.RootPath);
        }

        [Fact]
        public async Task A_release_without_digest_still_installs_and_records_its_hash()
        {
            var installer = Installer(new UnpublishedServer(GdkProtonArchive(), withDigest: false));
            RuntimePackage gdkProton = (await installer.GetManifestAsync()).For(GameRuntimeKind.GdkProton);
            Assert.Null(gdkProton.Sha256);
            await installer.InstallAsync(gdkProton, null, null, CancellationToken.None);
            Assert.True(installer.IsUpToDate(gdkProton));
            Assert.Equal(64, installer.GetInstalled(gdkProton).Sha256.Length);
        }

        [Fact]
        public async Task Offline_reports_the_runtime_list_problem()
        {
            var offline = new RuntimeInstaller(dir.Sub("r"), dir.Sub("d"), new HttpClient(new OfflineHandler()));
            var ex = await Assert.ThrowsAsync<RuntimeInstallException>(() => offline.GetManifestAsync());
            Assert.Contains("network unreachable", ex.Message);
        }
    }
}
