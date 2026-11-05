using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using BedrockLauncher.Pages.Preview;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Welcome
{
    /// <summary>First launch: welcome, storage location, profile, done (WelcomePageOne/Two/Three/Five).</summary>
    public partial class WelcomePage : UserControl
    {
        public WelcomePage()
        {
            InitializeComponent();
            ShowStep(1);
        }

        public void ShowStep(int step)
        {
            WelcomePageFrame.Content = step switch
            {
                1 => Step("FirstLaunch_WelcomeTitle", "FirstLaunch_WelcomeSubTitle", ("FirstLaunch_CloseButton", Finish), ("FirstLaunch_NextButton", () => ShowStep(2))),
                2 => new WelcomeStorageStep(() => ShowStep(1), () => ShowStep(3)),
                3 => ProfileStep(),
                _ => Step("FirstLaunch_AlmostThereTitle", "FirstLaunch_BeforeStartingToUse", ("FirstLaunch_BackButton", () => ShowStep(3)), ("FirstLaunch_FinishButton", Finish)),
            };
        }

        private Control ProfileStep()
        {
            var container = new ProfileContainer();
            container.GoBack += () => ShowStep(2);
            container.Confirm += () => ShowStep(5);
            return container;
        }

        private static void Finish()
        {
            var settings = Properties.LauncherSettings.Default;
            settings.IsFirstLaunch = false;
            settings.Save();
            if (MainDataModel.Default.Config.profiles.Count == 0)
                MainDataModel.Default.Config.Profile_Add("Default Profile", Guid.NewGuid().ToString(), "Default Profile", null);
            MainViewModel.Default.SetOverlayFrame(null);
        }

        /// <summary>The common layout of WelcomePageOne / WelcomePageFive: title, text, two buttons.</summary>
        private static Control Step(string title, string text, params (string Key, Action Action)[] buttons)
        {
            var grid = new Grid { Width = 596, Height = 254 };
            grid.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#FF313131")), Height = 1, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Avalonia.Thickness(0, 0, 0, 55) });
            var titleBlock = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.White, FontSize = 24, FontWeight = FontWeight.Bold, Margin = new Avalonia.Thickness(0, 30, 0, 0) };
            titleBlock.Bind(TextBlock.TextProperty, titleBlock.GetResourceObservable(title));
            var textBlock = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Foreground = Brushes.White, FontSize = 14, Margin = new Avalonia.Thickness(28.5, 87, 28.5, 0), TextWrapping = TextWrapping.Wrap };
            textBlock.Bind(TextBlock.TextProperty, textBlock.GetResourceObservable(text));
            grid.Children.Add(titleBlock);
            grid.Children.Add(textBlock);

            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Avalonia.Thickness(0, 0, 8, 8) };
            foreach (var (key, action) in buttons)
            {
                var button = new Button { Height = 32 };
                button.Theme = (Avalonia.Styling.ControlTheme)Avalonia.Application.Current.FindResource("DialogButton");
                button.Bind(ContentControl.ContentProperty, button.GetResourceObservable(key));
                button.Click += (_, _) => action();
                panel.Children.Add(button);
            }
            grid.Children.Add(panel);
            return grid;
        }
    }
}
