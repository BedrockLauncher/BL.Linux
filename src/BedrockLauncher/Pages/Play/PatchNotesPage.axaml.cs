using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Classes.Launcher;
using BedrockLauncher.Downloaders;
using BedrockLauncher.Pages.General;
using BedrockLauncher.Pages.Preview;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Play
{
    public partial class PatchNotesPage : UserControl
    {
        private readonly ChangelogDownloader downloader = new();

        public PatchNotesPage()
        {
            DataContext = downloader;
            InitializeComponent();
            downloader.PatchNotes.CollectionChanged += (_, _) => UpdateEmpty();
            AttachedToVisualTree += async (_, _) =>
            {
                if (downloader.PatchNotes.Count > 0) return;
                await downloader.UpdateList();
                NothingFound.PanelType = ResultPanelType.NoContent;
                UpdateEmpty();
            };
        }

        private void UpdateEmpty() => NothingFound.IsVisible = downloader.PatchNotes.Count == 0;

        private void RefreshList(object sender, RoutedEventArgs e) => downloader.ApplyFilter();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await downloader.UpdateList();

        private void MorePatchNotes_Click(object sender, RoutedEventArgs e) => Platform.OpenUrl(Constants.PATCHNOTES_RELEASE_CHANGELOG_URL);

        private void MoreBetaPatchNotes_Click(object sender, RoutedEventArgs e) => Platform.OpenUrl(Constants.PATCHNOTES_PREVIEW_CHANGELOG_URL);

        private void FeedItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is PatchNotes_Game_Item item)
                MainViewModel.Default.SetOverlayFrame(new ChangelogPreviewPage(item.body, item.title));
        }
    }
}
