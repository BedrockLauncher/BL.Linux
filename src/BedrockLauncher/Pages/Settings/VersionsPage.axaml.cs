using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Classes;
using BedrockLauncher.Handlers;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.Pages.Common;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Settings
{
    public partial class VersionsPage : UserControl
    {
        public VersionsPage()
        {
            InitializeComponent();
            var s = Properties.LauncherSettings.Default;
            ShowReleases.IsChecked = s.ShowReleases;
            ShowBetas.IsChecked = s.ShowBetas;
            ShowPreviews.IsChecked = s.ShowPreviews;
            ShowUWP.IsChecked = s.ShowUWP;
            ShowGDK.IsChecked = s.ShowGDK;

            MainDataModel.Default.Versions.CollectionChanged += (_, _) => RefreshList();
            MainDataModel.Default.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainDataModel.IsVersionsUpdating))
                    RefreshButton.IsEnabled = AddButton.IsEnabled = ImportGdkButton.IsEnabled = !MainDataModel.Default.IsVersionsUpdating;
            };
            MainDataModel.Default.ProgressBarState.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProgressBarModel.AllowEditing))
                {
                    Root.IsEnabled = MainDataModel.Default.ProgressBarState.AllowEditing;
                    RefreshList();
                }
            };
            AttachedToVisualTree += (_, _) => RefreshList();
            RefreshList();
        }

        /// <summary>Only installed versions are listed (upstream Filter_VersionList).</summary>
        public void RefreshList()
        {
            foreach (var v in MainDataModel.Default.Versions) v.UpdateFolderSize();
            var list = MainDataModel.Default.Versions.Where(FilterSortingHandler.Filter_VersionList).ToList();
            VersionsList.ItemsSource = list;
            NothingFound.IsVisible = list.Count == 0;
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            var s = Properties.LauncherSettings.Default;
            s.ShowReleases = ShowReleases.IsChecked == true;
            s.ShowBetas = ShowBetas.IsChecked == true;
            s.ShowPreviews = ShowPreviews.IsChecked == true;
            s.ShowUWP = ShowUWP.IsChecked == true;
            s.ShowGDK = ShowGDK.IsChecked == true;
            s.Save();
            RefreshList();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await MainWindow.RefreshApplication();
            RefreshList();
        }

        private async void AddPackage_Click(object sender, RoutedEventArgs e)
        {
            string file = await MainViewModel.Default.PickFile("Minecraft package", ("App packages", new[] { "*.appx", "*.msix", "*.appxbundle", "*.msixbundle" }));
            if (string.IsNullOrEmpty(file)) return;
            await MainDataModel.Default.PackageManager.AddPackage(file);
            RefreshList();
        }

        private async void ImportGdk_Click(object sender, RoutedEventArgs e)
        {
            string folder = await MainViewModel.Default.PickFolder(string.Format(LanguageManager.GetResource("Linux_GdkPickFolder"), "GDK"));
            if (string.IsNullOrEmpty(folder)) return;
            await MainDataModel.Default.PackageManager.AddPackage(folder);
            RefreshList();
        }

        private static MCVersion Item(object sender) => (sender as Control)?.DataContext as MCVersion;

        private void Repair_Click(object sender, RoutedEventArgs e) => MainDataModel.Default.RepairVersion(Item(sender));

        private void Folder_Click(object sender, RoutedEventArgs e) => Item(sender)?.OpenDirectory();

        private void More_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.ContextMenu?.Open(button);
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            var version = Item(sender);
            if (version == null) return;
            var result = await DialogPrompt.ShowDialog_YesNo(
                LanguageManager.GetResource("Dialog_DeleteItem_Title"),
                LanguageManager.GetResource("Dialog_DeleteItem_Text"),
                LanguageManager.GetResource("Dialog_Item_Version_Text"), version.DisplayName);
            if (result != DialogResult.Yes) return;
            await MainDataModel.Default.PackageManager.RemovePackage(version);
            RefreshList();
        }
    }
}
