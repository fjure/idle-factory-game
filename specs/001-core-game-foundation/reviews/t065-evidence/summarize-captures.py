"""Summarize T040 protocol TimeStats, CPU and memory from a T065 raw root.

Usage: python summarize-captures.py <raw-root> <label>
The SurfaceFlinger histogram buckets are lower bounds. Percentiles use nearest rank.
"""

import csv
import math
import re
import sys
from pathlib import Path


def field(text, name):
    match = re.search(rf"(?m)^{re.escape(name)} = ([^\r\n]+)", text)
    return match.group(1).strip() if match else ""


def percentile(buckets, proportion):
    count = sum(buckets.values())
    if not count:
        return ""
    rank = math.ceil(count * proportion)
    for milliseconds, frequency in sorted(buckets.items()):
        rank -= frequency
        if rank <= 0:
            return milliseconds
    raise AssertionError("Histogram rank exceeded count")


def pss(path):
    text = path.read_text(errors="replace")
    match = re.search(r"(?m)^\s*TOTAL\s+(\d+)\s+.*$", text)
    return int(match.group(1)) if match else ""


def summarize(run, label):
    time_stats = (run / "surfaceflinger-timestats.txt").read_text(errors="replace")
    layer = re.search(
        r"(?m)^layerName = [^\r\n]*SurfaceView\[com\.DefaultCompany\.idlefactorygame/"
        r"com\.idlefactory\.player\.IdleFactoryGameActivity\][^\r\n]*",
        time_stats,
    )
    if not layer:
        raise ValueError(f"Unity SurfaceView layer missing: {run}")
    time_stats = time_stats[layer.end() :]
    histogram = re.search(r"present2present histogram is as below:\s*([^\r\n]+)", time_stats)
    if not histogram:
        raise ValueError(f"Present interval histogram missing: {run}")
    buckets = {int(ms): int(count) for ms, count in re.findall(r"(\d+)ms=(\d+)", histogram.group(1))}
    cpu_samples = []
    for line in (run / "cpu-top.txt").read_text(errors="replace").splitlines():
        if line.rstrip().endswith("com.DefaultCompany.idlefactorygame"):
            parts = line.split()
            cpu_samples.append(float(parts[8]))
    state = (run / "capture-start-state.txt").read_text(errors="replace")
    transcript = (run.parent / "capture-transcript.txt").read_text(errors="replace")
    elapsed = re.search(rf"(?m)^run_{run.name[-1]}_capture_elapsed_seconds=([\d.]+)", transcript)
    return {
        "set": label,
        "run": run.name,
        "capture_seconds": elapsed.group(1) if elapsed else "",
        "state_sha256": re.search(r"factory_state_sha256=([0-9a-f]+)", state).group(1),
        "frames": field(time_stats, "totalFrames"),
        "dropped": field(time_stats, "droppedFrames"),
        "janky": field(time_stats, "jankyFrames"),
        "average_fps": field(time_stats, "averageFPS"),
        "p50_ms": percentile(buckets, 0.50),
        "p95_ms": percentile(buckets, 0.95),
        "p99_ms": percentile(buckets, 0.99),
        "intervals_ge_50_ms": sum(count for ms, count in buckets.items() if ms >= 50),
        "cpu_mean_excluding_first_percent": round(sum(cpu_samples[1:]) / len(cpu_samples[1:]), 2)
        if len(cpu_samples) > 1 else "",
        "cpu_samples": len(cpu_samples),
        "pss_start_kib": pss(run / "meminfo-start.txt"),
        "pss_end_kib": pss(run / "meminfo-end.txt"),
    }


if __name__ == "__main__":
    root = Path(sys.argv[1])
    rows = [summarize(root / f"run-{number}", sys.argv[2]) for number in (1, 2, 3)]
    writer = csv.DictWriter(sys.stdout, fieldnames=rows[0].keys(), lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
