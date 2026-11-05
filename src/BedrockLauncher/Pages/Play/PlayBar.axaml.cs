using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Classes;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Play
{
    public partial class PlayBar : UserControl
    {
        private bool launchEditor;

        public PlayBar()
        {
            InitializeComponent();
            MainDataModel.Default.ProgressBarState.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ProgressBarModel.AllowPlaying) or nameof(ProgressBarModel.PlayButtonString)
                    or nameof(ProgressBarModel.PlayEditorButtonString) or nameof(ProgressBarModel.IsGameRunning))
                    Update();
            };
            InstallationsList.SelectionChanged += (_, _) => Update();
            Update();
        }

        /// <summary>Creator Tools: the button starts the Editor and is only enabled for versions that have it.</summary>
        public bool LaunchEditor
        {
            get => launchEditor;
            set { launchEditor = value; Update(); }
        }

        private void Update()
        {
            var state = MainDataModel.Default.ProgressBarState;
            PlayButtonText.Text = launchEditor ? state.PlayEditorButtonString : state.PlayButtonString;

            if (MainDataModel.Default.PackageManager.isGameRunning)
            {
                MainPlayButton.IsEnabled = true;
                return;
            }

            BLInstallation selected = InstallationsList.SelectedItem;
            bool enabled = state.AllowPlaying;
            if (launchEditor && selected?.Version != null)
                enabled &= selected.Version.Compare(Constants.GetMinimumEditorVersion(selected.VersionType)) <= 0;
            MainPlayButton.IsEnabled = enabled;
        }

        private async void MainPlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainDataModel.Default.PackageManager.isGameRunning)
            {
                MainDataModel.Default.KillGame();
                return;
            }
            var installation = InstallationsList.SelectedItem;
            await MainDataModel.Default.Play(MainDataModel.Default.Config.CurrentProfile, installation,
                Properties.LauncherSettings.Default.KeepLauncherOpen, launchEditor);
        }
    }
}
