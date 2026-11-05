using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Welcome
{
    public partial class WelcomeStorageStep : UserControl
    {
        private readonly Action back, next;

        public WelcomeStorageStep() : this(null, null) { }

        public WelcomeStorageStep(Action back, Action next)
        {
            this.back = back;
            this.next = next;
            InitializeComponent();
            var settings = Properties.LauncherSettings.Default;
            PortableRadioButton.IsChecked = settings.PortableMode;
            FixedRadioButton.IsChecked = !settings.PortableMode;
            StorageDirectoryTextBox.Text = string.IsNullOrEmpty(settings.FixedDirectory) ? MainDataModel.Default.FilePaths.DefaultLocation : settings.FixedDirectory;
            PathBox.IsEnabled = !settings.PortableMode;
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (PathBox == null) return;
            PathBox.IsEnabled = FixedRadioButton.IsChecked == true;
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            string folder = await MainViewModel.Default.PickFolder("BedrockLauncher");
            if (!string.IsNullOrEmpty(folder)) StorageDirectoryTextBox.Text = folder;
        }

        private void Reset_Click(object sender, RoutedEventArgs e) => StorageDirectoryTextBox.Text = MainDataModel.Default.FilePaths.DefaultLocation;

        private void Back_Click(object sender, RoutedEventArgs e) => back?.Invoke();

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            var settings = Properties.LauncherSettings.Default;
            settings.PortableMode = PortableRadioButton.IsChecked == true;
            string path = StorageDirectoryTextBox.Text;
            settings.FixedDirectory = path == MainDataModel.Default.FilePaths.DefaultLocation ? string.Empty : path;
            settings.Save();
            MainDataModel.Default.LoadConfig();
            next?.Invoke();
        }
    }
}
