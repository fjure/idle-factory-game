"""Automated sprite checks (contracts/art-pipeline.md, FR-023, SC-004).

Every check returns a CheckResult; `run_asset_checks` composes the per-sprite set and
`write_report` emits the deterministic `check-report.json`.
"""
from __future__ import annotations

import json
import math
import random
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image

from .color import CVD_MATRICES, RGB, contrast_ratio, luma, relative_luminance, rgb_to_hex, simulate_cvd
from .palette import INK_GROUP, RESERVED_CUE_GROUP, Palette, validate_palette
from .pixel_pass import footprint_rect, opaque_bbox, pivot_for

OVERHANG_MAX_CELLS = 0.5
FLASH_DELTA = 0.10
FLASH_MAX_PER_SECOND = 3
IOU_MAX = 0.70
IOU_SMALL = 32
CONTRAST_NONTEXT = 3.0
CONTRAST_TEXT = 4.5
CUE_MIN_PX = 12
MB = 1024 * 1024
FAMILY_BUDGET_MB = 25
TOTAL_BUDGET_MB = 150
SOURCE_FILE_DECISION_MB = 10


@dataclass
class CheckResult:
    name: str
    passed: bool
    details: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {"name": self.name, "passed": self.passed, "details": self.details}


def _pixels(img: Image.Image):
    img = img.convert("RGBA")
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            yield x, y, px[x, y]


# --- palette ---------------------------------------------------------------------

def check_palette_definition(palette: Palette) -> CheckResult:
    problems = validate_palette(palette)
    return CheckResult("palette_definition", not problems,
                       {"colors": len(palette.colors), "cap": palette.cap, "problems": problems})


def check_palette(frames: list[Image.Image], palette: Palette, allowed_groups: set[str] | None = None,
                  group_limits: dict[str, int] | None = None) -> CheckResult:
    """Every opaque pixel is a palette color; distinct colors used per group stay within the group
    budget (or a tighter per-asset limit); groups outside `allowed_groups` are unused."""
    used: dict[RGB, int] = {}
    for f in frames:
        for _x, _y, p in _pixels(f):
            if p[3]:
                used[(p[0], p[1], p[2])] = used.get((p[0], p[1], p[2]), 0) + 1
    unknown = sorted(rgb_to_hex(c) for c in used if palette.group_of(c) is None)
    per_group: dict[str, set[RGB]] = {}
    for c in used:
        g = palette.group_of(c)
        if g is not None:
            per_group.setdefault(g, set()).add(c)
    over = {}
    for g, cols in per_group.items():
        limit = min(palette.budgets[g], (group_limits or {}).get(g, palette.budgets[g]))
        if len(cols) > limit:
            over[g] = {"used": len(cols), "limit": limit}
    disallowed = sorted(g for g in per_group if allowed_groups is not None and g not in allowed_groups)
    passed = not unknown and not over and not disallowed
    return CheckResult("palette", passed, {"unknown": unknown, "overBudget": over, "disallowedGroups": disallowed,
                                           "usedColors": len(used)})


# --- canvas / pivot / alpha / frames ---------------------------------------------

def check_canvas(frames: list[Image.Image], canvas: tuple[int, int]) -> CheckResult:
    bad = [i for i, f in enumerate(frames) if f.size != tuple(canvas)]
    return CheckResult("canvas", not bad, {"expected": list(canvas), "badFrames": bad})


def check_pivot(frames: list[Image.Image], entry: dict) -> CheckResult:
    """Manifest pivot equals the footprint-derived pivot and the shared content rests on the footprint bottom."""
    canvas, footprint = tuple(entry["canvas"]), tuple(entry["footprintPx"])
    expected = pivot_for(canvas, footprint)
    boxes = [b for b in (opaque_bbox(f) for f in frames) if b]
    bottom_ok = bool(boxes) and max(b[3] for b in boxes) == canvas[1]
    pivot_ok = tuple(round(v, 6) for v in entry["pivot"]) == tuple(expected)
    return CheckResult("pivot", pivot_ok and bottom_ok,
                       {"expected": list(expected), "manifest": list(entry["pivot"]), "restsOnFootprint": bottom_ok})


def check_alpha(frames: list[Image.Image]) -> CheckResult:
    bad = []
    for i, f in enumerate(frames):
        n = sum(1 for _x, _y, p in _pixels(f) if p[3] not in (0, 255))
        if n:
            bad.append({"frame": i, "pixels": n})
    return CheckResult("alpha", not bad, {"partialAlpha": bad})


def check_frames(frames: list[Image.Image], expected: int) -> CheckResult:
    return CheckResult("frames", len(frames) == expected, {"expected": expected, "actual": len(frames)})


