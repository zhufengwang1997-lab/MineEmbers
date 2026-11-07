#!/usr/bin/env python3
"""Summarize many PerfRunner CSVs (any API / tier / device) into one table.

    perf_summary.py perf_*.csv [-o summary.md]

compare.py answers "did this commit regress under identical conditions?" and refuses mixed inputs.
This answers "how do tiers / APIs / devices compare?" -- deliberately mixing conditions.
Output: Markdown table per (device, api, tier, zone): frames, GPU mean / p95, CPU mean, 1% low fps.
"""
import argparse, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Packages", "com.zfw.perfrunner", "Tools~"))
import compare as c

TIER_ORDER = {"low": 0, "medium": 1, "high": 2, "ultra": 3}

ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
ap.add_argument("csv", nargs="+"); ap.add_argument("-o", "--out")
a = ap.parse_args()

runs = sorted((c.load(p) for p in a.csv),
              key=lambda r: (r.meta.get("device", ""), r.meta.get("api", ""), TIER_ORDER.get(r.meta.get("tier", ""), 9)))
lines = ["| 设备 | API | 档位 | 区域 | 帧数 | GPU 平均 ms | GPU p95 ms | CPU 平均 ms | 1% Low fps | 入区峰值 ms | Draw Calls |",
         "| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |"]
for r in runs:
    for z in r.zone_order:
        s = c.summarize(r, z)
        d = r.zones[z]
        # Worst frame in the first 120 frames after entering the zone: shader/PSO compile hitches land here
        # (only meaningful with -perf-warmup 0, otherwise the hitch happened during the unrecorded warmup).
        head = [max(a or 0, b or 0) for a, b in zip(d["cpu_ms"][:120], d["gpu_ms"][:120])]
        spike = max(head) if head else None
        lines.append(f"| {r.meta.get('device','?')} | {r.meta.get('api','?')} | {r.meta.get('tier','?')} | {z} | {s['frames']} | "
                     f"{c.fmt(s['gpu_mean'])} | {c.fmt(s['gpu_p95'])} | {c.fmt(s['cpu_mean'])} | {c.fmt(s['low1'], 1)} | {c.fmt(spike, 1)} | {c.fmt(s['draw'], 0)} |")
meta = runs[0].meta if runs else {}
head = f"commit `{meta.get('commit','?')}` · unity {meta.get('unity','?')} · {len(runs)} runs\n\n"
text = head + "\n".join(lines) + "\n"
if a.out:
    open(a.out, "w", encoding="utf-8").write(text)
print(text)
