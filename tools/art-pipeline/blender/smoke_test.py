"""Reproducibility smoke test for the Blender route (R09): render the test cube twice and compare.

  python smoke_test.py [--blender <exe>] [--out <dir>] [--projection <p>]

Standard library only (decodes the 8-bit RGBA PNG itself), so it needs neither Pillow nor the
uv project. Metric (per frame): pixels whose opaque mask (alpha >= 128) differs, plus pixels
opaque in both whose RGB differs, divided by the opaque pixels of the first render. This is a
stricter proxy of the R09 definition (palette-index mismatches) because it runs before the pixel
pass. Tolerance: 0.5 % of opaque pixels. Exit 0 = within tolerance, 1 = exceeded, 2 = tooling unavailable.
"""
import argparse
import hashlib
import os
import struct
import subprocess
import sys
import zlib

TOLERANCE = 0.005
HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"


def read_png_rgba(path):
    with open(path, "rb") as f:
        data = f.read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n"
    pos, idat, w = 8, b"", None
    while pos < len(data):
        n, typ = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        if typ == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", body)
            assert (depth, ctype, interlace) == (8, 6, 0), "expected 8-bit RGBA, non-interlaced"
        elif typ == b"IDAT":
            idat += body
        pos += 12 + n
    raw = zlib.decompress(idat)
    stride, bpp = w * 4, 4
    rows, prev = [], bytearray(stride)
    for y in range(h):
        base = y * (stride + 1)
        ft, line = raw[base], bytearray(raw[base + 1:base + 1 + stride])
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if ft == 1:
                line[i] = (line[i] + a) & 255
            elif ft == 2:
                line[i] = (line[i] + b) & 255
            elif ft == 3:
                line[i] = (line[i] + ((a + b) >> 1)) & 255
            elif ft == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        rows.append(line)
        prev = line
    return w, h, rows


def diff_fraction(p1, p2):
    w, h, a = read_png_rgba(p1)
    w2, h2, b = read_png_rgba(p2)
    assert (w, h) == (w2, h2), "size mismatch"
    opaque = changed = 0
    for y in range(h):
        for x in range(w):
            i = x * 4
            oa, ob = a[y][i + 3] >= 128, b[y][i + 3] >= 128
            opaque += oa
            if oa != ob or (oa and a[y][i:i + 3] != b[y][i:i + 3]):
                changed += 1
    return changed, opaque


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--blender", default=os.environ.get("BLENDER_EXE", DEFAULT_BLENDER))
    ap.add_argument("--out", default=os.path.join(HERE, "smoke"))
    ap.add_argument("--projection", default="oblique-3-4")
    args = ap.parse_args()
    if not os.path.isfile(args.blender):
        print("tooling unavailable: Blender not found at " + args.blender)
        return 2
    runs = []
    for tag in ("run1", "run2"):
        out = os.path.join(args.out, tag)
        cmd = [args.blender, "-b", os.path.join(HERE, "rig-v1.blend"), "--factory-startup",
               "--python-exit-code", "1", "-P", os.path.join(HERE, "render.py"), "--", "cube",
               "--projection", args.projection, "--out", out]
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0:
            print(r.stdout[-2000:], r.stderr[-2000:])
            print("render failed with exit code", r.returncode)
            return 1
        runs.append(os.path.join(out, "cube.png"))
    hashes = [hashlib.sha256(open(r, "rb").read()).hexdigest() for r in runs]
    print("PNG sha256 identical:", hashes[0] == hashes[1], hashes[0])
    changed, opaque = diff_fraction(*runs)
    frac = changed / max(opaque, 1)
    print("opaque pixels: %d, differing pixels: %d, fraction: %.4f %% (tolerance %.1f %%)"
          % (opaque, changed, frac * 100, TOLERANCE * 100))
    print("RESULT", "OK" if frac <= TOLERANCE else "EXCEEDED")
    return 0 if frac <= TOLERANCE else 1


if __name__ == "__main__":
    sys.exit(main())
