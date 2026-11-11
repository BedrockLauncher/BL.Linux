using System;
using System.IO;
using System.Threading.Tasks;
using BedrockLauncher.Runtime;
using Xunit;

namespace BedrockLauncher.Linux.Tests
{
    /// <summary>Changes XDG_RUNTIME_DIR, so it shares the singletons collection (runs alone).</summary>
    [Collection("App singletons")]
    public class XodusServiceTests : IDisposable
    {
        private readonly TempDir dir = new();
        private readonly string previousRuntimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

        public XodusServiceTests() => Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", dir.Sub("run"));

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", previousRuntimeDir);
            dir.Dispose();
        }

        /// <summary>A stand-in xodus-service: binds $XDG_RUNTIME_DIR/xodus.sock like the real one.</summary>
        private string FakeService()
        {
            string path = dir.Sub("xodus", "xodus-service");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(dir.Sub("xodus", "xodus-cli"), "#!/bin/sh\n");
            File.WriteAllText(path, "#!/usr/bin/env python3\nimport os, socket\n" +
                "s = socket.socket(socket.AF_UNIX)\ns.bind(os.environ['XDG_RUNTIME_DIR'] + '/xodus.sock')\ns.listen(4)\n" +
                "while True:\n    c, _ = s.accept()\n    c.close()\n");
            File.SetUnixFileMode(path, (UnixFileMode)0b111_101_101);
            return path;
        }

        [Fact]
        public async Task The_service_is_started_once_and_reused()
        {
            Directory.CreateDirectory(dir.Sub("run"));
            string service = FakeService();
            Assert.Equal(service, XodusService.Locate(dir.Sub("xodus", "xodus-cli")));
            File.WriteAllText(dir.Sub("run", "xodus.sock"), "stale");

            var xodus = new XodusService();
            try
            {
                Assert.True(await xodus.EnsureRunningAsync(service, ProcessRunner.Default, _ => { }));
                Assert.True(XodusService.IsListening(dir.Sub("run", "xodus.sock")));
                string log = "";
                Assert.True(await xodus.EnsureRunningAsync(service, ProcessRunner.Default, l => log += l));
                Assert.Contains("already listening", log);
            }
            finally
            {
                xodus.Stop();
            }
        }

        [Fact]
        public async Task A_missing_service_is_reported()
        {
            Directory.CreateDirectory(dir.Sub("run"));
            Assert.False(await new XodusService().EnsureRunningAsync(dir.Sub("nope"), ProcessRunner.Default, _ => { }));
        }

        [Fact]
        public void The_runtime_descriptor_says_whether_xodus_interop_is_built_in()
        {
            Directory.CreateDirectory(dir.Sub("rt"));
            var runtime = new GameRuntime { Kind = GameRuntimeKind.WineGdk, Name = "winegdk", RootPath = dir.Sub("rt") };
            Assert.False(runtime.UsesXodusInterop);
            File.WriteAllText(dir.Sub("rt", "bl-runtime.json"), "{\"kind\":\"WineGdk\",\"xodusInterop\":true}");
            Assert.True(runtime.UsesXodusInterop);
            File.WriteAllText(dir.Sub("rt", "bl-runtime.json"), "{\"kind\":\"WineGdk\",\"xodusInterop\":false}");
            Assert.False(runtime.UsesXodusInterop);
        }
    }
}
