#!/usr/bin/env python3
"""PerfRunner CSV comparison (FR-PR-04).

    compare.py <baseline.csv> <new.csv> -o report.html [--threshold 0.05]

Exit codes:
    0  no regression
    1  regression: in some zone, mean gpu_ms grew by more than `threshold`
    2  the two runs are not comparable (api / device / tier differ) or bad input

Standard library only, so it runs on the Windows test box without pip.
"""
from __future__ import annotations

import argparse
import csv
import html
import math
import shlex
import sys
from dataclasses import dataclass, field

MATCH_KEYS = ("api", "device", "tier")


@dataclass
class Run:
    path: str
    meta: dict[str, str]
    # zone -> column -> list of values (None when the platform could not measure it)
    zones: dict[str, dict[str, list[float | None]]] = field(default_factory=dict)
    zone_order: list[str] = field(default_factory=list)


def parse_header(line: str) -> dict[str, str]:
    """'# commit=abc api=Direct3D12 device="NVIDIA ..."' -> dict (shell-style quoting)."""
    meta = {}
    for tok in shlex.split(line.lstrip("#").strip()):
        if "=" in tok:
            k, v = tok.split("=", 1)
            meta[k.strip()] = v.strip()
    return meta


def load(path: str) -> Run:
    with open(path, newline="", encoding="utf-8") as f:
        lines = f.read().splitlines()
    meta: dict[str, str] = {}
    body = []
    for ln in lines:
        if ln.startswith("#"):
            meta.update(parse_header(ln))
        elif ln.strip():
            body.append(ln)
    run = Run(path, meta)
    for row in csv.DictReader(body):
        z = row["zone"]
        if z not in run.zones:
            run.zones[z] = {}
            run.zone_order.append(z)
        for col in ("cpu_ms", "gpu_ms", "draw_calls", "setpass_calls", "gfx_mem_mb"):
            v = row.get(col, "")
            run.zones[z].setdefault(col, []).append(float(v) if v not in ("", None) else None)
    return run


def mean(xs):
    xs = [x for x in xs if x is not None]
    return sum(xs) / len(xs) if xs else None


def pct(xs, p):
    xs = sorted(x for x in xs if x is not None)
    if not xs:
        return None
    k = (len(xs) - 1) * p
    lo, hi = math.floor(k), math.ceil(k)
    return xs[lo] + (xs[hi] - xs[lo]) * (k - lo)


