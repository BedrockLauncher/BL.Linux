using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BedrockLauncher.Runtime
{
    /// <summary>A fully resolved command line: what is started, with which environment, from where.</summary>
    public sealed class LaunchCommand
    {
        public string FileName { get; set; }
        public List<string> Arguments { get; } = new List<string>();
        public Dictionary<string, string> Environment { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public string WorkingDirectory { get; set; }

        public ProcessStartInfo ToStartInfo(bool redirectOutput = true)
        {
            var info = new ProcessStartInfo(FileName)
            {
                UseShellExecute = false,
                WorkingDirectory = WorkingDirectory ?? string.Empty,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
            };
            foreach (string argument in Arguments) info.ArgumentList.Add(argument);
            foreach (var pair in Environment)
            {
                if (pair.Value == null) info.Environment.Remove(pair.Key);
                else info.Environment[pair.Key] = pair.Value;
            }
            return info;
        }

        /// <summary>Shell-style rendering for logs and the "copy launch command" debug action.</summary>
        public override string ToString()
        {
            static string Quote(string s) => s.Length > 0 && s.All(c => char.IsLetterOrDigit(c) || "/._-=:,+@%".Contains(c)) ? s : "'" + s.Replace("'", "'\\''") + "'";
            var env = Environment.Where(x => x.Value != null).Select(x => $"{x.Key}={Quote(x.Value)}");
            var cd = string.IsNullOrEmpty(WorkingDirectory) ? "" : $"cd {Quote(WorkingDirectory)} && ";
            return cd + string.Join(" ", env.Concat(new[] { Quote(FileName) }).Concat(Arguments.Select(Quote)));
        }
    }

    public sealed class LaunchRequest
    {
        public GameRuntime Runtime { get; set; }

        /// <summary>WINEPREFIX for wine runtimes; STEAM_COMPAT_DATA_PATH (the folder holding pfx/) for Proton.</summary>
        public string PrefixPath { get; set; }

        public string GameDirectory { get; set; }
        public string Executable { get; set; } = Constants.MINECRAFT_EXECUTABLE;
        public List<string> GameArguments { get; } = new List<string>();

        /// <summary>UWP only: the installation's package data folder (LocalState/RoamingState/TempState live inside).</summary>
        public string PackageDataPath { get; set; }

        public bool UseDxvk { get; set; } = true;
        public bool UseGameMode { get; set; }
        public bool UseMangoHud { get; set; }
        public string WineDebug { get; set; }
        public string ExtraEnvironment { get; set; }

        /// <summary>Steam install root handed to Proton; any existing folder works when Steam is not installed.</summary>
        public string SteamClientPath { get; set; }

        /// <summary>Looks a program up in PATH (injected for tests).</summary>
        public Func<string, string> FindInPath { get; set; } = LaunchCommandBuilder.FindInPath;
    }

    public static class LaunchCommandBuilder
    {
        public static LaunchCommand Build(LaunchRequest request)
        {
            if (request?.Runtime == null) throw new ArgumentException("A runtime is required.", nameof(request));
            if (string.IsNullOrEmpty(request.GameDirectory)) throw new ArgumentException("The game directory is required.", nameof(request));
            if (string.IsNullOrEmpty(request.PrefixPath)) throw new ArgumentException("The prefix path is required.", nameof(request));

            GameRuntime runtime = request.Runtime;
            string executable = Path.Combine(request.GameDirectory, request.Executable);
            var command = new LaunchCommand { WorkingDirectory = request.GameDirectory };
            var overrides = new List<string>();

            if (runtime.UsesProtonScript)
            {
                command.FileName = runtime.ProtonScript;
                command.Arguments.Add("waitforexitandrun");
                command.Arguments.Add(executable);
                command.Environment["STEAM_COMPAT_DATA_PATH"] = request.PrefixPath;
                command.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = request.SteamClientPath ?? request.PrefixPath;
                command.Environment["STEAM_COMPAT_INSTALL_PATH"] = request.GameDirectory;
                // Proton keys its game fixes on the app id; 0 means "no Steam game" (same as umu-launcher's default).
                command.Environment["SteamAppId"] = "0";
                command.Environment["SteamGameId"] = "0";
                if (!request.UseDxvk) command.Environment["PROTON_USE_WINED3D"] = "1";
            }
            else
            {
                command.FileName = runtime.WineBinary ?? throw new InvalidOperationException($"{runtime} has no wine binary.");
                command.Arguments.Add(executable);
                command.Environment["WINEPREFIX"] = request.PrefixPath;
                // Never let the prefix download Mono/Gecko on its own.
                overrides.Add("mscoree,mshtml=");
                if (!request.UseDxvk) overrides.Add("d3d9,d3d10core,d3d11,dxgi=b");
            }

            if (runtime.Kind == GameRuntimeKind.WineUwp)
            {
                if (!string.IsNullOrEmpty(request.PackageDataPath))
                    command.Environment["WINEUWP_PACKAGE_DATA"] = WinePath.ToWindows(request.PackageDataPath);
            }
            else
            {
                // GDK builds ship GameInputRedist; wine's builtin GameInput must win over the redistributable.
                overrides.Add("GameInputRedist=b");
            }

            command.Environment["WINEDEBUG"] = string.IsNullOrWhiteSpace(request.WineDebug) ? "-all" : request.WineDebug.Trim();
            if (overrides.Count > 0) command.Environment["WINEDLLOVERRIDES"] = string.Join(";", overrides);
            if (request.UseMangoHud) command.Environment["MANGOHUD"] = "1";

            foreach (var pair in ParseEnvironment(request.ExtraEnvironment))
            {
                if (pair.Key == "WINEDLLOVERRIDES" && command.Environment.TryGetValue(pair.Key, out string existing))
                    command.Environment[pair.Key] = existing + ";" + pair.Value;
                else
                    command.Environment[pair.Key] = pair.Value;
            }

            if (request.UseGameMode)
            {
                string gamemoderun = request.FindInPath?.Invoke("gamemoderun");
                if (gamemoderun != null)
                {
                    command.Arguments.Insert(0, command.FileName);
                    command.FileName = gamemoderun;
                }
            }

            return command;
        }

        /// <summary>Parses NAME=value lines; blank lines and # comments are skipped.</summary>
        public static IEnumerable<KeyValuePair<string, string>> ParseEnvironment(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                yield return new KeyValuePair<string, string>(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
            }
        }

        public static string FindInPath(string program)
        {
            string path = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(dir, program);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
