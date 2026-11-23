#!/usr/bin/env python3
"""Merge GpuCaps JSON reports into a capability matrix (FR-CAP-05).

    caps_merge.py caps_*.json -o matrix.html

Rows = fields, columns = device x API. Rows whose values differ across columns
are highlighted; each cell's tooltip shows the raw tier value.
Standard library only.
"""
from __future__ import annotations

import argparse
import html
import json
import sys

SUPPORTED_SCHEMA = {1, 2}


def flatten(rep: dict) -> dict[str, tuple[str, str]]:
    """report -> {row name: (display value, raw tooltip)}"""
    rows: dict[str, tuple[str, str]] = {}
    g = rep.get("gpu", {})
    rows["gpu.name"] = (str(g.get("name", "")), "")
    vid = g.get("vendorId")
    rows["gpu.vendorId"] = (f"0x{vid:04X}" if isinstance(vid, int) else str(vid or ""), str(vid or ""))
    rows["gpu.dedicatedMemoryMB"] = (str(g.get("dedicatedMemoryMB", "")), "")
    d = rep.get("driver", {})
    rows["driver.version"] = (str(d.get("version", "")), str(d.get("raw", "")))
    rows["os"] = (str(rep.get("os", "")), "")
    for k, v in rep.get("features", {}).items():
        if isinstance(v, dict):
            rows[f"features.{k}"] = (str(v.get("value", "")), str(v.get("raw", "")))
        else:
            rows[f"features.{k}"] = (str(v), "")
    for k, v in rep.get("unity", {}).items():
        rows[f"unity.{k}"] = (json.dumps(v) if isinstance(v, bool) else str(v), "")
    return rows


def column_title(rep: dict) -> str:
    return f"{rep.get('gpu', {}).get('name', '?')}<br><b>{rep.get('api', '?')}</b>"


CSS = """
:root{--bg:#fff;--fg:#1d1d1f;--muted:#6e6e73;--line:#e5e5ea;--diff:#fff4d6;--yes:#2e7d32;--no:#9e9e9e;--emu:#ef6c00}
@media (prefers-color-scheme:dark){:root{--bg:#161618;--fg:#f2f2f7;--muted:#a1a1a6;--line:#2c2c2e;--diff:#3a3020;--yes:#66bb6a;--no:#757575;--emu:#ffa726}}
body{background:var(--bg);color:var(--fg);font:13px/1.45 -apple-system,Segoe UI,sans-serif;margin:24px 16px}
.wrap{overflow-x:auto}table{border-collapse:collapse}th,td{border-bottom:1px solid var(--line);padding:6px 10px;text-align:left;vertical-align:top}
th{font-weight:500;color:var(--muted)}tr.diff td{background:var(--diff)}td.k{font-family:ui-monospace,Menlo,monospace;white-space:nowrap}
.supported{color:var(--yes)}.unsupported{color:var(--no)}.emulated{color:var(--emu)}td[title]:not([title=""]){text-decoration:underline dotted}
"""


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("reports", nargs="+")
    ap.add_argument("-o", "--out", default="matrix.html")
    a = ap.parse_args(argv)

    reps = []
    for p in a.reports:
        try:
            with open(p, encoding="utf-8") as f:
                r = json.load(f)
        except (OSError, json.JSONDecodeError) as e:
            print(f"error: {p}: {e}", file=sys.stderr)
            return 2
        if r.get("schema") not in SUPPORTED_SCHEMA:
            print(f"error: {p}: unsupported schema {r.get('schema')!r}", file=sys.stderr)
            return 2
        reps.append(r)

    flat = [flatten(r) for r in reps]
    keys: list[str] = []
    for f in flat:
        keys += [k for k in f if k not in keys]

    body, ndiff = [], 0
    for k in keys:
        cells = [f.get(k, ("—", "")) for f in flat]
        differs = len({c[0] for c in cells}) > 1 and not k.startswith(("gpu.", "driver.", "os"))
        ndiff += differs
        tds = "".join(
            f'<td class="{html.escape(v)}" title="{html.escape(raw)}">{html.escape(v)}</td>' for v, raw in cells)
        body.append(f'<tr class="{"diff" if differs else ""}"><td class=k>{html.escape(k)}</td>{tds}</tr>')

    head = "".join(f"<th>{column_title(r)}</th>" for r in reps)
    doc = (f"<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>"
           f"<title>GPU Caps Matrix</title><style>{CSS}</style><h1>GPU 能力矩阵</h1>"
           f"<p>{len(reps)} 份报告，{ndiff} 项特性在不同设备/API 间存在差异（黄色高亮；悬停看原始 Tier）。</p>"
           f"<div class=wrap><table><tr><th>字段</th>{head}</tr>{''.join(body)}</table></div>")
    with open(a.out, "w", encoding="utf-8") as f:
        f.write(doc)
    print(f"{len(reps)} reports, {ndiff} differing features -> {a.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
