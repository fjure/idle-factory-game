"""T410: guest CPU and PSS for the capture windows of a journey run, from scripts/f004-b1/sample-device.ps1 output.

Usage: python summarize-device.py <sampler-dir> <run-dir> [label ...]
A capture window ends when the journey writes fps-<label>-logcat.txt (file modification time) and lasts 60 s.
CPU = (utime+stime ticks difference) / (CLK_TCK * seconds) in percent of one core; the first sample inside the
window is the start reference (the T040 rule of excluding the first top sample is met by differencing).
PSS = the "TOTAL PSS" and the Graphics / GL mtrack values of the last dumpsys meminfo sample inside the window.
"""
import re
import sys
from pathlib import Path

CLK_TCK = 100.0
WINDOW_S = 60


def cpu_rows(path):
    rows = []
    for line in path.read_text().splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) >= 3:
            rows.append((int(parts[0]), int(parts[1]), int(parts[2]), float(parts[3]) if len(parts) > 3 and parts[3] else float("nan"), int(parts[4]) if len(parts) > 4 and parts[4] else -1))
    return rows


def mem_blocks(path):
    text = path.read_text(errors="replace")
    blocks = []
    for chunk in re.split(r"(?m)^=== ", text):
        m = re.match(r"epoch_ms=(\d+) pid=(\d+)", chunk)
        if m:
            blocks.append((int(m.group(1)), chunk))
    return blocks


def grab(chunk, label):
    m = re.search(rf"(?m)^\s*{re.escape(label)}:?\s+(\d+)", chunk)
    return int(m.group(1)) if m else None


def main():
    samplers = [Path(x) for x in sys.argv[1].split(",")]
    run = Path(sys.argv[2])
    labels = sys.argv[3:] or ["control-1", "producing", "control-2"]
    cpu = sorted(r for d in samplers for r in cpu_rows(d / "cpu-samples.tsv"))
    mem = [b for d in samplers if (d / "meminfo-samples.txt").exists() for b in mem_blocks(d / "meminfo-samples.txt")]
    print("label\tcpu_pct_of_one_core\tcpu_samples\thost_busy_threads_mean\tgame_procs_max\tpss_total_kb\tgraphics_kb\tgl_mtrack_kb\tnative_heap_kb\tcode_kb")
    for label in labels:
        f = run / f"fps-{label}-logcat.txt"
        if not f.exists():
            continue
        end = int(f.stat().st_mtime * 1000)
        start = end - WINDOW_S * 1000
        inwin = [r for r in cpu if start <= r[0] <= end]
        if len(inwin) >= 2:
            dt = (inwin[-1][0] - inwin[0][0]) / 1000.0
            dticks = inwin[-1][2] - inwin[0][2]
            pct = round(100.0 * dticks / (CLK_TCK * dt), 1) if dt > 0 and inwin[-1][1] == inwin[0][1] else "pid-changed"
        else:
            pct = "n/a"
        busy = [r[3] for r in inwin if r[3] == r[3]]
        host = round(sum(busy) / len(busy), 2) if busy else "n/a"
        games = [r[4] for r in inwin if r[4] >= 0]
        host = f"{host}	{max(games) if games else 'n/a'}"
        mw = [b for b in mem if start <= b[0] <= end + 15000]
        if mw:
            chunk = mw[-1][1]
            tot = re.search(r"(?m)^\s*TOTAL PSS:\s+(\d+)", chunk) or re.search(r"(?m)^\s*TOTAL\s+(\d+)", chunk)
            print(f"{label}\t{pct}\t{len(inwin)}\t{host}\t{tot.group(1) if tot else ''}\t{grab(chunk, 'Graphics')}\t{grab(chunk, 'GL mtrack')}\t{grab(chunk, 'Native Heap')}\t{grab(chunk, 'Code')}")
        else:
            print(f"{label}\t{pct}\t{len(inwin)}\t{host}\t\t\t\t\t")


main()
