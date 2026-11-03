"""F004 T464: scripted production of the three item tier chips (brief rows 27 to 29, route scripted, family `items`).

A chip is the tier cue of the T436 item sprites on its own, 32 x 16 px (30 x 14 art px before the 1 px ink outline),
pivot (16, 8). It carries the same triple redundant cue as the item frame: shape (Tier I circle, II square, III
triangle), pip count (1, 2, 3) and numeral (I, II, III); the frame colour (bronze `res_ore_1`, `ui_text`, gold `res_gold`)
is supplemental. The tier table (shape, frame colour, pips, numeral), the ring rule and the numeral bitmap (including its
locally authored numeral I) come from `art-source/f004/items/item_frames.py`: that file is imported, never copied, so
chip and item share one glyph source, and it stays untouched (T436 outputs are unchanged).

Layout in art coordinates (30 x 14):

  x 0..13   badge: the frame shape (circle d 12, square 14, triangle 14), 1 px ring in the frame colour, interior `ui_panel`
  x 14      gap
  x 15..29  label bar rows 0..13, `ui_text` fill; 2 x 2 `ink_0` pips on rows 3..4 (as in the item, pips sit on the
            `ui_text` bar: `ink_0` on `ui_panel` is only 1.47:1), numeral in `ink_0` on rows 6..10, both centred

Deterministic: a pure function of the palette and the source files, byte-identical on every run.
"""
from __future__ import annotations

import hashlib
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image

from . import checks as ck
from . import checks_v1 as v1
from . import pixel_pass as pp
from .color import contrast_ratio, hex_to_rgb
from .palette import PALETTE_V1_PATH, Palette, load_palette

REPO = Path(__file__).resolve().parents[3]
ITEMS_DIR = REPO / "art-source" / "f004" / "items"
if str(ITEMS_DIR) not in sys.path:
    sys.path.insert(0, str(ITEMS_DIR))
import item_frames as fr  # noqa: E402  (the T436 tier frames: one glyph and tier-spec source)

ART_BIBLE = "F004-PAB v1.0"
BRIEF = "F004-AB v1.0"
SCRIPT_VERSION = "t464-1"
FAMILY = "items"
ART = (30, 14)
CANVAS = FOOTPRINT = (32, 16)
PIVOT_PX = (16, 8)
PPU = 24
BADGE = 14
BAR_X0, BAR_X1, BAR_Y0, BAR_Y1 = 15, 30, 0, 14
PIP_ROW0 = 3
GLYPH_ROW0 = 6
GROUPS = {"resources", "ink", "ui"}
LIMITS = {"resources": 4, "ui": 4, "ink": 1}
SHEET_SEED = 4640
SURFACES = {"--bg": "#111827", "--surface": "#1F2937", "--surface-raised": "#374151"}   # Art Bible 9.1 and 11
CHIPS = {   # asset id -> (key, tier, brief row)
    "f003-item-tier-1": ("F003-ITEM-TIER-1", 1, 27),
    "f003-item-tier-2": ("F003-ITEM-TIER-2", 2, 28),
    "f003-item-tier-3": ("F003-ITEM-TIER-3", 3, 29),
}
NA = {
    "overhang": "overhang does not apply to tier chips (brief section 4: format UI, overhang n/a)",
    "flash": "single frame, no animation",
    "frame0_lights_on": "not a running machine loop",
    "overlay_keepout": "the 80 x 64 machine keep-out region does not exist on a 32 x 16 chip",
    "overlay_backdrop": "a tier chip is not an overlay on world tones (rules C1, C3, C8); its backdrops are the UI token surfaces (c6_ui_surface_outline_contrast)",
    "accent_subset": "no accent layer on a tier chip",
    "reserved_colours": "state_cues and cvd_safe are outside the chip allowance; asserted by the palette check",
    "cue_icon_size": "not a state cue",
    "intentionally_empty": "every chip has a PNG",
    "world_floor_contrast": "tier chips are UI sprites shown on UI surfaces (brief: format UI); the brief places them nowhere in the world, so no T435 plate or floor backdrop applies",
}
_SRC = (REPO / "tools" / "art-pipeline" / "art_pipeline" / "tier_chips.py", ITEMS_DIR / "item_frames.py")


@dataclass
class Chip:
    asset_id: str
    key: str
    tier: int
    row: int
    image: Image.Image
    meta: dict
    results: list = field(default_factory=list)


# --- drawing ------------------------------------------------------------------------------------

