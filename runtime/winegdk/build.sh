#!/usr/bin/env bash
# Builds Weather-OS/WineGDK (Wine with the GDK runtime: xgameruntime, GameInput, Xodus interop) for Minecraft GDK
# builds, adds DXVK + vkd3d-proton, and installs it where BL.Linux looks for it.
#
#   PREFIX   install location (default: ~/.local/share/BedrockLauncher/runtimes/winegdk)
#   SRC      source checkout  (default: ~/.cache/BedrockLauncher/winegdk-src)
#   REF      branch/commit    (default: main)
#   JOBS     parallel jobs    (default: nproc)
#   WOW64    1 = new-style WoW64 (default), 0 = 64-bit only
#   NO_DXVK  1 = do not download DXVK / vkd3d-proton
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/Weather-OS/WineGDK}"
REF="${REF:-main}"
PREFIX="${PREFIX:-$HOME/.local/share/BedrockLauncher/runtimes/winegdk}"
SRC="${SRC:-$HOME/.cache/BedrockLauncher/winegdk-src}"
JOBS="${JOBS:-$(nproc)}"
WOW64="${WOW64:-1}"
NO_DXVK="${NO_DXVK:-0}"

log() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
need() { command -v "$1" >/dev/null 2>&1 || { echo "missing build tool: $1" >&2; exit 1; }; }
for tool in git make gcc flex bison x86_64-w64-mingw32-gcc curl tar python3; do need "$tool"; done
[ "$WOW64" = 1 ] && need i686-w64-mingw32-gcc

if [ ! -d "$SRC/.git" ]; then
    log "Cloning $REPO_URL"
    mkdir -p "$(dirname "$SRC")"
    git clone --filter=blob:none "$REPO_URL" "$SRC"
fi
cd "$SRC"
git fetch --quiet origin "$REF"
git -c advice.detachedHead=false checkout --quiet --force FETCH_HEAD
commit="$(git rev-parse HEAD)"
log "WineGDK $commit ($(cat VERSION))"

mkdir -p "$SRC/build"
cd "$SRC/build"
if [ "$WOW64" = 1 ]; then archs="i386,x86_64"; else archs="x86_64"; fi
if [ ! -f Makefile ] || [ "$SRC/configure" -nt Makefile ]; then
    log "Configuring (archs: $archs)"
    "$SRC/configure" --prefix="$PREFIX" --enable-archs="$archs" --disable-tests ${CONFIGURE_ARGS:-}
fi
log "Building with $JOBS jobs (this takes a while)"
make -j"$JOBS"
log "Installing to $PREFIX"
make install >/dev/null

# DXVK (D3D9-11) and vkd3d-proton (D3D12): Minecraft's GDK renderer needs them. The launcher copies them into the
# prefix (dxvk/x64/bin -> system32) with native overrides.
if [ "$NO_DXVK" != 1 ]; then
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
    fetch_release() { # repo, pattern -> extracted folder
        local url
        url="$(curl -fsSL "https://api.github.com/repos/$1/releases/latest" | python3 -c '
import json, sys
pat = sys.argv[1]
print(next((a["browser_download_url"] for a in json.load(sys.stdin)["assets"] if a["name"].endswith(pat)), ""))' "$2")"
        [ -n "$url" ] || { echo "no $2 asset in $1" >&2; return 1; }
        log "Downloading $(basename "$url")"
        curl -fsSL -o "$tmp/pkg" "$url"
        mkdir -p "$tmp/$1" && tar -xf "$tmp/pkg" -C "$tmp/$1" --strip-components=1
    }
    fetch_release doitsujin/dxvk ".tar.gz"
    fetch_release HansKristian-Work/vkd3d-proton ".tar.zst"
    for arch in x64 x32; do
        vk_arch=x64; [ "$arch" = x32 ] && vk_arch=x86
        mkdir -p "$PREFIX/dxvk/$arch/bin"
        cp "$tmp/doitsujin/dxvk/$arch/"*.dll "$PREFIX/dxvk/$arch/bin/"
        cp "$tmp/HansKristian-Work/vkd3d-proton/$vk_arch/"*.dll "$PREFIX/dxvk/$arch/bin/"
    done
    [ -f "$PREFIX/dxvk/x64/bin/d3d11.dll" ] && [ -f "$PREFIX/dxvk/x64/bin/d3d12.dll" ] || { echo "DXVK / vkd3d-proton layout changed" >&2; exit 1; }
fi

cat > "$PREFIX/bl-runtime.json" <<JSON
{
  "kind": "WineGdk",
  "name": "winegdk",
  "source": "$REPO_URL",
  "commit": "$commit",
  "wow64": $([ "$WOW64" = 1 ] && echo true || echo false),
  "dxvk": $([ "$NO_DXVK" = 1 ] && echo false || echo true),
  "built": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSON
log "Done: $(ls "$PREFIX"/bin/wine "$PREFIX"/bin/wine64 2>/dev/null | head -1)"
