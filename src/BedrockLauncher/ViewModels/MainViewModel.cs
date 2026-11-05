using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.Pages.Common;
using BedrockLauncher.Pages.Preview;

namespace BedrockLauncher.ViewModels
{
    /// <summary>
    /// Port of upstream's UI MainViewModel: owns the overlay frame (editors, welcome) and the dialog frame (errors,
    /// prompts, waiting screen) that sit on top of the main page, and is the core's <see cref="IUiHost"/>.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged, IUiHost
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static MainViewModel Default { get; } = new MainViewModel();

        #region Frames

        private object errorFrameContent;
        private object overlayFrameContent;

        public object ErrorFrame_Content
        {
            get => errorFrameContent;
            set
            {
                errorFrameContent = value;
                OnPropertyChanged(nameof(ErrorFrame_Content));
                OnPropertyChanged(nameof(MainFrame_isEnabled));
                OnPropertyChanged(nameof(OverlayFrame_isEnabled));
            }
        }

        public object OverlayFrame_Content
        {
            get => overlayFrameContent;
            set
            {
                overlayFrameContent = value;
                OnPropertyChanged(nameof(OverlayFrame_Content));
                OnPropertyChanged(nameof(MainFrame_isEnabled));
            }
        }

        public bool MainFrame_isEnabled => ErrorFrame_Content == null && OverlayFrame_Content == null;
        public bool OverlayFrame_isEnabled => ErrorFrame_Content == null;

        public void SetOverlayFrame(object content, bool isStrict = false) => Dispatcher.UIThread.Post(() => OverlayFrame_Content = content);

        public void SetDialogFrame(object content) => Dispatcher.UIThread.Post(() => ErrorFrame_Content = content);

        public async Task ShowWaitingDialog(Func<Task> action)
        {
            SetDialogFrame(new WaitingPage());
            try
            {
                await action();
            }
            finally
            {
                SetDialogFrame(null);
            }
        }

        #endregion

        #region Window

        public Window MainWindow =>
            (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        public bool AllowedToClose()
        {
            bool doNotClose = Properties.LauncherSettings.Default.KeepLauncherOpen &&
                              MainDataModel.Default.PackageManager.isGameRunning &&
                              !MainDataModel.Default.AllowedToCloseWithGameOpen;
            return !doNotClose;
        }

        /// <summary>Asks what to do with the running game before the launcher closes (upstream LauncherCanNotCloseDialog).</summary>
        public async void LauncherCanNotCloseDialog(Action successAction)
        {
            var result = await DialogPrompt.ShowDialog_YesNoCancel(
                LanguageManager.GetResource("Dialog_CloseGame_Title"),
                LanguageManager.GetResource("Dialog_CloseGame_Text"));

            if (result == DialogResult.Yes)
            {
                await MainDataModel.Default.PackageManager.ClosePackage();
                MainDataModel.Default.AllowedToCloseWithGameOpen = true;
                successAction?.Invoke();
            }
            else if (result == DialogResult.No)
            {
                MainDataModel.Default.AllowedToCloseWithGameOpen = true;
                successAction?.Invoke();
            }
            else
            {
                MainDataModel.Default.AllowedToCloseWithGameOpen = false;
            }
        }

        #endregion

        #region IUiHost

        public Task ShowException(Exception ex) => ErrorScreen.ShowException(ex);

        public async Task<bool> Confirm(string titleResource, string messageResource, params object[] args)
        {
            var result = await DialogPrompt.ShowDialog_YesNo(LanguageManager.GetResource(titleResource), LanguageManager.GetResource(messageResource), args);
            return result == DialogResult.Yes;
        }

        public Task ShowMessage(string title, string message) => ErrorScreen.ShowMessage(title, message);

        public async Task<string> PickFolder(string title)
        {
            var window = MainWindow;
            if (window == null) return null;
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
                return folders.FirstOrDefault()?.TryGetLocalPath();
            });
        }

        public async Task<string> PickFile(string title, params (string Name, string[] Patterns)[] filters)
        {
            var window = MainWindow;
            if (window == null) return null;
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = filters.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Patterns }).ToList()
            });
            return files.FirstOrDefault()?.TryGetLocalPath();
        }

        public void CloseLauncher() => Dispatcher.UIThread.Post(() => MainWindow?.Close());

        #endregion
    }
}
