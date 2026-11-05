using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Classes.Launcher;
using BedrockLauncher.Downloaders;
using BedrockLauncher.Pages.General;

namespace BedrockLauncher.Pages.News
{
    public partial class NewsScreenTabs : UserControl
    {
        private bool loadedOfficial, loadedForums;

        public NewsScreenTabs()
        {
            InitializeComponent();
            AttachedToVisualTree += async (_, _) => await LoadCurrent(false);
        }

        private bool ShowingForums => ForumsTab.IsChecked == true;

        private async Task LoadCurrent(bool force)
        {
            ResultPanel.PanelType = ResultPanelType.Loading;
            ResultPanel.IsVisible = true;
            IList items;
            if (ShowingForums)
            {
                if (loadedForums && !force) { UpdateEmpty(ForumsList.ItemsSource as IList); return; }
                items = await RssDownloader.GetFeed(Constants.RSS_FORUMS_URL);
                ForumsList.ItemsSource = items;
                loadedForums = true;
            }
            else
            {
                if (loadedOfficial && !force) { UpdateEmpty(OfficialList.ItemsSource as IList); return; }
                items = await NewsDownloader.GetOfficialFeed();
                OfficialList.ItemsSource = items;
                loadedOfficial = true;
            }
            UpdateEmpty(items);
        }

        private void UpdateEmpty(IList items)
        {
            ResultPanel.PanelType = ResultPanelType.NoNews;
            ResultPanel.IsVisible = items == null || items.Count == 0;
        }

        private async void Tab_Click(object sender, RoutedEventArgs e)
        {
            bool forums = sender == ForumsTab;
            ForumsTab.IsChecked = forums;
            OfficialTab.IsChecked = !forums;
            ForumsView.IsVisible = forums;
            OfficialView.IsVisible = !forums;
            await LoadCurrent(false);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadCurrent(true);

        private void OfficialItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is News_OfficialItem item && !string.IsNullOrEmpty(item.Link)) Platform.OpenUrl(item.Link);
        }

        private void RssItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is RssItem item && !string.IsNullOrEmpty(item.Link)) Platform.OpenUrl(item.Link);
        }
    }
}
