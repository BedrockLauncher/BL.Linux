using Avalonia.Controls;
using BedrockLauncher.Classes;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Preview
{
    public partial class EditProfileScreen : UserControl
    {
        public EditProfileScreen() : this(null) { }

        public EditProfileScreen(BLProfile profile)
        {
            InitializeComponent();
            var container = new ProfileContainer(profile);
            container.GoBack += () => MainViewModel.Default.SetOverlayFrame(null);
            container.Confirm += () => MainViewModel.Default.SetOverlayFrame(null);
            ProfileControlContainer.Content = container;
        }
    }
}
