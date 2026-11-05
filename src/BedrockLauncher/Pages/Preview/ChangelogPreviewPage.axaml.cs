using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Preview
{
    /// <summary>Port of ChangelogPreviewPage: renders a patch note / news body (HTML) with the same small stylesheet.</summary>
    public partial class ChangelogPreviewPage : UserControl
    {
        private const string HTMLStyle = "<style>body { color: white; font-family: 'Noto Sans', sans-serif; font-size: 14px; } a { color: green; } img { height: auto; max-width: 100%; }</style>";
        private readonly string url;

        public ChangelogPreviewPage() => InitializeComponent();

        public ChangelogPreviewPage(string html, string header, string url = null) : this()
        {
            this.url = url;
            Header.Text = header;
            SourceButton.IsHitTestVisible = !string.IsNullOrEmpty(url);
            Renderer.Text = $"<!DOCTYPE html><html><head>{HTMLStyle}</head><body>{html}</body></html>";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => MainViewModel.Default.SetOverlayFrame(null);

        private void SourceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(url)) Platform.OpenUrl(url);
        }
    }
}
