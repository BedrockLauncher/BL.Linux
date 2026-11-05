#!/usr/bin/env python3
"""Adds the UWP ("_app") flavours of the VC++ 14 runtime to a wine-uwp tree.

UWP builds of Minecraft import msvcp140_app.dll, vcruntime140_app.dll, vcruntime140_1_app.dll and
concrt140_app.dll from the Microsoft.VCLibs.140.00 framework package. Wine only ships the desktop
flavours, so without the framework package the game fails to load. Every export of an _app DLL is
the same as its desktop twin, so each generated module is a pure forwarder (like msvcp120_app ->
msvcp120 upstream).

Usage: gen_app_crt_dlls.py <wine source dir>
"""
import re
import sys
from pathlib import Path

# (_app module, desktop module it forwards to)
MODULES = [
    ("msvcp140_app", "msvcp140"),
    ("vcruntime140_app", "vcruntime140"),
    ("vcruntime140_1_app", "vcruntime140_1"),
    ("concrt140_app", "concrt140"),
]

# "@ <type> [-flags...] name(args) [target]" or "@ extern [-flags] name [target]"
FUNC = re.compile(r"^(@|\d+)\s+(cdecl|stdcall|thiscall|fastcall|varargs)\s+((?:-\S+\s+)*)([^\s(]+)(\([^)]*\))")
EXTERN = re.compile(r"^(@|\d+)\s+extern\s+((?:-\S+\s+)*)(\S+)")


def forward_spec(source: str, target: str) -> str:
    out = []
    for line in source.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            out.append(line)
            continue
        m = FUNC.match(stripped)
        if m:
            ordinal, conv, flags, name, args = m.groups()
            out.append(f"{ordinal} {conv} {flags}{name}{args} {target}.{name}")
            continue
        m = EXTERN.match(stripped)
        if m:
            ordinal, flags, name = m.groups()
            out.append(f"{ordinal} extern {flags}{name} {target}.{name}")
            continue
        # stubs and anything unusual stay as they are
        out.append(line)
    return "\n".join(out) + "\n"


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__, file=sys.stderr)
        return 2
    root = Path(sys.argv[1])
    configure_ac = root / "configure.ac"
    if not configure_ac.is_file():
        print(f"{root} is not a wine source tree", file=sys.stderr)
        return 1

    ac = configure_ac.read_text()
    for module, target in MODULES:
        spec = root / "dlls" / target / f"{target}.spec"
        if not spec.is_file():
            print(f"missing {spec}", file=sys.stderr)
            return 1
        dest = root / "dlls" / module
        dest.mkdir(exist_ok=True)
        (dest / "Makefile.in").write_text(f"MODULE    = {module}.dll\nIMPORTLIB = {module}\n")
        (dest / f"{module}.spec").write_text(forward_spec(spec.read_text(), target))

        entry = f"WINE_CONFIG_MAKEFILE(dlls/{module})"
        if entry not in ac:
            anchor = f"WINE_CONFIG_MAKEFILE(dlls/{target})"
            if anchor not in ac:
                print(f"cannot place {entry}: {anchor} not in configure.ac", file=sys.stderr)
                return 1
            ac = ac.replace(anchor, anchor + "\n" + entry, 1)
        print(f"generated dlls/{module} -> {target}")

    configure_ac.write_text(ac)
    return 0


if __name__ == "__main__":
    sys.exit(main())
