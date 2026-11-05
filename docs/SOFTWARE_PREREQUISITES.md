# Software Prerequisites

- Linux x86_64 with a Vulkan driver (Mesa or NVIDIA) for DXVK / vkd3d-proton
- .NET 8 runtime (included in self-contained builds)
- A compatibility runtime for the builds you play:

| Minecraft build | Runtime | Setup |
| --- | --- | --- |
| UWP (before 1.21.120) | [wine-uwp](https://github.com/Rosentti/wine-uwp) with the BL.Linux patches | `runtime/wine-uwp/build.sh` ([details](../runtime/wine-uwp/README.md)) |
| GDK (1.21.120 and later) | [WineGDK](https://github.com/Weather-OS/WineGDK) (default) or [GDK-Proton](https://github.com/Weather-OS/GDK-Proton) | `runtime/winegdk/build.sh` (builds WineGDK + DXVK + vkd3d-proton) or `runtime/gdk-proton/install.sh` |
| GDK downloads | [Xodus](https://github.com/xodus-gaming/xodus): Xbox sign-in, MSIXVC download and licensing | `runtime/xodus/build.sh`, then Settings > Accounts > Sign in to Xodus |

A Microsoft account is required to download or import versions (Settings > Accounts). The sign-in uses the launcher's
own Azure app registration: create one at portal.azure.com (App registrations > New, "Personal Microsoft accounts
only", Authentication > Allow public client flows = Yes) and paste its Application (client) ID in Settings > Accounts
(or set `BL_MSA_CLIENT_ID`).

GDK packages are encrypted: with Xodus the launcher downloads them from the catalog and Xodus licenses and decrypts
them with your Xbox account when the game starts. Without Xodus, the game folder is copied once from a Windows install
and imported ([details](../runtime/gdk-proton/README.md)).

Optional: [GameMode](https://github.com/FeralInteractive/gamemode) (`gamemoderun`) and
[MangoHud](https://github.com/flightlessmango/MangoHud), both toggled in Settings > Linux.