def badge_mask(shape: str) -> set[tuple[int, int]]:
    """The T436 shape rules (`item_frames.shape_mask`) at the chip size, 14 x 14."""
    cells = set()
    for y in range(BADGE):
        for x in range(BADGE):
            if shape == "circle":
                if (x - 6.5) ** 2 + (y - 6.5) ** 2 <= 6.0 ** 2 + 0.25:
                    cells.add((x, y))
            elif shape == "square":
                cells.add((x, y))
            elif shape == "triangle":
                if abs(x - 6.5) <= 0.5 + y * 6.5 / (BADGE - 1):
                    cells.add((x, y))
            else:
                raise ValueError(shape)
    return cells


def pip_cells(tier: int) -> set[tuple[int, int]]:
    t = fr.TIERS[tier]
    width = t["pips"] * 2 + (t["pips"] - 1)
    x0 = BAR_X0 + (BAR_X1 - BAR_X0 - width) // 2
    y0 = PIP_ROW0
    return {(x0 + p * 3 + dx, y0 + dy) for p in range(t["pips"]) for dx in range(2) for dy in range(2)}


def numeral_cells(tier: int) -> set[tuple[int, int]]:
    numeral = fr.TIERS[tier]["numeral"]
    width = len(numeral) * 3 + (len(numeral) - 1)
    x0 = BAR_X0 + (BAR_X1 - BAR_X0 - width) // 2
    return {(x0 + gi * 4 + gx, GLYPH_ROW0 + gy) for gi in range(len(numeral))
            for gy, row in enumerate(fr.GLYPH_I) for gx, ch in enumerate(row) if ch == "#"}


def draw(tier: int, palette: Palette) -> tuple[Image.Image, dict]:
    t = fr.TIERS[tier]
    img = Image.new("RGBA", ART, pp.TRANSPARENT)
    px = img.load()
    cells = badge_mask(t["shape"])
    edge = fr.ring(cells)
    for x, y in cells:
        px[x, y] = palette.rgb("ui_panel") + (255,)
    for x, y in edge:
        px[x, y] = palette.rgb(t["frame"]) + (255,)
    ink = palette.rgb("ink_0") + (255,)
    for x in range(BAR_X0, BAR_X1):
        for y in range(BAR_Y0, BAR_Y1):
            px[x, y] = palette.rgb("ui_text") + (255,)
    pips, glyphs = pip_cells(tier), numeral_cells(tier)
    for x, y in pips | glyphs:
        px[x, y] = ink
    meta = {"badgeCells": len(cells), "ringCells": len(edge), "pipCells": len(pips), "numeralCells": len(glyphs),
            "pipsOnBar": all(BAR_X0 <= x < BAR_X1 and BAR_Y0 <= y < BAR_Y1 for x, y in pips)}
    return img, meta


def build_chip(tier: int, palette: Palette) -> tuple[Image.Image, dict]:
    art, meta = draw(tier, palette)
    img = pp.quantize(pp.threshold_alpha(art), palette, GROUPS)
    return pp.add_outline(img, palette.rgb("ink_0")), meta


# --- checks -------------------------------------------------------------------------------------

def _pairs(img: Image.Image, palette: Palette):
    return [(ink, rim) for ink, rim, tone in v1.outline_pairs(img, None, palette) if tone == "floor_0"]


def check_ui_surface_contrast(img: Image.Image, palette: Palette) -> ck.CheckResult:
    """Rule C6 (Art Bible 9.4) on the three UI token surfaces, the surfaces a chip sits on: share of section 9.3 pairs
    (ink OR rim reaches 3:1) at least 90 % per surface."""
    pairs = _pairs(img, palette)
    rows, ok = {}, bool(pairs)
    for name, hexv in SURFACES.items():
        s = hex_to_rgb(hexv)
        hit = sum(1 for ink, rim in pairs if contrast_ratio(ink, s) >= 3 or (rim is not None and contrast_ratio(rim, s) >= 3))
        share = hit / len(pairs) if pairs else 0.0
        rows[name] = {"pairs": len(pairs), "share": round(share, 4), "passed": share >= v1.SHARE_THRESHOLD}
        ok &= share >= v1.SHARE_THRESHOLD
    return ck.CheckResult("c6_ui_surface_outline_contrast", ok, {
        "threshold": v1.SHARE_THRESHOLD, "surfaces": rows,
        "note": "ink OR rim (the frame colour) at 3:1 against --bg, --surface, --surface-raised"})


