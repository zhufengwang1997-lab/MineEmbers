#!/usr/bin/env python3
"""Self-tests for the Python tools. Run: python3 tools/test_tools.py"""
import importlib.util
import json
import os
import random
import struct
import sys
import tempfile
import unittest
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_mod(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(ROOT, rel))
    m = importlib.util.module_from_spec(spec)
    sys.modules[name] = m
    spec.loader.exec_module(m)
    return m


compare = load_mod("compare", "Packages/com.zfw.perfrunner/Tools~/compare.py")


def write_csv(path, gpu_scale=1.0, device="NVIDIA GeForce GTX 1070 Ti", tier="low", gpu_empty=False):
    rnd = random.Random(1)
    with open(path, "w") as f:
        f.write(f'# commit=abc1234 api=Direct3D12 device="{device}" tier={tier} unity=6000.3.28f1 date=2027-02-10T21:00\n')
        f.write("frame,zone,cpu_ms,gpu_ms,draw_calls,setpass_calls,gfx_mem_mb\n")
        i = 0
        for zone, base in (("entrance", 12.0), ("lake", 15.0), ("furnace", 26.0)):
            for _ in range(600):
                g = "" if gpu_empty else f"{base * gpu_scale + rnd.uniform(-1, 1):.2f}"
                f.write(f"{i},{zone},{8 + rnd.uniform(-1, 1):.2f},{g},512,88,1430\n")
                i += 1


class CompareTests(unittest.TestCase):
    def setUp(self):
        self.d = tempfile.mkdtemp()
        self.base = os.path.join(self.d, "base.csv")
        write_csv(self.base)
        self.out = os.path.join(self.d, "r.html")

    def run_cmp(self, other, *extra):
        return compare.main([self.base, other, "-o", self.out, *extra])

    def test_no_regression(self):
        p = os.path.join(self.d, "same.csv"); write_csv(p, 1.02)
        self.assertEqual(self.run_cmp(p), 0)
        self.assertIn("无回退", open(self.out, encoding="utf-8").read())

    def test_regression(self):
        p = os.path.join(self.d, "slow.csv"); write_csv(p, 1.10)
        self.assertEqual(self.run_cmp(p), 1)
        self.assertIn("性能回退", open(self.out, encoding="utf-8").read())

    def test_threshold_flag(self):
        p = os.path.join(self.d, "slow.csv"); write_csv(p, 1.10)
        self.assertEqual(self.run_cmp(p, "--threshold", "0.2"), 0)

    def test_device_mismatch_refused(self):  # TC-07
        p = os.path.join(self.d, "mac.csv"); write_csv(p, device="Apple M5 Max")
        self.assertEqual(self.run_cmp(p), 2)

    def test_tier_mismatch_refused(self):
        p = os.path.join(self.d, "hi.csv"); write_csv(p, tier="high")
        self.assertEqual(self.run_cmp(p), 2)

    def test_missing_gpu_column_values(self):
        p = os.path.join(self.d, "nogpu.csv"); write_csv(p, gpu_empty=True)
        self.assertEqual(self.run_cmp(p), 0)  # can't judge -> not a regression, report says unavailable

    def test_header_quoting(self):
        m = compare.parse_header('# commit=a api=Vulkan device="NVIDIA GeForce GTX 1070 Ti" tier=low')
        self.assertEqual(m["device"], "NVIDIA GeForce GTX 1070 Ti")

    def test_one_pct_low(self):
        frames = [10.0] * 99 + [50.0]
        self.assertAlmostEqual(compare.one_pct_low_fps(frames), 20.0)


caps_merge = load_mod("caps_merge", "Packages/com.zfw.gpucaps/Tools~/caps_merge.py")
shot = load_mod("screenshot_diff", "Packages/com.zfw.perfrunner/Tools~/screenshot_diff.py")


def report(api, name, rt):
    return {"schema": 1, "api": api,
            "gpu": {"name": name, "vendorId": 4318, "dedicatedMemoryMB": 8192},
            "driver": {"version": "566.36", "raw": 2374565888}, "os": "Windows 11",
            "features": {"raytracing": {"value": rt, "raw": "tier1" if rt == "supported" else ""},
                         "bindless": {"value": "supported", "raw": "tier3"}},
            "unity": {"supportsComputeShaders": True}}


