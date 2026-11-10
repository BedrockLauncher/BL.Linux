using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace BedrockLauncher.Pages.Play
{
    public partial class GameTabs : UserControl
    {
        private readonly Dictionary<ToggleButton, Func<Control>> pages;
        private readonly Dictionary<ToggleButton, Control> created = new();
        private int currentIndex = -1;

        public GameTabs()
        {
            InitializeComponent();
            pages = new Dictionary<ToggleButton, Func<Control>>
            {
                [PlayButton] = () => new PlayScreenPage(),
                [CreatorToolsButton] = () => new CreatorToolsPage(),
                [InstallationsButton] = () => new InstallationsScreen(),
                [PatchNotesButton] = () => new PatchNotesPage(),
            };
            Navigate(PlayButton);
        }

        public void Navigate(ToggleButton target)
        {
            foreach (var button in pages.Keys) button.IsChecked = button == target;
            if (!created.TryGetValue(target, out Control page)) created[target] = page = pages[target]();
            int index = System.Array.IndexOf(System.Linq.Enumerable.ToArray(pages.Keys), target);
            BedrockLauncher.UI.PageAnimator.Navigate(MainPageFrame, page, currentIndex, index);
            currentIndex = index;
        }

        public void NavigateToInstallations() => Navigate(InstallationsButton);
        public void NavigateToPlay() => Navigate(PlayButton);

        private void Tab_Click(object sender, RoutedEventArgs e) => Navigate((ToggleButton)sender);
    }
}
