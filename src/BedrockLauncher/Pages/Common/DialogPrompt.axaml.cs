using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Common
{
    public enum DialogResult { None, Yes, No, Cancel }

    public partial class DialogPrompt : UserControl
    {
        private readonly TaskCompletionSource<DialogResult> result = new();

        public DialogPrompt()
        {
            InitializeComponent();
        }

        public static Task<DialogResult> ShowDialog_YesNo(string title, string content, params object[] args) =>
            Show(title, content, args, cancel: false, optional: null, optionalState: false).ContinueWith(t => t.Result.Item1);

        public static Task<DialogResult> ShowDialog_YesNoCancel(string title, string content) =>
            Show(title, content, Array.Empty<object>(), cancel: true, optional: null, optionalState: false).ContinueWith(t => t.Result.Item1);

        public static Task<Tuple<DialogResult, bool>> ShowDialog_YesNo_Optional(string title, string content, string optionalContent, bool optionalState, params object[] args) =>
            Show(title, content, args, cancel: false, optional: optionalContent, optionalState: optionalState);

        private static async Task<Tuple<DialogResult, bool>> Show(string title, string content, object[] args, bool cancel, string optional, bool optionalState)
        {
            var prompt = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var p = new DialogPrompt();
                p.DialogTitle.Text = Format(title, args);
                p.DialogText.Text = Format(content, args);
                p.CancelButton.IsVisible = cancel;
                if (optional != null)
                {
                    p.DialogOptional.Text = Format(optional, args);
                    p.DialogOptionalCheckbox.IsChecked = optionalState;
                    p.DialogOptionalCheckbox.IsVisible = true;
                }
                MainViewModel.Default.SetDialogFrame(p);
                return p;
            });

            DialogResult value = await prompt.result.Task;
            bool optionalChecked = await Dispatcher.UIThread.InvokeAsync(() => prompt.DialogOptionalCheckbox.IsChecked == true);
            return Tuple.Create(value, optionalChecked);
        }

        private static string Format(string text, object[] args)
        {
            if (args == null || args.Length == 0) return text;
            try { return string.Format(text, args); } catch (FormatException) { return text; }
        }

        private void Close(DialogResult value)
        {
            MainViewModel.Default.SetDialogFrame(null);
            result.TrySetResult(value);
        }

        private void YesButton_Click(object sender, RoutedEventArgs e) => Close(DialogResult.Yes);
        private void NoButton_Click(object sender, RoutedEventArgs e) => Close(DialogResult.No);
        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close(DialogResult.Cancel);
    }
}
