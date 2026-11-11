#!/usr/bin/env python3
"""Joins the *.entry.json files written by package.sh (and gdk-proton/entry.sh) into runtimes.json.

    make-manifest.py <dir with entries> <release download base URL> > runtimes.json

Entries with a "file" are hosted on the release (url = base/file); entries with a "url" point elsewhere
(GDK-Proton's own release)."""
import datetime, glob, json, os, sys

folder, base = sys.argv[1], sys.argv[2].rstrip("/")
order = {"wine-uwp": 0, "winegdk": 1, "gdk-proton": 2, "xodus": 3}
runtimes = []
for path in sorted(glob.glob(os.path.join(folder, "*.entry.json"))):
    entry = json.load(open(path))
    if "url" not in entry:
        entry["url"] = f"{base}/{entry.pop('file')}"
    entry.pop("file", None)
    for key in ("id", "kind", "version", "url", "sha256", "folder"):
        if not entry.get(key):
            sys.exit(f"{path}: missing {key}")
    runtimes.append(entry)
runtimes.sort(key=lambda e: order.get(e["id"], 99))
json.dump({"format": 1, "generated": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
           "runtimes": runtimes}, sys.stdout, indent=2)
print()
