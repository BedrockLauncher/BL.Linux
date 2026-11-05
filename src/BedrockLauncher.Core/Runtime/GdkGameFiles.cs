using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using BedrockLauncher.UpdateProcessor.Classes;
using BedrockLauncher.UpdateProcessor.Enums;

namespace BedrockLauncher.Runtime
{
    /// <summary>
    /// GDK builds come as encrypted MSIXVC packages that only Gaming Services on Windows can install and decrypt; there
    /// is no way to install them on Linux. What Linux can run is an already installed game folder (the Content folder
    /// of an Xbox app install, copied out while the package is mounted, e.g. with Invoke-CommandInDesktopPackage). This
    /// class checks such a folder and imports it as a launcher version.
    /// </summary>
    public static class GdkGameFiles
    {
        public const string GameConfigFileName = "MicrosoftGame.Config";

        public sealed class Inspection
        {
            public bool IsValid => Problems.Count == 0;
            public List<string> Problems { get; } = new List<string>();
            public string IdentityName { get; set; }
            public Version Version { get; set; }
            public VersionType VersionType { get; set; }
            public string Architecture { get; set; } = "x64";
            public string ExecutablePath { get; set; }

            public GdkPackageIdentity ToIdentity() => Version == null || IdentityName == null
                ? null
                : new GdkPackageIdentity(IdentityName, Version, Architecture, MinecraftPackageFamilies.PublisherId);
        }

        /// <summary>The folder the game files are in: the folder itself or its Content subfolder (Xbox app layout).</summary>
        public static string ResolveGameFolder(string folder)
        {
            if (File.Exists(Path.Combine(folder, Constants.MINECRAFT_EXECUTABLE))) return folder;
            string content = Path.Combine(folder, "Content");
            if (File.Exists(Path.Combine(content, Constants.MINECRAFT_EXECUTABLE))) return content;
            return folder;
        }

        public static Inspection Inspect(string folder)
        {
            var result = new Inspection();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                result.Problems.Add($"'{folder}' does not exist.");
                return result;
            }

            folder = ResolveGameFolder(folder);
            string exe = Path.Combine(folder, Constants.MINECRAFT_EXECUTABLE);
            result.ExecutablePath = exe;

            if (!File.Exists(exe))
                result.Problems.Add($"{Constants.MINECRAFT_EXECUTABLE} is missing.");
            else if (!IsPlainExecutable(exe))
                result.Problems.Add($"{Constants.MINECRAFT_EXECUTABLE} is encrypted. Copy the files while the game package is mounted on Windows (the copy straight from XboxGames is encrypted).");

            string config = Path.Combine(folder, GameConfigFileName);
            if (!File.Exists(config))
            {
                result.Problems.Add($"{GameConfigFileName} is missing: this is not a GDK build of Minecraft.");
                return result;
            }

            try
            {
                XElement identity = XDocument.Load(config).Root?.Elements().FirstOrDefault(x => x.Name.LocalName == "Identity");
                result.IdentityName = (string)identity?.Attribute("Name");
                if (System.Version.TryParse((string)identity?.Attribute("Version"), out Version version)) result.Version = version;

                if (!MinecraftPackageFamilies.TryGetVersionType(result.IdentityName, out VersionType type))
                    result.Problems.Add($"'{result.IdentityName}' is not a Minecraft identity.");
                result.VersionType = type;
                if (result.Version == null)
                    result.Problems.Add($"{GameConfigFileName} has no valid Identity/@Version.");
            }
            catch (Exception ex) when (ex is IOException || ex is System.Xml.XmlException)
            {
                result.Problems.Add($"{GameConfigFileName} is unreadable: {ex.Message}");
            }

            string manifest = Path.Combine(folder, "appxmanifest.xml");
            if (!File.Exists(manifest)) manifest = Path.Combine(folder, "AppxManifest.xml");
            if (File.Exists(manifest))
            {
                try
                {
                    string arch = AppxPackage.ReadManifestIdentity(manifest)?.ProcessorArchitecture;
                    if (!string.IsNullOrWhiteSpace(arch)) result.Architecture = arch;
                }
                catch (Exception ex) when (ex is IOException || ex is System.Xml.XmlException)
                {
                    // architecture stays x64
                }
            }

            return result;
        }

        /// <summary>True when the file starts with the "MZ" DOS header (an encrypted XVC payload does not).</summary>
        public static bool IsPlainExecutable(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>Copies the game folder into <paramref name="destination"/> (a version folder).</summary>
        public static async Task CopyAsync(string source, string destination, IProgress<(long Done, long Total)> progress = null,
            CancellationToken cancellationToken = default)
        {
            source = ResolveGameFolder(source);
            var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToList();
            long total = files.Sum(f => new FileInfo(f).Length);
            long done = 0;

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string target = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (FileStream input = File.OpenRead(file))
                using (FileStream output = File.Create(target))
                {
                    await input.CopyToAsync(output, cancellationToken);
                }
                done += new FileInfo(file).Length;
                progress?.Report((done, total));
            }
        }
    }
}
