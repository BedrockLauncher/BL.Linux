using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.Pages.Common;
using BedrockLauncher.Pages.News;
using BedrockLauncher.Pages.Play;
using BedrockLauncher.Pages.Preview;
using BedrockLauncher.Pages.Settings;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages
{
    public partial class MainPage : UserControl
    {
        private GameTabs gamePage;
        private SettingsTabs settingsScreenPage;
        private NewsScreenTabs newsScreenPage;
        private readonly List<Control> otherProfiles = new();

        public MainPage()
        {
            DataContext = MainDataModel.Default;
            InitializeComponent();
            ProfileContextMenu.Opening += (_, _) => UpdateProfileList();
        }

        #region Navigation

        private int pageIndex = -1;

        // Sidebar pages swipe vertically, by their position in the sidebar (upstream Navigator page indexes).
        private void Show(Control page, int index)
        {
            UI.PageAnimator.Navigate(MainWindowFrame, page, pageIndex, index, vertical: true);
            pageIndex = index;
        }

        private void Select(ToggleButton selected)
        {
            foreach (var button in new[] { NewsButton, BedrockEditionButton, SettingsButton })
                button.IsChecked = button == selected;
        }

        public void NavigateToNewsPage()
        {
            Select(NewsButton);
            Show(newsScreenPage ??= new NewsScreenTabs(), 0);
        }

        public void NavigateToGamePage()
        {
            Select(BedrockEditionButton);
            Show(gamePage ??= new GameTabs(), 1);
        }

        public void NavigateToSettings()
        {
            Select(SettingsButton);
            Show(settingsScreenPage ??= new SettingsTabs(), 4);
        }

        public void NavigateToAccounts()
        {
            NavigateToSettings();
            settingsScreenPage.NavigateToAccounts();
        }

        private void NewsButton_Click(object sender, RoutedEventArgs e) => NavigateToNewsPage();
        private void BedrockEditionButton_Click(object sender, RoutedEventArgs e) => NavigateToGamePage();
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => NavigateToSettings();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => MainDataModel.Default.PackageManager.Cancel();

        #endregion

        #region Profile button (Toolbar_ProfileButton)

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            ProfileButton.IsChecked = false;
            ProfileContextMenu.Open(ProfileButton);
        }

        private void UpdateProfileList()
        {
            foreach (var entry in otherProfiles) ProfileContextMenu.Items.Remove(entry);
            otherProfiles.Clear();

            var profiles = MainDataModel.Default.Config.profiles;
            bool several = profiles.Count > 1;
            RemoveProfileButton.IsEnabled = several;
            OtherAccountsHeader.IsVisible = several;
            OtherAccountsSeperator.IsVisible = several;
            if (!several) return;

            foreach (var entry in profiles)
            {
                var item = new Toolbar.ProfileItem(entry.Value);
                item.Selected += () => ProfileContextMenu.Close();
                otherProfiles.Add(item);
                ProfileContextMenu.Items.Add(item);
            }
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e) => MainViewModel.Default.SetOverlayFrame(new EditProfileScreen());

        private void EditProfileButton_Click(object sender, RoutedEventArgs e) =>
            MainViewModel.Default.SetOverlayFrame(new EditProfileScreen(MainDataModel.Default.Config.CurrentProfile));

        private async void RemoveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string uuid = Properties.LauncherSettings.Default.CurrentProfileUUID;
            if (!MainDataModel.Default.Config.profiles.TryGetValue(uuid, out var profile)) return;

            var result = await DialogPrompt.ShowDialog_YesNo(
                LanguageManager.GetResource("Dialog_DeleteItem_Title"),
                LanguageManager.GetResource("Dialog_DeleteItem_Text"),
                LanguageManager.GetResource("Dialog_Item_Profile_Text"), profile.Name);
            if (result == DialogResult.Yes) MainDataModel.Default.Config.Profile_Remove(uuid);
        }

        #endregion
    }
}