def check_internal_contrast(tier: int, palette: Palette) -> ck.CheckResult:
    """Numeral and pips (`ink_0`) are glyphs: 4.5:1 against their fill (the `ui_text` bar); the
    frame ring is a graphic: 3:1 against the `ui_panel` interior it encloses."""
    r = ck.check_contrast([{"fg": "ink_0", "bg": "ui_text", "kind": "text"},
                           {"fg": fr.TIERS[tier]["frame"], "bg": "ui_panel", "kind": "nontext"}], palette, 1.0)
    r.name = "contrast_internal"
    return r


def check_cue_geometry(tier: int, meta: dict) -> ck.CheckResult:
    t = fr.TIERS[tier]
    ok = meta["pipsOnBar"] and meta["pipCells"] == 4 * t["pips"]
    return ck.CheckResult("tier_cue_geometry", ok, {
        "shape": t["shape"], "pips": t["pips"], "numeral": t["numeral"], "pipsOnBar": meta["pipsOnBar"],
        "pipCells": meta["pipCells"], "frameColour": t["frame"] + " (supplemental)"})


def check_chip(chip: Chip, entry: dict, palette: Palette, twin: Image.Image) -> list[ck.CheckResult]:
    frames = [chip.image]
    pivot = ck.check_pivot(frames, entry)
    got = tuple(round(v, 4) for v in ck.pivot_px(entry))
    pivot.details["pivotPx"] = list(got)
    pivot.details["required"] = list(PIVOT_PX)
    pivot.passed = pivot.passed and got == tuple(float(v) for v in PIVOT_PX)
    budget = ck.check_palette(frames, palette, GROUPS, group_limits=LIMITS)
    budget.name = "palette_group_budget"
    res = [ck.check_palette(frames, palette, GROUPS), budget, ck.check_canvas(frames, CANVAS), pivot, ck.check_alpha(frames),
           ck.check_outline(frames, palette), ck.check_frames(frames, 1), check_cue_geometry(chip.tier, chip.meta),
           check_internal_contrast(chip.tier, palette), check_ui_surface_contrast(chip.image, palette)]
    res += [ck.CheckResult("not_applicable[%s]" % n, True, {"reason": r}) for n, r in sorted(NA.items())]
    res.append(ck.check_reproduction(frames, [twin], 0.0))
    return res


def check_tiers_distinct(chips: list[Chip]) -> ck.CheckResult:
    """Each cue channel separates the tiers on its own. Whole-chip silhouettes share the label bar, so the silhouette
    gate runs on the badge (the left 16 px), where the frame shape lives."""
    masks = {c.asset_id: ck.silhouette_mask(c.image.crop((0, 0, BADGE + 2, CANVAS[1]))) for c in chips}
    iou = ck.check_iou(masks)
    shapes = {c.asset_id: fr.TIERS[c.tier]["shape"] for c in chips}
    pips = {c.asset_id: fr.TIERS[c.tier]["pips"] for c in chips}
    nums = {c.asset_id: fr.TIERS[c.tier]["numeral"] for c in chips}
    glyphs = {tuple(sorted(numeral_cells(c.tier))) for c in chips}
    ok = iou.passed and all(len(set(v.values())) == 3 for v in (shapes, pips, nums)) and len(glyphs) == 3
    return ck.CheckResult("tier_cue_triple_redundant", ok, {
        "badgeSilhouetteIoU": iou.details, "frames": shapes, "pips": pips, "numerals": nums,
        "note": "shape, pip count and numeral each separate the tiers; the frame colour is supplemental"})


def _row_widths(cells: set[tuple[int, int]]) -> list[int]:
    return [sum(1 for (_x, yy) in cells if yy == y) for y in range(BADGE)]


