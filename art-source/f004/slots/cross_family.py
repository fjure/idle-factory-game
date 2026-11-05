"""F004 T435: cross-family checks of the real plates and floor tile against the sprites of the other families.

Read-only use of the files of T434 (machines, accents), T437 (badges, markers, selection frame, lock labels): nothing
here writes outside the T435 families. The world backdrop of a slot is built as the game draws it: a floor lattice
(the tile placed at the rectangular lattice pitch of 70 x 54 art px, Art Bible 3.5, centred on the footprint centre of the slot) under the plate.

The floor holds only `floor_0` and `floor_1`, so the tone sets seen by an outline or an overlay ring do not depend on
where the lattice phase lies relative to a slot; four phases (tile centre, half a pitch in x, half a pitch in y, both) are
evaluated anyway and the worst value is recorded.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image

from art_pipeline import checks, checks_v1 as c1
from art_pipeline.palette import Palette

CANVAS = (80, 64)
CENTRE = (40, 49)                       # footprint centre = pivot (40, 15 from the bottom), canvas px y down
LATTICE = ((70, 0), (0, 54))            # floor lattice vectors (rectangular period 70 x 54, Art Bible 3.5)
PHASES = ((0, 0), (35, 0), (0, 27), (35, 27))   # tile-centre offsets from the footprint centre (canonical first)
MACHINES = ("miner", "smelter", "press", "assembler")
LABEL_ALLOWED_EXTRA = frozenset({"dim_1"})   # T437: the digit sits on the locked-plate tones dim_0 / dim_1


def _load(p: Path) -> Image.Image:
    return Image.open(p).convert("RGBA")


def load_sources(art: Path) -> dict:
    """Pixel sprites of the other families, as committed (sorted, deterministic)."""
    s: dict = {"machines": {}, "badges": {}, "markers": {}, "labels": {}, "frame": None}
    for m in MACHINES:
        s["machines"][m] = [_load(art / "machines" / m / "pixel" / ("f004-machine-%s-run_%02d.png" % (m, f))) for f in range(6)]
    for p in sorted((art / "cues").glob("f003-badge-*/f003-badge-*_00.png")):
        s["badges"][p.parent.name] = _load(p)
    for p in sorted((art / "markers").glob("f003-marker-*/f003-marker-*_00.png")):
        s["markers"][p.parent.name] = _load(p)
    for n in ("l2", "l3", "l4", "l5"):
        s["labels"]["f004-label-lock-" + n] = _load(art / "slots" / ("f004-label-lock-" + n) / ("f004-label-lock-%s_00.png" % n))
    s["frame"] = _load(art / "slots" / "f003-slot-selected-frame" / "f003-slot-selected-frame_00.png")
    return s


def lattice_floor(tile: Image.Image, phase: tuple[int, int] = (0, 0), canvas: tuple[int, int] = CANVAS,
                  centre: tuple[int, int] = CENTRE) -> Image.Image:
    """The floor lattice under one slot: tile centres at `centre + phase + a * (70, 0) + b * (0, 54)`."""
    out = Image.new("RGBA", canvas, (0, 0, 0, 0))
    tw, th = tile.size
    cx, cy = centre[0] + phase[0], centre[1] + phase[1]
    for a in range(-3, 4):
        for b in range(-3, 4):
            x = cx + a * LATTICE[0][0] + b * LATTICE[1][0] - tw // 2
            y = cy + a * LATTICE[0][1] + b * LATTICE[1][1] - th // 2
            out.paste(tile, (x, y), tile)
    return out


def world(plate: Image.Image | None, tile: Image.Image, phase: tuple[int, int]) -> Image.Image:
    bg = lattice_floor(tile, phase)
    if plate is not None:
        bg.paste(plate, (0, 0), plate)
    return bg


def tones_of(img: Image.Image, palette: Palette) -> set[str]:
    return {palette.id_of(tuple(p[:3])) or "unknown" for p in img.convert("RGBA").getdata() if p[3]}


def _agg(name: str, rows: list[dict], key: str = "minShare") -> checks.CheckResult:
    ok = all(r["passed"] for r in rows)
    worst = min(rows, key=lambda r: r[key]) if rows and key in rows[0] else None
    det = {"cases": len(rows), "failed": [r for r in rows if not r["passed"]][:12]}
    if worst is not None:
        det["worst"] = worst
    return checks.CheckResult(name, ok, det)


def _outline_row(label: str, img: Image.Image, bd: Image.Image, palette: Palette, tones=None) -> dict:
    r = c1.check_outline_contrast(img, bd, palette, tones=tones)
    shares = {t: d["share"] for t, d in r.details["tones"].items()}
    lo = min(shares.items(), key=lambda kv: kv[1]) if shares else ("none", 0.0)
    return {"case": label, "passed": r.passed, "minShare": lo[1], "minTone": lo[0], "tones": shares,
            "ignoredTones": r.details["ignoredTones"]}


def _informational(r: checks.CheckResult) -> checks.CheckResult:
    """A measurement that is reported but not gated: `passed` stays True, `wouldPass` carries the verdict."""
    would = r.passed
    r.passed = True
    r.details["gated"] = False
    r.details["wouldPass"] = would
    return r


def machine_outline(sources: dict, plate: Image.Image, tile: Image.Image, palette: Palette) -> list[checks.CheckResult]:
    """T434 bodies over the real occupied plate (rule C5, section 9.3), 24 frames.

    Gated (the T434 method): backdrop = the plate sprite, transparent where the floor shows, so bare floor is evaluated
    twice (`floor_0`, `floor_1`) over exactly the same pairs; scored tones are the C5 set; the second check scores every
    tone found (the plate must add none outside the set). Informational: the physical lattice (floor tile pixels under
    the plate, 4 phases), where `floor_1` is only the few pixels of the two detail clusters, so its share rests on a
    handful of pairs."""
    gated, anyt, phys = [], [], []
    per_machine = {}
    for m, frames in sources["machines"].items():
        worst = {"minShare": 2.0}
        for i, f in enumerate(frames):
            case = "%s frame %d" % (m, i)
            r5 = _outline_row(case, f, plate, palette, tones=c1.BODY_BACKDROP_TONES)
            gated.append(r5)
            anyt.append(_outline_row(case, f, plate, palette, tones=None))
            if r5["minShare"] < worst["minShare"]:
                worst = r5
            for ph in PHASES:
                phys.append(_outline_row("%s phase %s" % (case, ph), f, world(plate, tile, ph), palette))
        per_machine[m] = {"minShare": round(worst["minShare"], 4), "tone": worst["minTone"], "case": worst["case"],
                          "shares": {k: round(v, 4) for k, v in worst["tones"].items()}}
    c5 = _agg("cross_machines_outline_contrast_C5", gated)
    c5.details["perMachine"] = per_machine
    c5.details["note"] = ("backdrop = real occupied plate, bare floor evaluated as floor_0 and floor_1 (T434 method); "
                          "scored tones plate_1, plate_2, ink_0, floor_0, floor_1")
    al = _agg("cross_machines_outline_contrast_all_tones", anyt)
    al.details["note"] = "every tone found behind the outline is scored (tones=None)"
    ph = _informational(_agg("cross_machines_outline_contrast_physical_floor", phys))
    ph.details["note"] = ("backdrop = floor lattice under the real plate, 4 phases; floor_1 exists only as the detail clusters, "
                          "so its share is a few pairs. Reported, not gated")
    return [c5, al, ph]


def overlay_checks(sources: dict, plates: dict[str, Image.Image], tile: Image.Image, palette: Palette) -> list[checks.CheckResult]:
    """T437 overlays over the real plates and floor: backdrop ring tone sets (C1, C3, C8, physical lattice, 4 phases) and
    outline contrast (9.3; gated on the plate sprite alone as in T437, informational on the physical lattice)."""
    a_fr = c1.a_fr(palette)
    res = []

    def sweep(name, items, allowed, plate_names):
        ring_rows, out_rows, phys_rows = [], [], []
        for plate_name in plate_names:
            for key, img in items.items():
                out_rows.append(_outline_row("%s over %s" % (key, plate_name), img, plates[plate_name], palette))
            for ph in PHASES:
                bd = world(plates[plate_name], tile, ph)
                for key, img in items.items():
                    case = "%s over %s phase %s" % (key, plate_name, ph)
                    r = c1.check_overlay_backdrop(img, bd, allowed, palette)
                    ring_rows.append({"case": case, "passed": r.passed, "ringPixels": r.details["ringPixels"],
                                      "forbiddenTones": r.details["forbiddenTones"], "minShare": 1.0 if r.passed else 0.0})
                    phys_rows.append(_outline_row(case, img, bd, palette))
        res.append(_agg("cross_%s_overlay_backdrop" % name, ring_rows))
        res.append(_agg("cross_%s_outline_contrast" % name, out_rows))
        res.append(_informational(_agg("cross_%s_outline_contrast_physical_floor" % name, phys_rows)))

    sweep("badges", sources["badges"], c1.A_OV, ("occupied",))
    sweep("markers", sources["markers"], c1.A_OV, ("occupied",))
    sweep("frame", {"f003-slot-selected-frame": sources["frame"]}, a_fr, ("occupied", "empty", "locked"))
    sweep("labels", sources["labels"], c1.A_OV | LABEL_ALLOWED_EXTRA, ("locked",))
    return res


def tone_audit(name: str, img: Image.Image, palette: Palette, allowed: set[str] | frozenset[str]) -> checks.CheckResult:
    used = tones_of(img, palette)
    bad = sorted(used - set(allowed))
    return checks.CheckResult(name, not bad, {"tones": sorted(used), "outsideAllowed": bad})
