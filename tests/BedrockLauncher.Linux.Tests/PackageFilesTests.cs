using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using BedrockLauncher.Runtime;
using BedrockLauncher.UpdateProcessor.Enums;
using Xunit;

namespace BedrockLauncher.Linux.Tests
{
    public class AppxPackageTests
    {
        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(content);
        }

        [Fact]
        public async Task Extract_decodes_names_and_drops_packaging_files()
        {
            using var dir = new TempDir();
            string appx = dir.Sub("Minecraft.appx");
            using (var zip = ZipFile.Open(appx, ZipArchiveMode.Create))
            {
                AddEntry(zip, "AppxManifest.xml", "<Package><Identity Name=\"Microsoft.MinecraftUWP\" Version=\"1.20.8101.0\" ProcessorArchitecture=\"x64\"/></Package>");
                AddEntry(zip, "Minecraft.Windows.exe", "MZ");
                AddEntry(zip, "data/resource_packs/vanilla/texts/en%20US.lang", "a=b");
                AddEntry(zip, "AppxSignature.p7x", "sig");
                AddEntry(zip, "AppxBlockMap.xml", "<x/>");
                AddEntry(zip, "[Content_Types].xml", "<x/>");
                AddEntry(zip, "AppxMetadata/CodeIntegrity.cat", "cat");
            }

            await AppxPackage.ExtractAsync(appx, dir.Sub("out"));

            Assert.True(File.Exists(dir.Sub("out", "Minecraft.Windows.exe")));
            Assert.True(File.Exists(dir.Sub("out", "data", "resource_packs", "vanilla", "texts", "en US.lang")));
            Assert.False(File.Exists(dir.Sub("out", "AppxSignature.p7x")));
            Assert.False(File.Exists(dir.Sub("out", "AppxBlockMap.xml")));
            Assert.False(File.Exists(dir.Sub("out", "[Content_Types].xml")));
            Assert.False(Directory.Exists(dir.Sub("out", "AppxMetadata")));

            var identity = AppxPackage.ReadManifestIdentity(dir.Sub("out", "AppxManifest.xml"));
            Assert.Equal("Microsoft.MinecraftUWP", identity.Name);
            Assert.Equal("1.20.8101.0", identity.Version);
            Assert.Equal("x64", identity.ProcessorArchitecture);
        }

        [Fact]
        public async Task Extract_picks_the_architecture_package_of_a_bundle()
        {
            using var dir = new TempDir();
            string inner64 = dir.Sub("x64.appx"), inner86 = dir.Sub("x86.appx");
            foreach (var (path, exe) in new[] { (inner64, "x64 exe"), (inner86, "x86 exe") })
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) AddEntry(zip, "Minecraft.Windows.exe", exe);

            string bundle = dir.Sub("Minecraft.appxbundle");
            using (var zip = ZipFile.Open(bundle, ZipArchiveMode.Create))
            {
                AddEntry(zip, "AppxMetadata/AppxBundleManifest.xml",
                    "<Bundle><Packages>" +
                    "<Package Type=\"application\" Architecture=\"x86\" FileName=\"Minecraft_x86.appx\"/>" +
                    "<Package Type=\"application\" Architecture=\"x64\" FileName=\"Minecraft_x64.appx\"/>" +
                    "</Packages></Bundle>");
                zip.CreateEntryFromFile(inner86, "Minecraft_x86.appx");
                zip.CreateEntryFromFile(inner64, "Minecraft_x64.appx");
            }

