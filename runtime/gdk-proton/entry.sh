#!/usr/bin/env bash
# Writes the runtimes.json entry for the latest Weather-OS/GDK-Proton release: the archive stays on GDK-Proton's own
# release, the entry records its URL, SHA-256 and top-level folder so the launcher can verify and unpack it.
#   entry.sh <out dir>        (GITHUB_TOKEN is used for the API call when set)
set -euo pipefail
out="$1"; repo="${REPO:-Weather-OS/GDK-Proton}"
mkdir -p "$out"
auth=(); [ -n "${GITHUB_TOKEN:-}" ] && auth=(-H "Authorization: Bearer $GITHUB_TOKEN")
read -r tag url < <(curl -fsSL "${auth[@]}" "https://api.github.com/repos/$repo/releases/latest" | python3 -I -c '
import json, sys
release = json.load(sys.stdin)
assets = [a for a in release.get("assets", []) if a["name"].endswith((".tar.gz", ".tar.xz", ".tar.zst"))]
print(release["tag_name"], assets[0]["browser_download_url"] if assets else "")')
[ -n "$url" ] || { echo "no archive in $repo $tag" >&2; exit 1; }
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
curl -fsSL -o "$tmp/a" "$url"
sha="$(sha256sum "$tmp/a" | cut -d' ' -f1)"; size="$(stat -c %s "$tmp/a")"
folder="$(tar -tf "$tmp/a" | head -1 | cut -d/ -f1)"
[ -n "$folder" ] && tar -tf "$tmp/a" | grep -q "^$folder/proton$" || { echo "unexpected layout in $url" >&2; exit 1; }
python3 -I - "$out/gdk-proton.entry.json" "$tag" "$url" "$sha" "$size" "$folder" <<'PY'
import json, sys
path, tag, url, sha, size, folder = sys.argv[1:]
json.dump({"id": "gdk-proton", "kind": "GdkProton", "version": tag, "url": url, "sha256": sha, "size": int(size),
           "folder": folder, "description": "Weather-OS/GDK-Proton (upstream release)"}, open(path, "w"), indent=2)
PY
echo "gdk-proton $tag $folder $sha"
