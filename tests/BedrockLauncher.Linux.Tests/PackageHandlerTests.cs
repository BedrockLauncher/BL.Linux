using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using BedrockLauncher.Classes;
using BedrockLauncher.Handlers;
using BedrockLauncher.Runtime;
using BedrockLauncher.UpdateProcessor.Classes;
using BedrockLauncher.UpdateProcessor.Enums;
using BedrockLauncher.ViewModels;
using Xunit;

namespace BedrockLauncher.Linux.Tests
{
    /// <summary>
    /// The launcher's Play pipeline (PackageHandler) end to end, offline, with stand-in runtimes: shell scripts in
    /// place of wine-uwp and GDK-Proton that record how they were called. Shares the app singletons, so the tests of
    /// this class run one after the other.
    /// </summary>
    [Collection("App singletons")]
    public class PackageHandlerTests : IDisposable
    {
        private sealed class UiStub : IUiHost
        {
            public string Folder;
            public Exception Error;
            public Task ShowException(Exception ex) { Error = ex; return Task.CompletedTask; }
            public Task<bool> Confirm(string t, string m, params object[] a) => Task.FromResult(true);
            public Task ShowMessage(string t, string m) => Task.CompletedTask;
            public Task<string> PickFolder(string title) => Task.FromResult(Folder);
            public Task<string> PickFile(string title, params (string, string[])[] f) => Task.FromResult<string>(null);
            public void CloseLauncher() { }
        }

        private readonly TempDir dir = new();
        private readonly UiStub ui = new();

        public PackageHandlerTests()
        {
            PathHandler.RootOverride = dir.Sub("home");
            Environment.SetEnvironmentVariable("BL_SKIP_ACCOUNT_CHECK", "1");
            Environment.SetEnvironmentVariable("XODUS_CLI", null);
            Properties.LauncherSettings.Load();
            MainDataModel.Default.PackageManager.GdkUrlResolver = _ => Array.Empty<string>();
            MainDataModel.Default.UiHost = ui;
            MainDataModel.Default.Versions.Clear();
            MainDataModel.Default.PackageManager.Runner = ProcessRunner.Default;
        }

        public void Dispose()
        {
            PathHandler.RootOverride = null;
            Environment.SetEnvironmentVariable("BL_SKIP_ACCOUNT_CHECK", null);
            Environment.SetEnvironmentVariable("XODUS_CLI", null);
            MainDataModel.Default.PackageManager.GdkUrlResolver = null;
            dir.Dispose();
        }

        private static void Script(string path, string body)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "#!/bin/sh\n" + body);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        private static async Task WaitFor(Func<bool> condition)
        {
            for (int i = 0; i < 100 && !condition(); i++) await Task.Delay(50);
        }

