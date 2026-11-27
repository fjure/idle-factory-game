"""T549: device (real APK on the AVD) and extra look-point sheets. Evidence only, no verdict. Reuses the helpers of t549_sheets.py.

Usage: python t549_device_sheets.py --visual DIR (the visual-acceptance-f006 folder) --before-png DIR --before-device DIR --commit SHA --baseline SHA
"""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image

import t549_sheets as ts

STATES = ["no-panel", "card", "details", "building", "hub", "system-view", "offline-summary"]
BEFORE_RIG = {"no-panel": "no-panel", "card": "machine-sheet", "details": "machine-sheet", "building": "build-sheet", "hub": "contracts", "system-view": "contracts", "offline-summary": "offline-summary"}
BEFORE_DEVICE = {"no-panel": "no-panel", "building": "build-sheet", "hub": "contracts", "system-view": "contracts"}  # T502 device captures (100 % only)
AFTER_DEVICE = {
    ("portrait", 100): {"no-panel": "portrait-100/02-f006-start", "card": "portrait-100/04-f006-select-card", "details": "portrait-100/d-details", "building": "portrait-100/11-f006-build-1-card",
                        "hub": "portrait-100/07-f006-hub-1-open", "system-view": "portrait-100/08-f006-hub-2-contracts", "offline-summary": "portrait-100/a-offline"},
    ("landscape", 100): {"no-panel": "landscape-100/b-nopanel", "card": "landscape-100/c-card", "details": "landscape-100/d-details", "building": "landscape-100/h-build",
                         "hub": "landscape-100/f-hub", "system-view": "landscape-100/g-system", "offline-summary": "landscape-100/a-offline"},
    ("portrait", 200): {"no-panel": "portrait-200/b-nopanel", "card": "portrait-200/c-card", "details": "portrait-200/d-details", "building": "portrait-200/h-build",
                        "hub": "portrait-200/f-hub", "system-view": "portrait-200/g-system", "offline-summary": "portrait-200/a-offline"},
    ("landscape", 200): {"no-panel": "landscape-200/b-nopanel", "card": "landscape-200/c-card", "details": "landscape-200/d-details", "building": "landscape-200/h-build",
                         "hub": "landscape-200/f-hub", "system-view": "landscape-200/g-system", "offline-summary": "landscape-200/a-offline"},
}
RIG_CLASS = {"portrait": "emulator-portrait-1080x2400", "landscape": "emulator-landscape-2400x1080"}


def thumb(image, k):
    return None if image is None else image.resize((max(1, round(image.width * k)), max(1, round(image.height * k))), Image.LANCZOS)


def one(a, orient, pct, state):
    cls = RIG_CLASS[orient]
    d = a.visual / "raw" / "device"
    items = [("BEFORE rig (T502 %s)" % BEFORE_RIG[state], ts.load(a.before_png / f"{cls}-{BEFORE_RIG[state]}-{pct}.png"))]
    if pct == 100 and state in BEFORE_DEVICE:
        items.append(("BEFORE device (T502 %s, fresh run, other fixture)" % BEFORE_DEVICE[state], ts.load(a.before_device / f"emulator-{orient}-{BEFORE_DEVICE[state]}-100.png")))
    items.append((f"AFTER device (real APK, {a.commit[:8]})", ts.load(d / (AFTER_DEVICE[(orient, pct)][state] + ".png"))))
    items.append((f"AFTER rig ({a.commit[:8]}, {state})", ts.load(a.visual / "raw" / "rig" / f"{cls}-{state}-{pct}.png")))
    w, h = next(i for _, i in items if i is not None).size
    cells = [ts.labeled(i, label, (w, h), 20) for label, i in items]
    rows = [cells] if w < h else [cells[:2], cells[2:]]
    return ts.fit(ts.grid(rows, f"T549 emulator {orient} {pct} % text - {state} (evidence only)", 20 if w < h else 30))


def overview(a, orient, pct):
    cls = RIG_CLASS[orient]
    d = a.visual / "raw" / "device"
    sample = ts.load(d / (AFTER_DEVICE[(orient, pct)]["no-panel"] + ".png"))
    w, h = sample.size
    k = min((ts.MAX_SIDE - 100) / (len(STATES) * w), (ts.MAX_SIDE - 200) / (3 * h))
    rows = []
    for title, fn in (("BEFORE rig", lambda s: a.before_png / f"{cls}-{BEFORE_RIG[s]}-{pct}.png"),
                      ("AFTER device", lambda s: d / (AFTER_DEVICE[(orient, pct)][s] + ".png")),
                      ("AFTER rig", lambda s: a.visual / "raw" / "rig" / f"{cls}-{s}-{pct}.png")):
        cells = []
        for s in STATES:
            im = thumb(ts.load(fn(s)), k)
            cells.append(ts.labeled(im, f"{title} {s}", (round(w * k), round(h * k)), 12))
        rows.append(cells)
    return ts.fit(ts.grid(rows, f"T549 emulator {orient} {pct} % overview - rows BEFORE rig, AFTER device, AFTER rig", 14))


