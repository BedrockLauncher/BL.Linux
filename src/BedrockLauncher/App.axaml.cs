using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            UiThread.CheckAccess = () => Dispatcher.UIThread.CheckAccess();
            UiThread.Post = action => Dispatcher.UIThread.Post(action);
            UiThread.InvokeAsync = func => Dispatcher.UIThread.InvokeAsync(func);

            // Upstream merges a LanguageDictionary into Application.Resources and binds with DynamicResource;
            // here the active table is copied into the resources, so {DynamicResource Key} works the same way.
            LanguageManager.LanguageChanged += ApplyStrings;

            Properties.LauncherSettings.Load();
            LanguageManager.Init(Properties.LauncherSettings.Default.Language);
            MainDataModel.Default.UiHost = MainViewModel.Default;
        }

        private void ApplyStrings(IReadOnlyDictionary<string, string> strings) => UiThread.Run(() =>
        {
            foreach (var pair in strings) Resources[pair.Key] = pair.Value;
        });

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