def check_outline(frames: list[Image.Image], palette: Palette) -> CheckResult:
    """Every silhouette-boundary pixel (4-neighbour transparent or beyond the canvas) is palette ink."""
    ink = {c.rgb for c in palette.colors_in(INK_GROUP)}
    bad = []
    for i, f in enumerate(frames):
        px = f.convert("RGBA").load()
        w, h = f.size
        boundary = missing = 0
        for y in range(h):
            for x in range(w):
                if px[x, y][3] != 255:
                    continue
                edge = any(not (0 <= x + dx < w and 0 <= y + dy < h) or px[x + dx, y + dy][3] != 255
                           for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                if edge:
                    boundary += 1
                    if px[x, y][:3] not in ink:
                        missing += 1
        if boundary == 0 or missing:
            bad.append({"frame": i, "boundaryPixels": boundary, "notInk": missing})
    return CheckResult("outline", not bad, {"badFrames": bad})


def check_overhang(frames: list[Image.Image], canvas: tuple[int, int], footprint: tuple[int, int], ppu: int,
                   max_cells: float = OVERHANG_MAX_CELLS) -> CheckResult:
    _l, top, _w, _h = footprint_rect(tuple(canvas), tuple(footprint))
    tops = [b[1] for b in (opaque_bbox(f) for f in frames) if b]
    over_px = max(0, top - min(tops)) if tops else 0
    cells = over_px / ppu
    return CheckResult("overhang", cells <= max_cells, {"overhangPx": over_px, "overhangCells": cells, "maxCells": max_cells})


# --- animation -------------------------------------------------------------------

def luminance_change(a: Image.Image, b: Image.Image) -> float:
    """Mean |relative-luminance change| over the pixels opaque in both frames (research R05)."""
    pa, pb = a.convert("RGBA").load(), b.convert("RGBA").load()
    total, n = 0.0, 0
    for y in range(a.height):
        for x in range(a.width):
            ra, rb = pa[x, y], pb[x, y]
            if ra[3] == 255 and rb[3] == 255:
                total += abs(relative_luminance(ra[:3]) - relative_luminance(rb[:3]))
                n += 1
    return total / n if n else 0.0


def flash_rate(frames: list[Image.Image], fps: int) -> dict:
    """Worst number of flashes in any one-second window of the repeating loop.

    A transition between consecutive frames (including last to first) is a flash when the mean
    relative-luminance change exceeds 0.10.
    """
    n = len(frames)
    if n < 2:
        return {"maxFlashesPerSecond": 0, "transitions": []}
    deltas = [luminance_change(frames[i], frames[(i + 1) % n]) for i in range(n)]
    flags = [1 if d > FLASH_DELTA else 0 for d in deltas]
    window = max(1, fps)
    worst = max(sum(flags[(s + j) % n] for j in range(window)) for s in range(n))
    return {"maxFlashesPerSecond": worst, "transitions": [round(d, 6) for d in deltas]}


def check_flash(frames: list[Image.Image], fps: int) -> CheckResult:
    r = flash_rate(frames, fps)
    return CheckResult("flash", r["maxFlashesPerSecond"] <= FLASH_MAX_PER_SECOND,
                       {"fps": fps, **r, "limit": FLASH_MAX_PER_SECOND})


def lit_pixel_count(img: Image.Image, palette: Palette) -> int:
    emissive = {c.rgb for c in palette.colors if c.emissive}
    return sum(1 for _x, _y, p in _pixels(img) if p[3] and p[:3] in emissive)


def check_frame0_lights_on(frames: list[Image.Image], palette: Palette) -> CheckResult:
    counts = [lit_pixel_count(f, palette) for f in frames]
    ok = bool(counts) and counts[0] > 0 and counts[0] >= max(counts)
    return CheckResult("frame0_lights_on", ok, {"litPixelsPerFrame": counts})


# --- silhouettes -----------------------------------------------------------------

def silhouette_mask(img: Image.Image) -> Image.Image:
    return img.convert("RGBA").getchannel("A").point(lambda a: 255 if a else 0)


def _pad_bottom_center(mask: Image.Image, size: tuple[int, int]) -> Image.Image:
    out = Image.new("L", size, 0)
    out.paste(mask, ((size[0] - mask.width) // 2, size[1] - mask.height))
    return out


def mask_iou(a: Image.Image, b: Image.Image) -> float:
    size = (max(a.width, b.width), max(a.height, b.height))
    pa, pb = _pad_bottom_center(a, size).load(), _pad_bottom_center(b, size).load()
    inter = union = 0
    for y in range(size[1]):
        for x in range(size[0]):
            va, vb = pa[x, y] > 0, pb[x, y] > 0
            inter += va and vb
            union += va or vb
    return inter / union if union else 0.0


def downsample_mask(mask: Image.Image, size: int = IOU_SMALL) -> Image.Image:
    """Area downsample to size x size (coverage >= 50 % is silhouette)."""
    return mask.resize((size, size), Image.Resampling.BOX).point(lambda v: 255 if v >= 128 else 0)


def check_iou(masks: dict[str, Image.Image], max_iou: float = IOU_MAX) -> CheckResult:
    """Pairwise silhouette IoU at full size and on a 32x32 downsample (aligned bottom-centre)."""
    names = sorted(masks)
    worst_full = worst_small = 0.0
    failing = []
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            size = (max(masks[a].width, masks[b].width), max(masks[a].height, masks[b].height))
            pa, pb = _pad_bottom_center(masks[a], size), _pad_bottom_center(masks[b], size)
            full = mask_iou(pa, pb)
            small = mask_iou(downsample_mask(pa), downsample_mask(pb))
            worst_full, worst_small = max(worst_full, full), max(worst_small, small)
            if full > max_iou or small > max_iou:
                failing.append({"pair": [a, b], "full": round(full, 4), "small": round(small, 4)})
    return CheckResult("silhouette_iou", not failing,
                       {"max": max_iou, "worstFull": round(worst_full, 4), "worstSmall": round(worst_small, 4), "failing": failing})


# --- contrast / cues -------------------------------------------------------------

def check_contrast(pairs: list[dict], palette: Palette, darkest_light: float) -> CheckResult:
    """pairs: {fg, bg, kind} with palette ids; kind 'nontext' >= 3:1, 'text' (glyphs) >= 4.5:1.
    Evaluated at full light and under the darkest lighting the Art Bible allows (`darkest_light`, 0-1)."""
    rows, ok = [], True
    for p in pairs:
        need = CONTRAST_TEXT if p["kind"] == "text" else CONTRAST_NONTEXT
        fg, bg = palette.rgb(p["fg"]), palette.rgb(p["bg"])
        full, dark = contrast_ratio(fg, bg), contrast_ratio(fg, bg, darkest_light)
        passed = full >= need and dark >= need
        ok &= passed
        rows.append({"fg": p["fg"], "bg": p["bg"], "kind": p["kind"], "need": need,
                     "ratio": round(full, 3), "ratioDarkest": round(dark, 3), "passed": passed})
    return CheckResult("contrast", ok, {"darkestLight": darkest_light, "pairs": rows})


def check_cue_icon_size(img: Image.Image, min_px: int = CUE_MIN_PX) -> CheckResult:
    box = opaque_bbox(img)
    w, h = (box[2] - box[0], box[3] - box[1]) if box else (0, 0)
    return CheckResult("cue_icon_size", w >= min_px and h >= min_px, {"width": w, "height": h, "min": min_px})


# --- review sheets ---------------------------------------------------------------

SHEET_MODES = ["grayscale", *sorted(CVD_MATRICES)]


def _transform(rgb: RGB, mode: str) -> RGB:
    if mode == "grayscale":
        g = luma(rgb)
        return (g, g, g)
    return simulate_cvd(rgb, mode)


def make_review_sheets(sprites: dict[str, Image.Image], seed: int, out_dir: Path | str, scale: int = 2,
                       background: RGB = (40, 40, 48)) -> dict:
    """Write the shuffled grayscale and CVD sheets for the independent agent reviewer, plus a separate
    key file (withheld from the reviewer). The shuffle is seeded, so a sheet is reproducible."""
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    order = sorted(sprites)
    random.Random(seed).shuffle(order)
    cell_w = max(s.width for s in sprites.values()) * scale + 4
    cell_h = max(s.height for s in sprites.values()) * scale + 4
    cols = math.ceil(math.sqrt(len(order)))
    rows = math.ceil(len(order) / cols)
    base = Image.new("RGB", (cols * cell_w, rows * cell_h), background)
    for i, name in enumerate(order):
        s = sprites[name].convert("RGBA")
        s = s.resize((s.width * scale, s.height * scale), Image.Resampling.NEAREST)
        x, y = (i % cols) * cell_w + 2, (i // cols) * cell_h + 2 + (cell_h - 4 - s.height)
        base.paste(s, (x, y), s)
    paths = {}
    for mode in SHEET_MODES:
        sheet = base.copy()
        px = sheet.load()
        for yy in range(sheet.height):
            for xx in range(sheet.width):
                px[xx, yy] = _transform(px[xx, yy], mode)
        p = out / f"sheet_{mode}.png"
        sheet.save(p, format="PNG", compress_level=9, optimize=False)
        paths[mode] = p
    key = out / "sheet_key.json"
    key.write_text(json.dumps({"seed": seed, "columns": cols, "order": order}, indent=2) + "\n", encoding="utf-8")
    return {"order": order, "sheets": paths, "key": key}


# --- size budgets ----------------------------------------------------------------

def check_size_budgets(art_root: Path | str, family_mb: float = FAMILY_BUDGET_MB, total_mb: float = TOTAL_BUDGET_MB,
                       file_mb: float = SOURCE_FILE_DECISION_MB, decided_large: set[str] | None = None) -> CheckResult:
    """25 MB per family (direct subfolders of `art_root`), 150 MB total; a single source file over 10 MB
    needs an explicit decision (names in `decided_large`). Folders starting with '_' (rejected archive) are skipped."""
    root = Path(art_root)
    families: dict[str, int] = {}
    large = []
    if root.is_dir():
        for fam in sorted(p for p in root.iterdir() if p.is_dir() and not p.name.startswith("_")):
            size = 0
            for f in sorted(fam.rglob("*")):
                if f.is_file():
                    s = f.stat().st_size
                    size += s
                    rel = f.relative_to(root).as_posix()
                    if s > file_mb * MB and rel not in (decided_large or set()):
                        large.append(rel)
            families[fam.name] = size
    over = {n: round(s / MB, 3) for n, s in families.items() if s > family_mb * MB}
    total = sum(families.values())
    ok = not over and total <= total_mb * MB and not large
    return CheckResult("size_budgets", ok, {"familyMB": {n: round(s / MB, 3) for n, s in families.items()},
                                            "totalMB": round(total / MB, 3), "overFamilyBudget": over,
                                            "familyLimitMB": family_mb, "totalLimitMB": total_mb,
                                            "undecidedLargeFiles": large})


# --- reproduction ----------------------------------------------------------------

def reproduction_diff(a: Image.Image, b: Image.Image) -> float:
    """Per-frame difference (research R09): symmetric alpha-mask difference plus palette-colour mismatches,
    as a fraction of the opaque pixels (union of both masks)."""
    if a.size != b.size:
        return 1.0
    pa, pb = a.convert("RGBA").load(), b.convert("RGBA").load()
    opaque = diff = 0
    for y in range(a.height):
        for x in range(a.width):
            oa, ob = pa[x, y][3] > 0, pb[x, y][3] > 0
            if oa or ob:
                opaque += 1
                if oa != ob or pa[x, y][:3] != pb[x, y][:3]:
                    diff += 1
    return diff / opaque if opaque else 0.0


def check_reproduction(actual: list[Image.Image], golden: list[Image.Image], tolerance: float) -> CheckResult:
    """`tolerance` 0.0 for scripted routes (pixel-identical), 0.005 for Blender and generative routes."""
    if len(actual) != len(golden):
        return CheckResult("reproduction", False, {"frames": [len(actual), len(golden)]})
    diffs = [reproduction_diff(a, g) for a, g in zip(actual, golden)]
    return CheckResult("reproduction", all(d <= tolerance for d in diffs),
                       {"tolerance": tolerance, "perFrameDiff": [round(d, 6) for d in diffs]})


# --- composition and report ------------------------------------------------------

def run_asset_checks(frames: list[Image.Image], entry: dict, palette: Palette, *, expected_frames: int, ppu: int,
                     allowed_groups: set[str] | None = None, running_loop: bool = False) -> list[CheckResult]:
    canvas, footprint = tuple(entry["canvas"]), tuple(entry["footprintPx"])
    if allowed_groups is None:
        allowed_groups = {g for g in palette.budgets if g != RESERVED_CUE_GROUP}
    results = [
        check_palette(frames, palette, allowed_groups),
        check_canvas(frames, canvas),
        check_pivot(frames, entry),
        check_alpha(frames),
        check_outline(frames, palette),
        check_frames(frames, expected_frames),
        check_overhang(frames, canvas, footprint, ppu),
    ]
    if expected_frames > 1:
        results.append(check_flash(frames, entry["fps"]))
    if running_loop:
        results.append(check_frame0_lights_on(frames, palette))
    return results


def build_report(assets: dict[str, list[CheckResult]], global_checks: list[CheckResult]) -> dict:
    return {
        "schemaVersion": 1,
        "passed": all(r.passed for rs in assets.values() for r in rs) and all(r.passed for r in global_checks),
        "global": [r.to_dict() for r in global_checks],
        "assets": {name: [r.to_dict() for r in rs] for name, rs in sorted(assets.items())},
    }


def report_json(report: dict) -> str:
    return json.dumps(report, indent=2, sort_keys=True) + "\n"


def write_report(path: Path | str, report: dict) -> None:
    Path(path).write_text(report_json(report), encoding="utf-8")
