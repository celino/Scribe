#!/bin/sh
# Runs the in-game rounds for this checkout inside the test image, on a throw-away copy of a prepared install.
# Homelab only (it drives the host's Docker daemon); GitHub runners cannot run KSP.
#
#   KSP_INSTALL=<host path of an install made by tools/ksp-dev-create.sh> ROUNDS="tx revert flight bench" \
#     sh tools/ksp-ingame-ci.sh
#
# The install is the read-only lower layer of an overlay volume; everything the game writes goes to a temporary
# upper layer that is deleted at the end. The checkout is copied into the container, the plugin and the round
# driver are built there, then every round runs through tools/run-ingame.sh. Results land in ./ksp-test/<round>/.
# DISPLAY must point at the host's X server (e.g. :0). Needs the image from tests/ingame/docker (docker build -t scribe-ksp-test tests/ingame/docker).
set -eu
cd "$(dirname "$0")/.."

: "${KSP_INSTALL:?set KSP_INSTALL to the prepared install (host path)}"
: "${CI_ROOT:?set CI_ROOT to a host folder the overlay layers can live in (no default)}"
: "${KSP_IMAGE:=scribe-ksp-test}"
: "${ROUNDS:=tx revert flight bench}"
id="scribe-${RUN_ID:-local-$$}"

cleanup() {
	docker rm -f "$id-ksp" >/dev/null 2>&1 || true
	docker volume rm "$id" >/dev/null 2>&1 || true
	docker run --rm -v "$CI_ROOT":/ci alpine rm -rf "/ci/$id" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run --rm -v "$CI_ROOT":/ci alpine sh -c "mkdir -p /ci/$id/upper /ci/$id/work && chown 1000:1000 /ci/$id/upper"
docker volume create --driver local --opt type=overlay --opt device=overlay \
	--opt "o=lowerdir=$KSP_INSTALL,upperdir=$CI_ROOT/$id/upper,workdir=$CI_ROOT/$id/work" "$id" >/dev/null

script='set -u
fail=0
mkdir -p /tmp/out && cp -r /repo /tmp/repo
for r in '"$ROUNDS"'; do
	sh /tmp/repo/tools/run-ingame.sh --install /game --round "$r" --out "/tmp/out/$r" --build-plugin || fail=1
done
exit $fail'
# The game needs a real display and a GPU (software rendering stalls). The container reaches the host's X server through
# its socket and the GPU through /dev/dri; the X server must accept the same local user (xhost +SI:localuser:<user>).
gpu=""
if [ -n "${DISPLAY:-}" ]; then gpu="-e LIBGL_ALWAYS_SOFTWARE=0 -e DISPLAY=$DISPLAY -v /tmp/.X11-unix:/tmp/.X11-unix --device /dev/dri"; fi
# shellcheck disable=SC2086
docker create --name "$id-ksp" --shm-size 2g $gpu -e MAX_S="${MAX_S:-1800}" -e IDLE_S="${IDLE_S:-1500}" \
	-v "$id":/game "$KSP_IMAGE" sh -c "$script" >/dev/null
docker cp . "$id-ksp:/repo" >/dev/null
docker start -a "$id-ksp" && status=0 || status=$?

rm -rf ksp-test; mkdir -p ksp-test
docker cp "$id-ksp:/tmp/out/." ksp-test/ >/dev/null 2>&1 || true
for r in $ROUNDS; do echo "== $r"; cat "ksp-test/$r/result.txt" 2>/dev/null || echo "no result"; done
exit "$status"
