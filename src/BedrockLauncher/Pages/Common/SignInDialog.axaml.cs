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
    /// Microsoft sign-in. Default: device code (the code is copied and microsoft.com/link opens; works with any
    /// redirect URI setup, only "Allow public client flows" is needed). "Sign in with the browser" switches to the
    /// authorization code flow with the http://localhost redirect, which comes back to the launcher by itself.
    /// </summary>
    public partial class SignInDialog : UserControl
    {
        private readonly CancellationTokenSource cancel = new();
        private CancellationTokenSource codeCancel;
        private bool useBrowser;
        private bool browserOpened;
        private string verificationUri;

        public SignInDialog() => InitializeComponent();

        /// <returns>true when an account is signed in afterwards.</returns>
        public static async Task<bool> ShowAsync()
        {
            var dialog = new SignInDialog();
            MainViewModel.Default.SetDialogFrame(dialog);
            try
            {
                dialog.codeCancel = CancellationTokenSource.CreateLinkedTokenSource(dialog.cancel.Token);
                try
                {
                    await AccountManager.Default.SignInAsync(prompt => Dispatcher.UIThread.Post(() => dialog.ShowPrompt(prompt)), dialog.codeCancel.Token);
                }
                catch (OperationCanceledException) when (dialog.useBrowser && !dialog.cancel.IsCancellationRequested)
                {
                    await AccountManager.Default.SignInWithBrowserAsync(prompt => Dispatcher.UIThread.Post(() => dialog.ShowBrowserPrompt(prompt)), dialog.cancel.Token);
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
                System.Diagnostics.Trace.WriteLine($"Sign-in failed: {ex}");
                MainViewModel.Default.SetDialogFrame(null);
                await ErrorScreen.ShowMessage(LanguageManager.GetResource("Linux_Account_CodeTitle"), ex.Message);
                return false;
            }
        }

        private async void ShowPrompt(DeviceCodePrompt prompt)
        {
            verificationUri = prompt.VerificationUri;
            Instructions.Text = string.Format(LanguageManager.GetResource("Linux_Account_CodeText"), prompt.VerificationUri);
            CodeText.Text = prompt.UserCode;
            CodeText.IsVisible = CopyButton.IsVisible = true;
            OpenBrowserButton.IsEnabled = UseBrowserButton.IsEnabled = true;
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null) await clipboard.SetTextAsync(prompt.UserCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Could not copy the sign-in code: {ex.Message}");
            }
            if (!browserOpened)
            {
                browserOpened = true;
                Platform.OpenUrl(prompt.VerificationUri);
            }
        }

        private void ShowBrowserPrompt(BrowserSignInPrompt prompt)
        {
            verificationUri = prompt.AuthorizeUrl;
            Instructions.Text = LanguageManager.GetResource("Linux_Account_BrowserText");
            CodeText.IsVisible = CopyButton.IsVisible = UseBrowserButton.IsVisible = false;
            OpenBrowserButton.IsEnabled = true;
            Platform.OpenUrl(prompt.AuthorizeUrl);
        }

        private void UseBrowser_Click(object sender, RoutedEventArgs e)
        {
            useBrowser = true;
            OpenBrowserButton.IsEnabled = UseBrowserButton.IsEnabled = false;
            codeCancel?.Cancel();
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
