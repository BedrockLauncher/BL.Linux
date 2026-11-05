using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Classes;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.UpdateProcessor.Enums;
using BedrockLauncher.UpdateProcessor.Extensions;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Preview
{
    public partial class EditInstallationScreen : UserControl
    {
        private readonly BLInstallation editing;

        public EditInstallationScreen() : this(null) { }

        public EditInstallationScreen(BLInstallation installation)
        {
            InitializeComponent();
            editing = installation;
            InstallationVersionSelect.SelectionChanged += (_, _) => UpdateHint();

            string selected = Constants.LATEST_RELEASE_UUID;
            if (installation != null)
            {
                selected = installation.VersionUUID;
                InstallationNameField.Text = installation.DisplayName;
                Header.Bind(TextBlock.TextProperty, Header.GetResourceObservable("EditInstallationScreen_AltTitle"));
                CreateButton.Bind(ContentControl.ContentProperty, CreateButton.GetResourceObservable("EditInstallationScreen_AltCreateButton"));
            }
            InstallationIconSelect.Init(installation);
            LoadVersions(selected);
        }

        /// <summary>Same filter as upstream: UWP versions always, GDK only for this machine's architecture.</summary>
        private void LoadVersions(string selectedUuid)
        {
            var versions = MainDataModel.Default.Versions
                .Where(v => v.PackageType == PackageType.UWP || VersionDbExtensions.DoesVersionArchMatch(Constants.CurrentArchitecture, v.Architecture) || v.UUID == selectedUuid)
                .ToList();
            InstallationVersionSelect.ItemsSource = versions;
            InstallationVersionSelect.SelectedItem = versions.FirstOrDefault(v => v.UUID == selectedUuid) ?? versions.FirstOrDefault();
        }

        private void UpdateHint()
        {
            var version = InstallationVersionSelect.SelectedItem as MCVersion;
            RuntimeHint.Text = version == null ? string.Empty : $"{LanguageManager.GetResource("Linux_Settings_Runtime")}: {version.RuntimeName}" +
                (version.IsGdk && !version.IsInstalled ? " · " + LanguageManager.GetResource("Linux_ImportGdk") : string.Empty);
        }

        private async void MoreVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new EditInstallationVersionSelectScreen();
            MainViewModel.Default.SetDialogFrame(window);
            string result = await window.GetVersionUUID();
            if (!string.IsNullOrWhiteSpace(result)) LoadVersions(result);
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var version = InstallationVersionSelect.SelectedItem as MCVersion;
            string name = InstallationNameField.Text;
            var config = MainDataModel.Default.Config;
            if (editing != null)
                config.Installation_Edit(editing.InstallationUUID, name, version, name, InstallationIconSelect.IconPath, InstallationIconSelect.IsIconCustom);
            else
                config.Installation_Create(name, version, name, InstallationIconSelect.IconPath, InstallationIconSelect.IsIconCustom);
            MainViewModel.Default.SetOverlayFrame(null);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => MainViewModel.Default.SetOverlayFrame(null);
    }
}
