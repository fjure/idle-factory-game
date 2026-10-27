"""T410: exit 1 when a game process ran, or the host was busier than a threshold, between two epoch-millisecond marks.

Usage: python check-clean.py <sampler-dir> <start_epoch_ms> <end_epoch_ms> [max_host_busy_threads]
"""
import sys
from pathlib import Path

d = Path(sys.argv[1])
start, end = int(sys.argv[2]), int(sys.argv[3])
limit = float(sys.argv[4]) if len(sys.argv) > 4 else 4.5
rows = [l.split("\t") for l in (d / "cpu-samples.tsv").read_text().splitlines()[1:]]
inwin = [r for r in rows if len(r) >= 5 and start <= int(r[0]) <= end]
games = max((int(r[4]) for r in inwin), default=-1)
busy = max((float(r[3]) for r in inwin), default=0.0)
mean = sum(float(r[3]) for r in inwin) / len(inwin) if inwin else 0.0
print(f"samples={len(inwin)} games_max={games} host_busy_max={busy:.2f} host_busy_mean={mean:.2f}")
sys.exit(1 if (games != 0 or mean > limit) else 0)
