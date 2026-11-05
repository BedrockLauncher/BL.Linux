using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using BedrockLauncher.ViewModels;
using Newtonsoft.Json;

namespace BedrockLauncher.Classes
{
    public class BLProfile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string Name { get; set; }
        public string UUID { get; set; }
        public string ProfilePath { get; set; }
        public ObservableCollection<BLInstallation> Installations { get; set; } = new ObservableCollection<BLInstallation>();

        #region Runtime Values

        [JsonIgnore]
        public string ImagePath
        {
            get
            {
                string profileDirectory = MainDataModel.Default.FilePaths.GetProfilePath(UUID);
                if (string.IsNullOrEmpty(profileDirectory)) return Constants.PROFILE_DEFAULT_IMG;
                string profileImage = Path.Combine(profileDirectory, Constants.PROFILE_CUSTOM_IMG_NAME);
                return File.Exists(profileImage) ? profileImage : Constants.PROFILE_DEFAULT_IMG;
            }
        }

        public void RefreshImage() => OnPropertyChanged(nameof(ImagePath));

        #endregion

        public BLProfile() { }

        public BLProfile(string name, string path, string uuid)
        {
            Name = name;
            ProfilePath = path;
            UUID = uuid;
        }
    }
}
