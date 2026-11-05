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
    /// The launcher's signed-in Microsoft / Xbox account. Downloading or importing game versions requires it; versions
    /// already on disk play without it. Only the refresh token is kept, in ~/.config/BedrockLauncher/account.json
    /// (mode 600).
    /// </summary>
    public sealed class AccountManager : INotifyPropertyChanged
    {
        public static AccountManager Default { get; } = new AccountManager();

        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) => UiThread.Run(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));

        private readonly XboxAuthenticator authenticator;
        private StoredAccount account;

        public AccountManager(XboxAuthenticator authenticator = null) => this.authenticator = authenticator ?? new XboxAuthenticator();

        public bool IsSignedIn => account != null;
        public string Gamertag => account?.Gamertag;
        public string Xuid => account?.Xuid;

        /// <summary>Tests and offline development: BL_SKIP_ACCOUNT_CHECK=1 lets downloads run without an account.</summary>
        public static bool CheckDisabled => Environment.GetEnvironmentVariable("BL_SKIP_ACCOUNT_CHECK") == "1";

        public bool MayDownload => IsSignedIn || CheckDisabled;

        public static string StorePath => Path.Combine(ViewModels.MainDataModel.Default.FilePaths.ConfigLocation, "account.json");

        public string ClientId
        {
            get
            {
                string fromEnv = Environment.GetEnvironmentVariable("BL_MSA_CLIENT_ID");
                return !string.IsNullOrWhiteSpace(fromEnv) ? fromEnv.Trim() : Properties.LauncherSettings.Default.MicrosoftClientId?.Trim();
            }
        }

        public async Task SignInAsync(Action<DeviceCodePrompt> prompt, CancellationToken cancellationToken)
        {
            Set(await authenticator.SignInAsync(ClientId, prompt, cancellationToken));
            Save();
        }

        /// <summary>Restores the stored account at start; a stale or revoked token signs the user out.</summary>
        public async Task RestoreAsync()
        {
            StoredAccount stored = Load();
            if (stored == null) return;
            Set(stored);
            try
            {
                Set(await authenticator.RefreshAsync(stored));
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
            }
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
            Changed(nameof(IsSignedIn));
            Changed(nameof(Gamertag));
            Changed(nameof(Xuid));
            Changed(nameof(MayDownload));
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