        [Fact]
        public async Task Gdk_play_imports_the_files_links_the_save_data_and_starts_proton()
        {
            // Stand-in GDK-Proton: creates its prefix on "run wineboot", records the game launch.
            string proton = dir.Sub("GDK-Proton10-32");
            Script(Path.Combine(proton, "proton"),
                "if [ \"$2\" = wineboot ]; then mkdir -p \"$STEAM_COMPAT_DATA_PATH/pfx/drive_c/users/steamuser/AppData/Roaming\"; touch \"$STEAM_COMPAT_DATA_PATH/pfx/system.reg\"; else env > \"$STEAM_COMPAT_DATA_PATH/env.log\"; fi\n" +
                "printf '%s\\n' \"$*\" >> \"$STEAM_COMPAT_DATA_PATH/calls.log\"\n");
            var settings = Properties.LauncherSettings.Default;
            settings.GdkRuntimeKind = GameRuntimeKind.GdkProton;
            settings.XodusPath = dir.Sub("no-xodus");
            settings.GdkRuntimePath = proton;

            // Decrypted game files as copied from a Windows install (Xbox app layout).
            string windowsCopy = dir.Sub("from-windows", "Minecraft for Windows");
            Directory.CreateDirectory(Path.Combine(windowsCopy, "Content"));
            File.WriteAllBytes(Path.Combine(windowsCopy, "Content", "Minecraft.Windows.exe"), new byte[] { (byte)'M', (byte)'Z', 0x90 });
            File.WriteAllText(Path.Combine(windowsCopy, "Content", "MicrosoftGame.Config"),
                "<Game><Identity Name=\"Microsoft.MinecraftUWP\" Publisher=\"CN=Microsoft\" Version=\"1.21.13101.0\"/></Game>");
            ui.Folder = windowsCopy;

            var required = new GdkPackageIdentity("Microsoft.MinecraftUWP", new Version(1, 21, 13101, 0), "x64", MinecraftPackageFamilies.PublisherId);
            var version = new MCVersion("gdk-1.21.131", "gdk-1.21.131", "1.21.131.1", VersionType.Release, "x64", PackageType.GDK, required);
            MainDataModel.Default.Versions.Add(version);
            Assert.False(version.IsInstalled);

            string data = dir.Sub("installation", "packageData");
            await MainDataModel.Default.PackageManager.Play(null, version, data, keepLauncherOpen: true, launchEditor: false);
            Assert.Null(ui.Error);

            Assert.True(version.IsInstalled);
            Assert.True(File.Exists(Path.Combine(version.GameDirectory, "Minecraft.Windows.exe")));

            string compat = Path.Combine(MainDataModel.Default.FilePaths.PrefixesFolder, "gdkproton");
            await WaitFor(() => File.Exists(Path.Combine(compat, "env.log")) && File.ReadAllText(Path.Combine(compat, "calls.log")).Contains("waitforexitandrun"));
            string[] calls = File.ReadAllLines(Path.Combine(compat, "calls.log"));
            Assert.Equal("run wineboot -u", calls[0]);
            Assert.Equal("waitforexitandrun " + Path.Combine(version.GameDirectory, "Minecraft.Windows.exe"), calls[1]);
            Assert.Contains("GameInputRedist=b", File.ReadAllText(Path.Combine(compat, "env.log")));

            var link = new DirectoryInfo(Path.Combine(compat, "pfx", "drive_c", "users", "steamuser", "AppData", "Roaming", "Minecraft Bedrock"));
            Assert.Equal(data, link.LinkTarget);
        }

        [Fact]
        public async Task Uwp_play_extracts_a_kept_appx_and_starts_wine_uwp_with_the_installation_data()
        {
            // Stand-in wine-uwp: wineboot creates the prefix; any other call records the game launch.
            string runtime = dir.Sub("runtimes-src", "wine-uwp");
            Script(Path.Combine(runtime, "bin", "wine64"),
                "if [ \"$1\" = wineboot ]; then touch \"$WINEPREFIX/system.reg\"; exit 0; fi\n" +
                "if [ \"$1\" = reg ]; then exit 0; fi\n" +
                "printf '%s|%s|%s\\n' \"$*\" \"$WINEUWP_PACKAGE_DATA\" \"$PWD\" > \"$WINEPREFIX/launch.log\"\n");
            Script(Path.Combine(runtime, "bin", "wineserver"), "exit 0\n");
            Properties.LauncherSettings.Default.UwpRuntimePath = runtime;

            var version = new MCVersion("uwp-id", "uwp-id", "1.20.81.1", VersionType.Release, "x64");
            MainDataModel.Default.Versions.Add(version);

            // A previously downloaded appx kept in AppxBackups (KeepAppx): no network needed.
            string backups = Path.Combine(MainDataModel.Default.FilePaths.VersionsFolder, "AppxBackups");
            Directory.CreateDirectory(backups);
            using (var zip = ZipFile.Open(Path.Combine(backups, "Minecraft-1.20.81.1-x64.appx"), ZipArchiveMode.Create))
            {
                using (var w = new StreamWriter(zip.CreateEntry("AppxManifest.xml").Open()))
                    w.Write("<Package><Identity Name=\"Microsoft.MinecraftUWP\" Version=\"1.20.8101.0\" ProcessorArchitecture=\"x64\"/></Package>");
                using (var w = new StreamWriter(zip.CreateEntry("Minecraft.Windows.exe").Open())) w.Write("MZ");
            }

            string data = dir.Sub("installation", "packageData");
            await MainDataModel.Default.PackageManager.Play(null, version, data, keepLauncherOpen: true, launchEditor: false);
            Assert.Null(ui.Error);
            Assert.True(version.IsInstalled);
            Assert.Equal("uwp-id", File.ReadAllText(version.IdentificationPath));

            string prefix = Path.Combine(MainDataModel.Default.FilePaths.PrefixesFolder, "wineuwp");
            await WaitFor(() => File.Exists(Path.Combine(prefix, "launch.log")));
            string[] launch = File.ReadAllText(Path.Combine(prefix, "launch.log")).Trim().Split('|');
            Assert.Equal(Path.Combine(version.GameDirectory, "Minecraft.Windows.exe"), launch[0]);
            Assert.Equal(WinePath.ToWindows(data), launch[1]);
            Assert.Equal(version.GameDirectory, launch[2]);
        }

