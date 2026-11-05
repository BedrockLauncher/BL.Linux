# GDK builds (1.21.120 and later)

Since 1.21.120 the Windows edition of Minecraft is a GDK game. Its packages (`.msixvc`) are encrypted.

**With [Xodus](https://github.com/xodus-gaming/xodus)** (`runtime/xodus/build.sh`, then Settings > Accounts > Sign in to
Xodus) the launcher downloads the version from the catalog (`xodus-cli streaming <url> <folder>`) and starts it with
`xodus-cli run <folder> <WineGDK wine>`, which gets the license from your Xbox account and decrypts the executable in
memory. The account must own Minecraft.

**Without Xodus** the game folder has to come from a Windows install:

1. On Windows (or in a Windows VM) install the version with the Xbox app or with BedrockLauncher.
2. Copy the game folder from inside the package context, where Windows serves the files decrypted (PowerShell's
   `Invoke-CommandInDesktopPackage` running a copy tool; [jphein/minecraft-bedrock-linux](https://github.com/jphein/minecraft-bedrock-linux)
   has scripts for it). A plain copy of `XboxGames` gives encrypted files: the launcher checks for the `MZ` header and
   refuses them.
3. In BL.Linux press Play on the GDK version (or Settings > Versions > *Import GDK game folder*) and pick the copy.

The game then runs with a GDK runtime:

| Runtime | Install | Notes |
| --- | --- | --- |
| [WineGDK](https://github.com/Weather-OS/WineGDK) (default) | `../winegdk/build.sh` | Wine with the GDK runtime (xgameruntime, GameInput, Xodus interop); the script adds DXVK + vkd3d-proton. |
| [GDK-Proton](https://github.com/Weather-OS/GDK-Proton) | `./install.sh`, then Settings > Linux > GDK-Proton | GE-Proton built with WineGDK, also usable from Steam. |

Each installation keeps its own data: inside the prefix `%APPDATA%\Minecraft Bedrock` (Preview: `Minecraft Bedrock
Preview`) is a symlink to the installation's `packageData` folder; existing data is kept as `.default`.

Known limits (upstream runtimes): Microsoft account sign-in does not work (XUser is not implemented), so no Realms or
featured servers; LAN and self-hosted servers work. `ONLINE=1 GAME_DIR=... ./install.sh` applies the XCurl
replacement from the GDK-Proton README for online play.
