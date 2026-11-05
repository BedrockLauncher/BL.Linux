#!/usr/bin/env bash
# Builds BedrockLauncher-x86_64.AppImage in the repository root.
#
#   needs: .NET 8 SDK; appimagetool (taken from PATH, $APPIMAGETOOL, or downloaded from AppImage/appimagetool)
#   APPDIR_ONLY=1   stop after building the AppDir (build/BedrockLauncher.AppDir)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
ARCH=x86_64
APPDIR="$ROOT/build/BedrockLauncher.AppDir"
OUT="$ROOT/BedrockLauncher-$ARCH.AppImage"
log() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }

command -v dotnet >/dev/null || { echo "missing: dotnet (.NET 8 SDK)" >&2; exit 1; }

log "Publishing the launcher (self-contained, linux-x64)"
rm -rf "$APPDIR"
dotnet publish "$ROOT/src/BedrockLauncher" -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
    -o "$APPDIR/usr/bin" >/dev/null

log "Assembling $APPDIR"
install -m 755 "$HERE/AppRun" "$APPDIR/AppRun"
install -m 644 "$HERE/bedrocklauncher.desktop" "$APPDIR/bedrocklauncher.desktop"
install -m 644 "$HERE/bedrocklauncher.png" "$APPDIR/bedrocklauncher.png"
install -D -m 644 "$HERE/bedrocklauncher.desktop" "$APPDIR/usr/share/applications/bedrocklauncher.desktop"
install -D -m 644 "$HERE/bedrocklauncher.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/bedrocklauncher.png"
# Runtime setup scripts travel with the launcher (wine-uwp build, GDK-Proton install).
mkdir -p "$APPDIR/usr/share/bedrocklauncher"
cp -r "$ROOT/runtime" "$APPDIR/usr/share/bedrocklauncher/"
ln -sf bedrocklauncher.png "$APPDIR/.DirIcon"

if [ "${APPDIR_ONLY:-0}" = 1 ]; then log "AppDir ready: $APPDIR"; exit 0; fi

tool="${APPIMAGETOOL:-$(command -v appimagetool || true)}"
if [ -z "$tool" ]; then
    tool="$ROOT/build/appimagetool-$ARCH.AppImage"
    if [ ! -x "$tool" ]; then
        log "Downloading appimagetool"
        curl -fL --progress-bar -o "$tool" "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-$ARCH.AppImage"
        chmod +x "$tool"
    fi
fi

log "Packing $OUT"
# Without FUSE the tool can still run by extracting itself.
APPIMAGE_EXTRACT_AND_RUN=1 ARCH=$ARCH "$tool" --no-appstream "$APPDIR" "$OUT"
log "Done: $OUT"