        [Fact]
        public async Task Missing_runtime_is_reported_with_the_setup_hint()
        {
            Properties.LauncherSettings.Default.UwpRuntimePath = dir.Sub("nothing-here");
            var version = new MCVersion("uwp-2", "uwp-2", "1.19.83.1", VersionType.Release, "x64");
            MainDataModel.Default.Versions.Add(version);
            Directory.CreateDirectory(version.GameDirectory);
            File.WriteAllText(version.ExecutablePath, "MZ");
            File.WriteAllText(version.ManifestPath, "<Package/>");

            await MainDataModel.Default.PackageManager.Play(null, version, dir.Sub("data"), true, false);

            Assert.IsType<Exceptions.RuntimeNotFoundException>(ui.Error);
            Assert.Contains("build.sh", ui.Error.Message);
        }
    

        [Fact]
        public async Task Without_an_account_new_game_files_are_refused()
        {
            Environment.SetEnvironmentVariable("BL_SKIP_ACCOUNT_CHECK", null);
            Accounts.AccountManager.Default.SignOut();
            ui.Folder = dir.Sub("anything");
            var version = new MCVersion("gdk-x", "gdk-x", "1.21.120.1", VersionType.Release, "x64", PackageType.GDK,
                new GdkPackageIdentity("Microsoft.MinecraftUWP", new Version(1, 21, 12001, 0), "x64", MinecraftPackageFamilies.PublisherId));
            MainDataModel.Default.Versions.Add(version);

            await MainDataModel.Default.PackageManager.Play(null, version, dir.Sub("data"), true, false);

            Assert.IsType<Exceptions.AccountRequiredException>(ui.Error);
            Assert.False(Directory.Exists(version.GameDirectory));
        }

