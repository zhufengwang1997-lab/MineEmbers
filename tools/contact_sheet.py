#!/usr/bin/env python3
"""contact_sheet.py <shots_dir> -o sheet.png [--names e01,l01,...] [--cols 3] [--scale 3]  (stdlib only)"""
import argparse, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Packages", "com.zfw.perfrunner", "Tools~"))
import screenshot_diff as s

ap = argparse.ArgumentParser(); ap.add_argument("dir"); ap.add_argument("-o", default="sheet.png")
ap.add_argument("--names"); ap.add_argument("--cols", type=int, default=3); ap.add_argument("--scale", type=int, default=3)
a = ap.parse_args()
names = a.names.split(",") if a.names else sorted(n[:-4] for n in os.listdir(a.dir) if n.endswith(".png"))
imgs = [s.read_png(os.path.join(a.dir, n + ".png")) for n in names]
w, h, _ = imgs[0]; sc = a.scale; tw, th = w // sc, h // sc
rows = (len(imgs) + a.cols - 1) // a.cols
W, H = tw * a.cols, th * rows
out = bytearray(W * H * 3)
for k, (iw, ih, px) in enumerate(imgs):
    ox, oy = (k % a.cols) * tw, (k // a.cols) * th
    for y in range(th):
        row = (y * sc) * iw
        for x in range(tw):
            o = (row + x * sc) * 3; d = ((oy + y) * W + ox + x) * 3
            out[d:d + 3] = px[o:o + 3]
s.write_png(a.o, W, H, bytes(out)); print(f"{len(imgs)} shots -> {a.o} ({W}x{H})")
