"""T410: summarize SurfaceFlinger TimeStats captures written by scripts/android-emulator-journey.ps1.

Usage: python summarize-fps.py <fps-*-surfaceflinger-timestats-full.txt> ...
The present2present histogram buckets are lower bounds; percentiles use nearest rank (as in T040/T065).
"""
import math
import re
import sys
from pathlib import Path

PACKAGE = "com.DefaultCompany.idlefactorygame"


def field(text, name):
    match = re.search(rf"(?m)^{re.escape(name)} = ([^\r\n]+)", text)
    return match.group(1).strip() if match else ""


def percentile(buckets, proportion):
    count = sum(buckets.values())
    if not count:
        return ""
    rank = math.ceil(count * proportion)
    for ms, freq in sorted(buckets.items()):
        rank -= freq
        if rank <= 0:
            return ms
    raise AssertionError("rank exceeded")


def summarize(path):
    dump = path.read_text(errors="replace")
    blocks = re.split(r"(?=layerName = )", dump)
    layers = [b for b in blocks if re.match(rf"layerName = .*SurfaceView\[{re.escape(PACKAGE)}", b)]
    if not layers:
        return {"file": path.name, "error": "no SurfaceView layer"}
    block = max(layers, key=lambda b: int(field(b, "totalFrames") or 0))
    hist = re.search(r"present2present histogram is as below:\s*([^\r\n]+)", block)
    buckets = {int(ms): int(c) for ms, c in re.findall(r"(\d+)ms=(\d+)", hist.group(1))} if hist else {}
    return {
        "file": path.name,
        "frames": field(block, "totalFrames"),
        "dropped": field(block, "droppedFrames"),
        "avg_fps": field(block, "averageFPS"),
        "p50_ms": percentile(buckets, 0.50),
        "p95_ms": percentile(buckets, 0.95),
        "p99_ms": percentile(buckets, 0.99),
        "ge_50ms": sum(c for ms, c in buckets.items() if ms >= 50),
        "histogram": " ".join(f"{ms}ms={c}" for ms, c in sorted(buckets.items())),
    }


if __name__ == "__main__":
    print("file\tframes\tdropped\tavg_fps\tp50_ms\tp95_ms\tp99_ms\tge_50ms\thistogram")
    for arg in sys.argv[1:]:
        r = summarize(Path(arg))
        if "error" in r:
            print(r["file"], r["error"], sep="\t")
        else:
            print("\t".join(str(r[k]) for k in ("file", "frames", "dropped", "avg_fps", "p50_ms", "p95_ms", "p99_ms", "ge_50ms", "histogram")))
