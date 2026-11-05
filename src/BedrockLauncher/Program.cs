using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            StartLogging();
            Trace.WriteLine($"Minecraft Bedrock Launcher for Linux {typeof(Program).Assembly.GetName().Version}");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();

        private static void StartLogging()
        {
            try
            {
                string logs = MainDataModel.Default.FilePaths.LogsFolder;
                Directory.CreateDirectory(logs);
                var writer = new StreamWriter(Path.Combine(logs, "launcher.log"), append: false) { AutoFlush = true };
                Trace.Listeners.Add(new TextWriterTraceListener(writer));
                Trace.Listeners.Add(new ConsoleTraceListener());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Logging disabled: {ex.Message}");
            }
        }
    }
}
