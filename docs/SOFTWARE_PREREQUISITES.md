# Software Prerequisites

- Linux x86_64 with a Vulkan driver (Mesa or NVIDIA) for DXVK / vkd3d-proton
- .NET 8 runtime (included in self-contained builds)
- A compatibility runtime for the builds you play:

| Minecraft build | Runtime | Setup |
| --- | --- | --- |
| UWP (before 1.21.120) | [wine-uwp](https://github.com/Rosentti/wine-uwp) with the BL.Linux patches | `runtime/wine-uwp/build.sh` ([details](../runtime/wine-uwp/README.md)) |
| GDK (1.21.120 and later) | [GDK-Proton](https://github.com/Weather-OS/GDK-Proton) (default) or [WineGDK](https://github.com/Weather-OS/WineGDK) | `runtime/gdk-proton/install.sh` ([details](../runtime/gdk-proton/README.md)) |

GDK builds cannot be downloaded and installed on Linux: their packages are encrypted and only Windows' Gaming Services
can install them. The game folder is copied once from a Windows install and imported into the launcher.

Optional: [GameMode](https://github.com/FeralInteractive/gamemode) (`gamemoderun`) and
[MangoHud](https://github.com/flightlessmango/MangoHud), both toggled in Settings > Linux.
