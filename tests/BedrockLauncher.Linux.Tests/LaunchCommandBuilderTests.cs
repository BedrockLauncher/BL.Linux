using System.IO;
using BedrockLauncher.Runtime;
using Xunit;

namespace BedrockLauncher.Linux.Tests
{
    public class LaunchCommandBuilderTests
    {
        private static GameRuntime WineRuntime(TempDir dir, GameRuntimeKind kind)
        {
            Directory.CreateDirectory(dir.Sub("rt", "bin"));
            File.WriteAllText(dir.Sub("rt", "bin", "wine64"), "");
            return new GameRuntime { Kind = kind, Name = "rt", RootPath = dir.Sub("rt") };
        }

        private static GameRuntime ProtonRuntime(TempDir dir)
        {
            Directory.CreateDirectory(dir.Sub("GDK-Proton10-32"));
            File.WriteAllText(dir.Sub("GDK-Proton10-32", "proton"), "");
            return new GameRuntime { Kind = GameRuntimeKind.GdkProton, Name = "GDK-Proton10-32", RootPath = dir.Sub("GDK-Proton10-32") };
        }

        [Fact]
        public void Uwp_runs_the_exe_with_wine_uwp_and_points_package_data_at_the_installation()
        {
            using var dir = new TempDir();
            var command = LaunchCommandBuilder.Build(new LaunchRequest
            {
                Runtime = WineRuntime(dir, GameRuntimeKind.WineUwp),
                PrefixPath = dir.Sub("prefix"),
                GameDirectory = dir.Sub("versions", "1.20.81.1"),
                PackageDataPath = "/home/steve/data/installations/Steve/Latest Release/packageData",
                FindInPath = _ => null,
            });

            Assert.Equal(dir.Sub("rt", "bin", "wine64"), command.FileName);
            Assert.Equal(new[] { dir.Sub("versions", "1.20.81.1", "Minecraft.Windows.exe") }, command.Arguments);
            Assert.Equal(dir.Sub("versions", "1.20.81.1"), command.WorkingDirectory);
            Assert.Equal(dir.Sub("prefix"), command.Environment["WINEPREFIX"]);
            Assert.Equal(@"Z:\home\steve\data\installations\Steve\Latest Release\packageData", command.Environment["WINEUWP_PACKAGE_DATA"]);
            Assert.Equal("mscoree,mshtml=", command.Environment["WINEDLLOVERRIDES"]);
            Assert.Equal("-all", command.Environment["WINEDEBUG"]);
        }

        [Fact]
        public void Gdk_proton_uses_the_proton_script_with_compat_data_and_builtin_gameinput()
        {
            using var dir = new TempDir();
            var command = LaunchCommandBuilder.Build(new LaunchRequest
            {
                Runtime = ProtonRuntime(dir),
                PrefixPath = dir.Sub("prefixes", "gdkproton"),
                GameDirectory = dir.Sub("versions", "1.21.131.1"),
                SteamClientPath = dir.Sub("steam"),
                UseDxvk = false,
                FindInPath = _ => null,
            });

            Assert.Equal(dir.Sub("GDK-Proton10-32", "proton"), command.FileName);
            Assert.Equal("waitforexitandrun", command.Arguments[0]);
            Assert.Equal(dir.Sub("versions", "1.21.131.1", "Minecraft.Windows.exe"), command.Arguments[1]);
            Assert.Equal(dir.Sub("prefixes", "gdkproton"), command.Environment["STEAM_COMPAT_DATA_PATH"]);
            Assert.Equal(dir.Sub("steam"), command.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"]);
            Assert.Equal("1", command.Environment["PROTON_USE_WINED3D"]);
            Assert.Equal("GameInputRedist=b", command.Environment["WINEDLLOVERRIDES"]);
            Assert.False(command.Environment.ContainsKey("WINEUWP_PACKAGE_DATA"));
            Assert.False(command.Environment.ContainsKey("WINEPREFIX"));
        }

        [Fact]
        public void Wine_gdk_runs_wine_directly()
        {
            using var dir = new TempDir();
            var command = LaunchCommandBuilder.Build(new LaunchRequest
            {
                Runtime = WineRuntime(dir, GameRuntimeKind.WineGdk),
                PrefixPath = dir.Sub("prefix"),
                GameDirectory = dir.Sub("game"),
                FindInPath = _ => null,
            });

            Assert.Equal(dir.Sub("rt", "bin", "wine64"), command.FileName);
            Assert.Equal("mscoree,mshtml=;GameInputRedist=b", command.Environment["WINEDLLOVERRIDES"]);
        }

        [Fact]
        public void Gamemode_mangohud_and_extra_environment_are_applied()
        {
            using var dir = new TempDir();
            var command = LaunchCommandBuilder.Build(new LaunchRequest
            {
                Runtime = WineRuntime(dir, GameRuntimeKind.WineUwp),
                PrefixPath = dir.Sub("prefix"),
                GameDirectory = dir.Sub("game"),
                UseGameMode = true,
                UseMangoHud = true,
                WineDebug = "+loaddll",
                ExtraEnvironment = "# comment\nDXVK_HUD=fps\n\nWINEDLLOVERRIDES=dwmapi=b\nbroken line",
                FindInPath = name => name == "gamemoderun" ? "/usr/bin/gamemoderun" : null,
            });

            Assert.Equal("/usr/bin/gamemoderun", command.FileName);
            Assert.Equal(dir.Sub("rt", "bin", "wine64"), command.Arguments[0]);
            Assert.Equal("1", command.Environment["MANGOHUD"]);
            Assert.Equal("+loaddll", command.Environment["WINEDEBUG"]);
            Assert.Equal("fps", command.Environment["DXVK_HUD"]);
            Assert.Equal("mscoree,mshtml=;dwmapi=b", command.Environment["WINEDLLOVERRIDES"]);
        }

        [Fact]
        public void Gamemode_is_skipped_when_not_installed()
        {
            using var dir = new TempDir();
            var command = LaunchCommandBuilder.Build(new LaunchRequest
            {
                Runtime = WineRuntime(dir, GameRuntimeKind.WineUwp),
                PrefixPath = dir.Sub("prefix"),
                GameDirectory = dir.Sub("game"),
                UseGameMode = true,
                FindInPath = _ => null,
            });
            Assert.Equal(dir.Sub("rt", "bin", "wine64"), command.FileName);
        }

        [Fact]
        public void Wine_path_maps_unix_paths_to_the_z_drive()
        {
            Assert.Equal(@"Z:\home\a b\c", WinePath.ToWindows("/home/a b/c"));
        }
    }
}