def one_pct_low_fps(frame_ms):
    """Average fps of the slowest 1% frames (definition in the SRS glossary)."""
    xs = sorted((x for x in frame_ms if x), reverse=True)
    if not xs:
        return None
    n = max(1, len(xs) // 100)
    return 1000.0 / (sum(xs[:n]) / n)


def summarize(run: Run, zone: str) -> dict:
    d = run.zones.get(zone, {})
    gpu, cpu = d.get("gpu_ms", []), d.get("cpu_ms", [])
    frame = [max(c or 0, g or 0) or None for c, g in zip(cpu, gpu)]
    return {
        "frames": len(cpu),
        "gpu_mean": mean(gpu),
        "gpu_p95": pct(gpu, 0.95),
        "cpu_mean": mean(cpu),
        "low1": one_pct_low_fps(frame),
        "draw": mean(d.get("draw_calls", [])),
    }


def fmt(v, nd=2):
    return "—" if v is None else f"{v:.{nd}f}"


def svg_chart(zone, base: list, new: list, w=640, h=180) -> str:
    vals = [v for v in base + new if v is not None]
    if not vals:
        return "<p class=muted>gpu_ms 不可用</p>"
    top = max(vals) * 1.1
    n = max(len(base), len(new), 2)

    def poly(series, color):
        pts = [f"{i * w / (n - 1):.1f},{h - (v / top) * h:.1f}" for i, v in enumerate(series) if v is not None]
        return f'<polyline fill="none" stroke="{color}" stroke-width="1.2" points="{" ".join(pts)}"/>'

    grid = "".join(
        f'<line x1="0" x2="{w}" y1="{h - ms / top * h:.1f}" y2="{h - ms / top * h:.1f}" class="grid"/>'
        f'<text x="2" y="{h - ms / top * h - 2:.1f}" class="lbl">{ms:g} ms</text>'
        for ms in (8.33, 16.67, 33.33) if ms < top
    )
    return (f'<svg viewBox="0 0 {w} {h}" width="100%" role="img" aria-label="{html.escape(zone)} gpu_ms">'
            f'{grid}{poly(base, "var(--base)")}{poly(new, "var(--new)")}</svg>')


CSS = """
:root{--bg:#fff;--fg:#1d1d1f;--muted:#6e6e73;--bad:#c62828;--ok:#2e7d32;--base:#8e8e93;--new:#1565c0;--line:#e5e5ea}
@media (prefers-color-scheme:dark){:root{--bg:#161618;--fg:#f2f2f7;--muted:#a1a1a6;--bad:#ef5350;--ok:#66bb6a;--base:#8e8e93;--new:#64b5f6;--line:#2c2c2e}}
body{background:var(--bg);color:var(--fg);font:14px/1.5 -apple-system,Segoe UI,sans-serif;max-width:900px;margin:24px auto;padding:0 16px}
table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid var(--line);padding:6px 8px;text-align:right}
td:first-child,th:first-child{text-align:left}.bad{color:var(--bad);font-weight:600}.ok{color:var(--ok)}.muted{color:var(--muted)}
.grid{stroke:var(--line)}.lbl{fill:var(--muted);font-size:10px}.verdict{font-size:20px;font-weight:600}
"""


def report(base: Run, new: Run, rows, regressed: bool, threshold: float) -> str:
    m = base.meta
    head = (f"<p class=muted>{html.escape(m.get('device', '?'))} · {html.escape(m.get('api', '?'))} · tier={html.escape(m.get('tier', '?'))}"
            f"<br>baseline {html.escape(base.meta.get('commit', '?'))} → new {html.escape(new.meta.get('commit', '?'))}"
            f" · 阈值 {threshold:.0%}</p>")
    verdict = (f'<p class="verdict bad">性能回退</p>' if regressed else '<p class="verdict ok">无回退</p>')
    trs = []
    for z, b, n, delta, bad in rows:
        d = "—" if delta is None else f"{delta:+.1%}"
        trs.append(
            f"<tr><td>{html.escape(z)}</td><td>{fmt(b['gpu_mean'])} → {fmt(n['gpu_mean'])}</td>"
            f"<td class={'bad' if bad else ''}>{d}</td><td>{fmt(b['gpu_p95'])} → {fmt(n['gpu_p95'])}</td>"
            f"<td>{fmt(b['cpu_mean'])} → {fmt(n['cpu_mean'])}</td><td>{fmt(b['low1'], 1)} → {fmt(n['low1'], 1)}</td></tr>")
    table = ("<table><tr><th>区域</th><th>GPU 平均 ms</th><th>变化</th><th>GPU p95 ms</th><th>CPU 平均 ms</th><th>1% Low fps</th></tr>"
             + "".join(trs) + "</table>")
    charts = "".join(
        f"<h3>{html.escape(z)}</h3>" + svg_chart(z, base.zones.get(z, {}).get("gpu_ms", []), new.zones.get(z, {}).get("gpu_ms", []))
        for z, *_ in rows)
    legend = '<p class=muted><span style="color:var(--base)">━ 基线</span>　<span style="color:var(--new)">━ 本次</span></p>'
    return (f"<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>"
            f"<title>Perf Compare</title><style>{CSS}</style><h1>性能对比</h1>{head}{verdict}{table}<h2>GPU 帧时间曲线</h2>{legend}{charts}")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("baseline")
    ap.add_argument("new")
    ap.add_argument("-o", "--out", default="report.html")
    ap.add_argument("--threshold", type=float, default=0.05)
    a = ap.parse_args(argv)

    try:
        base, new = load(a.baseline), load(a.new)
    except (OSError, KeyError, ValueError) as e:
        print(f"error: cannot read input: {e}", file=sys.stderr)
        return 2

    mismatch = [k for k in MATCH_KEYS if base.meta.get(k) != new.meta.get(k)]
    if mismatch:
        for k in mismatch:
            print(f"refuse: {k} differs: {base.meta.get(k)!r} vs {new.meta.get(k)!r}", file=sys.stderr)
        return 2

    rows, regressed = [], False
    for z in base.zone_order + [z for z in new.zone_order if z not in base.zones]:
        b, n = summarize(base, z), summarize(new, z)
        delta = None
        if b["gpu_mean"] and n["gpu_mean"] is not None:
            delta = (n["gpu_mean"] - b["gpu_mean"]) / b["gpu_mean"]
        bad = delta is not None and delta > a.threshold
        regressed |= bad
        rows.append((z, b, n, delta, bad))
        print(f"{z:<10} gpu {fmt(b['gpu_mean'])} -> {fmt(n['gpu_mean'])} ms "
              f"({'—' if delta is None else f'{delta:+.1%}'}){'  REGRESSION' if bad else ''}")

    with open(a.out, "w", encoding="utf-8") as f:
        f.write(report(base, new, rows, regressed, a.threshold))
    print(f"report: {a.out}")
    return 1 if regressed else 0


if __name__ == "__main__":
    sys.exit(main())