def montage(items, title, height=900):
    cells = []
    for label, path in items:
        im = ts.load(path)
        if im is not None:
            k = height / im.height
            im = im.resize((round(im.width * k), height), Image.LANCZOS)
            cells.append(ts.labeled(im, label, im.size, 16))
    return ts.fit(ts.grid([cells], title, 16))


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--visual", type=Path, required=True)
    p.add_argument("--before-png", type=Path, required=True)
    p.add_argument("--before-device", type=Path, required=True)
    p.add_argument("--commit", required=True)
    p.add_argument("--baseline", required=True)
    a = p.parse_args()
    out = a.visual / "sheets"
    d = a.visual / "raw" / "device"
    n = 0
    for orient in ("portrait", "landscape"):
        for pct in (100, 200):
            ts.save(overview(a, orient, pct), out / f"device-overview-emulator-{orient}-{pct}.png")
            n += 1
            for state in STATES:
                ts.save(one(a, orient, pct, state), out / f"device-sheet-emulator-{orient}-{pct}-{state}.png")
                n += 1
    # reduced motion: three frames 0.4 s apart with and without the preference, cropped to the region that animates
    box = (200, 780, 800, 960)
    cells = []
    for label, prefix in (("motion on (Follow system, off)", "m-motion"), ("reduced motion On", "m-reduced")):
        row = []
        for i in (1, 2, 3):
            im = ts.load(d / "portrait-100" / f"{prefix}-{i}.png")
            crop = im.crop(box).resize(((box[2] - box[0]) * 2, (box[3] - box[1]) * 2), Image.NEAREST)
            row.append(ts.labeled(crop, f"{label} frame {i} (+{(i - 1) * 0.4:.1f} s)", crop.size, 16))
        cells.append(row)
    ts.save(ts.grid(cells, "T549 reduced motion (emulator portrait 100 %, real APK): same crop 200..800 x 780..960, frames 0.4 s apart. Full-frame pixel differences: motion 3150 and 4410, reduced 0 and 0", 16), out / "reduced-motion-emulator-portrait.png")
    n += 1
    ts.save(montage([("no panel, motion", d / "portrait-100/m-motion-1.png"), ("no panel, reduced motion On", d / "portrait-100/m-reduced-1.png"),
                     ("Settings: Reduced motion Off selected", d / "portrait-100/s-settings-motion-off.png"), ("Settings: Reduced motion On selected", d / "portrait-100/s-settings-reduced-on.png")],
                    "T549 reduced motion - full frames and the Settings control (emulator portrait 100 %)"), out / "reduced-motion-full-frames.png")
    n += 1
    s = d / "small-classes"
    ts.save(montage([("phone 360x640 @160 dpi, no panel", s / "p360-nopanel.png"), ("phone 360x640 card open", s / "p360-card.png"),
                     ("phone 640x360 @160 dpi, 200 % text, no panel", s / "l640-nopanel.png"), ("640x360 200 %: build card, Confirm", s / "l640-build-200.png"),
                     ("640x360 200 %: Welcome back", s / "l640-offline.png")], "T549 smallest phone classes on the AVD (wm size 360x640 and 640x360, density 160): real APK", 640), out / "lookpoints-smallest-phone.png")
    n += 1
    ts.save(montage([("tablet portrait 768x1024 @160 dpi, fresh run", s / "t-tablet-portrait-fresh.png"), ("tablet landscape 1024x768 @160 dpi, fresh run", s / "t-tablet-landscape-fresh.png")],
                    "T549 tablet side panel hint (fresh run, real APK, wm size override)", 900), out / "lookpoints-tablet-hint.png")
    n += 1
    c = d / "contracts-w005l"
    ts.save(montage([("Welcome back, contract auto-delivered (real offline return)", c / "a-start.png"), ("hub with attention badges", c / "c-hub.png"), ("Contracts: offers", c / "d-contracts.png"),
                     ("accepted, ready to deliver", c / "e-accepted.png"), ("hand delivery: Delivered 167", c / "f-delivered-0.png")], "T549 contract moments on the W005-L checkpoint (emulator portrait 100 %, real APK)", 1000), out / "moments-contract.png")
    n += 1
    pd = d / "portrait-100"
    ts.save(montage([("card open, before Upgrade", pd / "04-f006-select-card.png"), ("after Upgrade", pd / "05-f006-upgrade.png")], "T549 upgrade moment (emulator portrait, real APK, W003-S set)", 1000), out / "moments-upgrade.png")
    n += 1
    e = d / "emu-capture-apk"
    for orient in ("portrait", "landscape"):
        ts.save(montage([(f"stage {i} {ts.STAGES[i]}", e / orient / f"sc006-emulator-s{i}.png") for i in range(3)],
                        f"T549 stage triptych - emulator {orient} - capture APK on the AVD - no before (new in F006)", 1000), out / f"triptych-emulator-{orient}.png")
        n += 1
    print(n)


if __name__ == "__main__":
    main()
