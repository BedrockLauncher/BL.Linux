using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BedrockLauncher.Classes;
using BedrockLauncher.UI;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Preview
{
    /// <summary>Port of Component_AddProfileContainer: profile picture + name, used by the welcome flow and the profile editor.</summary>
    public partial class ProfileContainer : UserControl
    {
        private readonly BLProfile editing;
        private string imagePath;

        public event Action GoBack;
        public event Action Confirm;

        public ProfileContainer() : this(null) { }

        public ProfileContainer(BLProfile profile)
        {
            InitializeComponent();
            editing = profile;
            if (profile != null)
            {
                ProfileNameTextbox.Text = profile.Name;
                Subtitle.Bind(TextBlock.TextProperty, Subtitle.GetResourceObservable("NewProfile_EditProfileSubTitle"));
                CreateProfileButtonText.Bind(TextBlock.TextProperty, CreateProfileButtonText.GetResourceObservable("NewProfile_EditProfileButton"));
            }
            ProfileImage.Source = ImagePathConverter.Load(profile?.ImagePath ?? Constants.PROFILE_DEFAULT_IMG);
        }

        private async void ImageButton_Click(object sender, RoutedEventArgs e)
        {
            string file = await MainViewModel.Default.PickFile("Image", ("Images", new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" }));
            if (string.IsNullOrEmpty(file)) return;
            imagePath = file;
            ProfileImage.Source = ImagePathConverter.Load(file);
        }

        private void ProfileNameTextbox_TextChanged(object sender, TextChangedEventArgs e) =>
            CreateProfileButton.IsEnabled = !string.IsNullOrWhiteSpace(ProfileNameTextbox.Text);

        private void ProfileNameTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && CreateProfileButton.IsEnabled) CreateProfileButton_Click(sender, e);
        }

        private void CreateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string name = ProfileNameTextbox.Text?.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var config = MainDataModel.Default.Config;
            if (editing != null)
            {
                config.Profile_Edit(name, editing.UUID, name, imagePath);
                editing.RefreshImage();
                config.OnPropertyChanged(nameof(BLProfileList.CurrentProfile));
                config.OnPropertyChanged(nameof(BLProfileList.CurrentProfileImagePath));
            }
            else
            {
                config.Profile_Add(name, Guid.NewGuid().ToString(), name, imagePath);
            }
            Confirm?.Invoke();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => GoBack?.Invoke();
    }
}
