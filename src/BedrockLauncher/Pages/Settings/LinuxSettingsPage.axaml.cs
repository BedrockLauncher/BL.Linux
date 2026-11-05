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
            GdkKind.SelectedIndex = s.GdkRuntimeKind == GameRuntimeKind.WineGdk ? 1 : 0;
            UseDxvk.IsChecked = s.UseDxvk;
            UseGameMode.IsChecked = s.UseGameMode;
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
            s.GdkRuntimeKind = GdkKind.SelectedIndex == 1 ? GameRuntimeKind.WineGdk : GameRuntimeKind.GdkProton;
            s.UseDxvk = UseDxvk.IsChecked == true;
            s.UseGameMode = UseGameMode.IsChecked == true;
            s.UseMangoHud = UseMangoHud.IsChecked == true;
            s.WineDebug = WineDebug.Text?.Trim() ?? string.Empty;
            s.ExtraEnvironment = ExtraEnvironment.Text ?? string.Empty;
            s.Save();
        }

        private void Detect()
        {
            var runtimes = MainDataModel.Default.PackageManager.Locator.FindAll();
            DetectedList.ItemsSource = runtimes.Count == 0
                ? new[] { LanguageManager.GetResource("Linux_Settings_NoneDetected") }
                : runtimes.Select(r => $"{r.Kind,-9} {r.Version}  ({r.Origin})  {r.RootPath}").ToArray();
        }

        private void Path_LostFocus(object sender, RoutedEventArgs e) => Save();
        private void Option_Click(object sender, RoutedEventArgs e) => Save();
        private void GdkKind_SelectionChanged(object sender, SelectionChangedEventArgs e) => Save();
        private void Detect_Click(object sender, RoutedEventArgs e) => Detect();

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
