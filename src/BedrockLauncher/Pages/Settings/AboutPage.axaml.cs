using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Localization.Language;

namespace BedrockLauncher.Pages.Settings
{
    public partial class AboutPage : UserControl
    {
        public AboutPage()
        {
            InitializeComponent();
            BuildText.Text = $"{LanguageManager.GetResource("AboutPage_CurrentBuild")} {typeof(AboutPage).Assembly.GetName().Version} (Linux)";
        }

        private void Upstream_Click(object sender, RoutedEventArgs e) => Platform.OpenUrl(Constants.UPSTREAM_PAGE);
        private void Linux_Click(object sender, RoutedEventArgs e) => Platform.OpenUrl("https://github.com/BedrockLauncher/BL.Linux");
    }
}
