using System;
using System.Threading.Tasks;

namespace BedrockLauncher
{
    /// <summary>
    /// The core has no UI dependency; the Avalonia app plugs its dispatcher in here so that property changes raised by
    /// background work (downloads, extraction, game process) reach bindings on the UI thread.
    /// </summary>
    public static class UiThread
    {
        public static Func<bool> CheckAccess { get; set; } = () => true;
        public static Action<Action> Post { get; set; } = action => action();
        public static Func<Func<Task>, Task> InvokeAsync { get; set; } = func => func();

        public static void Run(Action action)
        {
            if (CheckAccess()) action();
            else Post(action);
        }
    }

    /// <summary>Dialogs and window operations the core asks the UI for.</summary>
    public interface IUiHost
    {
        Task ShowException(Exception ex);
        Task<bool> Confirm(string titleResource, string messageResource, params object[] args);
        Task ShowMessage(string title, string message);
        /// <summary>Asks for a folder (null when cancelled).</summary>
        Task<string> PickFolder(string title);
        /// <summary>Asks for a file (null when cancelled); filters are (name, glob patterns).</summary>
        Task<string> PickFile(string title, params (string Name, string[] Patterns)[] filters);
        void CloseLauncher();
    }
}
