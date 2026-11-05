using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using BedrockLauncher.Runtime;
using BedrockLauncher.UpdateProcessor.Classes;
using BedrockLauncher.UpdateProcessor.Enums;
using BedrockLauncher.UpdateProcessor.Extensions;
using BedrockLauncher.ViewModels;
using Newtonsoft.Json;

namespace BedrockLauncher.Classes
{
    public class MCVersion : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public MCVersion(string uuid, string pkgId, string name, VersionType type, string architecture, PackageType packageType = PackageType.UWP, GdkPackageIdentity requiredGdkPackage = null)
        {
            if (packageType != PackageType.GDK && requiredGdkPackage != null)
                throw new ArgumentException("Only GDK versions have a required GDK package.", nameof(requiredGdkPackage));

            UUID = uuid;
            PackageID = pkgId;
            Name = name;
            Type = type;
            Architecture = architecture;
            PackageType = packageType;
            RequiredGdkPackage = requiredGdkPackage;
        }

        public MCVersion(string name)
        {
            Name = name;
            PackageType = PackageType.UWP;
        }

        public string UUID { get; set; }
        public string PackageID { get; set; }
        public string Name { get; set; }
        public string Architecture { get; set; }
        public string CustomName { get; set; }
        public VersionType Type { get; set; }
        public PackageType PackageType { get; private set; }
        public string PackageTypeString => PackageType.ToString();

        /// <summary>GDK only: the exact package this version requires (null for UWP and for unresolved GDK entries).</summary>
        public GdkPackageIdentity RequiredGdkPackage { get; private set; }

        public bool IsBeta => Type == VersionType.Beta;
        public bool IsRelease => Type == VersionType.Release;
        public bool IsPreview => Type == VersionType.Preview;
        public bool IsCustom => UUID != PackageID;
        public bool IsGdk => PackageType == PackageType.GDK;

        /// <summary>The runtime family that runs this version on Linux.</summary>
        public string RuntimeName => IsGdk ? "GDK-Proton" : "wine-uwp";

        public bool IsInstalled
        {
            get
            {
                _ = installStateVersion;
                return HasPlayableFiles;
            }
        }

        /// <summary>
        /// Linux has no package registration: a version is playable when its folder holds the game files.
        /// UWP: the extracted appx (executable + AppxManifest.xml). GDK: decrypted game files (MZ executable +
        /// MicrosoftGame.Config) imported from a Windows install.
        /// </summary>
        public bool HasPlayableFiles
        {
            get
            {
                if (!File.Exists(ExecutablePath)) return false;
                if (PackageType == PackageType.GDK)
                    return File.Exists(Path.Combine(GameDirectory, GdkGameFiles.GameConfigFileName)) &&
                           (GdkGameFiles.IsPlainExecutable(ExecutablePath) || Xodus.IsXodusInstall(GameDirectory));
                return File.Exists(ManifestPath);
            }
        }

        public string ExecutablePath => Path.Combine(GameDirectory, Constants.MINECRAFT_EXECUTABLE);

        public string GameDirectory
        {
            get
            {
                string versionsFolder = MainDataModel.Default.FilePaths.VersionsFolder;
                string folderName = GetVersionFolderName();
                bool hasConflictingVersion =
                    MainDataModel.Default.Versions.Any(version =>
                        version != this &&
                        string.Equals(version.Name, Name, StringComparison.OrdinalIgnoreCase) &&
                        (version.PackageType != PackageType ||
                         !string.Equals(version.Architecture, Architecture, StringComparison.OrdinalIgnoreCase)));

                string preferred = Path.GetFullPath(Path.Combine(
                    versionsFolder,
                    hasConflictingVersion
                        ? $"{folderName}-{PackageType}-{SanitizeFolderName(Architecture ?? string.Empty)}"
                        : folderName));

                if (hasConflictingVersion &&
                    !Directory.Exists(preferred) &&
                    IsVersionFolderForThisPackage(Path.Combine(versionsFolder, folderName)))
                {
                    return Path.GetFullPath(Path.Combine(versionsFolder, folderName));
                }

                if (!Directory.Exists(preferred) && !string.IsNullOrEmpty(UUID) && UUID != Name)
                {
                    string uuidFolder = Path.GetFullPath(Path.Combine(versionsFolder, SanitizeFolderName(UUID)));
                    if (Directory.Exists(uuidFolder)) return uuidFolder;
                }
                return preferred;
            }
        }

