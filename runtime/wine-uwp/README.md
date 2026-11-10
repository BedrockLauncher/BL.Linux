# wine-uwp for BL.Linux

UWP builds of Minecraft (before 1.21.120) run with [Rosentti/wine-uwp](https://github.com/Rosentti/wine-uwp), a Wine
fork that implements the UWP/WinRT APIs the game uses. `build.sh` builds it with the BL.Linux adaptations and installs
it where the launcher finds it (`~/.local/share/BedrockLauncher/runtimes/wine-uwp`).

```bash
# Debian/Ubuntu build dependencies (64-bit only build)
sudo apt install gcc-mingw-w64-x86-64 g++-mingw-w64-x86-64 gcc-mingw-w64-i686 flex bison autoconf make gcc \
    pkg-config libx11-dev libxext-dev libxrandr-dev libxi-dev libxcursor-dev libxcomposite-dev libfreetype-dev \
    libvulkan-dev libgnutls28-dev libpulse-dev libasound2-dev libwayland-dev libxkbcommon-dev libegl-dev \
    libgstreamer-plugins-base1.0-dev libsdl2-dev libudev-dev libdbus-1-dev meson ninja-build glslang-tools

WOW64=0 ./build.sh          # 64-bit only (Minecraft is x64), the fastest build
./build.sh                  # new-style WoW64 (also runs 32-bit PE code)
NO_DXVK=1 ./build.sh        # skip dxvk-uwp (OpenGL through wined3d)
```

## What BL.Linux changes

| Change | Why |
| --- | --- |
| Pinned to `15fd380d` | The fork's newest commit (`888416f1`, merge onto Wine 10.6) does not build: duplicate `WerRegisterCustomMetadata`, duplicate apiset entries, `IAgileReference` defined twice in the IDLs and a `configure.ac` that lists the missing `dlls/windows.storage.applicationdata`. `15fd380d` is Wine 9.16 with every UWP change of the fork. |
| `gen_app_crt_dlls.py` | Adds `msvcp140_app`, `vcruntime140_app`, `vcruntime140_1_app` and `concrt140_app` as forwarders to the desktop VC++ runtime. UWP Minecraft imports them from the `Microsoft.VCLibs.140.00` framework package, which Wine does not have. |
| `patches/0001-windows.storage-…` | `ApplicationData.LocalFolder` / `RoamingFolder` / `TemporaryFolder` returned the executable's folder, so worlds ended up inside the version folder. They now resolve to `%WINEUWP_PACKAGE_DATA%\{LocalState,RoamingState,TempState}` (the launcher points it at each installation's `packageData` folder, the same layout as `%LOCALAPPDATA%\Packages\<family>` on Windows), or `<exe dir>\<state>` when unset. |
| `tools/make_makefiles` before configure | Regenerates the makefile list from the tree. |
| esync (`ESYNC=1`, default) | Applies wine-staging v9.16's `eventfd_synchronization` patch set (and the three sets it depends on): the esync code GE-Proton's wine carries. Wine events, semaphores and mutexes become eventfd operations instead of wineserver round trips. The launcher starts the game with `WINEESYNC=1` and a raised open-file limit. |
| GE-Proton build flags | `-O2 -march=nocona -mtune=core-avx2 -mfpmath=sse -fwrapv -fno-strict-aliasing` for the Unix and PE sides (`OPT_CFLAGS`), as in GE-Proton's `Makefile.in`. |

## What "like GE-Proton" covers

Taken from GE-Proton: its compiler flags, the esync patch set, and the environment its `proton` script sets
(`WINEESYNC` / `WINEFSYNC` / `WINENTSYNC`, quiet DXVK / vkd3d logging, persistent shader caches). WineGDK is Wine 11,
which has ntsync built in (used when the kernel provides `/dev/ntsync`, 6.14+), and gets DXVK + vkd3d-proton.

Not taken: fsync (futex-based; its patches are written against Valve's wine tree, not vanilla 9.16) and Valve's
game-specific patches. No frame-rate comparison against GE-Proton has been made.

## Verified

`tests/BedrockLauncher.Linux.Tests` (`WineUwpIntegrationTests`, run with `BL_TEST_WINEUWP=<install dir>`) extracts an
appx holding `tests/fake-game` (a stand-in for `Minecraft.Windows.exe` built with mingw), creates the prefix, installs
dxvk-uwp, launches it through the launcher's command builder and checks that:

- the four `*_app` runtime DLLs load and export their functions;
- `ApplicationData.Current.LocalFolder` is the installation's `packageData\LocalState`;
- the world file lands in `packageData/LocalState/games/com.mojang/minecraftWorlds/`.

Not verified here: the real game. The fork's own notes list Minecraft as hanging after it reads `AnalyticsInfo`
(heap validation error), and mouse/keyboard input as not implemented yet.
