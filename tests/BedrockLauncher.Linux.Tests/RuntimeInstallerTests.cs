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
    }
}
