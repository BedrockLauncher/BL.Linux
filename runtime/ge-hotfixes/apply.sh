#!/usr/bin/env bash
# Applies the GE-Proton wine hotfixes in this folder to the wine tree in the current directory.
# Each patch is checked first: the ones that do not fit this tree (another Wine version) are skipped and listed.
#   usage: apply.sh   (run from the wine source root)
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
shopt -s nullglob
for patch in "$here"/*.patch; do
    name="$(basename "$patch" .patch)"
    if git apply --check --whitespace=nowarn "$patch" 2>/dev/null; then
        git apply --whitespace=nowarn "$patch"
        printf '    GE-Proton hotfix: %s\n' "$name"
    else
        printf '    GE-Proton hotfix: %s (does not apply to this tree, skipped)\n' "$name"
    fi
done
