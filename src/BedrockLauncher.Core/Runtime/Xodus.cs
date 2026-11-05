using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BedrockLauncher.Runtime
{
    /// <summary>
    /// xodus-gaming/xodus: Xbox sign-in, MSIXVC download/extraction and licensing for GDK games on Linux.
    /// The launcher drives its CLI:
    ///   xodus-cli login                              sign in (opens Microsoft's login page)
    ///   xodus-cli streaming &lt;msixvc url&gt; &lt;folder&gt;    download + extract with the account's license
    ///   xodus-cli run &lt;folder&gt; &lt;wine&gt; --exe X.exe    license + on-demand decryption, then starts the game
    /// A folder installed this way keeps the package in <see cref="StreamingPackageName"/>; its executables stay
    /// encrypted on disk and are only decrypted (into memory) by "xodus-cli run".
    /// </summary>
    public static class Xodus
    {
        public const string StreamingPackageName = ".xodus-streaming.msixvc";
        public const string CliName = "xodus-cli";

        public static IEnumerable<string> Candidates(string runtimesFolder, string configured)
        {
            if (!string.IsNullOrWhiteSpace(configured)) yield return configured;
            string env = Environment.GetEnvironmentVariable("XODUS_CLI");
            if (!string.IsNullOrWhiteSpace(env)) yield return env;
            if (!string.IsNullOrEmpty(runtimesFolder)) yield return Path.Combine(runtimesFolder, "xodus", CliName);
            string inPath = LaunchCommandBuilder.FindInPath(CliName);
            if (inPath != null) yield return inPath;
        }

        /// <summary>The xodus-cli binary to use, or null when Xodus is not installed.</summary>
        public static string Locate(string runtimesFolder, string configured) =>
            Candidates(runtimesFolder, configured).FirstOrDefault(File.Exists);

        public static bool IsXodusInstall(string gameDirectory) =>
            !string.IsNullOrEmpty(gameDirectory) && File.Exists(Path.Combine(gameDirectory, StreamingPackageName));

        public static LaunchCommand Login(string cli) => Command(cli, "login");

        public static LaunchCommand Logout(string cli) => Command(cli, "logout");

        public static LaunchCommand Streaming(string cli, string source, string destination)
        {
            var command = Command(cli, "streaming", source, destination);
            command.WorkingDirectory = Path.GetDirectoryName(destination);
            return command;
        }

        /// <summary>Turns a wine launch of the game into "xodus-cli run", keeping the environment (prefix, overrides).</summary>
        public static LaunchCommand WrapRun(string cli, LaunchCommand wineLaunch, string gameDirectory, string executable)
        {
            var command = new LaunchCommand { FileName = cli, WorkingDirectory = gameDirectory };
            string wine = wineLaunch.FileName;
            command.Arguments.AddRange(new[] { "run", gameDirectory, wine, "--exe", executable });
            foreach (var pair in wineLaunch.Environment) command.Environment[pair.Key] = pair.Value;
            return command;
        }

        private static LaunchCommand Command(string cli, params string[] arguments)
        {
            var command = new LaunchCommand { FileName = cli };
            command.Arguments.AddRange(arguments);
            return command;
        }
    }
}