        private bool IsVersionFolderForThisPackage(string folderPath)
        {
            string packageIdPath = Path.Combine(folderPath, MCVersionExtensions.IdentificationFilename);
            if (!File.Exists(packageIdPath)) return false;

            try
            {
                return string.Equals(File.ReadAllText(packageIdPath).Trim(), PackageID, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine($"Could not inspect version folder identity at {packageIdPath}: {ex}");
                return false;
            }
        }

        public string GetVersionFolderName()
        {
            // Human-readable version folders (versions/1.26.33.1/); synthetic entries (Latest Release...) use the UUID.
            if (!string.IsNullOrWhiteSpace(Name)
                && Name != UUID
                && !Name.StartsWith("latest", StringComparison.OrdinalIgnoreCase)
                && System.Version.TryParse(Name, out _))
            {
                return SanitizeFolderName(Name);
            }
            return SanitizeFolderName(UUID ?? Name ?? "unknown");
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        public string DisplayName
        {
            get
            {
                string typeSuffix = string.Empty;
                string archSuffix = string.Empty;
                string pkgSuffix = $" [{PackageType}]";

                if (!VersionDbExtensions.DoesVersionArchMatch(Constants.CurrentArchitecture, Architecture))
                    archSuffix = $" [{Architecture}]";

                switch (Type)
                {
                    case VersionType.Beta:
                        typeSuffix = " (Beta)";
                        break;
                    case VersionType.Preview:
                        typeSuffix = " (Preview)";
                        break;
                }

                return (IsCustom ? CustomName : Name) + typeSuffix + pkgSuffix + archSuffix;
            }
        }

        public string InstallationSize
        {
            get
            {
                if (requireSizeRecalculation && Constants.Debugging.CalculateVersionSizes) _ = Task.Run(GetInstallSize);
                return storedInstallationSize;
            }
        }

        public string IconPath
        {
            get
            {
                if (IsCustom) return Constants.CUSTOM_VERSION_ICONPATH;
                if (IsBeta) return Constants.BETA_VERSION_ICONPATH;
                if (IsPreview) return Constants.PREVIEW_VERSION_ICONPATH;
                if (IsRelease) return Constants.RELEASE_VERSION_ICONPATH;
                return Constants.UNKNOWN_VERSION_ICONPATH;
            }
        }

        public string ManifestPath => Path.Combine(GameDirectory, MCVersionExtensions.MainifestFileName);
        public string IdentificationPath => Path.Combine(GameDirectory, MCVersionExtensions.IdentificationFilename);

        #region Size Calculation

        [JsonIgnore] private string storedInstallationSize = "...";
        [JsonIgnore] private bool requireSizeRecalculation = true;
        [JsonIgnore] private int installStateVersion;
        private static readonly object sizeLock = new object();

        private void GetInstallSize()
        {
            lock (sizeLock)
            {
                if (!requireSizeRecalculation) return;
                requireSizeRecalculation = false;

                if (IsInstalled)
                {
                    long size = new DirectoryInfo(GameDirectory)
                        .EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 })
                        .Sum(f => f.Length);
                    storedInstallationSize = FormatSize(size);
                }
                else
                {
                    storedInstallationSize = "...";
                }
            }
            OnPropertyChanged(nameof(InstallationSize));
        }

        public static string FormatSize(double length)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (length >= 1024 && order < sizes.Length - 1)
            {
                order++;
                length /= 1024;
            }
            return $"{length:0.##} {sizes[order]}";
        }

        #endregion

        #region Methods

        public string GetPackageNameFromMainifest()
        {
            var identity = AppxPackage.ReadManifestIdentity(ManifestPath);
            return string.Join("_", identity?.Name, identity?.Version, identity?.ProcessorArchitecture);
        }

        public void OpenDirectory()
        {
            string directory = Path.GetFullPath(GameDirectory);
            Directory.CreateDirectory(directory);
            Platform.OpenFolder(directory);
        }

        /// <summary>The files changed (installed/removed): recompute size and installed state.</summary>
        public void UpdateFolderSize()
        {
            requireSizeRecalculation = true;
            installStateVersion++;
            OnPropertyChanged(nameof(IsInstalled));
            OnPropertyChanged(nameof(HasPlayableFiles));
            OnPropertyChanged(nameof(InstallationSize));
        }

        public int Compare(MCVersion y)
        {
            if (System.Version.TryParse(Name, out var a) && System.Version.TryParse(y.Name, out var b))
                return b.CompareTo(a);
            return string.Compare(y.Name, Name, StringComparison.Ordinal);
        }

        #endregion
    }

    public static class MCVersionExtensions
    {
        public const string IdentificationFilename = "PackageID.txt";
        public const string MainifestFileName = "AppxManifest.xml";

        public static Tuple<string, string, string> GetCommonPackageValues(string manifestPath)
        {
            try
            {
                var identity = AppxPackage.ParseIdentity(XDocument.Load(manifestPath));
                return Tuple.Create(identity?.Name ?? "???", identity?.Version ?? "???", identity?.ProcessorArchitecture ?? "???");
            }
            catch (Exception)
            {
                return Tuple.Create("???", "???", "???");
            }
        }
    }
}