        [Fact]
        public async Task Gdk_with_xodus_downloads_from_the_catalog_and_runs_through_xodus_with_winegdk()
        {
            // Stand-in WineGDK
            string winegdk = dir.Sub("winegdk");
            Script(Path.Combine(winegdk, "bin", "wine64"),
                "if [ \"$1\" = wineboot ]; then mkdir -p \"$WINEPREFIX/drive_c/users/$USER\"; touch \"$WINEPREFIX/system.reg\"; fi\nexit 0\n");
            Script(Path.Combine(winegdk, "bin", "wineserver"), "exit 0\n");
            // Stand-in xodus-cli: "streaming" extracts an encrypted game, "run" records how it was called.
            string xodus = dir.Sub("xodus", "xodus-cli");
            string record = dir.Sub("xodus", "calls.log");
            Script(xodus,
                "printf '%s\\n' \"$*\" >> \"" + record + "\"\n" +
                "if [ \"$1\" = streaming ]; then printf 'XVC' > \"$3/Minecraft.Windows.exe\"; " +
                "printf '<Game><Identity Name=\"Microsoft.MinecraftUWP\" Version=\"1.21.13101.0\"/></Game>' > \"$3/MicrosoftGame.Config\"; " +
                "printf 'xvd' > \"$3/.xodus-streaming.msixvc\"; fi\n" +
                "if [ \"$1\" = run ]; then printf '%s\\n' \"$WINEPREFIX\" > \"" + dir.Sub("xodus", "run-env.log") + "\"; fi\n");

            var settings = Properties.LauncherSettings.Default;
            settings.GdkRuntimeKind = GameRuntimeKind.WineGdk;
            settings.GdkRuntimePath = winegdk;
            settings.XodusPath = xodus;
            settings.XodusSignedIn = true;
            settings.UseDxvk = false;
            const string url = "http://assets1.xboxlive.com/Z/Microsoft.MinecraftUWP_1.21.13101.0_x64__8wekyb3d8bbwe.msixvc";
            MainDataModel.Default.PackageManager.GdkUrlResolver = _ => new[] { url };

            var required = new GdkPackageIdentity("Microsoft.MinecraftUWP", new Version(1, 21, 13101, 0), "x64", MinecraftPackageFamilies.PublisherId);
            var version = new MCVersion("gdk-131", "gdk-131", "1.21.131.1", VersionType.Release, "x64", PackageType.GDK, required);
            MainDataModel.Default.Versions.Add(version);

            string data = dir.Sub("installation", "packageData");
            await MainDataModel.Default.PackageManager.Play(null, version, data, true, false);
            Assert.Null(ui.Error);

            Assert.True(version.IsInstalled);
            Assert.True(Xodus.IsXodusInstall(version.GameDirectory));
            await WaitFor(() => File.Exists(dir.Sub("xodus", "run-env.log")));
            string[] calls = File.ReadAllLines(record);
            Assert.StartsWith("streaming " + url + " ", calls[0]);
            Assert.Equal($"run {version.GameDirectory} {Path.Combine(winegdk, "bin", "wine64")} --exe Minecraft.Windows.exe", calls[1]);
            string prefix = Path.Combine(MainDataModel.Default.FilePaths.PrefixesFolder, "winegdk");
            Assert.Equal(prefix, File.ReadAllText(dir.Sub("xodus", "run-env.log")).Trim());

            string user = Environment.GetEnvironmentVariable("USER") ?? Environment.UserName;
            var link = new DirectoryInfo(Path.Combine(prefix, "drive_c", "users", user, "AppData", "Roaming", "Minecraft Bedrock"));
            Assert.Equal(data, link.LinkTarget);
        }

        [Fact]
        public async Task Gdk_download_needs_the_xodus_sign_in()
        {
            string xodus = dir.Sub("xodus", "xodus-cli");
            Script(xodus, "exit 0\n");
            var settings = Properties.LauncherSettings.Default;
            settings.XodusPath = xodus;
            settings.XodusSignedIn = false;
            MainDataModel.Default.PackageManager.GdkUrlResolver = _ => new[] { "http://example.invalid/x.msixvc" };
            var version = new MCVersion("gdk-y", "gdk-y", "1.21.131.1", VersionType.Release, "x64", PackageType.GDK,
                new GdkPackageIdentity("Microsoft.MinecraftUWP", new Version(1, 21, 13101, 0), "x64", MinecraftPackageFamilies.PublisherId));
            MainDataModel.Default.Versions.Add(version);

            await MainDataModel.Default.PackageManager.Play(null, version, dir.Sub("data"), true, false);

            Assert.IsType<Exceptions.AccountRequiredException>(ui.Error);
            Assert.Contains("Xodus", ui.Error.Message);
        }
    }
}
