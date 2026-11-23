#!/usr/bin/env python3
"""把 .unitypackage 解到工程目录，可按前缀排除文件。

为什么不用 Unity 自带导入：有的商店包会带 Packages/manifest.json，
导入时会整份覆盖本工程的包清单（例如把 Input System 降级、删掉测试框架）。
.unitypackage 就是一个 tar.gz：每个资源一个 <guid>/ 目录，里面是
asset（文件内容）、asset.meta（含 GUID 的导入设置）、pathname（工程内路径）。
原样写回 asset + asset.meta，Unity 下次打开时按 GUID 导入，引用关系不变。
"""
import argparse, os, sys, tarfile

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("package"); ap.add_argument("project")
    ap.add_argument("--exclude", action="append", default=[], help="按路径前缀排除")
    a = ap.parse_args()
    root = os.path.realpath(a.project)
    entries = {}
    with tarfile.open(a.package, "r:gz") as tf:
        for m in tf.getmembers():
            if not m.isfile(): continue
            parts = m.name.lstrip("./").split("/")
            if len(parts) != 2: continue
            entries.setdefault(parts[0], {})[parts[1]] = m
        written = skipped = 0
        for guid, files in entries.items():
            if "pathname" not in files: continue
            path = tf.extractfile(files["pathname"]).read().decode("utf-8").splitlines()[0].strip()
            if any(path.startswith(p) for p in a.exclude) or not path.startswith("Assets/"):
                skipped += 1; print("skip", path); continue
            dst = os.path.realpath(os.path.join(root, path))
            if not dst.startswith(root + os.sep):  # 防路径穿越
                sys.exit(f"unsafe path: {path}")
            if "asset" in files:
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                with open(dst, "wb") as f: f.write(tf.extractfile(files["asset"]).read())
            else:
                os.makedirs(dst, exist_ok=True)
            if "asset.meta" in files:
                with open(dst + ".meta", "wb") as f: f.write(tf.extractfile(files["asset.meta"]).read())
            written += 1
    print(f"written {written}, skipped {skipped}")

if __name__ == "__main__":
    main()
