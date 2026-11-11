using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BedrockLauncher.Runtime
{
    /// <summary>One downloadable runtime in runtimes.json.</summary>
    public sealed class RuntimePackage
    {
        /// <summary>"wine-uwp", "winegdk", "gdk-proton" or "xodus".</summary>
        [JsonProperty("id")] public string Id { get; set; }
        /// <summary>"WineUwp", "WineGdk", "GdkProton" or "Xodus".</summary>
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("url")] public string Url { get; set; }
        [JsonProperty("sha256")] public string Sha256 { get; set; }
        [JsonProperty("size")] public long Size { get; set; }
        /// <summary>The single top-level folder of the archive; it is installed as runtimes/&lt;folder&gt;.</summary>
        [JsonProperty("folder")] public string Folder { get; set; }
        [JsonProperty("description")] public string Description { get; set; }

        public GameRuntimeKind? RuntimeKind => Enum.TryParse(Kind, out GameRuntimeKind kind) ? kind : null;
        public bool IsXodus => string.Equals(Kind, "Xodus", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class RuntimeManifest
    {
        [JsonProperty("format")] public int Format { get; set; } = 1;
        [JsonProperty("generated")] public string Generated { get; set; }
        [JsonProperty("runtimes")] public List<RuntimePackage> Runtimes { get; set; } = new();

        public RuntimePackage For(GameRuntimeKind kind) => Runtimes.FirstOrDefault(r => r.RuntimeKind == kind);
        public RuntimePackage Xodus => Runtimes.FirstOrDefault(r => r.IsXodus);
    }

    /// <summary>What runtimes/&lt;folder&gt;/.bl-installed.json records about an installed package.</summary>
    public sealed class InstalledRuntime
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("sha256")] public string Sha256 { get; set; }
        [JsonProperty("installed")] public DateTime Installed { get; set; }
    }

    public sealed class RuntimeInstallException : Exception
    {
        public RuntimeInstallException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// Downloads the prebuilt runtimes the launcher needs into its runtimes folder:
    ///   wine-uwp   Rosentti/wine-uwp + the BL.Linux patches (runtime/wine-uwp/build.sh) for UWP versions
    ///   winegdk    Weather-OS/WineGDK with Xodus interop + GE-Proton hotfixes (runtime/winegdk/build.sh) for GDK
    ///   gdk-proton Weather-OS/GDK-Proton, the upstream release, as the alternative GDK runtime
    ///   xodus      xodus-cli + xodus-service (runtime/xodus/build.sh) for GDK downloads, licenses and Xbox sign-in
    /// The list comes from runtimes.json on the BL.Linux "runtimes" release, which .github/workflows/runtimes.yml
    /// builds from the scripts in runtime/. Every archive is checked against its SHA-256 before it is unpacked.
    /// </summary>
    public sealed class RuntimeInstaller
    {
        public const string DefaultManifestUrl = "https://github.com/BedrockLauncher/BL.Linux/releases/download/runtimes/runtimes.json";
        public const string MarkerFile = ".bl-installed.json";

        private readonly HttpClient http;
        private readonly IProcessRunner runner;
        private readonly string runtimesFolder;
        private readonly string downloadsFolder;

        public RuntimeInstaller(string runtimesFolder, string downloadsFolder, HttpClient http = null, IProcessRunner runner = null)
        {
            this.runtimesFolder = runtimesFolder;
            this.downloadsFolder = downloadsFolder;
            this.http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            this.runner = runner ?? ProcessRunner.Default;
        }

        /// <summary>BL_RUNTIMES_MANIFEST overrides the manifest (forks, mirrors, tests); file paths work too.</summary>
        public static string ManifestUrl
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("BL_RUNTIMES_MANIFEST");
                return string.IsNullOrWhiteSpace(env) ? DefaultManifestUrl : env.Trim();
            }
        }

        public async Task<RuntimeManifest> GetManifestAsync(CancellationToken cancellationToken = default)
        {
            string url = ManifestUrl;
            try
            {
                string json = File.Exists(url) ? await File.ReadAllTextAsync(url, cancellationToken) : await http.GetStringAsync(url, cancellationToken);
                var manifest = JsonConvert.DeserializeObject<RuntimeManifest>(json) ?? new RuntimeManifest();
                manifest.Runtimes.RemoveAll(r => string.IsNullOrEmpty(r.Url) || string.IsNullOrEmpty(r.Folder) || string.IsNullOrEmpty(r.Sha256));
                return manifest;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is IOException || ex is InvalidOperationException
                                       || ex is UriFormatException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new RuntimeInstallException($"Could not read the runtime list ({url}): {ex.Message}", ex);
            }
        }

        public InstalledRuntime GetInstalled(RuntimePackage package)
        {
            string marker = Path.Combine(runtimesFolder, package.Folder, MarkerFile);
            try { return File.Exists(marker) ? JsonConvert.DeserializeObject<InstalledRuntime>(File.ReadAllText(marker)) : null; }
            catch (Exception ex) when (ex is IOException || ex is JsonException) { return null; }
        }

        public bool IsUpToDate(RuntimePackage package) =>
            string.Equals(GetInstalled(package)?.Sha256, package.Sha256, StringComparison.OrdinalIgnoreCase);

        /// <summary>Downloads, verifies and unpacks a package into runtimes/&lt;folder&gt;, replacing an older copy.</summary>
        /// <param name="progress">(bytes downloaded, total bytes; 0 when unknown)</param>
        public async Task InstallAsync(RuntimePackage package, Action<long, long> progress, Action<string> log, CancellationToken cancellationToken)
        {
            log ??= _ => { };
            if (package.Folder.Contains('/') || package.Folder.Contains("..") || package.Folder.StartsWith("."))
                throw new RuntimeInstallException($"Invalid folder name in the runtime list: {package.Folder}");

            Directory.CreateDirectory(downloadsFolder);
            Directory.CreateDirectory(runtimesFolder);
            string archive = Path.Combine(downloadsFolder, Path.GetFileName(new Uri(package.Url).AbsolutePath));
            string partial = archive + ".partial";
            string staging = Path.Combine(runtimesFolder, ".staging-" + package.Id);
            try
            {
                log($"Runtime {package.Id} {package.Version}: downloading {package.Url}");
                await DownloadAsync(package, partial, progress, cancellationToken);

                string hash = await Sha256Async(partial, cancellationToken);
                if (!hash.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new RuntimeInstallException($"{Path.GetFileName(archive)} is damaged (SHA-256 {hash}, expected {package.Sha256}). Try again.");
                File.Move(partial, archive, true);

                log($"Runtime {package.Id}: unpacking");
                DeleteDirectory(staging);
                Directory.CreateDirectory(staging);
                var tar = new LaunchCommand { FileName = "tar", WorkingDirectory = staging };
                tar.Arguments.AddRange(new[] { "-xf", archive, "-C", staging });
                int code = await runner.RunAsync(tar, log, cancellationToken);
                string unpacked = Path.Combine(staging, package.Folder);
                if (code != 0) throw new RuntimeInstallException($"tar could not unpack {Path.GetFileName(archive)} (exit code {code}). For .tar.zst archives the zstd program is needed.");
                if (!Directory.Exists(unpacked)) throw new RuntimeInstallException($"{Path.GetFileName(archive)} does not contain the folder {package.Folder}.");

                await File.WriteAllTextAsync(Path.Combine(unpacked, MarkerFile), JsonConvert.SerializeObject(new InstalledRuntime
                {
                    Id = package.Id, Version = package.Version, Sha256 = package.Sha256, Installed = DateTime.UtcNow,
                }, Formatting.Indented), cancellationToken);

                string target = Path.Combine(runtimesFolder, package.Folder);
                DeleteDirectory(target);
                Directory.Move(unpacked, target);
                log($"Runtime {package.Id} {package.Version} installed in {target}");
            }
            finally
            {
                DeleteDirectory(staging);
                TryDelete(partial);
                TryDelete(archive);
            }
        }

        private async Task DownloadAsync(RuntimePackage package, string destination, Action<long, long> progress, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await http.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? package.Size;
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var file = File.Create(destination);
                var buffer = new byte[1 << 16];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    done += read;
                    progress?.Invoke(done, total);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new RuntimeInstallException($"Could not download {package.Id} ({package.Url}): {ex.Message}", ex);
            }
        }

        private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(path);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        }

        private static void DeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Trace.WriteLine($"Could not delete {path}: {ex.Message}"); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Trace.WriteLine($"Could not delete {path}: {ex.Message}"); }
        }
    }
}
