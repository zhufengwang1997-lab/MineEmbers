#!/usr/bin/env bash
# Mac measurement suite: PSO warmup experiment (cold Metal cache, with/without GSC) + 4-tier perf runs.
# Usage: tools/run_mac_suite.sh [warmup]   (warmup = only the cold-cache experiment)
set -u
PART="${1:-all}"; REPS=3
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; cd "$ROOT"
OUT="$ROOT/out/suite"; rm -rf "$OUT/nogsc" "$OUT/gsc"; mkdir -p "$OUT/nogsc" "$OUT/gsc" "$OUT/tiers"
APP=Builds/Mac/MineEmbers.app/Contents/MacOS/MineEmbers
METAL_CACHE="$(getconf DARWIN_USER_CACHE_DIR)/com.zfw.mineembers"
RES="-screen-width 1920 -screen-height 1080 -screen-fullscreen 0"
cold() { rm -rf "$METAL_CACHE"; }

# 1) build without GSC files -> cold run, no warmup recording window
mkdir -p out/gsc_stash; mv Assets/StreamingAssets/gsc_Metal_*.graphicsstate out/gsc_stash/ 2>/dev/null
tools/unity.sh MineEmbers.EditorTools.BuildScript.BuildMac | tail -1
for r in $(seq $REPS); do mkdir -p "$OUT/nogsc/r$r"
  cold; $APP $RES -perfrun -tier=high -perf-warmup 0 -perf-seconds 8 -perf-out "$OUT/nogsc/r$r" -logFile "$OUT/nogsc/r$r/log.txt" >/dev/null
done
# 2) build with GSC files -> cold run again
mv out/gsc_stash/gsc_Metal_*.graphicsstate Assets/StreamingAssets/
tools/unity.sh MineEmbers.EditorTools.BuildScript.BuildMac | tail -1
for r in $(seq $REPS); do mkdir -p "$OUT/gsc/r$r"
  cold; $APP $RES -perfrun -tier=high -perf-warmup 0 -perf-seconds 8 -perf-out "$OUT/gsc/r$r" -logFile "$OUT/gsc/r$r/log.txt" >/dev/null
  grep "\[Warmup\]" "$OUT/gsc/r$r/log.txt"
done
[ "$PART" = warmup ] && { echo SUITE_DONE; exit 0; }
rm -rf "$OUT/tiers"; mkdir -p "$OUT/tiers"
# 3) four tiers, normal warmup, after the shadow-atlas fix
for t in low medium high ultra; do
  $APP $RES -perfrun -tier=$t -perf-out "$OUT/tiers" -logFile "$OUT/tiers/log_$t.txt" >/dev/null
  echo "$t done; atlas warnings: $(grep -c 'Reduced additional' "$OUT/tiers/log_$t.txt")"
done
echo SUITE_DONE
