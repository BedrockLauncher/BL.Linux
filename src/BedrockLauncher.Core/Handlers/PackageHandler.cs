using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BedrockLauncher.Accounts;
using BedrockLauncher.Classes;
using BedrockLauncher.Downloaders;
using BedrockLauncher.Enums;
using BedrockLauncher.Exceptions;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.Properties;
using BedrockLauncher.Runtime;
using BedrockLauncher.UpdateProcessor.Classes;
using BedrockLauncher.UpdateProcessor.Enums;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Handlers
{
    /// <summary>
    /// Linux counterpart of upstream's PackageHandler. Windows registers packages with the PackageManager / Gaming
    /// Services and activates them; on Linux a version is a folder of game files that a compatibility runtime runs:
    ///   UWP (before 1.21.120): appx downloaded from the Store, extracted, run with wine-uwp.
    ///   GDK (1.21.120 and later): decrypted game files imported from a Windows install, run with GDK-Proton/WineGDK.
    /// </summary>
    public class PackageHandler : IDisposable
    {
        private CancellationTokenSource CancelSource = new CancellationTokenSource();

        public VersionDownloader VersionDownloader { get; private set; } = new VersionDownloader();
        public Process GameHandle { get; private set; }
        public bool isGameRunning => GameHandle != null;

        private GameRuntime gameRuntime;
        private string gamePrefix;

        /// <summary>Replaced in tests.</summary>
        public IProcessRunner Runner { get; set; } = ProcessRunner.Default;

        private static MainDataModel Model => MainDataModel.Default;
        private static PathHandler Paths => MainDataModel.Default.FilePaths;
        private static ProgressBarModel Progress => MainDataModel.Default.ProgressBarState;

        public RuntimeLocator Locator => new RuntimeLocator(Paths.RuntimesFolder);

        /// <summary>Test hook: the HttpClient the runtime installer downloads with.</summary>
        public System.Net.Http.HttpClient RuntimeHttp { get; set; }

        public RuntimeInstaller Installer => new RuntimeInstaller(Paths.RuntimesFolder, Paths.DownloadsFolder, RuntimeHttp, Runner);

        #region Public Methods

        public async Task Play(BLProfile profile, MCVersion v, string dataPath, bool keepLauncherOpen, bool launchEditor)
        {
            if (v == null) return;
            if (isGameRunning)
            {
                await ClosePackage();
                return;
            }

            try
            {
                StartTask();
                Progress.SetProgressBarText(v.DisplayName);

                if (launchEditor)
                    Trace.WriteLine("Editor activation (minecraft://creator/?Editor=true) needs Windows protocol activation; starting the game normally.");

                if (v.IsGdk) await EnsureGdkFiles(v);
                else await EnsureUwpFiles(v);

                Progress.SetProgressBarState(LauncherState.isLaunching);
                Process process = await Launch(v, dataPath);

                if (keepLauncherOpen)
                {
                    AttachGameProcess(process);
                }
                else
                {
                    AttachGameProcess(process);
                    Model.UiHost?.CloseLauncher();
                }
            }
            catch (OperationCanceledException)
            {
                Trace.WriteLine("Operation canceled.");
            }
            catch (PackageManagerException e)
            {
                await ShowError(e);
            }
            catch (Exception e)
            {
                await ShowError(new AppLaunchFailedException(e));
            }
            finally
            {
                EndTask();
            }
        }

        /// <summary>Install / Repair without launching.</summary>
        public async Task InstallPackage(MCVersion v, string dataPath = null)
        {
            if (v == null) return;
            try
            {
                StartTask();
                if (v.IsGdk) await EnsureGdkFiles(v);
                else await EnsureUwpFiles(v);
                if (!string.IsNullOrEmpty(dataPath)) Directory.CreateDirectory(dataPath);
            }
            catch (OperationCanceledException)
            {
                Trace.WriteLine("Operation canceled.");
            }
            catch (PackageManagerException e)
            {
                await ShowError(e);
            }
            catch (Exception e)
            {
                await ShowError(new PackageManagerException(e.Message, e));
            }
            finally
            {
                EndTask();
            }
        }

        /// <summary>Repair: downloads and extracts a UWP version again; asks for the files again for GDK.</summary>
        public async Task DownloadPackage(MCVersion v)
        {
            if (v == null) return;
            try
            {
                StartTask();
                if (v.IsGdk)
                {
                    await ImportGdkInteractive(v);
                }
                else
                {
                    await DeleteDirectoryIfExists(v.GameDirectory);
                    await EnsureUwpFiles(v);
                }
            }
            catch (OperationCanceledException)
            {
                Trace.WriteLine("Operation canceled.");
            }
            catch (PackageManagerException e)
            {
                await ShowError(e);
            }
            catch (Exception e)
            {
                await ShowError(new PackageManagerException(e.Message, e));
            }
            finally
            {
                EndTask();
            }
        }

        public async Task RemovePackage(MCVersion v)
        {
            if (v == null) return;
            try
            {
                StartTask();
                Progress.SetProgressBarState(LauncherState.isUninstalling);
                await DeleteDirectoryIfExists(v.GameDirectory);
                v.UpdateFolderSize();
            }
            catch (Exception e)
            {
                await ShowError(new PackageManagerException(e.Message, e));
            }
            finally
            {
                EndTask();
            }
        }

        /// <summary>Adds a local .appx/.appxbundle (upstream's "Add package") or a GDK game folder as a version.</summary>
        public async Task AddPackage(string path)
        {
            try
            {
                StartTask();
                RequireAccount();
                if (Directory.Exists(path))
                {
                    await ImportGdkFolder(path, null);
                }
                else
                {
                    if (!AppxPackage.LooksLikePackage(path, out string problem))
                        throw new PackageManagerException($"{Path.GetFileName(path)}: {problem}.");

                    string staging = Path.Combine(Paths.VersionsFolder, ".import-" + Guid.NewGuid().ToString("N"));
                    Progress.SetProgressBarState(LauncherState.isExtracting);
                    await AppxPackage.ExtractAsync(path, staging, Constants.CurrentArchitecture, ExtractionProgress(), CancelSource.Token);

                    var identity = AppxPackage.ReadManifestIdentity(Path.Combine(staging, MCVersionExtensions.MainifestFileName));
                    string target = Path.Combine(Paths.VersionsFolder, $"{identity?.Version ?? "custom"}-{Guid.NewGuid().ToString("N").Substring(0, 6)}");
                    Directory.Move(staging, target);
                    await File.WriteAllTextAsync(Path.Combine(target, MCVersionExtensions.IdentificationFilename), Path.GetFileName(target));
                    await File.WriteAllTextAsync(Path.Combine(target, "custom_name.txt"), Path.GetFileNameWithoutExtension(path));
                }
            }
            catch (Exception e)
            {
                await ShowError(e as PackageManagerException ?? new PackageManagerException(e.Message, e));
            }
            finally
            {
                EndTask();
            }

            await Model.LoadVersions();
        }

        public void Cancel()
        {
            if (Progress.AllowCancel)
            {
                Progress.SetProgressBarState(LauncherState.isCanceling);
                CancelSource.Cancel();
            }
        }

        public async Task ClosePackage()
        {
            Process handle = GameHandle;
            if (handle != null)
            {
                try { handle.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            }
            if (gameRuntime != null && gamePrefix != null)
            {
                try { await new WinePrefixManager(Runner, Log).KillAsync(gameRuntime, gamePrefix); }
                catch (Exception ex) { Trace.WriteLine($"wineserver -k failed: {ex.Message}"); }
            }
        }

        #endregion

        #region UWP

        private async Task EnsureUwpFiles(MCVersion v)
        {
            if (v.HasPlayableFiles) return;

            string downloads = Directory.CreateDirectory(Paths.DownloadsFolder).FullName;
            string backups = Path.Combine(Paths.VersionsFolder, "AppxBackups");
            string fileName = $"Minecraft-{v.Name}-{v.Architecture}.appx";
            string backup = Path.Combine(backups, fileName);
            string package = File.Exists(backup) && AppxPackage.LooksLikePackage(backup, out _) ? backup : Path.Combine(downloads, fileName);

            if (package != backup)
            {
                RequireAccount();
                Progress.SetProgressBarState(LauncherState.isDownloading);
                Progress.AllowCancel = true;
                try
                {
                    await VersionDownloader.DownloadVersion(v.DisplayName, v.UUID, 1, package,
                        (current, total) => Progress.SetProgressBarProgress(current, total),
                        CancelSource.Token, v.Type);
                }
                catch (OperationCanceledException)
                {
                    SafeDelete(package);
                    throw;
                }
                catch (Exception ex)
                {
                    SafeDelete(package);
                    throw new PackageDownloadFailedException(ex);
                }

                if (!AppxPackage.LooksLikePackage(package, out string problem))
                {
                    SafeDelete(package);
                    throw new PackageDownloadFailedException(new IOException(problem));
                }
            }

            Progress.SetProgressBarState(LauncherState.isExtracting);
            Progress.ResetProgressBarProgress();
            string staging = v.GameDirectory.TrimEnd('/') + ".partial";
            try
            {
                await DeleteDirectoryIfExists(staging);
                await AppxPackage.ExtractAsync(package, staging, Constants.CurrentArchitecture, ExtractionProgress(), CancelSource.Token);
                await File.WriteAllTextAsync(Path.Combine(staging, MCVersionExtensions.IdentificationFilename), v.PackageID ?? v.UUID);
                await DeleteDirectoryIfExists(v.GameDirectory);
                Directory.Move(staging, v.GameDirectory);
            }
            catch (OperationCanceledException)
            {
                await DeleteDirectoryIfExists(staging);
                throw;
            }
            catch (Exception ex)
            {
                await DeleteDirectoryIfExists(staging);
                throw new PackageExtractionFailedException(ex);
            }
            finally
            {
                Progress.AllowCancel = false;
            }

            if (package != backup)
            {
                if (LauncherSettings.Default.KeepAppx)
                {
                    Directory.CreateDirectory(backups);
                    File.Move(package, backup, overwrite: true);
                }
                else
                {
                    SafeDelete(package);
                }
            }

            v.UpdateFolderSize();
        }

        #endregion

        #region GDK

        private string XodusCli => Xodus.Locate(Paths.RuntimesFolder, LauncherSettings.Default.XodusPath);

        /// <summary>Download URLs of a GDK version (the catalog; replaced in tests).</summary>
        public Func<MCVersion, string[]> GdkUrlResolver { get; set; }

        private async Task EnsureGdkFiles(MCVersion v)
        {
            if (v.HasPlayableFiles) return;

            // With Xodus the package is downloaded from the catalog (Rayth's GdkLinks) and licensed with the user's
            // Xbox account; without it the files have to come from a Windows install.
            string[] urls = (GdkUrlResolver ?? VersionDownloader.GetGdkDownloadUrls)(v);
            string cli = XodusCli;
            if (cli == null && urls.Length > 0)
            {
                RequireAccount();
                cli = await TryInstallXodus();
            }
            if (cli != null && urls.Length > 0)
            {
                await DownloadGdkWithXodus(cli, v, urls);
                return;
            }

            await ImportGdkInteractive(v);
            if (!v.HasPlayableFiles)
                throw new GdkFilesRequiredException(string.Format(LanguageManager.GetResource("Linux_GdkFilesRequired"), v.Name));
        }

        private async Task ImportGdkInteractive(MCVersion v)
        {
            RequireAccount();
            string folder = Model.UiHost == null ? null : await Model.UiHost.PickFolder(string.Format(LanguageManager.GetResource("Linux_GdkPickFolder"), v.Name));
            if (string.IsNullOrEmpty(folder)) throw new OperationCanceledException();
            await ImportGdkFolder(folder, v);
        }

        private async Task DownloadGdkWithXodus(string cli, MCVersion v, string[] urls)
        {
            RequireAccount();
            if (!LauncherSettings.Default.XodusSignedIn)
                throw new AccountRequiredException(LanguageManager.GetResource("Linux_XodusLoginRequired"));

            Progress.SetProgressBarState(LauncherState.isDownloading);
            Progress.IsIndeterminate = true;
            string staging = v.GameDirectory.TrimEnd('/') + ".partial";
            Exception last = null;
            foreach (string url in urls)
            {
                await DeleteDirectoryIfExists(staging);
                Directory.CreateDirectory(staging);
                Trace.WriteLine($"Xodus: streaming {url}");
                int code = await Runner.RunAsync(Xodus.Streaming(cli, url, staging), line =>
                {
                    Trace.WriteLine("[xodus] " + line);
                    if (!string.IsNullOrWhiteSpace(line)) Progress.SetProgressBarText(line.Trim());
                }, CancelSource.Token);

                if (code == 0 && File.Exists(Path.Combine(staging, Constants.MINECRAFT_EXECUTABLE)))
                {
                    await File.WriteAllTextAsync(Path.Combine(staging, MCVersionExtensions.IdentificationFilename), v.PackageID ?? v.UUID);
                    await DeleteDirectoryIfExists(v.GameDirectory);
                    Directory.Move(staging, v.GameDirectory);
                    v.UpdateFolderSize();
                    return;
                }
                last = new IOException($"xodus-cli streaming exited with code {code} for {url}");
                Trace.WriteLine(last.Message);
            }
            await DeleteDirectoryIfExists(staging);
            throw new PackageDownloadFailedException(last ?? new IOException("No download source worked."));
        }

        /// <summary>Imports decrypted GDK game files; with a target version they must be that exact build.</summary>
        public async Task ImportGdkFolder(string folder, MCVersion target)
        {
            RequireAccount();
            GdkGameFiles.Inspection inspection = GdkGameFiles.Inspect(folder);
            if (!inspection.IsValid)
                throw new GdkFilesRequiredException(string.Join("\n", inspection.Problems));

            GdkPackageIdentity found = inspection.ToIdentity();
            string destination;
            if (target != null)
            {
                if (target.RequiredGdkPackage != null && !target.RequiredGdkPackage.Equals(found))
                {
                    bool accept = Model.UiHost != null && await Model.UiHost.Confirm("Linux_GdkMismatch_Title", "Linux_GdkMismatch_Text",
                        found?.FullName ?? "?", target.RequiredGdkPackage.FullName);
                    if (!accept) throw new OperationCanceledException();
                }
                destination = target.GameDirectory;
            }
            else
            {
                string name = MinecraftVersion.TryParse(inspection.Version?.ToString(), out MinecraftVersion parsed) ? parsed.ToRealString() : inspection.Version?.ToString();
                destination = Path.Combine(Paths.VersionsFolder, name ?? ("gdk-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
                if (Directory.Exists(destination))
                    destination += "-GDK-" + inspection.Architecture;
            }

            Progress.SetProgressBarState(LauncherState.isExtracting);
            Progress.AllowCancel = true;
            string staging = destination.TrimEnd('/') + ".partial";
            try
            {
                await DeleteDirectoryIfExists(staging);
                await GdkGameFiles.CopyAsync(folder, staging, ExtractionProgress(), CancelSource.Token);
                await File.WriteAllTextAsync(Path.Combine(staging, MCVersionExtensions.IdentificationFilename), target?.PackageID ?? found?.FullName ?? Path.GetFileName(destination));
                await DeleteDirectoryIfExists(destination);
                Directory.Move(staging, destination);
            }
            catch (Exception)
            {
                await DeleteDirectoryIfExists(staging);
                throw;
            }
            finally
            {
                Progress.AllowCancel = false;
            }

            target?.UpdateFolderSize();
        }

        #endregion

        #region Launch

        private async Task<Process> Launch(MCVersion v, string dataPath)
        {
            LauncherSettings settings = LauncherSettings.Default;
            GameRuntimeKind kind = v.IsGdk ? settings.GdkRuntimeKind : GameRuntimeKind.WineUwp;
            string configured = v.IsGdk ? settings.GdkRuntimePath : settings.UwpRuntimePath;

            GameRuntime runtime = await ResolveOrInstallRuntime(kind, configured, v.IsGdk ? "Linux_GdkRuntimeMissing" : "Linux_UwpRuntimeMissing");

            Trace.WriteLine($"Runtime: {runtime}");
            string prefix = WinePrefixManager.GetPrefixPath(Paths.PrefixesFolder, runtime);
            var prefixes = new WinePrefixManager(Runner, Log);
            await prefixes.EnsureAsync(runtime, prefix, settings.UseDxvk, CancelSource.Token);

            if (!string.IsNullOrEmpty(dataPath)) Directory.CreateDirectory(dataPath);

            if (v.IsGdk && !string.IsNullOrEmpty(dataPath))
                await RedirectGdkSaveData(runtime, prefix, v.Type, dataPath);

            var request = new LaunchRequest
            {
                Runtime = runtime,
                PrefixPath = prefix,
                GameDirectory = v.GameDirectory,
                PackageDataPath = v.IsGdk ? null : dataPath,
                UseDxvk = settings.UseDxvk,
                UseFastSync = settings.UseFastSync,
                ShaderCachePath = Directory.CreateDirectory(Path.Combine(Paths.CacheLocation, "shadercache", runtime.Kind.ToString().ToLowerInvariant())).FullName,
                UseGameMode = settings.UseGameMode,
                UseMangoHud = settings.UseMangoHud,
                WineDebug = settings.WineDebug,
                ExtraEnvironment = settings.ExtraEnvironment,
                SteamClientPath = FindSteamRoot(),
            };

            LaunchCommand command = LaunchCommandBuilder.Build(request);
            if (v.IsGdk && Xodus.IsXodusInstall(v.GameDirectory))
            {
                // Installed through Xodus: the executable is still encrypted; xodus-cli licenses and decrypts it.
                string cli = XodusCli ?? await TryInstallXodus() ?? throw new RuntimeNotFoundException(LanguageManager.GetResource("Linux_XodusMissing"));
                if (runtime.UsesProtonScript)
                {
                    request.Runtime = new GameRuntime { Kind = GameRuntimeKind.WineGdk, Name = runtime.Name, RootPath = runtime.RootPath, Origin = runtime.Origin };
                    request.PrefixPath = WinePrefixManager.GetWinePrefix(prefix, runtime);
                    command = LaunchCommandBuilder.Build(request);
                }
                command = Xodus.WrapRun(cli, command, v.GameDirectory, Constants.MINECRAFT_EXECUTABLE);
            }
            if (v.IsGdk && runtime.UsesXodusInterop)
            {
                // WineGDK with Xodus interop: xgameruntime gets the Xbox Live sign-in from xodus-service and does not
                // start without it.
                string service = XodusService.Locate(XodusCli ?? await TryInstallXodus());
                if (!await XodusService.Default.EnsureRunningAsync(service, Runner, Log, CancelSource.Token))
                    throw new RuntimeNotFoundException(LanguageManager.GetResource("Linux_XodusServiceMissing"));
                command.Environment["XDG_RUNTIME_DIR"] = XodusService.RuntimeDirectory;
                if (!settings.XodusSignedIn) Trace.WriteLine("xodus-service: not signed in to Xodus, the game stays offline");
            }
            if (settings.UseFastSync && !runtime.UsesProtonScript) command = LaunchCommandBuilder.WithRaisedFileLimit(command);
            Trace.WriteLine(File.Exists("/dev/ntsync") ? "ntsync: available" : "ntsync: /dev/ntsync not present (kernel 6.14+ with the ntsync module); esync is used where the runtime has it");
            gameRuntime = runtime;
            gamePrefix = prefix;

            Process process = Runner.Start(command, GameLog());
            Trace.WriteLine($"Launch: OK (process {process.Id})");
            return process;
        }

        /// <summary>The runtime for a kind; when none is found it is downloaded (Settings > Linux > automatic runtimes).</summary>
        private async Task<GameRuntime> ResolveOrInstallRuntime(GameRuntimeKind kind, string configured, string missingResource)
        {
            GameRuntime runtime = Locator.Resolve(kind, configured);
            if (runtime != null) return runtime;
            string missing = string.Format(LanguageManager.GetResource(missingResource), Paths.RuntimesFolder);
            if (!string.IsNullOrWhiteSpace(configured) || !LauncherSettings.Default.AutoInstallRuntimes)
                throw new RuntimeNotFoundException(missing);

            try
            {
                RuntimeManifest manifest = await Installer.GetManifestAsync(CancelSource.Token);
                RuntimePackage package = manifest.For(kind) ?? throw new RuntimeInstallException($"The runtime list has no {kind} build.");
                await InstallRuntimePackage(package);
            }
            catch (RuntimeInstallException ex)
            {
                throw new RuntimeNotFoundException(missing + "\n\n" + string.Format(LanguageManager.GetResource("Linux_RuntimeAutoInstallFailed"), ex.Message));
            }
            return Locator.Resolve(kind, configured) ?? throw new RuntimeNotFoundException(missing);
        }

        /// <summary>Downloads xodus-cli + xodus-service; null (and the reason in the log) when that is not possible.</summary>
        private async Task<string> TryInstallXodus()
        {
            if (!LauncherSettings.Default.AutoInstallRuntimes || !string.IsNullOrWhiteSpace(LauncherSettings.Default.XodusPath)) return null;
            try
            {
                RuntimeManifest manifest = await Installer.GetManifestAsync(CancelSource.Token);
                RuntimePackage package = manifest.Xodus ?? throw new RuntimeInstallException("The runtime list has no Xodus build.");
                await InstallRuntimePackage(package);
            }
            catch (RuntimeInstallException ex)
            {
                Trace.WriteLine($"Xodus could not be installed automatically: {ex.Message}");
                return null;
            }
            return XodusCli;
        }

        /// <summary>
        /// Settings > Linux: installs or updates the runtimes the launcher uses (wine-uwp, the selected GDK runtime,
        /// Xodus). Returns one line per package.
        /// </summary>
        public async Task<List<string>> InstallOrUpdateRuntimes()
        {
            var report = new List<string>();
            try
            {
                StartTask();
                RuntimeManifest manifest = await Installer.GetManifestAsync(CancelSource.Token);
                var wanted = new[] { manifest.For(GameRuntimeKind.WineUwp), manifest.For(LauncherSettings.Default.GdkRuntimeKind), manifest.Xodus };
                foreach (RuntimePackage package in wanted.Where(p => p != null))
                {
                    if (Installer.IsUpToDate(package))
                    {
                        report.Add($"{package.Id} {package.Version}: {LanguageManager.GetResource("Linux_RuntimeUpToDate")}");
                        continue;
                    }
                    await InstallRuntimePackage(package);
                    report.Add($"{package.Id} {package.Version}: {LanguageManager.GetResource("Linux_RuntimeInstalled")}");
                }
            }
            catch (RuntimeInstallException ex)
            {
                report.Add(ex.Message);
            }
            catch (OperationCanceledException)
            {
                report.Add(LanguageManager.GetResource("Linux_RuntimeCancelled"));
            }
            finally
            {
                EndTask();
            }
            return report;
        }

        /// <summary>Installs one runtime package with the launcher's progress bar.</summary>
        public async Task InstallRuntimePackage(RuntimePackage package)
        {
            string previousText = Progress.Information;
            Progress.SetProgressBarState(LauncherState.isDownloading);
            Progress.SetProgressBarText(string.Format(LanguageManager.GetResource("Linux_RuntimeDownloading"), package.Id, package.Version));
            Progress.AllowCancel = true;
            try
            {
                await Installer.InstallAsync(package, (done, total) => Progress.SetProgressBarProgress(done, total), Log, CancelSource.Token);
            }
            finally
            {
                Progress.AllowCancel = false;
                Progress.ResetProgressBarProgress();
                Progress.SetProgressBarText(previousText);
            }
        }

        private async Task RedirectGdkSaveData(GameRuntime runtime, string prefix, VersionType type, string dataPath)
        {
            string winePrefix = WinePrefixManager.GetWinePrefix(prefix, runtime);
            if (runtime.UsesProtonScript && !WinePrefixManager.IsInitialized(winePrefix))
            {
                // Let Proton build its prefix before a folder is linked inside it.
                var init = new LaunchCommand { FileName = runtime.ProtonScript, WorkingDirectory = prefix };
                init.Arguments.AddRange(new[] { "run", "wineboot", "-u" });
                init.Environment["STEAM_COMPAT_DATA_PATH"] = prefix;
                init.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = FindSteamRoot() ?? prefix;
                init.Environment["WINEDEBUG"] = "-all";
                await Runner.RunAsync(init, Log, CancelSource.Token);
            }

            string gameData = Path.Combine(WinePath.GetPrefixRoamingAppData(winePrefix, runtime.UsesProtonScript),
                MinecraftPackageFamilies.GetGdkDataFolderName(type));
            try
            {
                var (state, backup) = new SaveDataRedirector().Redirect(gameData, dataPath);
                Trace.WriteLine($"Save data: {state}{(backup != null ? $" (previous data moved to {backup})" : "")}");
            }
            catch (Exception ex)
            {
                throw new SaveRedirectionFailedException(ex);
            }
        }

        private void AttachGameProcess(Process process)
        {
            GameHandle = process;
            Progress.SetGameRunningStatus(true);
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                Trace.WriteLine($"Minecraft exited with code {SafeExitCode(process)}");
                GameHandle = null;
                Progress.SetGameRunningStatus(false);
                process.Dispose();
            };
        }

        private static int SafeExitCode(Process process)
        {
            try { return process.ExitCode; } catch (InvalidOperationException) { return -1; }
        }

        private static string FindSteamRoot()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new[] { Path.Combine(home, ".steam", "root"), Path.Combine(home, ".local", "share", "Steam") }.FirstOrDefault(Directory.Exists);
        }

        private Action<string> GameLog()
        {
            Directory.CreateDirectory(Paths.LogsFolder);
            string path = Path.Combine(Paths.LogsFolder, $"game-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            Trace.WriteLine($"Game log: {path}");
            var writer = new StreamWriter(path, append: true) { AutoFlush = true };
            return line =>
            {
                lock (writer) writer.WriteLine(line);
            };
        }

        private static void Log(string line) => Trace.WriteLine(line);

        #endregion

        #region Helpers

        /// <summary>New game files (Store download, GDK import, package import) need a signed-in account that owns Minecraft.</summary>
        private static void RequireAccount()
        {
            string reason = AccountManager.Default.DownloadBlockReason;
            if (reason != null)
                throw new AccountRequiredException(LanguageManager.GetResource(reason));
        }

        private void StartTask()
        {
            if (CancelSource.IsCancellationRequested)
            {
                CancelSource.Dispose();
                CancelSource = new CancellationTokenSource();
            }
            Progress.ResetProgressBarProgress();
            Progress.SetProgressBarVisibility(true);
            Progress.SetProgressBarState(LauncherState.isInitializing);
        }

        private void EndTask()
        {
            Progress.AllowCancel = false;
            Progress.SetProgressBarVisibility(false);
            Progress.SetProgressBarState(LauncherState.None);
            Progress.SetProgressBarText();
            Progress.ResetProgressBarProgress();
        }

        private static IProgress<(long Done, long Total)> ExtractionProgress() =>
            new Progress<(long Done, long Total)>(p => Progress.SetProgressBarProgress(p.Done, p.Total));

        private static async Task ShowError(Exception e)
        {
            Trace.WriteLine(e);
            if (Model.UiHost != null) await Model.UiHost.ShowException(e);
        }

        private static async Task DeleteDirectoryIfExists(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;
            string versions = Path.GetFullPath(Paths.VersionsFolder);
            string full = Path.GetFullPath(directory);
            if (!full.StartsWith(versions, StringComparison.Ordinal) || full.TrimEnd('/') == versions.TrimEnd('/'))
                throw new InvalidOperationException($"Refusing to delete '{full}': it is not a version folder.");
            if (Directory.Exists(full)) await Task.Run(() => Directory.Delete(full, true));
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Trace.WriteLine($"Could not delete {path}: {ex.Message}"); }
        }

        public void Dispose()
        {
            CancelSource.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
