using System;
using System.IO;

namespace BedrockLauncher.Runtime
{
    public static class WinePath
    {
        /// <summary>Unix path as seen from Wine through the default Z: drive (/home/a/b -> Z:\home\a\b).</summary>
        public static string ToWindows(string unixPath)
        {
            if (string.IsNullOrEmpty(unixPath)) throw new ArgumentException("Path required.", nameof(unixPath));
            string full = Path.GetFullPath(unixPath);
            return "Z:" + full.Replace('/', '\\');
        }

        /// <summary>The Windows user profile folder inside a prefix (drive_c/users/&lt;user&gt;).</summary>
        public static string GetPrefixUserFolder(string prefix, bool proton)
        {
            // Proton always names the user steamuser; plain wine uses $USER.
            string user = proton ? "steamuser" : (Environment.GetEnvironmentVariable("USER") ?? Environment.UserName);
            return Path.Combine(prefix, "drive_c", "users", user);
        }

        /// <summary>%APPDATA% (Roaming) inside a prefix.</summary>
        public static string GetPrefixRoamingAppData(string prefix, bool proton) =>
            Path.Combine(GetPrefixUserFolder(prefix, proton), "AppData", "Roaming");
    }
}
