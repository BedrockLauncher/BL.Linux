using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Classes;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Toolbar
{
    public partial class ProfileItem : UserControl
    {
        private readonly BLProfile profile;
        public event Action Selected;

        public ProfileItem() : this(new BLProfile()) { }

        public ProfileItem(BLProfile profile)
        {
            this.profile = profile;
            DataContext = profile;
            InitializeComponent();
            SelectedMark.IsVisible = profile.UUID == Properties.LauncherSettings.Default.CurrentProfileUUID;
        }

        private void SourceButton_Click(object sender, RoutedEventArgs e)
        {
            SourceButton.IsChecked = false;
            MainDataModel.Default.Config.Profile_Switch(profile.UUID);
            Selected?.Invoke();
        }
    }
}