def check_matches_items(palette: Palette) -> ck.CheckResult:
    """The chip badge follows the shape rules of `item_frames.shape_mask`: each chip shape has the same row-width
    profile type as the item frame (circle widest in the middle and symmetric, square constant, triangle growing from the
    apex row), the tier table and the numeral glyph are the item's own (imported)."""
    profile = {}
    for shape in ("circle", "square", "triangle"):
        item_w = [sum(1 for (_x, yy) in fr.shape_mask(shape) if yy == y) for y in range(fr.SHAPE_ROWS)]
        chip_w = _row_widths(badge_mask(shape))
        sym = all(all((BADGE - 1 - x, y) in badge_mask(shape) for (x, y) in badge_mask(shape) if y == yy) for yy in range(BADGE))
        if shape == "square":
            ok = len(set(item_w)) == 1 and len(set(chip_w)) == 1
        elif shape == "triangle":
            ok = item_w == sorted(item_w) and chip_w == sorted(chip_w) and chip_w[0] < chip_w[-1] and item_w[0] < item_w[-1]
        else:
            ok = item_w.index(max(item_w)) in range(8, 14) and chip_w.index(max(chip_w)) in range(5, 9) and chip_w[0] < chip_w[6]
        profile[shape] = {"sameProfile": ok and sym, "chipRowWidths": chip_w}
    return ck.CheckResult("matches_t436_tier_frames", all(v["sameProfile"] for v in profile.values()), {
        "source": "art-source/f004/items/item_frames.py", "profiles": profile,
        "tiers": {str(t): {k: fr.TIERS[t][k] for k in ("shape", "frame", "pips", "numeral")} for t in sorted(fr.TIERS)},
        "glyphSource": "item_frames.GLYPH_I and the 3 x 5 numeral layout of compose(); imported, not copied"})


# --- build, provenance, manifest, report --------------------------------------------------------

