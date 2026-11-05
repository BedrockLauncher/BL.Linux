using System;
using System.ComponentModel;
using BedrockLauncher.Enums;
using BedrockLauncher.Localization.Language;
using PropertyChanged;

namespace BedrockLauncher.ViewModels
{
    public class ProgressBarModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ProgressBarModel()
        {
            LanguageManager.LanguageChanged += _ => PlayButtonLanguageChanged = !PlayButtonLanguageChanged;
        }

        protected void OnPropertyChanged(string propertyName) =>
            UiThread.Run(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));

        #region Various Properties

        public bool AllowCancel { get; set; }
        public bool IsGameRunning { get; set; }
        public bool PlayButtonLanguageChanged { get; set; }

        [DependsOn(nameof(IsGameRunning), nameof(PlayButtonLanguageChanged))]
        public string PlayButtonString => LanguageManager.GetResource(IsGameRunning ? "GameTab_PlayButton_Kill_Text" : "InstallationsPage_PlayButton");

        [DependsOn(nameof(IsGameRunning), nameof(PlayButtonLanguageChanged))]
        public string PlayEditorButtonString => LanguageManager.GetResource(IsGameRunning ? "GameTab_PlayButton_Kill_Text" : "CreatorToolsPage_PlayEditorButton");

        [DependsOn(nameof(AllowPlaying), nameof(IsGameRunning), nameof(Show))]
        public bool AllowEditing => AllowPlaying && !IsGameRunning && !Show;

        [DependsOn(nameof(CurrentState), nameof(Show))]
        public bool AllowPlaying => CurrentState == LauncherState.None && !Show;

        #endregion

        #region Common Properties

        /// <summary>Progress bar shown (it slides in/out at the bottom of the window).</summary>
        public bool Show { get; set; }

        public LauncherState CurrentState { get; set; }

        /// <summary>0..100.</summary>
        public long CurrentProgress { get; set; }
        public long TotalProgress => 100;
        public long ActualCurrentProgress { get; set; }
        public long ActualTotalProgress { get; set; }
        public bool IsIndeterminate { get; set; } = true;

        #endregion

        #region Text

        [DependsOn(nameof(CurrentState), nameof(PlayButtonLanguageChanged))]
        public string Description
        {
            get
            {
                string key = CurrentState switch
                {
                    LauncherState.isInitializing => "ProgressBar_Downloading",
                    LauncherState.isDownloading => "ProgressBar_Downloading",
                    LauncherState.isExtracting => "ProgressBar_Extracting",
                    LauncherState.isRegisteringPackage => "ProgressBar_RegisteringPackage",
                    LauncherState.isRemovingPackage => "ProgressBar_RemovingPackage",
                    LauncherState.isUninstalling => "ProgressBar_Uninstalling",
                    LauncherState.isLaunching => "ProgressBar_Launching",
                    LauncherState.isBackingUp => "ProgressBar_BackingUp",
                    _ => null
                };
                return key == null ? string.Empty : LanguageManager.GetResource(key);
            }
        }

        [DependsOn(nameof(CurrentState), nameof(CurrentProgress), nameof(ActualCurrentProgress), nameof(ActualTotalProgress))]
        public string TextualProgress
        {
            get
            {
                switch (CurrentState)
                {
                    case LauncherState.isDownloading:
                        string current = Math.Round((double)ActualCurrentProgress / 1024 / 1024, 2).ToString("0.00");
                        string total = Math.Round((double)ActualTotalProgress / 1024 / 1024, 2).ToString("0.00");
                        return $"{current} MB / {total} MB";
                    case LauncherState.isRemovingPackage:
                    case LauncherState.isRegisteringPackage:
                    case LauncherState.isExtracting:
                        return $"{CurrentProgress}%";
                    case LauncherState.isBackingUp:
                    case LauncherState.isUninstalling:
                        return $"{CurrentProgress} / {TotalProgress}";
                    default:
                        return string.Empty;
                }
            }
        }

        public string Information { get; set; }

        [DependsOn(nameof(Information))]
        public bool ShowInformation => !string.IsNullOrEmpty(Information);

        [DependsOn(nameof(TextualProgress))]
        public bool ShowTextualProgress => !string.IsNullOrEmpty(TextualProgress);

        #endregion

        #region Public Methods

        public void SetProgressBarVisibility(bool show) => Show = show;

        public void ResetProgressBarProgress()
        {
            CurrentProgress = 0;
            ActualCurrentProgress = 0;
            ActualTotalProgress = 0;
            IsIndeterminate = true;
        }

        public void SetProgressBarProgress(long currentProgress, long totalProgress)
        {
            int percent = 0;
            if (totalProgress > 0 && currentProgress >= 0)
                percent = (int)Math.Round(100.0 * currentProgress / totalProgress);

            CurrentProgress = Math.Clamp(percent, 0, 100);
            ActualCurrentProgress = currentProgress;
            ActualTotalProgress = totalProgress;
            IsIndeterminate = false;
        }

        public void SetGameRunningStatus(bool isRunning) => IsGameRunning = isRunning;

        public void SetProgressBarText(string text = null) => Information = text;

        public void SetProgressBarState(LauncherState? state = null) => CurrentState = state ?? LauncherState.None;

        #endregion
    }
}
