using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BedrockLauncher.Runtime
{
    /// <summary>
    /// Finds the compatibility runtimes: the ones the launcher installed (runtimes/), Steam compatibility tools
    /// (where GDK-Proton is normally extracted), the paths set in the settings and the WINEUWP_DIR / WINEGDK_DIR
    /// environment variables.
    /// </summary>
    public sealed class RuntimeLocator
    {
        private readonly string launcherRuntimesFolder;
        private readonly string home;

        public RuntimeLocator(string launcherRuntimesFolder, string home = null)
        {
            this.launcherRuntimesFolder = launcherRuntimesFolder;
            this.home = home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public IEnumerable<string> SteamCompatibilityToolFolders => new[]
        {
            Path.Combine(home, ".steam", "root", "compatibilitytools.d"),
            Path.Combine(home, ".steam", "steam", "compatibilitytools.d"),
            Path.Combine(home, ".local", "share", "Steam", "compatibilitytools.d"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam", "compatibilitytools.d"),
            "/usr/share/steam/compatibilitytools.d",
        };

        public List<GameRuntime> FindAll()
        {
            var found = new List<GameRuntime>();

            void Add(GameRuntime runtime)
            {
                if (runtime == null || !runtime.IsUsable) return;
                string root = Path.GetFullPath(runtime.RootPath);
                if (found.Any(x => x.Kind == runtime.Kind && Path.GetFullPath(x.RootPath) == root)) return;
                found.Add(runtime);
            }

            // Launcher-managed runtimes
            if (!string.IsNullOrEmpty(launcherRuntimesFolder) && Directory.Exists(launcherRuntimesFolder))
            {
                foreach (string dir in Directory.EnumerateDirectories(launcherRuntimesFolder))
                    Add(Classify(dir, "launcher"));
            }

            // Steam compatibility tools (GDK-Proton releases are extracted there)
            foreach (string folder in SteamCompatibilityToolFolders.Where(Directory.Exists))
            {
                foreach (string dir in Directory.EnumerateDirectories(folder))
                {
                    if (Path.GetFileName(dir).Contains("GDK", StringComparison.OrdinalIgnoreCase))
                        Add(Classify(dir, "steam"));
                }
            }

            Add(FromEnvironment("WINEUWP_DIR", GameRuntimeKind.WineUwp));
            Add(FromEnvironment("WINEGDK_DIR", GameRuntimeKind.WineGdk));

            return found;
        }

        /// <summary>Resolves the runtime for a kind: the configured path when set, else the first one found.</summary>
        public GameRuntime Resolve(GameRuntimeKind kind, string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var configured = new GameRuntime
                {
                    Kind = kind,
                    Name = Path.GetFileName(configuredPath.TrimEnd('/')),
                    RootPath = configuredPath,
                    Origin = "settings"
                };
                return configured.IsUsable ? configured : null;
            }

            return FindAll().FirstOrDefault(x => x.Kind == kind);
        }

        /// <summary>Tells a runtime folder's kind from its contents.</summary>
        public static GameRuntime Classify(string dir, string origin)
        {
            string name = Path.GetFileName(dir.TrimEnd('/'));

            if (File.Exists(Path.Combine(dir, "proton")))
            {
                return new GameRuntime { Kind = GameRuntimeKind.GdkProton, Name = name, RootPath = dir, Origin = origin };
            }

            string descriptor = Path.Combine(dir, "bl-runtime.json");
            if (File.Exists(descriptor))
            {
                string text = File.ReadAllText(descriptor);
                GameRuntimeKind kind = text.Contains("\"WineGdk\"") ? GameRuntimeKind.WineGdk : GameRuntimeKind.WineUwp;
                return new GameRuntime { Kind = kind, Name = name, RootPath = dir, Origin = origin };
            }

            if (File.Exists(Path.Combine(dir, "bin", "wine")))
            {
                GameRuntimeKind kind = name.Contains("gdk", StringComparison.OrdinalIgnoreCase) ? GameRuntimeKind.WineGdk : GameRuntimeKind.WineUwp;
                return new GameRuntime { Kind = kind, Name = name, RootPath = dir, Origin = origin };
            }

            return null;
        }

        private static GameRuntime FromEnvironment(string variable, GameRuntimeKind kind)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value)) return null;
            return new GameRuntime { Kind = kind, Name = Path.GetFileName(value.TrimEnd('/')), RootPath = value, Origin = variable };
        }
    }
}
