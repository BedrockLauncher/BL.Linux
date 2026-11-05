using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Common
{
    public partial class ErrorScreen : UserControl
    {
        private readonly TaskCompletionSource<bool> closed = new();

        public ErrorScreen()
        {
            InitializeComponent();
        }

        public static Task ShowException(Exception ex) => Show(null, ex.GetType().Name.Replace("Exception", ""), ex.Message, ex.ToString());

        public static Task ShowMessage(string title, string message) => Show(title, null, message, null);

        private static async Task Show(string title, string subtitle, string text, string details)
        {
            var screen = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var s = new ErrorScreen();
                if (title != null) s.ErrorTitle.Text = title;
                if (subtitle != null) s.ErrorType.Text = subtitle;
                s.ErrorText.Text = text;
                s.ErrorStackTrace.Text = details;
                s.ErrorScreenViewCrashButton.IsVisible = !string.IsNullOrEmpty(details);
                MainViewModel.Default.SetDialogFrame(s);
                return s;
            });
            await screen.closed.Task;
        }

        private void ErrorScreenCloseButton_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel.Default.SetDialogFrame(null);
            closed.TrySetResult(true);
        }

        private void ErrorScreenViewCrashButton_Click(object sender, RoutedEventArgs e) => ErrorStackTrace.IsVisible = !ErrorStackTrace.IsVisible;
    }
}
