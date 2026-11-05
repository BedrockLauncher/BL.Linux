using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using BedrockLauncher.Enums;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.UpdateProcessor.Enums;
using BedrockLauncher.ViewModels;
using Newtonsoft.Json;

namespace BedrockLauncher.Classes
{
    public class BLInstallation : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string DisplayName { get; set; }
        public string VersionUUID { get; set; }
        public string IconPath { get; set; }
        public bool IsCustomIcon { get; set; } = false;
        public string DirectoryName { get; set; }
        public bool ReadOnly { get; set; }
        public VersioningMode VersioningMode { get; set; } = VersioningMode.None;
        public DateTime LastPlayed { get; set; }
        public string InstallationUUID { get; set; } = Guid.NewGuid().ToString();

        #region Runtime Values

        [JsonIgnore]
        public string IconPath_Full
        {
            get
            {
                if (IsCustomIcon) return Path.Combine(MainDataModel.Default.FilePaths.GetCacheFolderPath(), IconPath ?? string.Empty);
                return Constants.INSTALLATIONS_ICONPATH + (IconPath ?? Constants.INSTALLATIONS_FALLBACK_ICONPATH);
            }
        }

        [JsonIgnore]
        public string DisplayName_Full
        {
            get
            {
                if (VersionUUID == Constants.LATEST_RELEASE_UUID && ReadOnly) return LanguageManager.GetResource("VersionEntries_LatestRelease");
                if (VersionUUID == Constants.LATEST_PREVIEW_UUID && ReadOnly) return LanguageManager.GetResource("VersionEntries_LatestPreview");
                if (string.IsNullOrWhiteSpace(DisplayName)) return LanguageManager.GetResource("VersionEntries_UnnamedInstallation");
                return DisplayName;
            }
        }

        [JsonIgnore]
        public string DirectoryName_Full
        {
            get
            {
                if (string.IsNullOrEmpty(DirectoryName))
                {
                    char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
                    return new string((DisplayName ?? string.Empty).Where(ch => !invalidFileNameChars.Contains(ch)).ToArray());
                }
                return DirectoryName;
            }
        }

        [JsonIgnore]
        public MCVersion Version => MainDataModel.Default.PackageManager.VersionDownloader.GetVersion(VersioningMode, VersionUUID);

        [JsonIgnore]
        public bool IsRelease => VersioningMode == VersioningMode.LatestRelease || (Version?.IsRelease ?? false);

        [JsonIgnore]
        public bool IsBeta => VersioningMode == VersioningMode.LatestBeta || (Version?.IsBeta ?? false);

        [JsonIgnore]
        public bool IsPreview => VersioningMode == VersioningMode.LatestPreview || (Version?.IsPreview ?? false);

        [JsonIgnore]
        public string VersionName => Version?.Name ?? "???";

        [JsonIgnore]
        public string LastPlayedT => LastPlayed.ToString("s");

        [JsonIgnore]
        public VersionType VersionType => Version?.Type ?? VersionType.Release;

        #endregion

        public BLInstallation Clone(string newName = null, bool keepUuid = false)
        {
            var clone = (BLInstallation)MemberwiseClone();
            clone.PropertyChanged = null;
            if (!keepUuid) clone.InstallationUUID = Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(newName)) clone.DisplayName = newName;
            return clone;
        }

        /// <summary>
        /// The version values come from the version list, which loads after the installations are shown; called once
        /// the list is loaded so they are read again.
        /// </summary>
        public void RefreshVersion()
        {
            foreach (string property in new[] { nameof(Version), nameof(VersionName), nameof(IsRelease), nameof(IsBeta), nameof(IsPreview), nameof(VersionType), nameof(DisplayName_Full) })
                OnPropertyChanged(property);
        }

        public void DeleteUserData()
        {
            string directory = MainDataModel.Default.FilePaths.GetInstallationPath(Properties.LauncherSettings.Default.CurrentProfileUUID, DirectoryName_Full);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        public void OpenDirectory()
        {
            string directory = MainDataModel.Default.FilePaths.GetInstallationPackageDataPath(Properties.LauncherSettings.Default.CurrentProfileUUID, DirectoryName_Full);
            Directory.CreateDirectory(directory);
            Platform.OpenFolder(directory);
        }
    }
}
