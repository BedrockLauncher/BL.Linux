using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BedrockLauncher.Runtime
{
    /// <summary>
    /// Reads and extracts .appx/.msix packages (and bundles) without Windows: they are zip files whose entry names are
    /// percent-encoded, plus signature/metadata entries that do not belong in the game folder. wine-uwp runs the
    /// extracted folder directly, which is what Windows does with a loose (developer) registration.
    /// </summary>
    public static class AppxPackage
    {
        private static readonly HashSet<string> PackagingEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AppxSignature.p7x", "AppxBlockMap.xml", "[Content_Types].xml"
        };

        public sealed class Identity
        {
            public string Name { get; init; }
            public string Version { get; init; }
            public string ProcessorArchitecture { get; init; }
            public string Publisher { get; init; }
        }

        public static bool IsBundle(ZipArchive archive) =>
            archive.Entries.Any(e => string.Equals(e.FullName, "AppxMetadata/AppxBundleManifest.xml", StringComparison.OrdinalIgnoreCase));

        /// <summary>A downloaded package that is really an HTML/JSON error page fails here instead of at extraction.</summary>
        public static bool LooksLikePackage(string path, out string problem)
        {
            problem = null;
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 4) { problem = "the file is empty"; return false; }
            Span<byte> header = stackalloc byte[4];
            using (var stream = File.OpenRead(path)) stream.ReadExactly(header);
            if (header[0] == (byte)'P' && header[1] == (byte)'K') return true;
            problem = header[0] == (byte)'<' || header[0] == (byte)'{'
                ? "the server returned a text page instead of the package"
                : "the file is not a zip-based package";
            return false;
        }

        public static Identity ReadManifestIdentity(string manifestPath) => ParseIdentity(XDocument.Load(manifestPath));

        public static Identity ParseIdentity(XDocument manifest)
        {
            XElement identity = manifest.Descendants().FirstOrDefault(x => x.Name.LocalName == "Identity");
            if (identity == null) return null;
            return new Identity
            {
                Name = (string)identity.Attribute("Name"),
                Version = (string)identity.Attribute("Version"),
                ProcessorArchitecture = (string)identity.Attribute("ProcessorArchitecture"),
                Publisher = (string)identity.Attribute("Publisher"),
            };
        }

        /// <summary>Extracts a package (or the architecture's package inside a bundle) into <paramref name="destination"/>.</summary>
        public static async Task ExtractAsync(string packagePath, string destination, string architecture = "x64",
            IProgress<(long Done, long Total)> progress = null, CancellationToken cancellationToken = default)
        {
            using ZipArchive archive = ZipFile.OpenRead(packagePath);
            if (IsBundle(archive))
            {
                ZipArchiveEntry inner = SelectBundlePackage(archive, architecture)
                    ?? throw new InvalidDataException($"The bundle has no {architecture} package.");
                string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination)) ?? ".", $".{Guid.NewGuid():N}.appx");
                try
                {
                    inner.ExtractToFile(temporary, overwrite: true);
                    await ExtractAsync(temporary, destination, architecture, progress, cancellationToken);
                }
                finally
                {
                    File.Delete(temporary);
                }
                return;
            }

            Directory.CreateDirectory(destination);
            string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            var entries = archive.Entries.Where(e => !IsPackagingEntry(e.FullName)).ToList();
            long total = entries.Sum(e => e.Length);
            long done = 0;

            foreach (ZipArchiveEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = DecodeEntryName(entry.FullName);
                string target = Path.GetFullPath(Path.Combine(destination, relative));
                if (!target.StartsWith(root, StringComparison.Ordinal))
                    throw new InvalidDataException($"Package entry '{entry.FullName}' points outside the destination.");

                if (entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (Stream input = entry.Open())
                using (FileStream output = File.Create(target))
                {
                    await input.CopyToAsync(output, cancellationToken);
                }
                done += entry.Length;
                progress?.Report((done, total));
            }
        }

        /// <summary>Appx entry names are URI-escaped ("Data/my%20file.txt").</summary>
        public static string DecodeEntryName(string name) => Uri.UnescapeDataString(name).Replace('\\', '/');

        public static bool IsPackagingEntry(string name)
        {
            if (name.StartsWith("AppxMetadata/", StringComparison.OrdinalIgnoreCase)) return true;
            return PackagingEntries.Contains(name);
        }

        private static ZipArchiveEntry SelectBundlePackage(ZipArchive bundle, string architecture)
        {
            ZipArchiveEntry manifestEntry = bundle.GetEntry("AppxMetadata/AppxBundleManifest.xml");
            if (manifestEntry != null)
            {
                using Stream stream = manifestEntry.Open();
                XDocument manifest = XDocument.Load(stream);
                var package = manifest.Descendants()
                    .Where(x => x.Name.LocalName == "Package" && (string)x.Attribute("Type") == "application")
                    .FirstOrDefault(x => string.Equals((string)x.Attribute("Architecture"), architecture, StringComparison.OrdinalIgnoreCase));
                string fileName = (string)package?.Attribute("FileName");
                if (fileName != null) return bundle.GetEntry(fileName) ?? bundle.GetEntry(Uri.EscapeDataString(fileName));
            }

            return bundle.Entries.FirstOrDefault(e =>
                (e.Name.EndsWith(".appx", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase)) &&
                e.Name.Contains("_" + architecture, StringComparison.OrdinalIgnoreCase));
        }
    }
}
