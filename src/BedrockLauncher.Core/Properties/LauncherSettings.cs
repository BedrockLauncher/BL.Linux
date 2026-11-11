using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using BedrockLauncher.Enums;
using BedrockLauncher.Runtime;
using BedrockLauncher.ViewModels;
using Newtonsoft.Json;

namespace BedrockLauncher.Properties
{
    public class LauncherSettings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static LauncherSettings Default { get; private set; } = new LauncherSettings();

        /// <summary>Raised when <see cref="Default"/> is replaced by <see cref="Load"/>.</summary>
        public static event Action DefaultChanged;

        private static JsonSerializerSettings JsonSerializerSettings => new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public static void Load()
        {
            try
            {
                string settingsPath = MainDataModel.Default.FilePaths.GetSettingsFilePath();
                LauncherSettings loaded = null;
                if (File.Exists(settingsPath))
                {
                    try
                    {
                        loaded = JsonConvert.DeserializeObject<LauncherSettings>(File.ReadAllText(settingsPath), JsonSerializerSettings);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Settings file is unreadable, using defaults: {ex.Message}");
                    }
                }
                Default = loaded ?? new LauncherSettings();
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LauncherSettings.Load failed; using defaults.");
                Trace.WriteLine(ex);
                Default = new LauncherSettings();
            }
            DefaultChanged?.Invoke();
        }

        public void Save()
        {
            try
            {
                string path = MainDataModel.Default.FilePaths.GetSettingsFilePath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to save launcher settings:");
                Trace.WriteLine(ex);
            }
        }

        public bool GetIsFirstLaunch(int loadedConfigCount) => CurrentProfileUUID == "" || IsFirstLaunch || loadedConfigCount == 0;

        #region Upstream settings

        private InstallationSort _installationsSortMode = InstallationSort.LatestPlayed;

        public InstallationSort InstallationsSortMode
        {
            get => _installationsSortMode;
            set
            {
                _installationsSortMode = value;
                Save();
            }
        }
        public bool FetchVersionsFromMicrosoftStore { get; set; } = true;
        public bool AnimatePageTransitions { get; set; } = false;
        public string CurrentTheme { get; set; } = "LatestUpdate";
        public string Language { get; set; } = "en-US";
        public bool KeepLauncherOpen { get; set; } = false;
        public bool KeepAppx { get; set; } = true;
        public bool UseBetaBuilds { get; set; } = false;
        public bool PortableMode { get; set; } = false;
        public string FixedDirectory { get; set; } = "";
        public bool IsFirstLaunch { get; set; } = true;
        public string CurrentInstallationUUID { get; set; } = string.Empty;
        public string CurrentProfileUUID { get; set; } = "";
        public bool ShowReleases { get; set; } = true;
        public bool ShowBetas { get; set; } = true;
        public bool ShowPreviews { get; set; } = true;
        public bool ShowUWP { get; set; } = true;
        public bool ShowGDK { get; set; } = true;
        public int CurrentInsiderAccountIndex { get; set; } = 0;

        #endregion

        #region Account

        /// <summary>Application (client) ID of the launcher's Azure app registration, used for Microsoft sign-in.</summary>
        public string MicrosoftClientId { get; set; } = "";

        /// <summary>xodus-cli binary; empty = runtimes/xodus/xodus-cli or PATH.</summary>
        public string XodusPath { get; set; } = "";

        /// <summary>Set after "xodus-cli login" succeeded (Xodus keeps its own credentials in the keyring).</summary>
        public bool XodusSignedIn { get; set; } = false;

        #endregion

        #region Linux runtime settings

        /// <summary>Download missing runtimes (wine-uwp, WineGDK, GDK-Proton, Xodus) from the BL.Linux releases when needed.</summary>
        public bool AutoInstallRuntimes { get; set; } = true;

        /// <summary>wine-uwp install folder (holds bin/wine); empty = auto-detect.</summary>
        public string UwpRuntimePath { get; set; } = "";

        /// <summary>Runtime that runs GDK builds.</summary>
        public GameRuntimeKind GdkRuntimeKind { get; set; } = GameRuntimeKind.WineGdk;

        /// <summary>GDK-Proton folder (holds the 'proton' script) or WineGDK install folder; empty = auto-detect.</summary>
        public string GdkRuntimePath { get; set; } = "";

        /// <summary>Use DXVK/vkd3d (Vulkan). Off falls back to wined3d (OpenGL).</summary>
        public bool UseDxvk { get; set; } = true;

        /// <summary>esync / fsync / ntsync (the synchronization speed-ups GE-Proton enables).</summary>
        public bool UseFastSync { get; set; } = true;

        /// <summary>Wrap the game with Feral GameMode's gamemoderun when it is installed.</summary>
        public bool UseGameMode { get; set; } = false;

        /// <summary>Show the MangoHud overlay (MANGOHUD=1).</summary>
        public bool UseMangoHud { get; set; } = false;

        /// <summary>Extra environment for the game, one NAME=value per line.</summary>
        public string ExtraEnvironment { get; set; } = "";

        /// <summary>Wine debug channels (WINEDEBUG); empty keeps wine quiet.</summary>
        public string WineDebug { get; set; } = "";

        #endregion
    }
}
