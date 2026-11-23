#!/usr/bin/env bash
# Re-record the Metal PSO collections (one per tier) after shaders/materials change.
# GSC tracing only works in Development players (CASE-06), so: dev build -> one perf pass per tier with
# -gsc-record -> copy persistentDataPath/gsc_Metal.graphicsstate to StreamingAssets/gsc_Metal_<tier>.
# The release build used for measurements must be rebuilt afterwards (tools/run_mac_suite.sh does).
set -eu
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; cd "$ROOT"
APP=Builds/Mac/MineEmbers.app/Contents/MacOS/MineEmbers
RES="-screen-width 1920 -screen-height 1080 -screen-fullscreen 0"
# Usage: tools/record_gsc_mac.sh [--no-build] [tier ...]   (default: build + all four tiers)
BUILD=1; [ "${1:-}" = --no-build ] && { BUILD=0; shift; }
TIERS="${*:-low medium high ultra}"
OUT="$ROOT/out/gsc_record"; mkdir -p "$OUT"
[ $BUILD = 1 ] && tools/unity.sh MineEmbers.EditorTools.BuildScript.BuildMac -dev | tail -1
for t in $TIERS; do
  rm -rf "$(getconf DARWIN_USER_CACHE_DIR)/com.zfw.mineembers"
  $APP $RES -gsc-record -perfrun -tier=$t -perf-warmup 1 -perf-seconds 6 -perf-out "$OUT/$t" -logFile "$OUT/log_$t.txt" >/dev/null
  line=$(grep "\[Warmup\] saved" "$OUT/log_$t.txt"); echo "$t: $line"
  src=$(echo "$line" | sed -E 's/.*-> (.*) \(ok\)$/\1/')
  cp "$src" "Assets/StreamingAssets/gsc_Metal_$t.graphicsstate"
done
