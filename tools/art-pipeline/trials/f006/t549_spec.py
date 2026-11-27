"""T549: capture specification for the F006 visual-acceptance sheets (stage triptych and the P3 look points at every screen class).

Reuses the T545 driver (F006TrialCapturePlayerTests.CaptureAll); jobs are the sc006 stage frames (Early / Partial / Established per FR-036) and the
P3 shots (hub dot, docked card, upgrade marker, ghost preview). Usage: python t549_spec.py <spec.json>
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import f006_trials as ft  # noqa: E402

CLASSES = {
    "phone-portrait-360x640": (360, 640), "phone-landscape-640x360": (640, 360),
    "phone-portrait-390x844": (390, 844), "phone-landscape-844x390": (844, 390),
    "tablet-portrait-768x1024": (768, 1024), "tablet-landscape-1024x768": (1024, 768),
    "desktop-1280x720": (1280, 720), "desktop-1920x1080": (1920, 1080),
}
UNLOCKED = (4, 8, 12)
MACHINES = ("miner", "smelter", "press", "assembler")


def stage_slots(unlocked: int, stage: int) -> list[dict]:
    slots = []
    for s in range(ft.SLOTS):
        if s >= unlocked:
            slots.append(ft._slot("locked", level=2 + s // 4))
        elif s % 3 == 2 and stage < 2:
            slots.append(ft._slot("empty"))
        else:
            slots.append(ft._slot("occupied", MACHINES[s % 4], "running", min(1 + stage, 5)))
    return slots


def build(path: Path, emulator: bool = False) -> int:
    """emulator=True: one class "emulator" at the device's own window (the Android driver ignores w and h), for the capture APK."""
    jobs = []
    classes = {"emulator": (1080, 2400)} if emulator else CLASSES
    for name, (w, h) in classes.items():
        base = {"class": name, "cls": name, "w": w, "h": h, "dpi": 420.0 if emulator else 96.0, "source": "emulator" if emulator else "windows-batch"}
        for stage, unlocked in enumerate(UNLOCKED):
            assert ft.stage_of(unlocked) == stage
            jobs.append({**base, "id": f"sc006-{name}-s{stage}", "trial": "sc006", "factoryUnlocked": unlocked, "expectStage": stage,
                         "mix": "t549", "slots": stage_slots(unlocked, stage)})
        for tile in ("p3-hub-dot", "p3-hub-no-dot", "p3-card-next-to-machine", "p3-upgrade-marker", "p3-ghost-preview"):
            jobs.append({**base, "id": f"sc005-{name}-{tile}", "trial": "sc005", "set": name,
                         "shot": {"id": tile, "p3": ft.P3_BY_ID[tile]["capture"]}})
    ft.write_json(path, {"schema": 1, "jobs": jobs})
    return len(jobs)


if __name__ == "__main__":
    print(build(Path(sys.argv[1]), "--emu" in sys.argv))
