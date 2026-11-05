using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BedrockLauncher.Accounts;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Common
{
    /// <summary>Shows the Microsoft device code and waits for the user to finish signing in in the browser.</summary>
    public partial class SignInDialog : UserControl
    {
        private readonly CancellationTokenSource cancel = new();
        private string verificationUri;

        public SignInDialog() => InitializeComponent();

        /// <returns>true when an account is signed in afterwards.</returns>
        public static async Task<bool> ShowAsync()
        {
            var dialog = new SignInDialog();
            MainViewModel.Default.SetDialogFrame(dialog);
            try
            {
                await AccountManager.Default.SignInAsync(prompt => Dispatcher.UIThread.Post(() => dialog.ShowPrompt(prompt)), dialog.cancel.Token);
                MainViewModel.Default.SetDialogFrame(null);
                return true;
            }
            catch (OperationCanceledException)
            {
                MainViewModel.Default.SetDialogFrame(null);
                return false;
            }
            catch (Exception ex)
            {
                MainViewModel.Default.SetDialogFrame(null);
                await ErrorScreen.ShowMessage(LanguageManager.GetResource("Linux_Account_CodeTitle"), ex.Message);
                return false;
            }
        }

        private void ShowPrompt(DeviceCodePrompt prompt)
        {
            verificationUri = prompt.VerificationUri;
            Instructions.Text = string.Format(LanguageManager.GetResource("Linux_Account_CodeText"), prompt.VerificationUri);
            CodeText.Text = prompt.UserCode;
            OpenBrowserButton.IsEnabled = CopyButton.IsEnabled = true;
        }

        private void OpenBrowser_Click(object sender, RoutedEventArgs e) => Platform.OpenUrl(verificationUri);

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(CodeText.Text);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => cancel.Cancel();
    }
}
