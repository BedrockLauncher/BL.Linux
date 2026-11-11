# Software Prerequisites

- Linux x86_64 with a Vulkan driver (Mesa or NVIDIA) for DXVK / vkd3d-proton
- .NET 8 runtime (included in self-contained builds)
- A compatibility runtime for the builds you play:

| Minecraft build | Runtime | Setup |
| --- | --- | --- |
| UWP (before 1.21.120) | [wine-uwp](https://github.com/Rosentti/wine-uwp) with the BL.Linux patches | `runtime/wine-uwp/build.sh` ([details](../runtime/wine-uwp/README.md)) |
| GDK (1.21.120 and later) | [WineGDK](https://github.com/Weather-OS/WineGDK) (default) or [GDK-Proton](https://github.com/Weather-OS/GDK-Proton) | `runtime/winegdk/build.sh` (builds WineGDK + DXVK + vkd3d-proton) or `runtime/gdk-proton/install.sh` |
| GDK downloads | [Xodus](https://github.com/xodus-gaming/xodus): Xbox sign-in, MSIXVC download and licensing | `runtime/xodus/build.sh`, then Settings > Accounts > Sign in to Xodus |

A Microsoft account that owns Minecraft for Windows (Bedrock) is required to download or import versions
(Settings > Accounts). The sign-in uses the launcher's own Azure app registration: create one at portal.azure.com
(App registrations > New, "Personal Microsoft accounts only", Authentication > Allow public client flows = Yes) and
paste its Application (client) ID in Settings > Accounts (or set `BL_MSA_CLIENT_ID`).

## Minecraft license check

After the sign-in the launcher checks that the account owns Minecraft Bedrock, with two licensing sources:

1. **Minecraft services**, like the official Minecraft Launcher: Xbox Live token for `rp://api.minecraftservices.com/`,
   then the account's entitlements (`product_minecraft_bedrock` / `game_minecraft_bedrock`). Mojang only answers Azure
   apps it has approved ([request form](https://aka.ms/mce-reviewappid)); a new app gets "not approved".
2. **The Microsoft Store license**, through Xodus (`xodus-cli license`): asks licensing.mp.microsoft.com for the
   game's content license with your Xodus account. Used when (1) does not confirm ownership; sign in to Xodus in
   Settings > Accounts and press "Verify again".

Downloads stay blocked until one of them confirms the license. A confirmed answer is kept for 7 days when the
services cannot be reached; versions already installed always play.

## Playing online

| Build | Online |
| --- | --- |
| GDK on WineGDK | `runtime/winegdk/build.sh` turns on WineGDK's Xodus interop (`XODUS_INTEROP=1`): the game's Xbox sign-in (XUser) gets its token from `xodus-service`, which the launcher starts before the game. Needs Xodus built and signed in, and a running keyring (GNOME Keyring / KWallet). Experimental upstream. |
| GDK on GDK-Proton | LAN and servers that do not require Xbox sign-in. |
| UWP on wine-uwp | LAN and servers that do not require Xbox sign-in: the in-game Xbox sign-in uses Windows' token broker, which wine-uwp does not implement. |

GDK packages are encrypted: with Xodus the launcher downloads them from the catalog and Xodus licenses and decrypts
them with your Xbox account when the game starts. Without Xodus, the game folder is copied once from a Windows install
and imported ([details](../runtime/gdk-proton/README.md)).

Optional: [GameMode](https://github.com/FeralInteractive/gamemode) (`gamemoderun`) and
[MangoHud](https://github.com/flightlessmango/MangoHud), both toggled in Settings > Linux.
