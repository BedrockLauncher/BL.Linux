using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BedrockLauncher.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace BedrockLauncher.Linux.Tests
{
    /// <summary>
    /// Opt-in: installs every package of a real runtimes.json (BL_TEST_RUNTIME_MANIFEST, e.g. the BL.Linux "runtimes"
    /// release or a local http.server over runtime/package.sh output) into BL_TEST_RUNTIME_DIR (default: a temp folder)
    /// and checks that the launcher recognises them and that their wine starts.
    /// </summary>
    [Collection("App singletons")]
    public class RuntimeDownloadIntegrationTests
    {
        private readonly ITestOutputHelper output;
        public RuntimeDownloadIntegrationTests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public async Task Released_runtimes_install_and_start()
        {
            string manifestUrl = Environment.GetEnvironmentVariable("BL_TEST_RUNTIME_MANIFEST");
            if (string.IsNullOrEmpty(manifestUrl)) { output.WriteLine("BL_TEST_RUNTIME_MANIFEST not set: skipped."); return; }

            string root = Environment.GetEnvironmentVariable("BL_TEST_RUNTIME_DIR") ?? Path.Combine(Path.GetTempPath(), "bl-runtime-it-" + Guid.NewGuid().ToString("N"));
            string previous = Environment.GetEnvironmentVariable("BL_RUNTIMES_MANIFEST");
            Environment.SetEnvironmentVariable("BL_RUNTIMES_MANIFEST", manifestUrl);
            try
            {
                var installer = new RuntimeInstaller(Path.Combine(root, "runtimes"), Path.Combine(root, "downloads"));
                RuntimeManifest manifest = await installer.GetManifestAsync();
                Assert.NotEmpty(manifest.Runtimes);
                foreach (RuntimePackage package in manifest.Runtimes)
                {
                    await installer.InstallAsync(package, null, output.WriteLine, CancellationToken.None);
                    Assert.True(installer.IsUpToDate(package));
                }

                var locator = new RuntimeLocator(Path.Combine(root, "runtimes"), home: root);
                foreach (RuntimePackage package in manifest.Runtimes.Where(p => p.RuntimeKind != null))
                {
                    GameRuntime runtime = locator.Resolve(package.RuntimeKind.Value, null);
                    Assert.NotNull(runtime);
                    if (runtime.UsesProtonScript) continue;
                    var version = new LaunchCommand { FileName = runtime.WineBinary };
                    version.Arguments.Add("--version");
                    string text = "";
                    Assert.Equal(0, await ProcessRunner.Default.RunAsync(version, l => text += l));
                    output.WriteLine($"{package.Id}: {runtime.WineBinary} -> {text}");
                    Assert.Contains("wine-", text);
                }
                if (manifest.Xodus != null)
                    Assert.NotNull(Xodus.Locate(Path.Combine(root, "runtimes"), null));
            }
            finally
            {
                Environment.SetEnvironmentVariable("BL_RUNTIMES_MANIFEST", previous);
                if (Environment.GetEnvironmentVariable("BL_TEST_RUNTIME_DIR") == null)
                    try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }
    }
}
