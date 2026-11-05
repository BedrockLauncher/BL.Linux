using System;
using System.Collections.Generic;
using System.Linq;
using BedrockLauncher.Classes;
using BedrockLauncher.Enums;
using BedrockLauncher.UpdateProcessor.Enums;

namespace BedrockLauncher.Handlers
{
    /// <summary>Port of upstream FilterSortingHandler (filters/sort orders of the installation and version lists).</summary>
    public static class FilterSortingHandler
    {
        public static string InstallationsSearchFilter { get; set; } = string.Empty;

        public static IEnumerable<BLInstallation> Sort_InstallationList(IEnumerable<BLInstallation> source) =>
            Properties.LauncherSettings.Default.InstallationsSortMode switch
            {
                InstallationSort.Name => source.OrderBy(x => x.DisplayName_Full, StringComparer.CurrentCultureIgnoreCase),
                InstallationSort.None => source,
                _ => source.OrderByDescending(x => x.LastPlayed),
            };

        public static bool Filter_InstallationList(BLInstallation installation)
        {
            if (installation == null) return false;
            var settings = Properties.LauncherSettings.Default;
            if (!settings.ShowPreviews && installation.IsPreview) return false;
            if (!settings.ShowBetas && installation.IsBeta) return false;
            if (!settings.ShowReleases && installation.IsRelease) return false;
            if (!string.IsNullOrEmpty(InstallationsSearchFilter) &&
                (installation.DisplayName_Full == null || !installation.DisplayName_Full.Contains(InstallationsSearchFilter, StringComparison.OrdinalIgnoreCase)))
                return false;
            return true;
        }

        public static bool Filter_VersionList(MCVersion version)
        {
            if (version == null || !version.IsInstalled) return false;
            var settings = Properties.LauncherSettings.Default;
            if (version.PackageType == PackageType.UWP && !settings.ShowUWP) return false;
            if (version.PackageType == PackageType.GDK && !settings.ShowGDK) return false;
            if (version.IsPreview) return settings.ShowPreviews;
            if (version.IsBeta) return settings.ShowBetas;
            if (version.IsRelease) return settings.ShowReleases;
            return false;
        }
    }
}
