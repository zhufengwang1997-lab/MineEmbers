#!/usr/bin/env python3
"""Compare fixed-camera screenshots between two runs (FR-PD-02).

    screenshot_diff.py <dirA> <dirB> -o diff.html [--pixel-threshold 16] [--fail-ratio 0.01]

For each PNG present in both folders: mean absolute difference (0-255) and the
fraction of pixels whose max channel difference exceeds --pixel-threshold.
Writes a heatmap PNG per shot next to the report. Exit 1 if any shot's ratio
exceeds --fail-ratio (needs human review), else 0.

Contains a tiny PNG codec (8-bit gray/RGB/RGBA, non-interlaced) so it needs no
third-party packages.
"""
from __future__ import annotations

import argparse
import html
import os
import struct
import sys
import zlib

PNG_SIG = b"\x89PNG\r\n\x1a\n"
CHANNELS = {0: 1, 2: 3, 4: 2, 6: 4}  # color type -> channels


def read_png(path: str):
    """Return (width, height, rgb bytes). Supports 8-bit, non-interlaced, no palette."""
    with open(path, "rb") as f:
        data = f.read()
    if data[:8] != PNG_SIG:
        raise ValueError(f"{path}: not a PNG")
    pos, idat = 8, []
    w = h = depth = ctype = interlace = None
    while pos < len(data):
        (length,) = struct.unpack(">I", data[pos:pos + 4])
        tag = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if tag == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", chunk)
        elif tag == b"IDAT":
            idat.append(chunk)
        elif tag == b"IEND":
            break
    if depth != 8 or ctype not in CHANNELS or interlace:
        raise ValueError(f"{path}: unsupported PNG (depth={depth}, color={ctype}, interlace={interlace})")
    bpp = CHANNELS[ctype]
    raw = zlib.decompress(b"".join(idat))
    stride = w * bpp
    out = bytearray(h * stride)
    prev = bytearray(stride)
    i = 0
    for y in range(h):
        ft = raw[i]; i += 1
        line = bytearray(raw[i:i + stride]); i += stride
        # Undo the per-row filter (PNG spec section 9).
        if ft == 1:
            for x in range(bpp, stride):
                line[x] = (line[x] + line[x - bpp]) & 255
        elif ft == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 255
        elif ft == 3:
            for x in range(stride):
                left = line[x - bpp] if x >= bpp else 0
                line[x] = (line[x] + ((left + prev[x]) >> 1)) & 255
        elif ft == 4:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pred = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[x] = (line[x] + pred) & 255
        out[y * stride:(y + 1) * stride] = line
        prev = line
    # Normalize to RGB.
    if bpp == 3:
        return w, h, bytes(out)
    rgb = bytearray(w * h * 3)
    for p in range(w * h):
        if bpp == 4:
            rgb[p * 3:p * 3 + 3] = out[p * 4:p * 4 + 3]
        else:
            g = out[p * bpp]
            rgb[p * 3:p * 3 + 3] = bytes((g, g, g))
    return w, h, bytes(rgb)


def write_png(path: str, w: int, h: int, rgb: bytes):
    def chunk(tag, body):
        return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)
    raw = b"".join(b"\x00" + rgb[y * w * 3:(y + 1) * w * 3] for y in range(h))
    with open(path, "wb") as f:
        f.write(PNG_SIG + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))


def heat(v: int) -> tuple[int, int, int]:
    """0..255 diff -> black -> red -> yellow -> white."""
    t = min(1.0, v / 64.0)
    return (int(255 * min(1, t * 3)), int(255 * min(1, max(0, t * 3 - 1))), int(255 * max(0, t * 3 - 2)))


def diff(a, b, pix_thr: int):
    (wa, ha, pa), (wb, hb, pb) = a, b
    if (wa, ha) != (wb, hb):
        return None
    n = wa * ha
    total, over = 0, 0
    hm = bytearray(n * 3)
    for p in range(n):
        o = p * 3
        d = max(abs(pa[o] - pb[o]), abs(pa[o + 1] - pb[o + 1]), abs(pa[o + 2] - pb[o + 2]))
        total += abs(pa[o] - pb[o]) + abs(pa[o + 1] - pb[o + 1]) + abs(pa[o + 2] - pb[o + 2])
        if d > pix_thr:
            over += 1
        hm[o:o + 3] = bytes(heat(d))
    return total / (n * 3), over / n, bytes(hm)


