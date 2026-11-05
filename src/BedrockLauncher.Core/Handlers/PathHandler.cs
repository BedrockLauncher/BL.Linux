using System;
using System.Diagnostics;
using System.IO;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Handlers
{
    /// <summary>
    /// Same layout as upstream (profiles/installations/versions under one data root), placed according to the XDG base
    /// directory spec instead of %APPDATA%\.minecraft_bedrock.
    /// </summary>
    public class PathHandler
    {
        #region Strings

        public string UserDataFileName => "user_profile.json";
        public string SettingsFileName => "settings.json";
        public string WinStoreVersionsDBFileName => "winstore_versions.json";
        public string CommunityVersionsDBFileName => "community_versions.json";
        public string GdkLinksVersionsDBFileName => "gdk_links_versions.json";
        public string GdkVersionsDBFileName => "gdk_versions.json";
        public string AppFolderName => "BedrockLauncher";
        public string InstallationsFolderName => "installations";
        public string InstallersFolder => Path.Combine(CurrentLocation, InstallationsFolderName) + Path.DirectorySeparatorChar;
        public string PackageDataFolderName => "packageData";
        public string IconCacheFolderName => "icon_cache";

        #endregion

        #region Common Paths

        /// <summary>Overrides every location (used by tests and portable setups).</summary>
        public static string RootOverride { get; set; } = Environment.GetEnvironmentVariable("BL_LINUX_HOME");

        public string CurrentLocation => Properties.LauncherSettings.Default.PortableMode ? ExecutableDataDirectory : GetFixedPath();

        public string ExecutableDataDirectory
        {
            get
            {
                string path = Path.Combine(AppContext.BaseDirectory, "data");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string XdgDataHome => XdgDirectory("XDG_DATA_HOME", ".local/share");
        public static string XdgConfigHome => XdgDirectory("XDG_CONFIG_HOME", ".config");
        public static string XdgCacheHome => XdgDirectory("XDG_CACHE_HOME", ".cache");

        public string DefaultLocation => RootOverride != null ? Path.Combine(RootOverride, "data") : Path.Combine(XdgDataHome, AppFolderName);
        public string ConfigLocation => RootOverride != null ? Path.Combine(RootOverride, "config") : Path.Combine(XdgConfigHome, AppFolderName);
        public string CacheLocation => RootOverride != null ? Path.Combine(RootOverride, "cache") : Path.Combine(XdgCacheHome, AppFolderName);

        public string VersionsFolder => Path.Combine(CurrentLocation, "versions") + Path.DirectorySeparatorChar;
        public string ThemesFolder => Path.Combine(CurrentLocation, "themes") + Path.DirectorySeparatorChar;
        public string LanguagesFolder => Path.Combine(CurrentLocation, "languages") + Path.DirectorySeparatorChar;
        public string DownloadsFolder => Path.Combine(CacheLocation, "downloads");
        public string LogsFolder => Path.Combine(CacheLocation, "logs");

        /// <summary>Compatibility runtimes the launcher installed itself (wine-uwp builds, GDK-Proton releases).</summary>
        public string RuntimesFolder => Path.Combine(CurrentLocation, "runtimes");

        /// <summary>Wine prefixes, one per runtime kind (the game data itself lives in the installation folders).</summary>
        public string PrefixesFolder => Path.Combine(CurrentLocation, "prefixes");

        private static string XdgDirectory(string variable, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value)) return value;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);
        }

        #endregion

        #region Dynamic Paths

        private string GetFixedPath()
        {
            string fixedDirectory = string.IsNullOrEmpty(Properties.LauncherSettings.Default.FixedDirectory)
                ? DefaultLocation
                : Properties.LauncherSettings.Default.FixedDirectory;

            if (!Directory.Exists(fixedDirectory))
            {
                try
                {
                    Directory.CreateDirectory(fixedDirectory);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Trace.WriteLine("Unable to Create Fixed Directory. Reverting to Fallback");
                    Properties.LauncherSettings.Default.FixedDirectory = string.Empty;
                    fixedDirectory = DefaultLocation;
                    Directory.CreateDirectory(fixedDirectory);
                }
            }
            return fixedDirectory;
        }

        public string GetSettingsFilePath() => Path.Combine(ConfigLocation, SettingsFileName);
        public string GetCommunityVersionsDBFile() => Path.Combine(CurrentLocation, CommunityVersionsDBFileName);
        public string GetGdkLinksVersionsDBFile() => Path.Combine(CurrentLocation, GdkLinksVersionsDBFileName);
        public string GetGdkVersionsDBFile() => Path.Combine(CurrentLocation, GdkVersionsDBFileName);
        public string GetWinStoreVersionsDBFile() => Path.Combine(CurrentLocation, WinStoreVersionsDBFileName);
        public string GetProfilesFilePath() => Path.Combine(CurrentLocation, UserDataFileName);

        public string GetCacheFolderPath()
        {
            string cacheDir = Path.Combine(CurrentLocation, IconCacheFolderName);
            Directory.CreateDirectory(cacheDir);
            return cacheDir;
        }

        public string GetProfilePath(string profileUUID)
        {
            if (string.IsNullOrEmpty(profileUUID)) return string.Empty;
            if (!MainDataModel.Default.Config.profiles.TryGetValue(profileUUID, out var profile)) return string.Empty;
            return Path.Combine(CurrentLocation, InstallationsFolderName, profile.ProfilePath ?? profile.Name ?? profileUUID);
        }

        public string GetInstallationPath(string profileUUID, string installationDirectory)
        {
            string profilePath = GetProfilePath(profileUUID);
            if (string.IsNullOrEmpty(profilePath)) return string.Empty;
            return Path.Combine(profilePath, installationDirectory);
        }

        public string GetInstallationPackageDataPath(string profileUUID, string installationDirectory)
        {
            string installationPath = GetInstallationPath(profileUUID, installationDirectory);
            if (string.IsNullOrEmpty(installationPath)) return string.Empty;
            return Path.Combine(installationPath, PackageDataFolderName);
        }

        #endregion

        #region Image Cache

        public string GenerateIconCacheFileName(string extension)
        {
            string cacheDir = GetCacheFolderPath();
            string destFileName = string.Empty;

            while (destFileName == string.Empty || File.Exists(destFileName))
            {
                string cacheFilename = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + extension;
                destFileName = Path.Combine(cacheDir, cacheFilename);
            }

            return destFileName;
        }

        public bool RemoveImageFromIconCache(string filePath)
        {
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return false;
            }
        }

        public string AddImageToIconCache(string sourceFilePath)
        {
            string destFileName = GenerateIconCacheFileName(Path.GetExtension(sourceFilePath));

            try
            {
                File.Copy(sourceFilePath, destFileName);
                return destFileName;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return string.Empty;
            }
        }

        #endregion
    }
}
