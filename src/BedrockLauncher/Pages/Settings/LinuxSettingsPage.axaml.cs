using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.Runtime;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Settings
{
    public partial class LinuxSettingsPage : UserControl
    {
        private bool loading = true;

        public LinuxSettingsPage()
        {
            InitializeComponent();
            var s = Properties.LauncherSettings.Default;
            UwpPath.Text = s.UwpRuntimePath;
            GdkPath.Text = s.GdkRuntimePath;
            GdkKind.SelectedIndex = s.GdkRuntimeKind == GameRuntimeKind.GdkProton ? 1 : 0;
            AutoInstallRuntimes.IsChecked = s.AutoInstallRuntimes;
            UseDxvk.IsChecked = s.UseDxvk;
            UseGameMode.IsChecked = s.UseGameMode;
            UseFastSync.IsChecked = s.UseFastSync;
            UseMangoHud.IsChecked = s.UseMangoHud;
            WineDebug.Text = s.WineDebug;
            ExtraEnvironment.Text = s.ExtraEnvironment;
            loading = false;
            Detect();
        }

        private void Save()
        {
            if (loading) return;
            var s = Properties.LauncherSettings.Default;
            s.UwpRuntimePath = UwpPath.Text?.Trim() ?? string.Empty;
            s.GdkRuntimePath = GdkPath.Text?.Trim() ?? string.Empty;
            s.GdkRuntimeKind = GdkKind.SelectedIndex == 1 ? GameRuntimeKind.GdkProton : GameRuntimeKind.WineGdk;
            s.AutoInstallRuntimes = AutoInstallRuntimes.IsChecked == true;
            s.UseDxvk = UseDxvk.IsChecked == true;
            s.UseGameMode = UseGameMode.IsChecked == true;
            s.UseFastSync = UseFastSync.IsChecked == true;
            s.UseMangoHud = UseMangoHud.IsChecked == true;
            s.WineDebug = WineDebug.Text?.Trim() ?? string.Empty;
            s.ExtraEnvironment = ExtraEnvironment.Text ?? string.Empty;
            s.Save();
        }

        private void Detect()
        {
            var settings = Properties.LauncherSettings.Default;
            var locator = MainDataModel.Default.PackageManager.Locator;
            var runtimes = locator.FindAll();
            DetectedList.ItemsSource = runtimes.Count == 0
                ? new[] { LanguageManager.GetResource("Linux_Settings_NoneDetected") }
                : runtimes.Select(r => $"{r.Kind,-9} {r.Version}  ({r.Origin})  {r.RootPath}").ToArray();

            GameRuntime uwp = locator.Resolve(GameRuntimeKind.WineUwp, settings.UwpRuntimePath);
            GameRuntime gdk = locator.Resolve(settings.GdkRuntimeKind, settings.GdkRuntimePath)
                ?? (string.IsNullOrWhiteSpace(settings.GdkRuntimePath) ? locator.Resolve(GameRuntimeKind.GdkProton, null) : null);
            string xodus = Xodus.Locate(MainDataModel.Default.FilePaths.RuntimesFolder, settings.XodusPath);

            ShowStatus(UwpStatus, uwp == null ? null : $"{uwp.Name} {uwp.Version}");
            ShowStatus(GdkStatus, gdk == null ? null : $"{gdk.Name} ({gdk.Kind}) {gdk.Version}");
            ShowStatus(XodusStatus, xodus == null ? null : LanguageManager.GetResource(settings.XodusSignedIn ? "Linux_Status_XodusSignedIn" : "Linux_Status_XodusSignedOut"));
        }

        private static void ShowStatus(TextBlock target, string installed)
        {
            target.Text = installed == null
                ? "✖ " + LanguageManager.GetResource("Linux_Status_Missing")
                : "✔ " + installed;
            target.Foreground = Avalonia.Media.Brush.Parse(installed == null ? "#FFE8B33B" : "#FF52A535");
        }

        private void Advanced_Click(object sender, RoutedEventArgs e)
        {
            Advanced.IsVisible = !Advanced.IsVisible;
            AdvancedToggle.Content = LanguageManager.GetResource(Advanced.IsVisible ? "Linux_Settings_HideAdvanced" : "Linux_Settings_ShowAdvanced");
        }

        private void Path_LostFocus(object sender, RoutedEventArgs e) => Save();
        private void Option_Click(object sender, RoutedEventArgs e) => Save();
        private void GdkKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Save();
            if (!loading) Detect();
        }
        private void Detect_Click(object sender, RoutedEventArgs e) => Detect();

        private async void InstallRuntimes_Click(object sender, RoutedEventArgs e)
        {
            InstallRuntimesButton.IsEnabled = false;
            try
            {
                var report = await MainDataModel.Default.PackageManager.InstallOrUpdateRuntimes();
                InstallReport.Text = string.Join("\n", report);
                InstallReport.IsVisible = report.Count > 0;
                Detect();
            }
            finally
            {
                InstallRuntimesButton.IsEnabled = true;
            }
        }

        private async void BrowseUwp_Click(object sender, RoutedEventArgs e)
        {
            string folder = await MainViewModel.Default.PickFolder("wine-uwp");
            if (string.IsNullOrEmpty(folder)) return;
            UwpPath.Text = folder;
            Save();
        }

        private async void BrowseGdk_Click(object sender, RoutedEventArgs e)
        {
            string folder = await MainViewModel.Default.PickFolder("GDK-Proton / WineGDK");
            if (string.IsNullOrEmpty(folder)) return;
            GdkPath.Text = folder;
            Save();
        }

        private void OpenRuntimes_Click(object sender, RoutedEventArgs e)
        {
            string folder = MainDataModel.Default.FilePaths.RuntimesFolder;
            Directory.CreateDirectory(folder);
            Platform.OpenFolder(folder);
        }

        private void OpenLogs_Click(object sender, RoutedEventArgs e)
        {
            string folder = MainDataModel.Default.FilePaths.LogsFolder;
            Directory.CreateDirectory(folder);
            Platform.OpenFolder(folder);
        }
    }
}
