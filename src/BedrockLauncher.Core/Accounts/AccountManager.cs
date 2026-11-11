using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BedrockLauncher.Accounts
{
    /// <summary>
    /// The launcher's signed-in Microsoft / Xbox account. Downloading or importing game versions requires it AND a
    /// verified Minecraft Bedrock license (<see cref="MinecraftOwnershipChecker"/>); versions already on disk play
    /// without it. Only the refresh token is kept, in ~/.config/BedrockLauncher/account.json (mode 600).
    /// </summary>
    public sealed class AccountManager : INotifyPropertyChanged
    {
        public static AccountManager Default { get; } = new AccountManager();

        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) => UiThread.Run(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));

        private readonly XboxAuthenticator authenticator;
        private readonly MinecraftOwnershipChecker ownershipChecker;
        private StoredAccount account;

        /// <summary>How long a decisive answer (owned / not owned) is trusted when a later check cannot reach a service.</summary>
        public static readonly TimeSpan OwnershipCacheLifetime = TimeSpan.FromDays(7);

        public AccountManager(XboxAuthenticator authenticator = null, MinecraftOwnershipChecker ownershipChecker = null)
        {
            this.authenticator = authenticator ?? new XboxAuthenticator();
            this.ownershipChecker = ownershipChecker ?? new MinecraftOwnershipChecker(
                xodusCli: () => Runtime.Xodus.Locate(ViewModels.MainDataModel.Default.FilePaths.RuntimesFolder, Properties.LauncherSettings.Default.XodusPath),
                xodusSignedIn: () => Properties.LauncherSettings.Default.XodusSignedIn);
        }

        public bool IsSignedIn => account != null;
        public string Gamertag => account?.Gamertag;
        public string Xuid => account?.Xuid;
        public OwnershipState Ownership => account?.Ownership?.State ?? OwnershipState.Unknown;
        public OwnershipResult OwnershipDetails => account?.Ownership;
        public bool IsCheckingOwnership { get; private set; }
        public bool OwnsMinecraft => Ownership == OwnershipState.Owned;

        /// <summary>Tests and offline development: BL_SKIP_ACCOUNT_CHECK=1 lets downloads run without an account.</summary>
        public static bool CheckDisabled => Environment.GetEnvironmentVariable("BL_SKIP_ACCOUNT_CHECK") == "1";

        public bool MayDownload => CheckDisabled || IsSignedIn && OwnsMinecraft;

        /// <summary>Localization key of the reason downloads are blocked, or null when they are allowed.</summary>
        public string DownloadBlockReason
        {
            get
            {
                if (MayDownload) return null;
                if (!IsSignedIn) return "Linux_AccountRequired";
                return Ownership switch
                {
                    OwnershipState.NotOwned => "Linux_NotOwned",
                    OwnershipState.Unverified when account?.Ownership?.Detail == "app-not-approved" => "Linux_OwnershipAppNotApproved",
                    _ => "Linux_OwnershipUnverified",
                };
            }
        }

        public static string StorePath => Path.Combine(ViewModels.MainDataModel.Default.FilePaths.ConfigLocation, "account.json");

        /// <summary>
        /// The launcher's Azure app registration ("Bedrock Launcher", personal Microsoft accounts, public client flows
        /// on, redirect URI http://localhost for "Mobile and desktop applications"). A public client ID is not a secret.
        /// </summary>
        public const string DefaultClientId = "e2e40a45-c8e9-47ef-bf59-de46d75e5f86";

        /// <summary>BL_MSA_CLIENT_ID, else the one set in Settings > Accounts, else <see cref="DefaultClientId"/>.</summary>
        public string ClientId
        {
            get
            {
                string fromEnv = Environment.GetEnvironmentVariable("BL_MSA_CLIENT_ID");
                if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();
                string configured = Properties.LauncherSettings.Default.MicrosoftClientId?.Trim();
                return string.IsNullOrEmpty(configured) ? DefaultClientId : configured;
            }
        }

        /// <summary>Device code sign-in (type a code on microsoft.com/link).</summary>
        public async Task SignInAsync(Action<DeviceCodePrompt> prompt, CancellationToken cancellationToken)
        {
            Set(await authenticator.SignInAsync(ClientId, prompt, cancellationToken));
            Save();
            await VerifyOwnershipAsync(cancellationToken);
        }

        /// <summary>Browser sign-in: Microsoft's page redirects back to the launcher on http://localhost.</summary>
        public async Task SignInWithBrowserAsync(Action<BrowserSignInPrompt> openBrowser, CancellationToken cancellationToken)
        {
            Set(await authenticator.SignInWithBrowserAsync(ClientId, openBrowser, cancellationToken));
            Save();
            await VerifyOwnershipAsync(cancellationToken);
        }

        /// <summary>
        /// Checks Minecraft Bedrock ownership for the signed-in account. An inconclusive answer (service unreachable)
        /// keeps a recent decisive one so a flaky connection does not lock the user out.
        /// </summary>
        public async Task<OwnershipResult> VerifyOwnershipAsync(CancellationToken cancellationToken = default)
        {
            StoredAccount current = account;
            if (current == null) return null;
            IsCheckingOwnership = true;
            Changed(nameof(IsCheckingOwnership));
            try
            {
                OwnershipResult result = await ownershipChecker.CheckAsync(current.XboxUserToken, cancellationToken);
                OwnershipResult previous = current.Ownership;
                bool keepPrevious = result.State == OwnershipState.Unverified && previous != null
                    && (previous.State == OwnershipState.Owned || previous.State == OwnershipState.NotOwned)
                    && DateTime.UtcNow - previous.CheckedAt < OwnershipCacheLifetime;
                if (!keepPrevious) current.Ownership = result;
                Trace.WriteLine($"Minecraft Bedrock ownership: {current.Ownership}{(keepPrevious ? " (cached; now " + result + ")" : "")}");
                if (ReferenceEquals(current, account))
                {
                    Save();
                    RaiseAccountChanged();
                }
                return current.Ownership;
            }
            finally
            {
                IsCheckingOwnership = false;
                Changed(nameof(IsCheckingOwnership));
            }
        }

        /// <summary>Restores the stored account at start; a stale or revoked token signs the user out.</summary>
        public async Task RestoreAsync()
        {
            StoredAccount stored = Load();
            if (stored == null) return;
            Set(stored);
            try
            {
                StoredAccount refreshed = await authenticator.RefreshAsync(stored);
                refreshed.Ownership = stored.Ownership;
                Set(refreshed);
                Save();
            }
            catch (XboxAccountException ex)
            {
                Trace.WriteLine($"Stored sign-in is no longer valid: {ex.Message}");
                SignOut();
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                // Offline: keep the stored account so installed versions and their data stay usable.
                Trace.WriteLine($"Could not refresh the sign-in (offline?): {ex.Message}");
                return;
            }
            await VerifyOwnershipAsync();
        }

        public void SignOut()
        {
            Set(null);
            try { if (File.Exists(StorePath)) File.Delete(StorePath); }
            catch (IOException ex) { Trace.WriteLine($"Could not delete {StorePath}: {ex.Message}"); }
        }

        private void Set(StoredAccount value)
        {
            account = value;
            RaiseAccountChanged();
        }

        private void RaiseAccountChanged()
        {
            Changed(nameof(IsSignedIn));
            Changed(nameof(Gamertag));
            Changed(nameof(Xuid));
            Changed(nameof(Ownership));
            Changed(nameof(OwnershipDetails));
            Changed(nameof(OwnsMinecraft));
            Changed(nameof(MayDownload));
            Changed(nameof(DownloadBlockReason));
        }

        private void Save()
        {
            if (account == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
            File.WriteAllText(StorePath, JsonConvert.SerializeObject(account, Formatting.Indented));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(StorePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        private static StoredAccount Load()
        {
            try
            {
                return File.Exists(StorePath) ? JsonConvert.DeserializeObject<StoredAccount>(File.ReadAllText(StorePath)) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                Trace.WriteLine($"Could not read {StorePath}: {ex.Message}");
                return null;
            }
        }
    }
}
