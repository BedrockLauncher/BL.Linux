using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BedrockLauncher.Runtime
{
    /// <summary>
    /// Creates and maintains the wine prefixes. Mirrors wine-uwp's pfx.sh: wineboot, then the UWP-aware DXVK build is
    /// copied into system32 with native overrides (the stock DXVK does not handle CoreWindow swapchains, so it must
    /// not replace it). Proton runtimes create and update their prefix themselves on every run.
    /// </summary>
    public sealed class WinePrefixManager
    {
        /// <summary>DXVK (D3D8-11) and vkd3d-proton (D3D12) libraries copied into the prefix when the runtime ships them.</summary>
        public static readonly string[] DxvkLibraries = { "d3d8", "d3d9", "d3d10core", "d3d11", "dxgi", "d3d12", "d3d12core" };

        private readonly IProcessRunner runner;
        private readonly Action<string> log;

        public WinePrefixManager(IProcessRunner runner = null, Action<string> log = null)
        {
            this.runner = runner ?? ProcessRunner.Default;
            this.log = log ?? (_ => { });
        }

        /// <summary>The prefix folder for a runtime kind under the launcher's prefixes folder.</summary>
        public static string GetPrefixPath(string prefixesFolder, GameRuntime runtime) =>
            Path.Combine(prefixesFolder, runtime.Kind.ToString().ToLowerInvariant());

        /// <summary>The wine prefix proper (Proton keeps it in &lt;compat data&gt;/pfx).</summary>
        public static string GetWinePrefix(string prefixPath, GameRuntime runtime) =>
            runtime.UsesProtonScript ? Path.Combine(prefixPath, "pfx") : prefixPath;

        public static bool IsInitialized(string winePrefix) => File.Exists(Path.Combine(winePrefix, "system.reg"));

        public async Task EnsureAsync(GameRuntime runtime, string prefixPath, bool useDxvk, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(prefixPath);
            if (runtime.UsesProtonScript) return;

            if (!IsInitialized(prefixPath))
            {
                log($"Creating wine prefix {prefixPath} with {runtime.Name}");
                int code = await runner.RunAsync(WineCommand(runtime, prefixPath, "wineboot", "-u"), log, cancellationToken);
                // wineboot returns before wineserver has flushed the registry to disk.
                await WaitForWineServerAsync(runtime, prefixPath, cancellationToken);
                if (code != 0 || !IsInitialized(prefixPath))
                    throw new InvalidOperationException($"wineboot failed (exit code {code}) for prefix {prefixPath}.");
            }

            // wine-uwp ships dxvk-uwp (CoreWindow swapchains); runtime/winegdk/build.sh adds DXVK + vkd3d-proton.
            if (useDxvk)
                await InstallDxvkAsync(runtime, prefixPath, cancellationToken);
        }

        /// <summary>Copies dxvk-uwp into the prefix and sets native overrides; idempotent.</summary>
        public async Task InstallDxvkAsync(GameRuntime runtime, string prefixPath, CancellationToken cancellationToken = default)
        {
            var targets = new List<(string Source, string System)>
            {
                (Path.Combine(runtime.DxvkFolder, "x64", "bin"), Path.Combine(prefixPath, "drive_c", "windows", "system32")),
                (Path.Combine(runtime.DxvkFolder, "x32", "bin"), Path.Combine(prefixPath, "drive_c", "windows", "syswow64")),
            };

            var installed = new List<string>();
            foreach (var (source, system) in targets)
            {
                if (!Directory.Exists(source) || !Directory.Exists(system)) continue;
                foreach (string library in DxvkLibraries)
                {
                    string src = Path.Combine(source, library + ".dll");
                    if (!File.Exists(src)) continue;
                    string dst = Path.Combine(system, library + ".dll");
                    if (File.Exists(dst) && FilesEqual(src, dst)) { installed.Add(library); continue; }
                    if (File.Exists(dst) && !File.Exists(dst + ".old")) File.Move(dst, dst + ".old");
                    File.Copy(src, dst, overwrite: true);
                    installed.Add(library);
                }
            }

            if (installed.Count == 0)
            {
                log("This runtime ships no DXVK; the game will use wined3d (OpenGL).");
                return;
            }

            string marker = Path.Combine(prefixPath, ".bl-dxvk");
            string stamp = string.Join(",", installed.Distinct().OrderBy(x => x));
            if (File.Exists(marker) && File.ReadAllText(marker) == stamp) return;

            foreach (string library in installed.Distinct())
            {
                await runner.RunAsync(WineCommand(runtime, prefixPath, "reg", "add", @"HKEY_CURRENT_USER\Software\Wine\DllOverrides",
                    "/v", library, "/d", "native", "/f"), log, cancellationToken);
            }
            File.WriteAllText(marker, stamp);
            log($"dxvk-uwp installed in {prefixPath}: {stamp}");
        }

        /// <summary>Waits until every process of the prefix has exited and the registry is saved (wineserver -w).</summary>
        public async Task WaitForWineServerAsync(GameRuntime runtime, string prefixPath, CancellationToken cancellationToken = default)
        {
            string wineserver = runtime.WineServerBinary;
            if (wineserver == null) return;
            var command = new LaunchCommand { FileName = wineserver };
            command.Arguments.Add("-w");
            command.Environment["WINEPREFIX"] = GetWinePrefix(prefixPath, runtime);
            await runner.RunAsync(command, log, cancellationToken);
        }

        /// <summary>Stops every process of the prefix (wineserver -k).</summary>
        public async Task KillAsync(GameRuntime runtime, string prefixPath)
        {
            string wineserver = runtime.WineServerBinary;
            if (wineserver == null) return;
            var command = new LaunchCommand { FileName = wineserver };
            command.Arguments.Add("-k");
            command.Environment["WINEPREFIX"] = GetWinePrefix(prefixPath, runtime);
            await runner.RunAsync(command, log);
        }

        public static LaunchCommand WineCommand(GameRuntime runtime, string prefixPath, params string[] arguments)
        {
            var command = new LaunchCommand { FileName = runtime.WineBinary ?? throw new InvalidOperationException($"{runtime} has no wine binary.") };
            command.Arguments.AddRange(arguments);
            command.Environment["WINEPREFIX"] = prefixPath;
            command.Environment["WINEDEBUG"] = "-all";
            command.Environment["WINEDLLOVERRIDES"] = "mscoree,mshtml=";
            return command;
        }

        private static bool FilesEqual(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }
    }
}
