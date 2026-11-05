using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Settings
{
    public partial class GeneralSettingsPage : UserControl
    {
        public GeneralSettingsPage()
        {
            InitializeComponent();
            var s = Properties.LauncherSettings.Default;
            KeepAppx.IsChecked = s.KeepAppx;
            KeepLauncherOpen.IsChecked = s.KeepLauncherOpen;
            AnimatePageTransitions.IsChecked = s.AnimatePageTransitions;
            FetchVersionsFromMicrosoftStore.IsChecked = s.FetchVersionsFromMicrosoftStore;
            PortableModeCheckBox.IsChecked = s.PortableMode;
            StorageDirectoryTextBox.Text = string.IsNullOrEmpty(s.FixedDirectory) ? MainDataModel.Default.FilePaths.DefaultLocation : s.FixedDirectory;
            PathBox.IsEnabled = !s.PortableMode;
            MainDataModel.Default.ProgressBarState.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProgressBarModel.AllowEditing))
                    RestartButton.IsEnabled = KeepLauncherOpen.IsEnabled = MainDataModel.Default.ProgressBarState.AllowEditing;
            };
        }

        private void Checkbox_Click(object sender, RoutedEventArgs e)
        {
            var s = Properties.LauncherSettings.Default;
            s.KeepAppx = KeepAppx.IsChecked == true;
            s.KeepLauncherOpen = KeepLauncherOpen.IsChecked == true;
            s.AnimatePageTransitions = AnimatePageTransitions.IsChecked == true;
            s.FetchVersionsFromMicrosoftStore = FetchVersionsFromMicrosoftStore.IsChecked == true;
            s.Save();
        }

        private void PortableMode_Click(object sender, RoutedEventArgs e) => PathBox.IsEnabled = PortableModeCheckBox.IsChecked != true;

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            string folder = await MainViewModel.Default.PickFolder("BedrockLauncher");
            if (!string.IsNullOrEmpty(folder)) StorageDirectoryTextBox.Text = folder;
        }

        private void Reset_Click(object sender, RoutedEventArgs e) => StorageDirectoryTextBox.Text = MainDataModel.Default.FilePaths.DefaultLocation;

        /// <summary>Saves the location and restarts the launcher so everything is read from the new place.</summary>
        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            var s = Properties.LauncherSettings.Default;
            s.PortableMode = PortableModeCheckBox.IsChecked == true;
            string path = StorageDirectoryTextBox.Text;
            s.FixedDirectory = path == MainDataModel.Default.FilePaths.DefaultLocation ? string.Empty : path;
            s.Save();

            string exe = System.Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
            MainViewModel.Default.CloseLauncher();
        }

        private void Crowdin_Click(object sender, RoutedEventArgs e) => Platform.OpenUrl("https://crowdin.com/project/bedrocklauncher");
    }
}
