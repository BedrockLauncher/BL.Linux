using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BedrockLauncher.Runtime
{
    public interface IProcessRunner
    {
        /// <summary>Runs to completion and returns the exit code; output goes to <paramref name="log"/>.</summary>
        Task<int> RunAsync(LaunchCommand command, Action<string> log, CancellationToken cancellationToken = default);

        /// <summary>Starts and returns the process without waiting for it.</summary>
        Process Start(LaunchCommand command, Action<string> log);
    }

    public sealed class ProcessRunner : IProcessRunner
    {
        public static readonly ProcessRunner Default = new ProcessRunner();

        public async Task<int> RunAsync(LaunchCommand command, Action<string> log, CancellationToken cancellationToken = default)
        {
            using Process process = Start(command, log);
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                throw;
            }
            return process.ExitCode;
        }

        public Process Start(LaunchCommand command, Action<string> log)
        {
            log?.Invoke("$ " + command);
            var process = new Process { StartInfo = command.ToStartInfo(redirectOutput: log != null), EnableRaisingEvents = true };
            if (log != null)
            {
                process.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
            }
            process.Start();
            if (log != null)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            return process;
        }
    }
}
