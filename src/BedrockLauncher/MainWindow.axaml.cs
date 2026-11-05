using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.Pages.Welcome;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = MainViewModel.Default;
            InitializeComponent();
            Opened += async (_, _) => await OnLoaded();
            Closing += (_, e) =>
            {
                if (MainViewModel.Default.AllowedToClose()) return;
                e.Cancel = true;
                MainViewModel.Default.LauncherCanNotCloseDialog(Close);
            };
        }

        /// <summary>Upstream Program.OnApplicationLoaded + MainWindow.Window_Initialized.</summary>
        private async Task OnLoaded()
        {
            if (Design.IsDesignMode) return;

            await MainViewModel.Default.ShowWaitingDialog(async () =>
            {
                Trace.WriteLine("Preparing Application...");
                MainDataModel.Default.LoadConfig();
                await MainDataModel.Default.LoadVersions(true);
                Trace.WriteLine("Preparing Application: DONE");
            });

            MainPage.NavigateToGamePage();

            bool isFirstLaunch = Properties.LauncherSettings.Default.GetIsFirstLaunch(MainDataModel.Default.Config.profiles.Count);
            if (isFirstLaunch) MainViewModel.Default.SetOverlayFrame(new WelcomePage(), true);
        }

        public static async Task RefreshApplication()
        {
            await MainViewModel.Default.ShowWaitingDialog(async () =>
            {
                MainDataModel.Default.LoadConfig();
                await MainDataModel.Default.LoadVersions();
            });
        }
    }
}
