using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using BedrockLauncher.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace BedrockLauncher.Linux.Tests
{
    /// <summary>
    /// End to end through a real wine-uwp build: package -> extract -> prefix -> launch -> save data in the
    /// installation folder. Set BL_TEST_WINEUWP to a wine-uwp install (runtime/wine-uwp/build.sh output); without it,
    /// or without mingw to build the stand-in game (tests/fake-game), the test only logs why it did nothing.
    /// </summary>
    public class WineUwpIntegrationTests
    {
        private readonly ITestOutputHelper output;

        public WineUwpIntegrationTests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public async Task Fake_uwp_game_runs_and_saves_into_the_installation_folder()
        {
            string runtimeDir = Environment.GetEnvironmentVariable("BL_TEST_WINEUWP");
            string gcc = LaunchCommandBuilder.FindInPath("x86_64-w64-mingw32-gcc");
            if (string.IsNullOrEmpty(runtimeDir) || gcc == null)
            {
                output.WriteLine("Skipped: set BL_TEST_WINEUWP and install mingw-w64 to run this test.");
                return;
            }

            using var dir = new TempDir();
            string fakeGame = Path.Combine(AppContext.BaseDirectory, "fake-game");
            string exe = dir.Sub("Minecraft.Windows.exe");
            var build = Process.Start(new ProcessStartInfo(gcc, new[] { "-O1", "-o", exe, Path.Combine(fakeGame, "fakegame.c") }));
            await build.WaitForExitAsync();
            Assert.Equal(0, build.ExitCode);

            // A minimal appx, like the one the Store delivers.
            string appx = dir.Sub("Minecraft-1.20.81.1.appx");
            using (var zip = ZipFile.Open(appx, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(Path.Combine(fakeGame, "AppxManifest.xml"), "AppxManifest.xml");
                zip.CreateEntryFromFile(exe, "Minecraft.Windows.exe");
                zip.CreateEntry("AppxSignature.p7x");
            }

            string gameDir = dir.Sub("versions", "1.20.81.1");
            await AppxPackage.ExtractAsync(appx, gameDir);

            GameRuntime runtime = RuntimeLocator.Classify(runtimeDir, "test");
            Assert.NotNull(runtime);
            Assert.Equal(GameRuntimeKind.WineUwp, runtime.Kind);

            var log = new StringBuilder();
            void Log(string line) { lock (log) log.AppendLine(line); }

            string prefix = dir.Sub("prefixes", "wineuwp");
            await new WinePrefixManager(log: Log).EnsureAsync(runtime, prefix, useDxvk: false);
            Assert.True(WinePrefixManager.IsInitialized(prefix));

            string packageData = dir.Sub("installations", "Steve", "Latest Release", "packageData");
            var command = LaunchCommandBuilder.Build(new LaunchRequest
            {
                Runtime = runtime,
                PrefixPath = prefix,
                GameDirectory = gameDir,
                PackageDataPath = packageData,
                UseDxvk = false,
                FindInPath = _ => null,
            });

            int code = await ProcessRunner.Default.RunAsync(command, Log);
            output.WriteLine(log.ToString());

            Assert.Equal(0, code);
            Assert.Contains(@"OK LocalFolder Z:" + packageData.Replace('/', '\\') + @"\LocalState", log.ToString());
            Assert.True(File.Exists(Path.Combine(packageData, "LocalState", "games", "com.mojang", "minecraftWorlds", "fake-world", "level.dat")));

            // dxvk-uwp (built with the runtime) goes into system32 with native overrides.
            if (Directory.Exists(Path.Combine(runtime.DxvkFolder, "x64", "bin")))
            {
                await new WinePrefixManager(log: Log).InstallDxvkAsync(runtime, prefix);
                string system32 = Path.Combine(prefix, "drive_c", "windows", "system32");
                Assert.Equal(File.ReadAllBytes(Path.Combine(runtime.DxvkFolder, "x64", "bin", "d3d11.dll")), File.ReadAllBytes(Path.Combine(system32, "d3d11.dll")));
                Assert.True(File.Exists(Path.Combine(system32, "d3d11.dll.old")));
                await new WinePrefixManager(log: Log).WaitForWineServerAsync(runtime, prefix);
                Assert.Contains("\"d3d11\"=\"native\"", File.ReadAllText(Path.Combine(prefix, "user.reg")));
            }
        }
    }
}
