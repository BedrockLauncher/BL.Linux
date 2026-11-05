using System;
using System.IO;
using System.Linq;

namespace BedrockLauncher.Runtime
{
    public enum SaveDataState
    {
        AlreadyLinked,
        Linked
    }

    /// <summary>
    /// Port of upstream's GdkSaveDataRedirector. A GDK build keeps its data in %APPDATA%\Minecraft Bedrock (Preview:
    /// "Minecraft Bedrock Preview"); inside the prefix that folder becomes a symlink to the installation's data folder,
    /// so every installation keeps its own worlds. Existing real data is moved to a ".default" backup, never deleted,
    /// and put back if the link cannot be created.
    /// </summary>
    public sealed class SaveDataRedirector
    {
        public const string BackupSuffix = ".default";

        public (SaveDataState State, string BackupPath) Redirect(string gameDataPath, string installationDataPath)
        {
            if (string.IsNullOrWhiteSpace(gameDataPath)) throw new ArgumentException("The game data path is required.", nameof(gameDataPath));
            if (string.IsNullOrWhiteSpace(installationDataPath)) throw new ArgumentException("The installation data path is required.", nameof(installationDataPath));

            string target = Path.GetFullPath(installationDataPath);
            Directory.CreateDirectory(target);

            string backupPath = null;
            var info = new DirectoryInfo(gameDataPath);

            if (info.LinkTarget != null)
            {
                if (PointsTo(info, target))
                    return (SaveDataState.AlreadyLinked, null);

                // A link to another installation: removing the link leaves that installation's data untouched.
                info.Delete();
            }
            else if (info.Exists)
            {
                if (!Directory.EnumerateFileSystemEntries(gameDataPath).Any())
                {
                    Directory.Delete(gameDataPath, false);
                }
                else
                {
                    backupPath = GetFreeBackupPath(gameDataPath);
                    Directory.Move(gameDataPath, backupPath);
                }
            }
            else if (File.Exists(gameDataPath))
            {
                throw new IOException($"'{gameDataPath}' is a file, not the Minecraft data folder.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(gameDataPath)));

            try
            {
                Directory.CreateSymbolicLink(gameDataPath, target);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (backupPath != null && !Directory.Exists(gameDataPath))
                    Directory.Move(backupPath, gameDataPath);
                throw new IOException($"Could not link '{gameDataPath}' to '{target}': {ex.Message}", ex);
            }

            return (SaveDataState.Linked, backupPath);
        }

        private static bool PointsTo(DirectoryInfo link, string target)
        {
            string linkTarget = link.LinkTarget;
            if (string.IsNullOrWhiteSpace(linkTarget)) return false;

            string resolved = Path.GetFullPath(Path.IsPathRooted(linkTarget)
                ? linkTarget
                : Path.Combine(link.Parent?.FullName ?? string.Empty, linkTarget));

            return string.Equals(resolved.TrimEnd('/'), target.TrimEnd('/'), StringComparison.Ordinal);
        }

        private static string GetFreeBackupPath(string gameDataPath)
        {
            string candidate = gameDataPath + BackupSuffix;
            for (int i = 1; Directory.Exists(candidate) || File.Exists(candidate); i++)
                candidate = $"{gameDataPath}{BackupSuffix}{i}";
            return candidate;
        }
    }
}
