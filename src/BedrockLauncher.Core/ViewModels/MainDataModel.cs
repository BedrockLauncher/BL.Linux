using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BedrockLauncher.Classes;
using BedrockLauncher.Handlers;
using BedrockLauncher.UpdateProcessor.Enums;

namespace BedrockLauncher.ViewModels
{
    public class MainDataModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            UiThread.Run(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));

        public static MainDataModel Default { get; set; } = new MainDataModel();

        #region Properties

        public IUiHost UiHost { get; set; }
        public ProgressBarModel ProgressBarState { get; set; } = new ProgressBarModel();
        public PathHandler FilePaths { get; private set; } = new PathHandler();
        public PackageHandler PackageManager { get; set; } = new PackageHandler();
        public BLProfileList Config { get; private set; } = new BLProfileList();
        public ObservableCollection<MCVersion> Versions { get; private set; } = new ObservableCollection<MCVersion>();

        public bool AllowedToCloseWithGameOpen { get; set; } = false;
        public bool IsVersionsUpdating { get; private set; }

        #endregion

        #region Methods

        public async Task LoadVersions(bool onLoad = false)
        {
            if (IsVersionsUpdating) return;
            IsVersionsUpdating = true;
            try
            {
                await PackageManager.VersionDownloader.UpdateVersionList(Versions, onLoad);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Version list update failed: {ex}");
            }
            finally
            {
                foreach (BLProfile profile in Config.profiles.Values)
                    foreach (BLInstallation installation in profile.Installations ?? Enumerable.Empty<BLInstallation>())
                        installation.RefreshVersion();
                IsVersionsUpdating = false;
            }
        }

        public void LoadConfig()
        {
            Config = BLProfileList.Load(FilePaths.GetProfilesFilePath(), Properties.LauncherSettings.Default.CurrentProfileUUID, Properties.LauncherSettings.Default.CurrentInstallationUUID);
        }

        public async void KillGame() => await PackageManager.ClosePackage();

        public async void RepairVersion(MCVersion v) => await PackageManager.DownloadPackage(v);

        public async void RemoveVersion(MCVersion v) => await PackageManager.RemovePackage(v);

        public async Task Play(BLProfile p, BLInstallation i, bool keepLauncherOpen, bool launchEditor, bool save = true)
        {
            if (p == null || i == null || i.Version == null) return;

            i.LastPlayed = DateTime.Now;
            Config.Installation_UpdateLP(i);

            if (save)
            {
                Properties.LauncherSettings.Default.CurrentInstallationUUID = i.InstallationUUID;
                Properties.LauncherSettings.Default.Save();
            }

            string path = FilePaths.GetInstallationPackageDataPath(p.UUID, i.DirectoryName_Full);
            await PackageManager.Play(p, i.Version, path, keepLauncherOpen, launchEditor);
        }

        /// <summary>Installs (downloads / imports) without launching the game.</summary>
        public async Task Install(BLProfile p, BLInstallation i)
        {
            if (p == null || i?.Version == null)
            {
                Trace.WriteLine($"Install skipped: the version of installation '{i?.DisplayName_Full}' is not available.");
                return;
            }

            string path = FilePaths.GetInstallationPackageDataPath(p.UUID, i.DirectoryName_Full);
            await PackageManager.InstallPackage(i.Version, path);
        }

        #endregion
    }
}
