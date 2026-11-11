using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BedrockLauncher.Runtime
{
    /// <summary>
    /// xodus-service: the Linux stand-in for Gaming Services that WineGDK's xgameruntime talks to over the unix socket
    /// $XDG_RUNTIME_DIR/xodus.sock. With Xodus interop it hands the game the MSA token of the signed-in Xodus account,
    /// so the game signs in to Xbox Live (Realms, featured servers, friends). The launcher starts one instance and
    /// stops it when it exits; an instance that is already running (started by the user) is reused.
    /// </summary>
    public sealed class XodusService
    {
        public const string ServiceName = "xodus-service";
        public const string SocketName = "xodus.sock";

        public static XodusService Default { get; } = new XodusService();

        private readonly object gate = new();
        private Process process;

        public XodusService() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();

        /// <summary>xodus-service next to xodus-cli, or in PATH.</summary>
        public static string Locate(string xodusCli)
        {
            if (!string.IsNullOrEmpty(xodusCli))
            {
                string sibling = Path.Combine(Path.GetDirectoryName(xodusCli) ?? "", ServiceName);
                if (File.Exists(sibling)) return sibling;
            }
            return LaunchCommandBuilder.FindInPath(ServiceName);
        }

        /// <summary>$XDG_RUNTIME_DIR, or /run/user/&lt;uid&gt; when the session did not set it.</summary>
        public static string RuntimeDirectory
        {
            get
            {
                string dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                if (!string.IsNullOrEmpty(dir)) return dir;
                string fallback = $"/run/user/{GetUid()}";
                return Directory.Exists(fallback) ? fallback : null;
            }
        }

        public static string SocketPath => RuntimeDirectory is string dir ? Path.Combine(dir, SocketName) : null;

        /// <summary>True when something accepts connections on the socket.</summary>
        public static bool IsListening(string socketPath)
        {
            if (string.IsNullOrEmpty(socketPath) || !File.Exists(socketPath)) return false;
            try
            {
                using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                socket.Connect(new UnixDomainSocketEndPoint(socketPath));
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>
        /// Makes sure xodus-service listens on the socket; starts it when needed. Returns false (and logs why) when it
        /// cannot be started, e.g. no XDG_RUNTIME_DIR or the binary is missing.
        /// </summary>
        public async Task<bool> EnsureRunningAsync(string service, IProcessRunner runner, Action<string> log, CancellationToken cancellationToken = default)
        {
            string socketPath = SocketPath;
            if (socketPath == null) { log("xodus-service: no XDG_RUNTIME_DIR, cannot place xodus.sock"); return false; }
            if (IsListening(socketPath)) { log($"xodus-service: already listening on {socketPath}"); return true; }
            if (string.IsNullOrEmpty(service) || !File.Exists(service)) { log("xodus-service: not installed"); return false; }

            Process started;
            lock (gate)
            {
                if (process == null || process.HasExited)
                {
                    // A socket left behind by a crashed instance makes bind() fail.
                    if (File.Exists(socketPath)) File.Delete(socketPath);
                    var command = new LaunchCommand { FileName = service };
                    command.Environment["XDG_RUNTIME_DIR"] = RuntimeDirectory;
                    process = runner.Start(command, line => log("[xodus-service] " + line));
                }
                started = process;
            }

            for (int i = 0; i < 100 && !cancellationToken.IsCancellationRequested; i++)
            {
                if (IsListening(socketPath)) { log($"xodus-service: listening on {socketPath}"); return true; }
                if (started.HasExited) { log($"xodus-service exited with code {started.ExitCode}"); return false; }
                await Task.Delay(100, cancellationToken);
            }
            log("xodus-service: no socket after 10 s");
            return false;
        }

        public void Stop()
        {
            lock (gate)
            {
                try { if (process != null && !process.HasExited) process.Kill(); }
                catch (InvalidOperationException) { }
                process = null;
            }
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getuid")]
        private static extern uint GetUidNative();

        private static uint GetUid()
        {
            try { return GetUidNative(); }
            catch (Exception) { return 1000; }
        }
    }
}