class CapsMergeTests(unittest.TestCase):
    def test_matrix_highlights_differences(self):
        d = tempfile.mkdtemp()
        paths = []
        for i, (api, rt) in enumerate((("Direct3D12", "unsupported"), ("Vulkan", "unsupported"), ("Metal", "supported"))):
            p = os.path.join(d, f"c{i}.json"); json.dump(report(api, "GPU", rt), open(p, "w")); paths.append(p)
        out = os.path.join(d, "m.html")
        self.assertEqual(caps_merge.main([*paths, "-o", out]), 0)
        doc = open(out, encoding="utf-8").read()
        self.assertIn('<tr class="diff"><td class=k>features.raytracing', doc)
        self.assertIn('<tr class=""><td class=k>features.bindless', doc)
        self.assertIn('title="tier1"', doc)

    def test_rejects_unknown_schema(self):
        d = tempfile.mkdtemp(); p = os.path.join(d, "x.json")
        json.dump({"schema": 99}, open(p, "w"))
        self.assertEqual(caps_merge.main([p, "-o", os.path.join(d, "m.html")]), 2)


def gradient(w, h, shift=0):
    return bytes(v for y in range(h) for x in range(w)
                 for v in ((x * 7 + shift) & 255, (y * 5) & 255, ((x ^ y) * 3) & 255))


class ScreenshotDiffTests(unittest.TestCase):
    def test_roundtrip_through_system_encoder(self):
        """sips re-encodes with adaptive row filters (Sub/Up/Avg/Paeth) -> exercises the decoder."""
        d = tempfile.mkdtemp(); w, h = 97, 61; px = gradient(w, h)
        src, dst = os.path.join(d, "a.png"), os.path.join(d, "b.png")
        shot.write_png(src, w, h, px)
        if os.system(f"sips -s format png '{src}' --out '{dst}' >/dev/null 2>&1") != 0:
            self.skipTest("sips not available")
        self.assertEqual(shot.read_png(dst), (w, h, px))

    def test_all_five_row_filters(self):
        """Hand-encode rows cycling None/Sub/Up/Average/Paeth and check the decoder inverts each."""
        w, h, bpp = 13, 10, 3; px = gradient(w, h); stride = w * bpp
        rows, prev = [], bytes(stride)
        for y in range(h):
            cur = px[y * stride:(y + 1) * stride]; ft = y % 5; enc = bytearray()
            for x in range(stride):
                a = cur[x - bpp] if x >= bpp else 0; b = prev[x]; c = prev[x - bpp] if x >= bpp else 0
                p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pred = [0, a, b, (a + b) >> 1, a if pa <= pb and pa <= pc else (b if pb <= pc else c)][ft]
                enc.append((cur[x] - pred) & 255)
            rows.append(bytes([ft]) + enc); prev = cur
        chunk = lambda t, b: struct.pack(">I", len(b)) + t + b + struct.pack(">I", zlib.crc32(t + b))
        p = os.path.join(tempfile.mkdtemp(), "f.png")
        open(p, "wb").write(shot.PNG_SIG + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                            + chunk(b"IDAT", zlib.compress(b"".join(rows))) + chunk(b"IEND", b""))
        self.assertEqual(shot.read_png(p), (w, h, px))

    def test_identical_and_changed(self):
        A, B = tempfile.mkdtemp(), tempfile.mkdtemp(); w, h = 40, 30
        shot.write_png(os.path.join(A, "s01.png"), w, h, gradient(w, h))
        shot.write_png(os.path.join(B, "s01.png"), w, h, gradient(w, h))
        out = os.path.join(tempfile.mkdtemp(), "d.html")
        self.assertEqual(shot.main([A, B, "-o", out]), 0)
        shot.write_png(os.path.join(B, "s01.png"), w, h, gradient(w, h, shift=40))
        self.assertEqual(shot.main([A, B, "-o", out]), 1)


if __name__ == "__main__":
    unittest.main(verbosity=2)
