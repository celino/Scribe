#!/bin/sh
# Builds dist/Scribe-<version>.zip with GameData/ at the root (the layout SpaceDock and CKAN expect), plus README.md,
# CHANGELOG.md and LICENSE. The DLL is not in git: build it first (see README, "Building from source"); it is read from
# GameData/Scribe/Plugins/Scribe.dll, which Source/Scribe.csproj does not write, so copy Source/out/Scribe.dll there.
set -eu
cd "$(dirname "$0")/.."

dll=GameData/Scribe/Plugins/Scribe.dll
[ -f "$dll" ] || { echo "missing $dll: build Source/ and copy Source/out/Scribe.dll there" >&2; exit 1; }

version=$(python3 -c 'import json; v = json.load(open("GameData/Scribe/Scribe.version"))["VERSION"]; print("%d.%d.%d" % (v["MAJOR"], v["MINOR"], v["PATCH"]))')
out="dist/Scribe-${version}.zip"

rm -rf dist
mkdir -p dist
python3 - "$out" <<'PY'
import os, sys, zipfile
out = sys.argv[1]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk("GameData"):
        for name in sorted(files):
            z.write(os.path.join(root, name))
    for name in ("README.md", "CHANGELOG.md", "LICENSE"):
        z.write(name)
PY
echo "$out"
