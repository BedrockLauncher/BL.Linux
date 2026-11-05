using System;
using System.Diagnostics;

namespace BedrockLauncher
{
    /// <summary>Desktop integration (xdg-open instead of explorer.exe / ShellExecute).</summary>
    public static class Platform
    {
        public static void OpenFolder(string path) => Open(path);

        public static void OpenUrl(string url) => Open(url);

        public static void Open(string target)
        {
            try
            {
                var info = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                info.ArgumentList.Add(target);
                Process.Start(info)?.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Could not open {target}: {ex.Message}");
            }
        }
    }
}
