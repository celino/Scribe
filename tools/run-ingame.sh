#!/bin/sh
# Runs one scripted round in a throw-away KSP install made by tools/ksp-dev-create.sh and prints PASS/FAIL.
#
#   tools/run-ingame.sh --install <dir> --round tx|revert|flight|bench|dock|kct [--out <dir>] [--no-seed] [--build-plugin]
#
# The round's driver (tests/ingame/driver) is built, copied into <dir>/GameData/zzScribeRound, and the game is
# started with the synthetic save (saves/scribe-test). The driver prints "ASSERT Fnn PASS|FAIL" lines and quits;
# tests/ingame/check_round.py reads KSP.log and sets the exit status. The driver is removed again at the end.
#
# Display: uses $DISPLAY when set; otherwise starts Xvfb (needs xvfb-run). Software GL works but is slow (minutes).
# Building needs `dotnet` on PATH (SDK 8+). Only the plugin and driver DLLs are touched inside <dir>.
set -eu
here="$(cd "$(dirname "$0")/.." && pwd)"
install=""; round=""; out=""; seed=1; build_plugin=0
while [ $# -gt 0 ]; do
	case "$1" in
		--install) install="$2"; shift 2 ;;
		--round) round="$2"; shift 2 ;;
		--out) out="$2"; shift 2 ;;
		--no-seed) seed=0; shift ;;
		--build-plugin) build_plugin=1; shift ;;
		-h|--help) sed -n '2,12p' "$0"; exit 0 ;;
		*) echo "unknown option: $1" >&2; exit 2 ;;
	esac
done
[ -n "$install" ] && [ -n "$round" ] || { echo "usage: $0 --install <dir> --round <round>" >&2; exit 2; }
[ -x "$install/KSP.x86_64" ] || { echo "$install is not a KSP install" >&2; exit 1; }
save=scribe-test
[ -d "$install/saves/$save" ] || { echo "$install/saves/$save missing: create the install with tools/ksp-dev-create.sh" >&2; exit 1; }

# which features each round must prove (a missing ASSERT is a failure)
case "$round" in
	tx) expect=F01 ;;
	revert) expect=F07 ;;
	flight) expect=F08,F10 ;;
	bench) expect=F13,F03 ;;
	dock) expect=F09 ;;
	kct) expect=F11 ;;
	*) echo "unknown round: $round" >&2; exit 2 ;;
esac
# F10 includes a contract entry, which needs the contracts scenario; the other F10 asserts still count there
allow=""; [ "$round" = flight ] && allow=""

[ -n "$out" ] || out="$here/ksp-test/$round-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$out"

# --build-plugin: compile Scribe.dll and Scribe.Core.dll from this checkout into the install (CI; locally the install already has them)
if [ "$build_plugin" = 1 ]; then
	dotnet build "$here/Source/Scribe.csproj" -c Release -p:KspDir="$install" -o "$out/plugin" -v q -nologo >"$out/build-plugin.txt" 2>&1 \
		|| { echo "plugin build failed, see $out/build-plugin.txt" >&2; exit 1; }
	cp "$out/plugin/Scribe.dll" "$out/plugin/Scribe.Core.dll" "$install/GameData/Scribe/Plugins/"
fi

# build the driver against this install
# (DRIVER_DLL=<path> skips the build, for machines where dotnet only runs in a container)
if [ -n "${DRIVER_DLL:-}" ]; then mkdir -p "$out/driver"; cp "$DRIVER_DLL" "$out/driver/ScribeRound.dll"; else
dotnet build "$here/tests/ingame/driver/ScribeRound.csproj" -c Release -p:KspDir="$install" -o "$out/driver" -v q -nologo >"$out/build.txt" 2>&1 \
	|| { echo "driver build failed, see $out/build.txt" >&2; exit 1; }
fi