CSS = """
:root{--bg:#fff;--fg:#1d1d1f;--muted:#6e6e73;--line:#e5e5ea;--bad:#c62828;--ok:#2e7d32}
@media (prefers-color-scheme:dark){:root{--bg:#161618;--fg:#f2f2f7;--muted:#a1a1a6;--line:#2c2c2e;--bad:#ef5350;--ok:#66bb6a}}
body{background:var(--bg);color:var(--fg);font:14px/1.5 -apple-system,Segoe UI,sans-serif;max-width:1100px;margin:24px auto;padding:0 16px}
.shot{border-top:1px solid var(--line);padding:12px 0}.imgs{display:grid;grid-template-columns:repeat(3,1fr);gap:8px}
.imgs img{width:100%;image-rendering:pixelated}.bad{color:var(--bad);font-weight:600}.ok{color:var(--ok)}.muted{color:var(--muted)}
@media (max-width:640px){.imgs{grid-template-columns:1fr}}
"""


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("dir_a")
    ap.add_argument("dir_b")
    ap.add_argument("-o", "--out", default="diff.html")
    ap.add_argument("--pixel-threshold", type=int, default=16)
    ap.add_argument("--fail-ratio", type=float, default=0.01)
    a = ap.parse_args(argv)

    names_a = {n for n in os.listdir(a.dir_a) if n.lower().endswith(".png")}
    names_b = {n for n in os.listdir(a.dir_b) if n.lower().endswith(".png")}
    common = sorted(names_a & names_b)
    out_dir = os.path.dirname(os.path.abspath(a.out))
    hm_dir = os.path.join(out_dir, "diff_heatmaps")
    os.makedirs(hm_dir, exist_ok=True)

    blocks, flagged = [], 0
    for n in common:
        pa, pb = os.path.join(a.dir_a, n), os.path.join(a.dir_b, n)
        r = diff(read_png(pa), read_png(pb), a.pixel_threshold)
        if r is None:
            flagged += 1
            blocks.append(f"<div class=shot><h3>{html.escape(n)}</h3><p class=bad>分辨率不同，无法对比</p></div>")
            print(f"{n}: size mismatch")
            continue
        mad, ratio, hm = r
        w, h, _ = read_png(pa)
        write_png(os.path.join(hm_dir, n), w, h, hm)
        bad = ratio > a.fail_ratio
        flagged += bad
        rel = lambda p: html.escape(os.path.relpath(os.path.abspath(p), out_dir))
        blocks.append(
            f"<div class=shot><h3>{html.escape(n)} <span class={'bad' if bad else 'ok'}>"
            f"{'需人工检查' if bad else '一致'}</span></h3><p class=muted>平均绝对差 {mad:.2f} · 超阈值像素 {ratio:.2%}</p>"
            f"<div class=imgs><img src='{rel(pa)}' alt=A><img src='{rel(pb)}' alt=B>"
            f"<img src='{rel(os.path.join(hm_dir, n))}' alt=heatmap></div></div>")
        print(f"{n}: mad={mad:.2f} over={ratio:.2%}{'  CHECK' if bad else ''}")

    only = sorted(names_a ^ names_b)
    extra = f"<p class=bad>只在一侧存在：{html.escape(', '.join(only))}</p>" if only else ""
    doc = (f"<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>"
           f"<title>Screenshot Diff</title><style>{CSS}</style><h1>截图差异</h1>"
           f"<p class=muted>A = {html.escape(a.dir_a)}<br>B = {html.escape(a.dir_b)}<br>"
           f"像素阈值 {a.pixel_threshold}，失败比例 {a.fail_ratio:.1%}</p>"
           f"<p>{len(common)} 个机位，{flagged} 个需要人工检查。</p>{extra}{''.join(blocks)}")
    with open(a.out, "w", encoding="utf-8") as f:
        f.write(doc)
    print(f"report: {a.out}")
    return 1 if flagged or only else 0


if __name__ == "__main__":
    sys.exit(main())
