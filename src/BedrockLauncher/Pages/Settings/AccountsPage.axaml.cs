using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Accounts;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.Pages.Common;
using BedrockLauncher.Runtime;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Settings
{
    public partial class AccountsPage : UserControl
    {
        public AccountsPage()
        {
            InitializeComponent();
            ClientIdBox.Text = Properties.LauncherSettings.Default.MicrosoftClientId;
            XodusPathBox.Text = Properties.LauncherSettings.Default.XodusPath;
            AccountManager.Default.PropertyChanged += (_, _) => Update();
            LanguageManager.LanguageChanged += _ => Update();
            Update();
        }

        private void Update()
        {
            var account = AccountManager.Default;
            StatusText.Text = account.IsSignedIn
                ? string.Format(LanguageManager.GetResource("Linux_Account_SignedInAs"), account.Gamertag)
                : LanguageManager.GetResource("Linux_Account_SignedOut");
            XuidText.Text = account.IsSignedIn ? "XUID " + account.Xuid : string.Empty;
            SignInButton.IsVisible = !account.IsSignedIn;
            SignOutButton.IsVisible = account.IsSignedIn;

            var settings = Properties.LauncherSettings.Default;
            string cli = Xodus.Locate(MainDataModel.Default.FilePaths.RuntimesFolder, settings.XodusPath);
            XodusPathText.Text = cli ?? LanguageManager.GetResource("Linux_Xodus_NotInstalled");
            XodusStatus.Text = LanguageManager.GetResource(settings.XodusSignedIn ? "Linux_Xodus_SignedIn" : "Linux_Xodus_SignedOut");
            XodusSignInButton.IsVisible = !settings.XodusSignedIn;
            XodusSignInButton.IsEnabled = cli != null;
            XodusSignOutButton.IsVisible = settings.XodusSignedIn;
        }

        private void XodusPath_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.LauncherSettings.Default.XodusPath = XodusPathBox.Text?.Trim() ?? string.Empty;
            Properties.LauncherSettings.Default.Save();
            Update();
        }

        /// <summary>"xodus-cli login" opens Microsoft's sign-in page in its own window and exits once signed in.</summary>
        private async void XodusSignIn_Click(object sender, RoutedEventArgs e)
        {
            string cli = Xodus.Locate(MainDataModel.Default.FilePaths.RuntimesFolder, Properties.LauncherSettings.Default.XodusPath);
            if (cli == null) return;
            int code = -1;
            await MainViewModel.Default.ShowWaitingDialog(async () =>
                code = await ProcessRunner.Default.RunAsync(Xodus.Login(cli), line => System.Diagnostics.Trace.WriteLine("[xodus] " + line)));
            Properties.LauncherSettings.Default.XodusSignedIn = code == 0;
            Properties.LauncherSettings.Default.Save();
            if (code != 0) await ErrorScreen.ShowMessage(LanguageManager.GetResource("Linux_Xodus_Title"), $"xodus-cli login exited with code {code}. See ~/.cache/BedrockLauncher/logs/launcher.log.");
            Update();
        }

        private async void XodusSignOut_Click(object sender, RoutedEventArgs e)
        {
            string cli = Xodus.Locate(MainDataModel.Default.FilePaths.RuntimesFolder, Properties.LauncherSettings.Default.XodusPath);
            if (cli != null) await ProcessRunner.Default.RunAsync(Xodus.Logout(cli), line => System.Diagnostics.Trace.WriteLine("[xodus] " + line));
            Properties.LauncherSettings.Default.XodusSignedIn = false;
            Properties.LauncherSettings.Default.Save();
            Update();
        }

        private void ClientId_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.LauncherSettings.Default.MicrosoftClientId = ClientIdBox.Text?.Trim() ?? string.Empty;
            Properties.LauncherSettings.Default.Save();
        }

        private async void SignIn_Click(object sender, RoutedEventArgs e)
        {
            ClientId_LostFocus(sender, e);
            await SignInDialog.ShowAsync();
        }

        private void SignOut_Click(object sender, RoutedEventArgs e) => AccountManager.Default.SignOut();
    }
}
