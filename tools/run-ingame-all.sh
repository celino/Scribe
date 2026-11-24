#!/bin/sh
# Runs every in-game round of every scenario in tests/ingame/scenarios.json, one throw-away install per scenario,
# and prints one PASS/FAIL line per round. Exit status 0 only if all rounds passed.
#
#   tools/run-ingame-all.sh --ksp <KSP 1.12.5 dir> [--scenarios minimal,kct] [--work <dir>] [--keep]
#
# Needs a display (the game runs in a window: software rendering stalls, the host GPU does not), python3, and either
# dotnet or Docker with the image from tests/ingame/docker. $GAME_WRAPPER (e.g. a launcher that keeps the screen awake)
# is passed through to run-ingame.sh. Scenarios that are not in the list are skipped (full-career is local only).
set -eu
here="$(cd "$(dirname "$0")/.." && pwd)"
ksp=""; scenarios="minimal,kct"; work="$here/ksp-test/run-all"; keep=0
while [ $# -gt 0 ]; do
	case "$1" in
		--ksp) ksp="$2"; shift 2 ;;
		--scenarios) scenarios="$2"; shift 2 ;;
		--work) work="$2"; shift 2 ;;
		--keep) keep=1; shift ;;
		-h|--help) sed -n '2,11p' "$0"; exit 0 ;;
		*) echo "unknown option: $1" >&2; exit 2 ;;
	esac
done
[ -n "$ksp" ] || { echo "usage: $0 --ksp <dir> [--scenarios a,b]" >&2; exit 2; }
[ -n "${DISPLAY:-}" ] || { echo "DISPLAY is not set: the rounds need a display (see the header)" >&2; exit 2; }

mkdir -p "$work"
summary="$work/summary.txt"; : >"$summary"; failed=0
for sc in $(echo "$scenarios" | tr ',' ' '); do
	rounds=$(python3 -I -c 'import json,sys; print(" ".join(json.load(open(sys.argv[1]))["scenarios"][sys.argv[2]]["rounds"]))' "$here/tests/ingame/scenarios.json" "$sc")
	dest="$work/install-$sc"
	rm -rf "$dest"
	sh "$here/tools/ksp-dev-create.sh" --ksp "$ksp" --dest "$dest" --scenario "$sc" >"$work/create-$sc.txt" 2>&1 \
		|| { echo "$sc: could not create the install (see $work/create-$sc.txt)" | tee -a "$summary"; failed=1; continue; }
	for r in $rounds; do
		if sh "$here/tools/run-ingame.sh" --install "$dest" --round "$r" --out "$work/$sc-$r" --build-plugin >"$work/$sc-$r.log" 2>&1; then
			echo "PASS $sc/$r" | tee -a "$summary"
		else
			echo "FAIL $sc/$r (see $work/$sc-$r/result.txt)" | tee -a "$summary"; failed=1
		fi
	done
	[ "$keep" = 1 ] || rm -rf "$dest"
done
echo "== $(grep -c '^PASS' "$summary") passed, $(grep -c '^FAIL\|could not' "$summary") failed"
exit "$failed"
