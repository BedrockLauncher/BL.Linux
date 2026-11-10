#!/usr/bin/env bash
# Builds Rosentti/wine-uwp (a Wine fork implementing UWP/WinRT APIs) adapted for Minecraft UWP, plus its
# UWP-aware DXVK, and installs both where BL.Linux looks for the UWP runtime.
#
#   PREFIX   install location   (default: ~/.local/share/BedrockLauncher/runtimes/wine-uwp)
#   SRC      source checkout    (default: ~/.cache/BedrockLauncher/wine-uwp-src)
#   JOBS     parallel jobs      (default: nproc)
#   WOW64    1 = new-style WoW64 (i386 PE code inside the 64-bit build, no 32-bit host libraries needed)
#            0 = 64-bit only (Minecraft is x64; smallest/fastest build)
#   NO_DXVK  1 = skip dxvk-uwp (the prefix then uses wined3d)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_URL="${REPO_URL:-https://github.com/Rosentti/wine-uwp}"
# 15fd380d is the last wine-uwp commit before the "get upstream changes" merge (888416f1). That merge onto Wine
# 10.6 does not build: duplicate WerRegisterCustomMetadata, duplicate apiset entries, IAgileReference defined twice
# in the IDLs and a configure.ac that lists a missing dlls/windows.storage.applicationdata. 15fd380d is Wine 9.16
# plus every UWP change of the fork.
COMMIT="${COMMIT:-15fd380d6a700a80cfe83511a48a45091abc9ede}"
PREFIX="${PREFIX:-$HOME/.local/share/BedrockLauncher/runtimes/wine-uwp}"
SRC="${SRC:-$HOME/.cache/BedrockLauncher/wine-uwp-src}"
JOBS="${JOBS:-$(nproc)}"
WOW64="${WOW64:-1}"
NO_DXVK="${NO_DXVK:-0}"
# 1 = apply the esync (eventfd synchronization) patch set of wine-staging, the same code GE-Proton's wine carries.
ESYNC="${ESYNC:-1}"
STAGING_URL="${STAGING_URL:-https://github.com/wine-staging/wine-staging}"
STAGING_TAG="${STAGING_TAG:-v9.16}"   # wine-uwp is based on Wine 9.16
STAGING_SRC="${STAGING_SRC:-$HOME/.cache/BedrockLauncher/wine-staging-$STAGING_TAG}"
# Same optimisation flags as GE-Proton's Makefile.in (x86_64_CFLAGS / CFLAGS).
OPT_CFLAGS="${OPT_CFLAGS:--O2 -march=nocona -mtune=core-avx2 -mfpmath=sse -fwrapv -fno-strict-aliasing}"

log() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
need() { command -v "$1" >/dev/null 2>&1 || { echo "missing build tool: $1 (see runtime/wine-uwp/README.md)" >&2; exit 1; }; }

for tool in git make gcc flex bison autoconf x86_64-w64-mingw32-gcc python3; do need "$tool"; done
[ "$WOW64" = 1 ] && need i686-w64-mingw32-gcc
[ "$NO_DXVK" = 1 ] || { need meson; need ninja; need glslangValidator; }

# --- source -------------------------------------------------------------------------------------------
if [ ! -d "$SRC/.git" ]; then
    log "Cloning $REPO_URL"
    mkdir -p "$(dirname "$SRC")"
    git clone --filter=blob:none "$REPO_URL" "$SRC"
fi
cd "$SRC"
git fetch --quiet origin "$COMMIT" 2>/dev/null || git fetch --quiet origin
log "Checking out $COMMIT"
git -c advice.detachedHead=false checkout --quiet --force "$COMMIT"
git reset --quiet --hard "$COMMIT"
git clean --quiet -fdx -e build/
git submodule update --init --recursive --quiet || true

# --- BL.Linux adaptations -------------------------------------------------------------------------------
log "Applying BL.Linux adaptations"
python3 -I "$HERE/gen_app_crt_dlls.py" "$SRC"
shopt -s nullglob
for patch in "$HERE"/patches/*.patch; do
    log "  $(basename "$patch")"
    git apply --whitespace=nowarn "$patch"
done
shopt -u nullglob
# esync: wineserver round trips for events/semaphores/mutexes become eventfd operations (what WINEESYNC=1 enables in
# Proton). Only the patch sets it needs are taken; they apply cleanly on top of the fork.
if [ "$ESYNC" = 1 ]; then
    [ -d "$STAGING_SRC/.git" ] || git clone --quiet --depth 1 -b "$STAGING_TAG" "$STAGING_URL" "$STAGING_SRC"
    for set in server-Realtime_Priority server-PeekMessage server-Signal_Thread eventfd_synchronization; do
        log "  wine-staging $STAGING_TAG: $set"
        for patch in "$STAGING_SRC/patches/$set"/*.patch; do
            git apply --whitespace=nowarn "$patch"
        done
    done
    tools/make_requests
fi

# Regenerate the makefile list from the tree. Upstream's configure.ac (and configure) still list
# dlls/windows.storage.applicationdata, which is not in the repository, so a fresh clone does not configure
# without this step. make_makefiles only sees files git knows about, hence the add.
git add -A dlls programs include
tools/make_makefiles
autoconf --force && autoheader --force

# --- wine -----------------------------------------------------------------------------------------------
mkdir -p "$SRC/build/wine"
cd "$SRC/build/wine"
if [ "$WOW64" = 1 ]; then archs="i386,x86_64"; else archs="x86_64"; fi
if [ ! -f Makefile ] || [ "$SRC/configure" -nt Makefile ]; then
    log "Configuring wine-uwp (archs: $archs)"
    "$SRC/configure" --prefix="$PREFIX" --enable-archs="$archs" --disable-tests \
        CFLAGS="$OPT_CFLAGS" CROSSCFLAGS="$OPT_CFLAGS" ${CONFIGURE_ARGS:-}
fi
log "Building wine-uwp with $JOBS jobs (this takes a while)"
make -j"$JOBS"
log "Installing to $PREFIX"
make install >/dev/null

# --- dxvk-uwp -------------------------------------------------------------------------------------------
if [ "$NO_DXVK" != 1 ]; then
    cd "$SRC/dxvk-uwp"
    for bits in 64 32; do
        [ "$bits" = 32 ] && [ "$WOW64" != 1 ] && continue
        dir="x$bits"; [ "$bits" = 64 ] && dir="x64"
        log "Building dxvk-uwp (win$bits)"
        rm -rf "$SRC/build/dxvk-w$bits"
        meson setup --cross-file "build-win$bits.txt" --buildtype release \
            --prefix "$PREFIX/dxvk/$dir" "$SRC/build/dxvk-w$bits" >/dev/null
        ninja -C "$SRC/build/dxvk-w$bits" install >/dev/null
    done
fi

# --- runtime descriptor (read by the launcher) ------------------------------------------------------------
cat > "$PREFIX/bl-runtime.json" <<JSON
{
  "kind": "WineUwp",
  "name": "wine-uwp",
  "source": "$REPO_URL",
  "commit": "$COMMIT",
  "wow64": $([ "$WOW64" = 1 ] && echo true || echo false),
  "dxvk": $([ "$NO_DXVK" = 1 ] && echo false || echo true),
  "esync": $([ "$ESYNC" = 1 ] && echo true || echo false),
  "built": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSON
log "Done: $(ls "$PREFIX"/bin/wine "$PREFIX"/bin/wine64 2>/dev/null | head -1)"
