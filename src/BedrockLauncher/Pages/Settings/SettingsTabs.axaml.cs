using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace BedrockLauncher.Pages.Settings
{
    public partial class SettingsTabs : UserControl
    {
        private readonly Dictionary<ToggleButton, Func<Control>> pages;
        private readonly Dictionary<ToggleButton, Control> created = new();
        private int currentIndex = -1;

        public SettingsTabs()
        {
            InitializeComponent();
            pages = new Dictionary<ToggleButton, Func<Control>>
            {
                [GeneralButton] = () => new GeneralSettingsPage(),
                [AccountsButton] = () => new AccountsPage(),
                [VersionsButton] = () => new VersionsPage(),
                [LinuxButton] = () => new LinuxSettingsPage(),
                [AboutButton] = () => new AboutPage(),
            };
            Navigate(GeneralButton);
        }

        public void Navigate(ToggleButton target)
        {
            foreach (var button in pages.Keys) button.IsChecked = button == target;
            if (!created.TryGetValue(target, out Control page)) created[target] = page = pages[target]();
            int index = System.Array.IndexOf(System.Linq.Enumerable.ToArray(pages.Keys), target);
            BedrockLauncher.UI.PageAnimator.Navigate(SettingsScreenFrame, page, currentIndex, index);
            currentIndex = index;
        }

        public void NavigateToLinux() => Navigate(LinuxButton);
        public void NavigateToAccounts() => Navigate(AccountsButton);

        private void Tab_Click(object sender, RoutedEventArgs e) => Navigate((ToggleButton)sender);
    }
}
