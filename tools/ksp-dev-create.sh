#!/bin/sh
# Builds a throw-away KSP test install from a KSP 1.12.5 copy you already own.
# Nothing is downloaded except mods (through CKAN); the game itself is never modified.
#
#   tools/ksp-dev-create.sh --ksp <KSP dir> --dest <new dir> [--scenario minimal|contracts|kct]
#                           [--ckan <ckan.exe>] [--copy]
#
# What it does:
#   1. checks the game version (buildID.txt = 03190, KSP 1.12.5) and the free disk space;
#   2. creates <dest> with the game files hard-linked from <KSP dir> (--copy: real copies; the
#      default falls back to a copy when the two folders are on different filesystems);
#      GameData holds only Squad, SquadExpansion (if present) and the mods of the scenario;
#   3. installs the scenario's mods with CKAN (tests/ingame/scenarios.json lists id + version);
#   4. copies the synthetic save (tests/ingame/save, craft included) as saves/scribe-test;
#   5. installs the Scribe plugin (GameData/Scribe) from this checkout.
# To undo: delete <dest>. Nothing outside it is changed.
set -eu
here="$(cd "$(dirname "$0")/.." && pwd)"
ksp=""; dest=""; scenario=minimal; ckan="${CKAN:-$HOME/ckan/ckan.exe}"; mode=link
while [ $# -gt 0 ]; do
	case "$1" in
		--ksp) ksp="$2"; shift 2 ;;
		--dest) dest="$2"; shift 2 ;;
		--scenario) scenario="$2"; shift 2 ;;
		--ckan) ckan="$2"; shift 2 ;;
		--copy) mode=copy; shift ;;
		-h|--help) sed -n '2,17p' "$0"; exit 0 ;;
		*) echo "unknown option: $1" >&2; exit 2 ;;
	esac
done
[ -n "$ksp" ] && [ -n "$dest" ] || { echo "usage: $0 --ksp <dir> --dest <dir> [--scenario ...]" >&2; exit 2; }
[ ! -e "$dest" ] || { echo "$dest already exists; remove it first" >&2; exit 1; }
[ -x "$ksp/KSP.x86_64" ] || { echo "$ksp/KSP.x86_64 not found (Linux build of KSP expected)" >&2; exit 1; }

# 1. version and disk space
build=$(sed -n 's/^build id = //p' "$ksp/buildID.txt")
[ "$build" = 03190 ] || { echo "expected KSP 1.12.5 (build id 03190), found '$build'" >&2; exit 1; }
need_kb=$(du -sk "$ksp/KSP_Data" | cut -f1)
free_kb=$(df -Pk "$(dirname "$dest")" | awk 'NR==2 {print $4}')
if [ "$mode" = copy ] && [ "$free_kb" -lt $((need_kb * 2)) ]; then echo "not enough free space for --copy" >&2; exit 1; fi

# scenario -> mod list (python3 reads the JSON; no jq needed)
mods=$(python3 -I - "$here/tests/ingame/scenarios.json" "$scenario" <<'PY'
import json, sys
s = json.load(open(sys.argv[1]))["scenarios"].get(sys.argv[2])
if s is None: sys.exit("unknown scenario: " + sys.argv[2])
if s["mods"] == "install-as-is": sys.exit("scenario %s uses the install as it is; nothing to create" % sys.argv[2])
for m in s["mods"]: print("%s=%s" % (m["id"], m["version"]))
PY
)

# 2. game files (everything except GameData, saves, logs and the personal CKAN folder)
link() { if [ "$mode" = link ] && cp -al "$1" "$2" 2>/dev/null; then :; else cp -a "$1" "$2"; fi; }
mkdir -p "$dest/GameData" "$dest/saves"
for f in "$ksp"/*; do
	n=$(basename "$f")
	case "$n" in GameData|saves|Screenshots|thumbs|@thumbs|CKAN|Logs|PluginData|KSP.log|*.log|*.bak*|Ships|Missions|KCTVesselSave|PartDatabase.cfg|settings.cfg|Plugins) continue ;; esac
	link "$f" "$dest/$n"
done
for n in Squad SquadExpansion; do [ -d "$ksp/GameData/$n" ] && link "$ksp/GameData/$n" "$dest/GameData/$n"; done
mkdir -p "$dest/Plugins" "$dest/Ships/VAB" "$dest/Ships/SPH" "$dest/Ships/@thumbs"
[ -d "$ksp/Missions" ] && link "$ksp/Missions" "$dest/Missions"

# 3. mods through CKAN
if [ -n "$mods" ]; then
	[ -f "$ckan" ] || { echo "CKAN not found at $ckan (use --ckan)" >&2; exit 1; }
	inst="scribe-test-$$"
	mono "$ckan" instance fake "$inst" "$dest" 1.12.5 --game KSP --set-default >/dev/null 2>&1 || mono "$ckan" instance add "$inst" "$dest" >/dev/null
	trap 'mono "$ckan" instance forget "$inst" >/dev/null 2>&1 || true' EXIT
	mono "$ckan" update --instance "$inst" >/dev/null
	# shellcheck disable=SC2086
	mono "$ckan" install --instance "$inst" --headless --no-recommends $(echo "$mods" | sed 's/=/=/') 
fi

# 4. synthetic save and craft
cp -a "$here/tests/ingame/save" "$dest/saves/scribe-test"
# 5. the plugin under test
mkdir -p "$dest/GameData/Scribe"
cp -a "$here/GameData/Scribe/." "$dest/GameData/Scribe/"

echo "created $dest (scenario $scenario); GameData: $(ls "$dest/GameData" | tr '\n' ' ')"
echo "to undo: rm -rf '$dest'"
