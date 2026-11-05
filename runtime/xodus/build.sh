#!/usr/bin/env bash
# Builds xodus-gaming/xodus (Xbox login, MSIXVC download and licensing for GDK games on Linux) and installs
# xodus-cli / xodus-service where BL.Linux looks for them.
#
#   needs: Rust >= 1.98 (rustup), protoc (protobuf compiler), webkit2gtk 4.1 + libsoup 3 dev files (login window),
#          a Secret Service keyring (GNOME Keyring / KWallet) for the stored credentials
#   PREFIX   install folder (default: ~/.local/share/BedrockLauncher/runtimes/xodus)
#   SRC      source checkout (default: ~/.cache/BedrockLauncher/xodus-src)
#   REF      branch/commit  (default: main)
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/xodus-gaming/xodus}"
REF="${REF:-main}"
PREFIX="${PREFIX:-$HOME/.local/share/BedrockLauncher/runtimes/xodus}"
SRC="${SRC:-$HOME/.cache/BedrockLauncher/xodus-src}"
log() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
for tool in git cargo protoc; do command -v "$tool" >/dev/null || { echo "missing: $tool" >&2; exit 1; }; done

if [ ! -d "$SRC/.git" ]; then
    log "Cloning $REPO_URL"
    mkdir -p "$(dirname "$SRC")"
    git clone --filter=blob:none "$REPO_URL" "$SRC"
fi
cd "$SRC"
git fetch --quiet origin "$REF"
git -c advice.detachedHead=false checkout --quiet --force FETCH_HEAD
log "Building xodus $(git rev-parse --short HEAD)"
cargo build --release --workspace
mkdir -p "$PREFIX"
install -m 755 target/release/xodus-cli "$PREFIX/xodus-cli"
[ -f target/release/xodus-service ] && install -m 755 target/release/xodus-service "$PREFIX/xodus-service"
git rev-parse HEAD > "$PREFIX/commit"
log "Done: $PREFIX/xodus-cli (sign in from the launcher: Settings > Accounts > Xodus)"
