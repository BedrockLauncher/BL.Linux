using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.UI;

namespace BedrockLauncher.Pages.Settings
{
    public partial class AboutPage : UserControl
    {
        private const string Credits = Constants.APP_RESOURCEPATH_PREFIX + "Resources/images/credits/";

        public AboutPage()
        {
            InitializeComponent();
            BuildText.Text = $"{LanguageManager.GetResource("AboutPage_CurrentBuild")} {typeof(AboutPage).Assembly.GetName().Version} (Linux)";

            Founder.Content = Card("carjem_logo.png", "Founder: Carjem Generations", null, 34, 220, 170, 18, 220,
                ("Twitter", "https://twitter.com/carter5467_99"), ("GitHub", "https://github.com/CarJem"));

            Team.Children.Add(Small("raythmc.png", "Rayth", "Lead Dev/Admin", ("GitHub", "https://github.com/Rayth")));
            Team.Children.Add(Small("mrmariix.png", "mrmariix", "Developer", ("GitHub", "https://github.com/mrmariixx"), ("YouTube", "https://youtube.com/@mrmariixx")));
            Team.Children.Add(Small("MetalManeMc.jpg", "MetalManeMC", "Developer", ("GitHub", "https://github.com/MetalManeMc"),
                ("BlueSky", "https://bsky.app/profile/metalmanemc.bsky.social"), ("ModBay", "https://modbay.org/user/metalmanemc/")));
            Team.Children.Add(Small("DLazaridisGM.png", "DLazaridisGM", "Developer", ("GitHub", "https://github.com/DLazaridisGM")));

            Original.Content = Card("xlynxx.jpg", "XlynxX", "Forked from the work of XlynxX", 24, 150, 120, 12, 150,
                ("Twitter", "https://twitter.com/xlynxx0"), ("GitHub", "https://github.com/XlynxX"));

            Thanks.Children.Add(Small("mcmrarm_icon.jfif", "MCMrARM", LanguageManager.GetResource("AboutPage_McMrArm_Text"),
                ("Twitter", "https://twitter.com/mcmrarm"), ("GitHub", "https://github.com/MCMrARM")));
            Thanks.Children.Add(Small("mojang_logo.png", "Mojang", LanguageManager.GetResource("AboutPage_Mojang_Text"),
                ("Website", "https://www.minecraft.net/en-us/")));

            Linux.Children.Add(Small(null, "wine-uwp", "UWP runtime by Rosentti", ("GitHub", "https://github.com/Rosentti/wine-uwp")));
            Linux.Children.Add(Small(null, "WineGDK", "GDK runtime by Weather-OS", ("GitHub", "https://github.com/Weather-OS/WineGDK")));
            Linux.Children.Add(Small(null, "Xodus", "Xbox services on Linux", ("GitHub", "https://github.com/xodus-gaming/xodus")));
            Linux.Children.Add(Small(null, "GE-Proton", "Build flags, esync and runtime setup", ("GitHub", "https://github.com/GloriousEggroll/proton-ge-custom")));
        }

        private Control Small(string image, string title, string subtitle, params (string Label, string Url)[] links)
        {
            var card = Card(image, title, subtitle, 24, 138, 118, 10, 150, links);
            card.Margin = new Thickness(4);
            return card;
        }

        /// <summary>A credit card as in upstream: #262626 panel, bordered picture on the left, name / role / links.</summary>
        private Border Card(string image, string title, string subtitle, double titleSize, double imageWidth, double imageHeight,
            double padding, double minHeight, params (string Label, string Url)[] links)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            if (image != null)
            {
                grid.Children.Add(new Border
                {
                    Width = imageWidth, Height = imageHeight, Margin = new Thickness(0, 0, 18, 0),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                    BorderBrush = new SolidColorBrush(Color.Parse("#555555")), BorderThickness = new Thickness(1),
                    Child = new Image { Source = ImagePathConverter.Load(Credits + image), Stretch = Stretch.Uniform },
                });
            }

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = titleSize, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            if (subtitle != null)
                text.Children.Add(new TextBlock { Text = subtitle, FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#D8D8D8")), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 5) });

            var linkPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, subtitle == null ? 12 : 0, 0, 0) };
            foreach (var (label, url) in links)
            {
                var button = new Button { Content = label, FontSize = subtitle == null ? 15 : 13, Theme = (ControlTheme)Application.Current.FindResource("LinkButton") };
                button.Click += (_, _) => Platform.OpenUrl(url);
                linkPanel.Children.Add(button);
            }
            text.Children.Add(linkPanel);
            grid.Children.Add(text);

            return new Border { Background = new SolidColorBrush(Color.Parse("#262626")), Padding = new Thickness(padding), MinHeight = minHeight, Child = grid };
        }

        private void Releases_Click(object sender, RoutedEventArgs e) => Platform.OpenUrl(Constants.UPDATES_RELEASE_PAGE);
    }
}