def _file_hash(p: Path) -> str:
    return hashlib.sha256(p.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def source_hashes() -> dict[str, str]:
    return {str(p.relative_to(REPO)).replace("\\", "/"): _file_hash(p) for p in _SRC}


def script_sha256() -> str:
    h = hashlib.sha256()
    for name, digest in sorted(source_hashes().items()):
        h.update(name.encode() + b":" + digest.encode() + b"\n")
    return h.hexdigest()


def build_chips(palette: Palette) -> list[Chip]:
    out = []
    for aid, (key, tier, row) in CHIPS.items():
        img, meta = build_chip(tier, palette)
        out.append(Chip(aid, key, tier, row, img, meta))
    return out


def _provenance(c: Chip, palette: Palette, outputs: list[dict], report_hash: str) -> dict:
    t = fr.TIERS[c.tier]
    base = REPO / "tools" / "art-pipeline"
    return {
        "assetId": c.asset_id, "revision": 1, "route": "scripted", "key": c.key,
        "sources": [{"path": p, "sha256": h} for p, h in sorted(source_hashes().items())]
                   + [{"path": "tools/art-pipeline/art_pipeline/pixel_pass.py", "sha256": _file_hash(base / "art_pipeline" / "pixel_pass.py")},
                      {"path": "tools/art-pipeline/palette/palette-v1.json", "sha256": _file_hash(base / "palette" / "palette-v1.json")}],
        "tools": [{"name": "art_pipeline.tier_chips", "version": SCRIPT_VERSION},
                  {"name": "Pillow", "version": "11.3.0 (pinned in tools/art-pipeline/pyproject.toml)"},
                  {"name": "Python", "version": ">=3.12,<3.14"}],
        "parameters": {"scriptVersion": SCRIPT_VERSION, "key": c.key, "briefRow": c.row, "tier": c.tier, "canvas": list(CANVAS),
                       "artBeforeOutline": list(ART), "pivotPx": list(PIVOT_PX), "ppu": PPU,
                       "cue": {"frame": t["shape"], "frameColour": t["frame"] + " (supplemental)", "pips": t["pips"], "numeral": t["numeral"]},
                       "layout": c.meta, "seed": None, "randomness": "none"},
        "touchUp": [],
        "pixelPass": {"steps": ["scripted drawing (palette ids only)", "threshold_alpha", "quantize(resources, ink, ui groups)",
                                "add_outline(ink_0, 1 px)"],
                      "scriptSha256": script_sha256(), "paletteId": palette.palette_id, "paletteVersion": palette.version},
        "outputs": outputs,
        "license": "authored: project-owned scripted drawing, no third-party material",
        "trainingDataNote": "not applicable: no generative model, LoRA or training data is used on the scripted route",
        "producedBy": "Asset Artist (scripted route), MAQA T464 / Issue #632",
        "reviewedBy": {},
        "candidateSelection": "none: a single deterministic render, no candidates, no seed",
        "interactiveSteps": "none: the chips were drawn and checked by the committed script only; no Blender, ComfyUI or other interactive tool was used",
        "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
        "checkReportHash": report_hash, "checkReportPath": "art-source/f004/items/check-report-t464.json",
    }


def _entry(c: Chip, palette: Palette, prov: dict) -> dict:
    placement = pp.Placement(CANVAS, FOOTPRINT, pp.footprint_rect(CANVAS, FOOTPRINT), pp.pivot_for(CANVAS, FOOTPRINT), (0, 0))
    gate = dict(prov, reviewedBy="pending: T439 independent review")
    e = pp.build_manifest_entry(c.asset_id, "scripted", [c.image], placement, palette, PPU, 0, gate, frame_dir=c.asset_id + "/pixel")
    t = fr.TIERS[c.tier]
    e.update(key=c.key, family=FAMILY, kind="tier-chip", format="UI", briefRow=c.row, tier=c.tier, pivotPx=list(PIVOT_PX),
             tierCue={"frame": t["shape"], "pips": t["pips"], "numeral": t["numeral"], "frameColour": t["frame"] + " (supplemental)"})
    return e


def build_all(palette: Palette | None = None) -> dict:
    palette = palette or load_palette(PALETTE_V1_PATH)
    chips = build_chips(palette)
    twins = {c.asset_id: c for c in build_chips(palette)}
    entries, assets = {}, {}
    for c in chips:
        stub = _provenance(c, palette, [{"path": c.asset_id, "sha256": "0" * 64}], "0" * 64)
        entries[c.asset_id] = _entry(c, palette, stub)
        c.results = check_chip(c, entries[c.asset_id], palette, twins[c.asset_id].image)
        assets[c.asset_id] = c.results
    glob = [ck.check_palette_definition(palette), check_tiers_distinct(chips), check_matches_items(palette),
            ck.CheckResult("inventory", len(chips) == 3 and {c.row for c in chips} == {27, 28, 29},
                           {"assets": sorted(CHIPS), "briefRows": "27-29"})]
    report = ck.build_report(assets, glob)
    report["family"] = {"family": FAMILY, "task": "T464", "issue": 632, "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
                        "tokenSurfaces": SURFACES, "route": "scripted",
                        "crossFamily": "no real T435 plate or floor backdrop applies: tier chips are UI sprites (see not_applicable[world_floor_contrast])",
                        "intentionallyEmpty": "no intentionallyEmpty entry in this family slice"}
    return {"palette": palette, "chips": chips, "entries": entries, "report": report}


def _write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def write_all(out_dir: Path | str = ITEMS_DIR, palette: Palette | None = None) -> dict:
    """Write the chips under `out_dir` (`art-source/f004/items`) with the `-t464` file names; T436 files are not touched."""
    out = Path(out_dir)
    b = build_all(palette)
    pal = b["palette"]
    report_text = ck.report_json(b["report"])
    _write(out / "check-report-t464.json", report_text.encode("utf-8"))
    report_hash = pp.sha256_hex(report_text.encode("utf-8"))
    entries = []
    for c in b["chips"]:
        data = pp.png_bytes(c.image)
        rel = "%s/pixel/%s_00.png" % (c.asset_id, c.asset_id)
        _write(out / rel, data)
        outputs = [{"path": "art-source/f004/items/" + rel, "sha256": pp.sha256_hex(data), "pixelHash": pp.pixel_hash(c.image)}]
        prov = _provenance(c, pal, outputs, report_hash)
        _write(out / c.asset_id / "provenance.json", (json.dumps(prov, indent=2, sort_keys=True) + "\n").encode("utf-8"))
        entries.append(_entry(c, pal, prov))
    man = pp.build_manifest(entries)
    man["note"] = "T464 candidate entries for T440; reviewedBy pending (T439). Tier chips 32 x 16, pivot (16, 8), tier cue = frame shape + pips + numeral."
    _write(out / "manifest-t464.json", pp.manifest_json(man).encode("utf-8"))
    sheets = ck.make_review_sheets({c.asset_id: c.image for c in b["chips"]}, SHEET_SEED, out / "sheets-t464", scale=4,
                                   background=pal.rgb("ui_bg"))
    key_file = Path(sheets["key"])
    _write(out / "answer-key" / "sheet_key_t464.json", key_file.read_bytes().replace(b"\r\n", b"\n"))
    key_file.unlink()
    strip = Image.new("RGB", (3 * 136 + 8, 72), pal.rgb("ui_bg"))
    for i, c in enumerate(sorted(b["chips"], key=lambda k: k.asset_id)):
        big = c.image.resize((128, 64), Image.NEAREST)
        strip.paste(big, (4 + i * 136, 4), big)
    strip.save(out / "sheets-t464" / "chips_x4_on_ui_bg.png", compress_level=9)
    b["sheets"] = sheets
    return b
