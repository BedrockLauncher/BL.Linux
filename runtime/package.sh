#!/usr/bin/env bash
# Packs a built runtime for the launcher's automatic download (RuntimeInstaller) and writes its runtimes.json entry.
#
#   package.sh <id> <kind> <install dir> <out dir> <version> [description]
#     id       wine-uwp | winegdk | xodus
#     kind     WineUwp | WineGdk | Xodus
#   Produces <out dir>/<id>-x86_64.tar.xz (one top-level folder named <id>) and <out dir>/<id>.entry.json.
set -euo pipefail
id="$1"; kind="$2"; src="$3"; out="$4"; version="$5"; description="${6:-}"
[ -d "$src" ] || { echo "no such folder: $src" >&2; exit 1; }
mkdir -p "$out"
stage="$(mktemp -d)"; trap 'rm -rf "$stage"' EXIT
cp -a "$src" "$stage/$id"
rm -f "$stage/$id/.bl-installed.json"
archive="$out/$id-x86_64.tar.xz"
tar --sort=name --owner=0 --group=0 --numeric-owner -C "$stage" -cf - "$id" | xz -T0 -6 > "$archive"
sha="$(sha256sum "$archive" | cut -d' ' -f1)"
size="$(stat -c %s "$archive")"
python3 -I - "$out/$id.entry.json" "$id" "$kind" "$version" "$(basename "$archive")" "$sha" "$size" "$description" <<'PY'
import json, sys
path, id_, kind, version, file, sha, size, description = sys.argv[1:]
json.dump({"id": id_, "kind": kind, "version": version, "file": file, "sha256": sha, "size": int(size),
           "folder": id_, "description": description}, open(path, "w"), indent=2)
PY
echo "$archive $sha $size"