            await AppxPackage.ExtractAsync(bundle, dir.Sub("out"), "x64");
            Assert.Equal("x64 exe", File.ReadAllText(dir.Sub("out", "Minecraft.Windows.exe")));
        }

        [Fact]
        public async Task Extract_refuses_entries_outside_the_destination()
        {
            using var dir = new TempDir();
            string appx = dir.Sub("evil.appx");
            using (var zip = ZipFile.Open(appx, ZipArchiveMode.Create)) AddEntry(zip, "..%2F..%2Fescape.txt", "x");
            await Assert.ThrowsAsync<InvalidDataException>(() => AppxPackage.ExtractAsync(appx, dir.Sub("out")));
        }

        [Fact]
        public void Text_responses_are_not_packages()
        {
            using var dir = new TempDir();
            File.WriteAllText(dir.Sub("page.appx"), "<html>blocked</html>");
            Assert.False(AppxPackage.LooksLikePackage(dir.Sub("page.appx"), out string problem));
            Assert.Contains("text page", problem);
        }
    }

    public class GdkGameFilesTests
    {
        private static void WriteGame(string folder, byte[] exe, string identity = "Microsoft.MinecraftUWP", string version = "1.21.13101.0")
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "Minecraft.Windows.exe"), exe);
            File.WriteAllText(Path.Combine(folder, "MicrosoftGame.Config"),
                $"<Game configVersion=\"1\"><Identity Name=\"{identity}\" Publisher=\"CN=Microsoft\" Version=\"{version}\"/></Game>");
            File.WriteAllText(Path.Combine(folder, "appxmanifest.xml"),
                $"<Package><Identity Name=\"{identity}\" Version=\"{version}\" ProcessorArchitecture=\"x64\"/></Package>");
        }

        [Fact]
        public void A_decrypted_xbox_app_folder_is_accepted_through_its_content_folder()
        {
            using var dir = new TempDir();
            WriteGame(dir.Sub("Minecraft for Windows", "Content"), new byte[] { (byte)'M', (byte)'Z', 0x90, 0 });

            var inspection = GdkGameFiles.Inspect(dir.Sub("Minecraft for Windows"));

            Assert.True(inspection.IsValid, string.Join("; ", inspection.Problems));
            Assert.Equal(VersionType.Release, inspection.VersionType);
            Assert.Equal("Microsoft.MinecraftUWP_1.21.13101.0_x64__8wekyb3d8bbwe", inspection.ToIdentity().FullName);
        }

        [Fact]
        public void An_encrypted_executable_is_rejected()
        {
            using var dir = new TempDir();
            WriteGame(dir.Sub("game"), new byte[] { 0x13, 0x37, 0x00, 0x42 });
            var inspection = GdkGameFiles.Inspect(dir.Sub("game"));
            Assert.False(inspection.IsValid);
            Assert.Contains(inspection.Problems, p => p.Contains("encrypted"));
        }

        [Fact]
        public void Preview_identity_is_classified_as_preview()
        {
            using var dir = new TempDir();
            WriteGame(dir.Sub("game"), new byte[] { (byte)'M', (byte)'Z' }, "Microsoft.MinecraftWindowsBeta", "1.21.13020.0");
            var inspection = GdkGameFiles.Inspect(dir.Sub("game"));
            Assert.True(inspection.IsValid);
            Assert.Equal(VersionType.Preview, inspection.VersionType);
        }

        [Fact]
        public void A_uwp_folder_is_not_a_gdk_build()
        {
            using var dir = new TempDir();
            Directory.CreateDirectory(dir.Sub("uwp"));
            File.WriteAllBytes(dir.Sub("uwp", "Minecraft.Windows.exe"), new byte[] { (byte)'M', (byte)'Z' });
            var inspection = GdkGameFiles.Inspect(dir.Sub("uwp"));
            Assert.False(inspection.IsValid);
            Assert.Contains(inspection.Problems, p => p.Contains("MicrosoftGame.Config"));
        }

        [Fact]
        public async Task Copy_keeps_the_folder_structure()
        {
            using var dir = new TempDir();
            WriteGame(dir.Sub("src", "Content"), new byte[] { (byte)'M', (byte)'Z' });
            Directory.CreateDirectory(dir.Sub("src", "Content", "data"));
            File.WriteAllText(dir.Sub("src", "Content", "data", "a.txt"), "a");

            await GdkGameFiles.CopyAsync(dir.Sub("src"), dir.Sub("dst"));

            Assert.True(File.Exists(dir.Sub("dst", "Minecraft.Windows.exe")));
            Assert.True(File.Exists(dir.Sub("dst", "data", "a.txt")));
        }
    }

    public class SaveDataRedirectorTests
    {
        [Fact]
        public void Existing_data_is_kept_aside_and_the_folder_becomes_a_link()
        {
            using var dir = new TempDir();
            string game = dir.Sub("pfx", "AppData", "Roaming", "Minecraft Bedrock");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "options.txt"), "old");

            var (state, backup) = new SaveDataRedirector().Redirect(game, dir.Sub("installation", "packageData"));

            Assert.Equal(SaveDataState.Linked, state);
            Assert.Equal(game + SaveDataRedirector.BackupSuffix, backup);
            Assert.Equal("old", File.ReadAllText(Path.Combine(backup, "options.txt")));
            Assert.Equal(dir.Sub("installation", "packageData"), new DirectoryInfo(game).LinkTarget);

            File.WriteAllText(Path.Combine(game, "new.txt"), "x");
            Assert.True(File.Exists(dir.Sub("installation", "packageData", "new.txt")));
        }

        [Fact]
        public void Switching_installation_replaces_only_the_link()
        {
            using var dir = new TempDir();
            string game = dir.Sub("Minecraft Bedrock");
            var redirector = new SaveDataRedirector();
            redirector.Redirect(game, dir.Sub("a"));
            File.WriteAllText(dir.Sub("a", "world.txt"), "a");

            Assert.Equal(SaveDataState.AlreadyLinked, redirector.Redirect(game, dir.Sub("a")).State);
            Assert.Equal(SaveDataState.Linked, redirector.Redirect(game, dir.Sub("b")).State);

            Assert.True(File.Exists(dir.Sub("a", "world.txt")));
            Assert.Equal(dir.Sub("b"), new DirectoryInfo(game).LinkTarget);
        }
    }

    public class RuntimeLocatorTests
    {
        [Fact]
        public void Finds_launcher_runtimes_and_gdk_proton_in_steam_compatibility_tools()
        {
            using var dir = new TempDir();
            Directory.CreateDirectory(dir.Sub("runtimes", "wine-uwp", "bin"));
            File.WriteAllText(dir.Sub("runtimes", "wine-uwp", "bin", "wine64"), "");
            File.WriteAllText(dir.Sub("runtimes", "wine-uwp", "bl-runtime.json"), "{\"kind\":\"WineUwp\",\"commit\":\"15fd380d6a700a80cfe8\"}");
            Directory.CreateDirectory(dir.Sub("home", ".steam", "root", "compatibilitytools.d", "GDK-Proton10-32"));
            File.WriteAllText(dir.Sub("home", ".steam", "root", "compatibilitytools.d", "GDK-Proton10-32", "proton"), "");
            File.WriteAllText(dir.Sub("home", ".steam", "root", "compatibilitytools.d", "GDK-Proton10-32", "version"), "1740000000 GDK-Proton10-32");
            Directory.CreateDirectory(dir.Sub("home", ".steam", "root", "compatibilitytools.d", "GE-Proton9-1"));
            File.WriteAllText(dir.Sub("home", ".steam", "root", "compatibilitytools.d", "GE-Proton9-1", "proton"), "");

            var locator = new RuntimeLocator(dir.Sub("runtimes"), dir.Sub("home"));
            var all = locator.FindAll();

            Assert.Equal(2, all.Count);
            var uwp = locator.Resolve(GameRuntimeKind.WineUwp, null);
            Assert.Equal("15fd380d6a", uwp.Version);
            var gdk = locator.Resolve(GameRuntimeKind.GdkProton, null);
            Assert.Equal("GDK-Proton10-32", gdk.Version);
            Assert.Null(locator.Resolve(GameRuntimeKind.WineGdk, null));
        }

        [Fact]
        public void A_configured_path_wins_and_must_be_usable()
        {
            using var dir = new TempDir();
            var locator = new RuntimeLocator(dir.Sub("runtimes"), dir.Sub("home"));
            Assert.Null(locator.Resolve(GameRuntimeKind.WineUwp, dir.Sub("missing")));

            Directory.CreateDirectory(dir.Sub("custom", "bin"));
            File.WriteAllText(dir.Sub("custom", "bin", "wine"), "");
            Assert.Equal(dir.Sub("custom"), locator.Resolve(GameRuntimeKind.WineUwp, dir.Sub("custom")).RootPath);
        }
    }
}
