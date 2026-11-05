using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace BedrockLauncher.Runtime
{
    public enum GameRuntimeKind
    {
        /// <summary>Rosentti/wine-uwp built by runtime/wine-uwp/build.sh: runs UWP (pre-1.21.120) builds.</summary>
        WineUwp,

        /// <summary>Weather-OS/GDK-Proton (GE-Proton + WineGDK): runs GDK (1.21.120+) builds through its proton script.</summary>
        GdkProton,

        /// <summary>A plain WineGDK build (bin/wine); used directly like wine-uwp.</summary>
        WineGdk
    }

    /// <summary>A compatibility runtime found on disk.</summary>
    public sealed class GameRuntime
    {
        public GameRuntimeKind Kind { get; init; }
        public string Name { get; init; }

        /// <summary>Install root: the wine prefix-install folder (bin/wine) or the Proton folder (proton script).</summary>
        public string RootPath { get; init; }

        /// <summary>Where it was found (launcher folder, Steam compatibilitytools.d, settings, environment).</summary>
        public string Origin { get; init; }

        public string WineBinary => FirstExisting(
            Path.Combine(RootPath, "bin", "wine"),
            Path.Combine(RootPath, "files", "bin", "wine"),
            Path.Combine(RootPath, "bin", "wine64"),
            Path.Combine(RootPath, "files", "bin", "wine64"));

        public string WineServerBinary => FirstExisting(
            Path.Combine(RootPath, "bin", "wineserver"),
            Path.Combine(RootPath, "files", "bin", "wineserver"));

        public string ProtonScript => Path.Combine(RootPath, "proton");

        /// <summary>DXVK shipped with a wine-uwp build (dxvk-uwp), if it was built.</summary>
        public string DxvkFolder => Path.Combine(RootPath, "dxvk");

        public bool UsesProtonScript => Kind == GameRuntimeKind.GdkProton;

        public bool IsUsable => UsesProtonScript ? File.Exists(ProtonScript) : WineBinary != null;

        public string Version
        {
            get
            {
                try
                {
                    string descriptor = Path.Combine(RootPath, "bl-runtime.json");
                    if (File.Exists(descriptor))
                    {
                        var json = JObject.Parse(File.ReadAllText(descriptor));
                        string commit = (string)json["commit"];
                        return commit?.Length > 10 ? commit.Substring(0, 10) : commit;
                    }

                    string versionFile = Path.Combine(RootPath, "version");
                    if (File.Exists(versionFile))
                    {
                        // Proton's "version" file: "<timestamp> <name>"
                        string[] parts = File.ReadAllText(versionFile).Trim().Split(' ', 2);
                        return parts.Length == 2 ? parts[1] : parts[0];
                    }
                }
                catch (Exception)
                {
                    // descriptive only
                }
                return Name;
            }
        }

        public override string ToString() => $"{Name} ({Kind}, {RootPath})";

        private static string FirstExisting(params string[] paths)
        {
            foreach (string path in paths)
                if (File.Exists(path)) return path;
            return null;
        }
    }
}
