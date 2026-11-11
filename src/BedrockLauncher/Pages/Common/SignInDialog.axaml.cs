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
    /// <summary>
    /// Microsoft sign-in: opens Microsoft's page in the browser, which redirects back to the launcher on localhost.
    /// "Use a code" switches to the device code flow (type a code on microsoft.com/link, e.g. from a phone).
    /// </summary>
    public partial class SignInDialog : UserControl
    {
        private readonly CancellationTokenSource cancel = new();
        private CancellationTokenSource browserCancel;
        private bool useCode;
        private string verificationUri;

        public SignInDialog() => InitializeComponent();

        /// <returns>true when an account is signed in afterwards.</returns>
        public static async Task<bool> ShowAsync()
        {
            var dialog = new SignInDialog();
            MainViewModel.Default.SetDialogFrame(dialog);
            try
            {
                dialog.browserCancel = CancellationTokenSource.CreateLinkedTokenSource(dialog.cancel.Token);
                try
                {
                    await AccountManager.Default.SignInWithBrowserAsync(prompt => Dispatcher.UIThread.Post(() => dialog.ShowBrowserPrompt(prompt)), dialog.browserCancel.Token);
                }
                catch (OperationCanceledException) when (dialog.useCode && !dialog.cancel.IsCancellationRequested)
                {
                    await AccountManager.Default.SignInAsync(prompt => Dispatcher.UIThread.Post(() => dialog.ShowPrompt(prompt)), dialog.cancel.Token);
                }
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

        private void ShowBrowserPrompt(BrowserSignInPrompt prompt)
        {
            verificationUri = prompt.AuthorizeUrl;
            Instructions.Text = LanguageManager.GetResource("Linux_Account_BrowserText");
            OpenBrowserButton.IsEnabled = UseCodeButton.IsEnabled = true;
            Platform.OpenUrl(prompt.AuthorizeUrl);
        }

        private void ShowPrompt(DeviceCodePrompt prompt)
        {
            verificationUri = prompt.VerificationUri;
            Instructions.Text = string.Format(LanguageManager.GetResource("Linux_Account_CodeText"), prompt.VerificationUri);
            CodeText.Text = prompt.UserCode;
            CodeText.IsVisible = CopyButton.IsVisible = true;
            UseCodeButton.IsVisible = false;
            OpenBrowserButton.IsEnabled = true;
        }

        private void UseCode_Click(object sender, RoutedEventArgs e)
        {
            useCode = true;
            OpenBrowserButton.IsEnabled = UseCodeButton.IsEnabled = false;
            browserCancel?.Cancel();
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
