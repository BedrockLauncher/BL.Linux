#!/usr/bin/env bash
# Installs the latest GDK-Proton release (Weather-OS/GDK-Proton: GE-Proton built with WineGDK) where BL.Linux and
# Steam both find it.
#
#   DEST     install folder  (default: ~/.steam/root/compatibilitytools.d, else the launcher's runtimes folder)
#   TAG      release tag     (default: latest)
#   ONLINE=1 also set up online play in an imported Minecraft folder: GAME_DIR=<versions/<version>> (see below)
set -euo pipefail

REPO="${REPO:-Weather-OS/GDK-Proton}"
TAG="${TAG:-latest}"
log() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
for tool in curl tar python3; do command -v "$tool" >/dev/null || { echo "missing: $tool" >&2; exit 1; }; done

if [ -z "${DEST:-}" ]; then
    if [ -d "$HOME/.steam/root" ]; then DEST="$HOME/.steam/root/compatibilitytools.d"
    else DEST="${XDG_DATA_HOME:-$HOME/.local/share}/BedrockLauncher/runtimes"; fi
fi
mkdir -p "$DEST"

if [ "$TAG" = latest ]; then api="https://api.github.com/repos/$REPO/releases/latest"
else api="https://api.github.com/repos/$REPO/releases/tags/$TAG"; fi
log "Reading $api"
url="$(curl -fsSL "$api" | python3 -c '
import json, sys
assets = json.load(sys.stdin).get("assets", [])
names = [a for a in assets if a["name"].endswith((".tar.gz", ".tar.xz", ".tar.zst"))]
print(names[0]["browser_download_url"] if names else "")')"
[ -n "$url" ] || { echo "no tarball in the release; download it by hand from https://github.com/$REPO/releases" >&2; exit 1; }

tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
log "Downloading $(basename "$url")"
curl -fL --progress-bar -o "$tmp/gdk-proton.tar" "$url"
log "Extracting into $DEST"
tar -xf "$tmp/gdk-proton.tar" -C "$DEST"
log "Done. The launcher detects it (Settings > Linux); in Steam it appears as a compatibility tool."

# Online play (from the GDK-Proton README): Minecraft's XCurl.dll is replaced by MSYS2's libcurl and a CA bundle
# is placed next to the game. Microsoft account sign-in still does not work (XUser is not implemented).
if [ "${ONLINE:-0}" = 1 ]; then
    : "${GAME_DIR:?set GAME_DIR to the imported game folder (versions/<version>)}"
    command -v zstd >/dev/null || { echo "missing: zstd" >&2; exit 1; }
    mirror="https://mirror.msys2.org/mingw/mingw64"
    pkg="$(curl -fsSL "$mirror/" | grep -o 'mingw-w64-x86_64-curl-[0-9][^"]*-any\.pkg\.tar\.zst' | grep -v '\.sig' | sort -V | tail -1)"
    log "Downloading $pkg"
    curl -fsSL -o "$tmp/curl.pkg.tar.zst" "$mirror/$pkg"
    mkdir -p "$tmp/curl" && tar --zstd -xf "$tmp/curl.pkg.tar.zst" -C "$tmp/curl"
    [ -f "$GAME_DIR/XCurl.dll" ] && [ ! -f "$GAME_DIR/XCurl.dll.original" ] && cp "$GAME_DIR/XCurl.dll" "$GAME_DIR/XCurl.dll.original"
    cp "$tmp/curl/mingw64/bin/libcurl-4.dll" "$GAME_DIR/XCurl.dll"
    mkdir -p "$GAME_DIR/etc/ssl/certs"
    curl -fsSL -o "$GAME_DIR/etc/ssl/certs/ca-bundle.crt" https://curl.se/ca/cacert.pem
    log "XCurl.dll replaced (original kept as XCurl.dll.original). libcurl's own DLL dependencies must be present too: see the GDK-Proton README."
fi
