# Compiling BL.Linux

## Launcher

```bash
# .NET 8 SDK (Ubuntu/Debian: sudo apt install dotnet-sdk-8.0, Fedora: sudo dnf install dotnet-sdk-8.0,
# Arch: sudo pacman -S dotnet-sdk-8.0, Gentoo: dev-dotnet/dotnet-sdk-bin)
git clone https://github.com/BedrockLauncher/BL.Linux
cd BL.Linux
dotnet run --project src/BedrockLauncher -c Release          # build and start
dotnet publish src/BedrockLauncher -c Release -r linux-x64 --self-contained -o out   # standalone build in out/
```

| Path | |
| --- | --- |
| `src/BedrockLauncher` | Avalonia app: a 1:1 port of the WPF pages (`BedrockLauncher/Pages`) and styles (`Resources/styles`) |
| `src/BedrockLauncher.Core` | Profiles, installations, versions, settings, downloads, the Linux package handler and runtimes (`Runtime/`) |
| `src/BedrockLauncher.UpdateProcessor` | Upstream's version catalog and Store download module, built for net8.0 |
| `runtime/wine-uwp` | Builds wine-uwp with the Minecraft patches ([README](../runtime/wine-uwp/README.md)) |
| `runtime/gdk-proton` | Installs GDK-Proton ([README](../runtime/gdk-proton/README.md)) |
| `tests/` | Unit tests, launcher pipeline tests with stand-in runtimes, a wine-uwp end-to-end test |
| `tools/Screenshots` | Renders every screen headlessly: `dotnet run --project tools/Screenshots -- out/ [locale]` |

## Tests

```bash
dotnet test                                                                      # no runtime needed
BL_TEST_WINEUWP=~/.local/share/BedrockLauncher/runtimes/wine-uwp dotnet test     # + wine-uwp end-to-end test (needs mingw-w64)
```

## Where things go

| | |
| --- | --- |
| Settings | `~/.config/BedrockLauncher/settings.json` |
| Profiles, installations, versions, runtimes, prefixes | `~/.local/share/BedrockLauncher/` (or the folder chosen in Settings) |
| Installation data (worlds, packs, options) | `~/.local/share/BedrockLauncher/installations/<profile>/<installation>/packageData` |
| Logs (launcher + one file per game launch) | `~/.cache/BedrockLauncher/logs` |

`BL_LINUX_HOME=<folder>` puts all of the above in one folder (useful for testing).
