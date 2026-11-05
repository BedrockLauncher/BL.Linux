using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using BedrockLauncher;
using BedrockLauncher.Pages;
using BedrockLauncher.Pages.Common;
using BedrockLauncher.Pages.Preview;
using BedrockLauncher.Pages.Welcome;
using BedrockLauncher.ViewModels;

public static class Screenshots
{
    public static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 0 ? args[0] : "screenshots");
        Directory.CreateDirectory(output);
        string home = Path.Combine(Path.GetTempPath(), "bl-linux-screens-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BL_LINUX_HOME", home);
        BedrockLauncher.Handlers.PathHandler.RootOverride = home;
        if (args.Length > 1) BedrockLauncher.Localization.Language.LanguageManager.Init(args[1]);

        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var window = new MainWindow();
        window.Show();
        Pump(TimeSpan.FromSeconds(1));

        void Shot(string name, double seconds = 1.2)
        {
            Pump(TimeSpan.FromSeconds(seconds));
            var frame = window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(output, name + ".png"));
            Console.WriteLine($"{name}.png");
        }

        // Wait for the version list / first launch flow
        for (int i = 0; i < 120 && MainViewModel.Default.ErrorFrame_Content is not null; i++) Pump(TimeSpan.FromMilliseconds(500));
        Shot("01-welcome");

        // Create a profile like a user would, then close the welcome overlay
        MainDataModel.Default.Config.Profile_Add("Steve", Guid.NewGuid().ToString(), "Steve", null);
        Properties_Done();
        MainViewModel.Default.SetOverlayFrame(null);
        var main = window.FindControl<MainPage>("MainPage");
        main.NavigateToGamePage();
        Shot("02-play", 2.5);

        var tabs = (BedrockLauncher.Pages.Play.GameTabs)main.FindControl<ContentControl>("MainWindowFrame").Content;
        tabs.Navigate(tabs.FindControl<Avalonia.Controls.Primitives.ToggleButton>("CreatorToolsButton"));
        Shot("03-creator-tools");
        tabs.NavigateToInstallations();
        Shot("04-installations");
        tabs.Navigate(tabs.FindControl<Avalonia.Controls.Primitives.ToggleButton>("PatchNotesButton"));
        Shot("05-patch-notes", 6);

        MainViewModel.Default.SetOverlayFrame(new EditInstallationScreen());
        Shot("06-edit-installation");
        MainViewModel.Default.SetOverlayFrame(null);

        main.NavigateToSettings();
        Shot("07-settings-general");
        var settings = (BedrockLauncher.Pages.Settings.SettingsTabs)main.FindControl<ContentControl>("MainWindowFrame").Content;
        settings.Navigate(settings.FindControl<Avalonia.Controls.Primitives.ToggleButton>("VersionsButton"));
        Shot("08-settings-versions");
        settings.NavigateToLinux();
        Shot("09-settings-linux");
        settings.NavigateToAccounts();
        Shot("09b-settings-accounts");
        settings.Navigate(settings.FindControl<Avalonia.Controls.Primitives.ToggleButton>("AboutButton"));
        Shot("10-settings-about");

        main.NavigateToNewsPage();
        Shot("11-news", 6);

        _ = DialogPrompt.ShowDialog_YesNo("Delete item", "Are you sure you want to delete installation \"{0}\"?", "Latest Release");
        Shot("12-dialog");
        MainViewModel.Default.SetDialogFrame(null);

        MainViewModel.Default.SetOverlayFrame(new EditProfileScreen());
        Shot("13-add-profile");
        MainViewModel.Default.SetOverlayFrame(null);

        MainDataModel.Default.ProgressBarState.SetProgressBarVisibility(true);
        MainDataModel.Default.ProgressBarState.SetProgressBarState(BedrockLauncher.Enums.LauncherState.isDownloading);
        MainDataModel.Default.ProgressBarState.SetProgressBarProgress(412_000_000, 1_020_000_000);
        MainDataModel.Default.ProgressBarState.SetProgressBarText("1.20.81.1 (Release) [UWP]");
        main.NavigateToGamePage();
        Shot("14-downloading");
        return 0;
    }

    private static void Properties_Done()
    {
        BedrockLauncher.Properties.LauncherSettings.Default.IsFirstLaunch = false;
        BedrockLauncher.Properties.LauncherSettings.Default.Save();
    }

    private static void Pump(TimeSpan time)
    {
        var end = DateTime.UtcNow + time;
        while (DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            System.Threading.Thread.Sleep(15);
        }
    }
}