# the save as the round must start: a fresh copy of the synthetic save, with the SCRIBE scenario node seeded unless
# --no-seed (the "menu" round tests the module adding itself)
backup="$out/persistent.sfs.orig"; cp "$install/saves/$save/persistent.sfs" "$backup"
prepare_save() {
	cp "$backup" "$install/saves/$save/persistent.sfs"
	[ "$seed" = 1 ] || return 0
	python3 -I - "$install/saves/$save/persistent.sfs" <<'PY'
import sys
p = sys.argv[1]
s = open(p).read()
if "name = ScribeModule" not in s:
    i = s.index("\tSCENARIO")
    s = s[:i] + "\tSCENARIO\n\t{\n\t\tname = ScribeModule\n\t\tscene = 5, 6, 7, 8\n\t}\n" + s[i:]
    open(p, "w").write(s)
PY
}

drv="$install/GameData/zzScribeRound"
cleanup() { rm -rf "$drv"; cp "$backup" "$install/saves/$save/persistent.sfs"; }
trap cleanup EXIT
mkdir -p "$drv"
sed -e "s/^\( *mode =\).*/\1 $round/" -e "s/^\( *save =\).*/\1 $save/" "$here/tests/ingame/driver/scenario.cfg" >"$drv/scenario.cfg"
cp "$out/driver/ScribeRound.dll" "$drv/"

start=$(date +%s); max="${MAX_S:-1800}"; idle="${IDLE_S:-300}"; attempts="${ATTEMPTS:-3}"
# the game runs as ./KSP.x86_64 with <install> as its working directory: match on that, not on the command line
stop_game() {
	for d in /proc/[0-9]*; do
		[ "$(readlink "$d/cwd" 2>/dev/null)" = "$(cd "$install" && pwd -P)" ] && grep -aq 'KSP.x86_64' "$d/cmdline" 2>/dev/null && kill ${1:-} "${d#/proc/}" 2>/dev/null || true
	done
}
run() {
	cd "$install"
	"$@" ./KSP.x86_64 -force-glcore -screen-fullscreen 0 ${KSP_ARGS:--screen-width 640 -screen-height 360} >/dev/null 2>&1
}
# Under software rendering the game sometimes stalls while a scene loads (seen: log silent for 20+ minutes, no
# error). A stalled attempt (no "done", no failed assertion) is retried from a fresh save, up to $attempts times.
attempt=1
while :; do
	prepare_save; rm -f "$install/KSP.log"; astart=$(date +%s)
	if [ -n "${DISPLAY:-}" ]; then run env & else run xvfb-run -a -s "-screen 0 640x360x24" & fi
	pid=$!
	status=ok
	while kill -0 "$pid" 2>/dev/null; do
		sleep 5; now=$(date +%s); last=$(stat -c %Y "$install/KSP.log" 2>/dev/null || echo "$now")
		if grep -qa "\[ScribeRound\] done" "$install/KSP.log" 2>/dev/null && [ $((now - astart)) -gt 30 ]; then
			sleep 15; status="ok (stopped after done)"; stop_game; break
		fi
		if [ $((now - astart)) -gt "$max" ] || { [ $((now - astart)) -gt 60 ] && [ $((now - last)) -gt "$idle" ]; }; then
			status="killed after $((now - astart)) s"; stop_game; break
		fi
	done
	wait "$pid" 2>/dev/null || true
	sleep 3; stop_game -9
	grep -qa "\[ScribeRound\] done" "$install/KSP.log" 2>/dev/null && break
	grep -qa "ASSERT F[0-9]* FAIL" "$install/KSP.log" 2>/dev/null && break
	[ "$attempt" -lt "$attempts" ] || break
	echo "attempt $attempt stalled ($status); trying again" >&2
	cp "$install/KSP.log" "$out/KSP-attempt$attempt.log" 2>/dev/null || :
	attempt=$((attempt + 1))
done
cp "$install/KSP.log" "$out/KSP.log" 2>/dev/null || :
cp -r "$install/saves/$save" "$out/save" 2>/dev/null || :
echo "round $round: $(( $(date +%s) - start )) s, $status, attempt $attempt; output in $out"
python3 -I "$here/tests/ingame/check_round.py" "$out/KSP.log" --expect "$expect" ${allow:+--allow-missing "$allow"} | tee "$out/result.txt"
grep -q '^RESULT PASS' "$out/result.txt"
