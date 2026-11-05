using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using BedrockLauncher.Classes;
using BedrockLauncher.Enums;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Handlers
{
    /// <summary>Port of upstream InstallationBackupHandler: zips / restores an installation's data folder.</summary>
    public static class InstallationBackupHandler
    {
        private const string BackupsFolderName = "backups";
        private const string RestoreSuffix = ".before-restore-";

        private static IUiHost Ui => MainDataModel.Default.UiHost;

        public static string GetBackupsFolder() => Path.Combine(MainDataModel.Default.FilePaths.CurrentLocation, BackupsFolderName);

        private static string GetDataPath(BLInstallation installation) =>
            MainDataModel.Default.FilePaths.GetInstallationPackageDataPath(Properties.LauncherSettings.Default.CurrentProfileUUID, installation.DirectoryName_Full);

        private static string BackupPrefix(BLInstallation installation)
        {
            string name = installation.DisplayName_Full ?? "installation";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name + "_";
        }

        public static async Task BackupAsync(BLInstallation installation)
        {
            if (installation == null) return;

            string source = GetDataPath(installation);
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source) || !Directory.EnumerateFileSystemEntries(source).Any())
            {
                await Ui.ShowMessage("Nothing to back up", $"\"{installation.DisplayName_Full}\" has no saved data yet.");
                return;
            }
            if (MainDataModel.Default.PackageManager.isGameRunning)
            {
                await Ui.ShowMessage("Close Minecraft first", "Minecraft is running and keeps its files open. Close it and try again.");
                return;
            }

            string backupsFolder = GetBackupsFolder();
            Directory.CreateDirectory(backupsFolder);
            string destination = Path.Combine(backupsFolder, $"{BackupPrefix(installation)}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");
            string temporary = destination + ".tmp";

            await RunWithProgress(async () =>
            {
                try
                {
                    await Task.Run(() => ZipFile.CreateFromDirectory(source, temporary, CompressionLevel.Optimal, includeBaseDirectory: false));
                    File.Move(temporary, destination, overwrite: true);
                    Trace.WriteLine($"Backup of '{installation.DisplayName_Full}' saved to {destination}");
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            });

            Platform.OpenFolder(backupsFolder);
        }

        public static async Task RestoreAsync(BLInstallation installation)
        {
            if (installation == null) return;
            string target = GetDataPath(installation);
            if (string.IsNullOrEmpty(target)) return;

            if (MainDataModel.Default.PackageManager.isGameRunning)
            {
                await Ui.ShowMessage("Close Minecraft first", "Minecraft is running and keeps its files open. Close it and try again.");
                return;
            }

            Directory.CreateDirectory(GetBackupsFolder());
            string archive = await Ui.PickFile("Restore backup", ("Backup (*.zip)", new[] { "*.zip" }));
            if (string.IsNullOrEmpty(archive)) return;

            string previous = target + RestoreSuffix + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            bool confirmed = await Ui.Confirm("Restore backup", $"Replace the data of \"{installation.DisplayName_Full}\" with {Path.GetFileName(archive)}? " +
                                                                 $"The current data is kept in {Path.GetFileName(previous)} next to it.");
            if (!confirmed) return;

            await RunWithProgress(async () =>
            {
                bool movedAside = false;
                try
                {
                    if (Directory.Exists(target))
                    {
                        Directory.Move(target, previous);
                        movedAside = true;
                    }
                    await Task.Run(() => ZipFile.ExtractToDirectory(archive, target));
                }
                catch
                {
                    if (Directory.Exists(target) && movedAside) Directory.Delete(target, true);
                    if (movedAside) Directory.Move(previous, target);
                    throw;
                }
            });
        }

        private static async Task RunWithProgress(Func<Task> action)
        {
            var progress = MainDataModel.Default.ProgressBarState;
            progress.ResetProgressBarProgress();
            progress.SetProgressBarVisibility(true);
            progress.SetProgressBarState(LauncherState.isBackingUp);
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                await Ui.ShowException(ex);
            }
            finally
            {
                progress.SetProgressBarVisibility(false);
                progress.SetProgressBarState(LauncherState.None);
            }
        }
    }
}
