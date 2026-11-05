# GDK builds (1.21.120 and later)

Since 1.21.120 the Windows edition of Minecraft is a GDK game. Its packages (`.msixvc`) are encrypted and only Gaming
Services on Windows can install them, so **Linux cannot install a GDK version from the download**. What runs on Linux
is an already installed game folder:

1. On Windows (or in a Windows VM) install the version with the Xbox app or with BedrockLauncher.
2. Copy the game folder from inside the package context, where Windows serves the files decrypted (PowerShell's
   `Invoke-CommandInDesktopPackage` running a copy tool; [jphein/minecraft-bedrock-linux](https://github.com/jphein/minecraft-bedrock-linux)
   has scripts for it). A plain copy of `XboxGames` gives encrypted files: the launcher checks for the `MZ` header and
   refuses them.
3. In BL.Linux press Play on the GDK version (or Settings > Versions > *Import GDK game folder*) and pick the copy.

The game then runs with a GDK runtime:

| Runtime | Install | Notes |
| --- | --- | --- |
| [GDK-Proton](https://github.com/Weather-OS/GDK-Proton) (default) | `./install.sh` | GE-Proton built with WineGDK. Extracted to `~/.steam/root/compatibilitytools.d`, also usable from Steam. |
| [WineGDK](https://github.com/Weather-OS/WineGDK) | build it, set its folder in Settings > Linux | Plain Wine with GDK support; community reports say it runs 1.26.x where GDK-Proton does not. |

Each installation keeps its own data: inside the prefix `%APPDATA%\Minecraft Bedrock` (Preview: `Minecraft Bedrock
Preview`) is a symlink to the installation's `packageData` folder; existing data is kept as `.default`.

Known limits (upstream runtimes): Microsoft account sign-in does not work (XUser is not implemented), so no Realms or
featured servers; LAN and self-hosted servers work. `ONLINE=1 GAME_DIR=... ./install.sh` applies the XCurl
replacement from the GDK-Proton README for online play.
