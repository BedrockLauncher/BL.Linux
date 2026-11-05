using System;

namespace BedrockLauncher.Exceptions
{
    /// <summary>Base of the errors shown in the launcher's error screen.</summary>
    public class PackageManagerException : Exception
    {
        public PackageManagerException(string message, Exception inner = null) : base(message, inner) { }
    }

    public class RuntimeNotFoundException : PackageManagerException
    {
        public RuntimeNotFoundException(string message) : base(message) { }
    }

    public class PackageDownloadFailedException : PackageManagerException
    {
        public PackageDownloadFailedException(Exception inner) : base("The package could not be downloaded: " + inner.Message, inner) { }
    }

    public class PackageExtractionFailedException : PackageManagerException
    {
        public PackageExtractionFailedException(Exception inner) : base("The package could not be extracted: " + inner.Message, inner) { }
    }

    public class GdkFilesRequiredException : PackageManagerException
    {
        public GdkFilesRequiredException(string message) : base(message) { }
    }

    public class AppLaunchFailedException : PackageManagerException
    {
        public AppLaunchFailedException(Exception inner) : base("Minecraft could not be started: " + inner.Message, inner) { }
    }

    public class SaveRedirectionFailedException : PackageManagerException
    {
        public SaveRedirectionFailedException(Exception inner) : base("The installation's data folder could not be linked: " + inner.Message, inner) { }
    }
}
