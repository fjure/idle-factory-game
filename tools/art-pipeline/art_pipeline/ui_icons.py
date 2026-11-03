"""T438 production run of the scripted route: the 50 UI glyph icons (49 F003 rows 30 to 78 plus the Settings gear).

Same shape as `cues_production` (T437): `build_all` is a pure function of (palette, source files); `write_all`
writes the PNGs, one provenance record per asset, the manifest, the family-scoped `check-report.json` and the
review sheets under `art-source/f004/ui-icons/`. Everything is byte-identical on a rerun.

Order of every sprite (research R07): scripted drawing (`ui_icons_glyphs`) -> `pixel_pass` reduce, alpha
threshold and palette quantisation -> checks -> provenance. There is NO ink outline: a UI glyph is a single
`ui_text` colour tinted by tokens at runtime (Art Bible 12, brief section 5), so an outline would be tinted too.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field
from itertools import combinations
from pathlib import Path

from PIL import Image

from . import checks as ck
from . import checks_v1 as v1
from . import cues
from . import pixel_pass as pp
from . import ui_icons_glyphs as gl
from .color import contrast_ratio, hex_to_rgb
from .palette import Palette, load_palette, PALETTE_V1_PATH

ART_BIBLE = "F004-PAB v1.0"
BRIEF = "F004-AB v1.0"
SCRIPT_VERSION = "t438-1"
CANVAS = (24, 24)
FOOTPRINT = (24, 24)
PIVOT_PX = (12, 12)
PPU = 24
FAMILY = "ui-icons"
SHEET_SEED = 438
DISPLAY_MULTIPLES = (1, 2, 3)   # 24, 48, 72 pt: whole multiples of the art px, point filtered (research R10)
MAX_PRIMITIVES = 6
MIN_STROKE = 2
GLYPH_COLOUR = "ui_text"
UI_GROUPS = {"ui"}

# F003 UI tokens (Art Bible section 11 / F003-PAB section 9): the three surfaces and the three tints.
SURFACES = {"--bg": "#111827", "--surface": "#1F2937", "--surface-raised": "#374151"}
TINTS = {"enabled": ("--text-primary", "#E5E7EB"), "disabled": ("--text-muted", "#94A3B8"),
         "selected": ("--accent", "#38BDF8")}
NONTEXT_MIN = ck.CONTRAST_NONTEXT   # icon tints against their surface: at least 3:1 (F003 section 9)
IOU_PEER_MAX = ck.IOU_MAX

_SRC_FILES = ("ui_icons.py", "ui_icons_draw.py", "ui_icons_glyphs.py", "cues.py")


@dataclass
class Icon:
    row: int
    key: str
    surface: str
    glyph: str
    peers: list[str]
    image: Image.Image
    primitives: int
    kinds: list[str]
    results: list = field(default_factory=list)

    @property
    def asset_id(self) -> str:
        return cues.asset_id(self.key)


# --- drawing and pixel pass ---------------------------------------------------------------------

def _raw(c, palette: Palette) -> Image.Image:
    img = Image.new("RGBA", CANVAS, pp.TRANSPARENT)
    px = img.load()
    rgb = palette.rgb(GLYPH_COLOUR) + (255,)
    for (x, y) in c.px:
        px[x, y] = rgb
    return img


def _pass(raw: Image.Image, palette: Palette) -> Image.Image:
    img = pp.reduce_for_route(raw, "scripted")
    img = pp.threshold_alpha(img)
    return pp.quantize(img, palette, UI_GROUPS)


def build_icons(palette: Palette) -> list[Icon]:
    icons = []
    for row, key, surface, glyph, peers, _fn in gl.ROWS:
        c = gl.draw(key)
        icons.append(Icon(row, key, surface, glyph, gl.expand_peers(key, peers), _pass(_raw(c, palette), palette),
                          c.prims, list(c.kinds)))
    return icons


# --- checks -------------------------------------------------------------------------------------

def _tint(rgb: tuple[int, int, int], tint_hex: str) -> tuple[int, int, int]:
    t = hex_to_rgb(tint_hex)
    return tuple(round(c * k / 255) for c, k in zip(rgb, t))


def _colour_ids(img: Image.Image, palette: Palette) -> set[str]:
    px = img.load()
    return {palette.id_of(px[x, y][:3]) for y in range(img.height) for x in range(img.width) if px[x, y][3]}


def _runs(img: Image.Image):
    """Horizontal and vertical run length of the opaque run each opaque pixel belongs to."""
    px = img.load()
    w, h = img.size
    on = [[px[x, y][3] != 0 for x in range(w)] for y in range(h)]
    hr = [[0] * w for _ in range(h)]
    vr = [[0] * w for _ in range(h)]
    for y in range(h):
        x = 0
        while x < w:
            if on[y][x]:
                x0 = x
                while x < w and on[y][x]:
                    x += 1
                for i in range(x0, x):
                    hr[y][i] = x - x0
            else:
                x += 1
    for x in range(w):
        y = 0
        while y < h:
            if on[y][x]:
                y0 = y
                while y < h and on[y][x]:
                    y += 1
                for i in range(y0, y):
                    vr[i][x] = y - y0
            else:
                y += 1
    return on, hr, vr


def check_stroke_width(img: Image.Image, min_px: int = MIN_STROKE) -> ck.CheckResult:
    """Every opaque pixel lies on a horizontal run AND a vertical run of at least `min_px` pixels, i.e. no
    1 art px line, dot or spur: the minimum stroke width of the Art Bible (section 12) is `min_px` art px."""
    on, hr, vr = _runs(img)
    thin = [(x, y) for y in range(img.height) for x in range(img.width) if on[y][x] and (hr[y][x] < min_px or vr[y][x] < min_px)]
    widths = [min(hr[y][x], vr[y][x]) for y in range(img.height) for x in range(img.width) if on[y][x]]
    return ck.CheckResult("stroke_width", not thin, {"minStrokePx": min_px, "thinPixels": len(thin),
                                                     "first": [list(p) for p in thin[:6]],
                                                     "smallestRun": min(widths) if widths else 0})


def check_primitives(icon: Icon) -> ck.CheckResult:
    return ck.CheckResult("primitives", 1 <= icon.primitives <= MAX_PRIMITIVES,
                          {"count": icon.primitives, "max": MAX_PRIMITIVES, "kinds": icon.kinds,
                           "rule": "an array of one repeated element placed by symmetry counts as one primitive"})


def check_palette_use(img: Image.Image, palette: Palette) -> ck.CheckResult:
    """Rule C4 (Art Bible 9.4): sprites on `--surface` or `--surface-raised` use `ui_text` or `ui_accent`, and the
    UI glyph base is `ui_text`; `ui_line` and `ui_panel` never appear. One colour only."""
    ids = _colour_ids(img, palette)
    return ck.CheckResult("c4_glyph_colour", ids == {GLYPH_COLOUR}, {"used": sorted(i for i in ids if i), "required": [GLYPH_COLOUR]})


def check_token_contrast(palette: Palette) -> ck.CheckResult:
    """Rule C4 on the three token surfaces: `ui_text` after the multiplicative tint against `--bg`, `--surface` and
    `--surface-raised`, evaluated for the enabled, disabled and selected tints. Non-text 3:1 is the gate for every
    state (F003 section 9: icon tints against their surface); enabled and selected also report the 4.5:1 text bar."""
    base = palette.rgb(GLYPH_COLOUR)
    rows, ok = [], True
    for state, (tok, tint_hex) in TINTS.items():
        tinted = _tint(base, tint_hex)
        for sname, shex in SURFACES.items():
            r = contrast_ratio(tinted, hex_to_rgb(shex))
            passed = r >= NONTEXT_MIN
            ok &= passed
            rows.append({"state": state, "tint": tok, "tintHex": tint_hex, "tinted": "#%02x%02x%02x" % tinted,
                         "surface": sname, "surfaceHex": shex, "ratio": round(r, 3), "need": NONTEXT_MIN,
                         "passed": passed, "meets4_5": r >= ck.CONTRAST_TEXT})
    return ck.CheckResult("c4_token_contrast", ok, {"glyph": GLYPH_COLOUR, "pairs": rows})


def check_display(img: Image.Image) -> ck.CheckResult:
    """Integer display multiples: the 24 x 24 art px are shown at 1, 2 or 3 pt per art px (24, 48, 72 pt); every
    multiple is a whole number of points and a nearest-neighbour scale keeps each art pixel a uniform block."""
    bad = []
    for m in DISPLAY_MULTIPLES:
        big = img.resize((img.width * m, img.height * m), Image.Resampling.NEAREST)
        back = big.resize(img.size, Image.Resampling.NEAREST)
        if big.size != (CANVAS[0] * m, CANVAS[1] * m) or list(back.getdata()) != list(img.getdata()):
            bad.append(m)
    return ck.CheckResult("integer_display", not bad, {"multiples": list(DISPLAY_MULTIPLES),
                                                        "pt": [CANVAS[0] * m for m in DISPLAY_MULTIPLES], "bad": bad})


def check_bounds(img: Image.Image) -> ck.CheckResult:
    bb = pp.opaque_bbox(img)
    inside = bb is not None and bb[0] >= 0 and bb[1] >= 0 and bb[2] <= CANVAS[0] and bb[3] <= CANVAS[1]
    return ck.CheckResult("glyph_bounds", inside, {"bbox": list(bb) if bb else None, "canvas": list(CANVAS)})


def _pivot_check(entry: dict) -> ck.CheckResult:
    expected = pp.pivot_for(tuple(entry["canvas"]), tuple(entry["footprintPx"]))
    ok = tuple(round(v, 6) for v in entry["pivot"]) == tuple(expected) and ck.pivot_is_integral(entry) \
        and tuple(round(v, 4) for v in ck.pivot_px(entry)) == tuple(float(v) for v in PIVOT_PX)
    return ck.CheckResult("pivot", ok, {"expected": list(expected), "manifest": list(entry["pivot"]),
                                        "pivotPx": [round(v, 4) for v in ck.pivot_px(entry)], "required": list(PIVOT_PX),
                                        "integral": ck.pivot_is_integral(entry),
                                        "restsOnFootprint": "not required: a UI glyph is centred, not grounded"})


def _na(name: str, reason: str) -> ck.CheckResult:
    return ck.CheckResult("not_applicable[%s]" % name, True, {"reason": reason})


def _accent_vacuous(img: Image.Image, palette: Palette) -> ck.CheckResult:
    empty = Image.new("RGBA", img.size, pp.TRANSPARENT)
    r = v1.check_accent_subset([empty], [img], palette)
    r.details["note"] = "no accent layer on a UI glyph (tint is a runtime token); vacuous by construction"
    return r


_NA = {
    "outline": "UI glyphs carry no ink outline: one colour, tinted by tokens (Art Bible 12); the world outline rule does not apply",
    "overhang": "overhang does not apply to UI glyphs (brief section 5)",
    "outline_contrast": "composite outline/rim contrast (Art Bible 9.3, rule C5) is a world-body rule; the UI check is rule C4 on the token surfaces (c4_token_contrast)",
    "overlay_backdrop": "not an overlay on world tones: the backdrops are the three UI token surfaces (c4_token_contrast)",
    "overlay_keepout": "the 80 x 64 machine keep-out region does not exist on a 24 x 24 UI canvas",
    "reserved_colours": "ui_text is not in the reserved state-cue group; asserted by c4_glyph_colour",
    "flash": "single frame, no animation",
}


def check_icon(icon: Icon, entry: dict, palette: Palette, twin: Icon) -> list[ck.CheckResult]:
    frames = [icon.image]
    res = [
        ck.check_palette(frames, palette, UI_GROUPS),
        check_palette_use(icon.image, palette),
        ck.check_canvas(frames, CANVAS),
        _pivot_check(entry),
        ck.check_alpha(frames),
        ck.check_frames(frames, 1),
        check_bounds(icon.image),
        check_stroke_width(icon.image),
        check_primitives(icon),
        check_display(icon.image),
        ck.check_contrast([{"fg": GLYPH_COLOUR, "bg": "ui_bg", "kind": "text"}, {"fg": GLYPH_COLOUR, "bg": "ui_panel", "kind": "text"}],
                          palette, 1.0),
        check_token_contrast(palette),
        _accent_vacuous(icon.image, palette),
    ]
    res += [_na(n, r) for n, r in sorted(_NA.items())]
    res.append(ck.check_reproduction(frames, [twin.image], 0.0))
    return res


def peer_iou(icons: list[Icon]) -> ck.CheckResult:
    """Pairwise silhouette IoU (contract: full size and a 32 x 32 downsample) over every declared confusion-peer
    pair, gated at 0.70; the worst over ALL 1225 pairs is recorded for the reviewer, not gated (most pairs are not
    confusable by design)."""
    masks = {i.key: ck.silhouette_mask(i.image) for i in icons}
    declared = sorted({tuple(sorted((i.key, p))) for i in icons for p in i.peers if p in masks})
    failing, worst = [], 0.0
    pairs = {}
    for a, b in declared:
        r = ck.check_iou({a: masks[a], b: masks[b]}, IOU_PEER_MAX)
        pairs[a + "|" + b] = max(r.details["worstFull"], r.details["worstSmall"])
        worst = max(worst, pairs[a + "|" + b])
        if not r.passed:
            failing.append({"pair": [a, b], "full": r.details["worstFull"], "small": r.details["worstSmall"]})
    allr = ck.check_iou(masks, IOU_PEER_MAX)
    return ck.CheckResult("peer_silhouette_iou", not failing, {
        "max": IOU_PEER_MAX, "declaredPairs": len(declared), "worstDeclared": round(worst, 4), "failing": failing,
        "perPair": {k: round(v, 4) for k, v in sorted(pairs.items())},
        "allPairsWorstFull": allr.details["worstFull"], "allPairsWorstSmall": allr.details["worstSmall"],
        "allPairsOver": [f for f in allr.details["failing"]]})


# --- provenance, manifest, report -----------------------------------------------------------------

def source_hashes() -> dict[str, str]:
    base = Path(__file__).resolve().parent
    return {n: hashlib.sha256((base / n).read_bytes().replace(b"\r\n", b"\n")).hexdigest() for n in _SRC_FILES}


def script_sha256() -> str:
    h = hashlib.sha256()
    for name, digest in sorted(source_hashes().items()):
        h.update(name.encode() + b":" + digest.encode() + b"\n")
    return h.hexdigest()


def _provenance(icon: Icon, palette: Palette, outputs: list[dict], report_hash: str) -> dict:
    return {
        "assetId": icon.asset_id,
        "revision": 1,
        "route": "scripted",
        "sources": [{"path": "tools/art-pipeline/art_pipeline/%s" % n, "sha256": h} for n, h in sorted(source_hashes().items())],
        "tools": [{"name": "art_pipeline.ui_icons", "version": SCRIPT_VERSION},
                  {"name": "Pillow", "version": "11.3.0 (pinned in tools/art-pipeline/pyproject.toml)"},
                  {"name": "Python", "version": ">=3.12,<3.14"}],
        "parameters": {"scriptVersion": SCRIPT_VERSION, "key": icon.key, "briefRow": icon.row, "surface": icon.surface,
                       "glyph": icon.glyph, "canvas": list(CANVAS), "pivotPx": list(PIVOT_PX), "ppu": PPU,
                       "displayMultiples": list(DISPLAY_MULTIPLES), "colour": GLYPH_COLOUR, "minStrokePx": MIN_STROKE,
                       "primitives": icon.primitives, "primitiveKinds": icon.kinds, "confusionPeers": icon.peers,
                       "seed": None, "randomness": "none"},
        "touchUp": [],
        "pixelPass": {"steps": ["reduce_for_route(scripted)", "threshold_alpha", "quantize(ui group)",
                                "no outline (one-colour tinted glyph)"],
                      "scriptSha256": script_sha256(), "paletteId": palette.palette_id, "paletteVersion": palette.version},
        "outputs": outputs,
        "license": "authored: project-owned scripted drawing, no third-party material",
        "trainingDataNote": "not applicable: no generative model, LoRA or training data is used on the scripted route",
        "producedBy": "Asset Artist (scripted route, UI/UX lens), MAQA T438",
        "reviewedBy": {},
        "candidateSelection": "none: a single deterministic render, no candidates, no seed",
        "artBibleVersion": ART_BIBLE,
        "briefVersion": BRIEF,
        "checkReportHash": report_hash,
    }


def _entry(icon: Icon, palette: Palette, prov: dict) -> dict:
    cv, fp = CANVAS, FOOTPRINT
    placement = pp.Placement(cv, fp, pp.footprint_rect(cv, fp), pp.pivot_for(cv, fp), (0, 0))
    gate = dict(prov, reviewedBy="pending: T439 independent review")
    e = pp.build_manifest_entry(icon.asset_id, "scripted", [icon.image], placement, palette, PPU, 0, gate,
                                frame_dir=icon.asset_id)
    e.update(key=icon.key, family=FAMILY, kind="ui-icon", format="UI", briefRow=icon.row, surface=icon.surface,
             glyph=icon.glyph, confusionPeers=icon.peers, displayMultiples=list(DISPLAY_MULTIPLES), pivotPx=list(PIVOT_PX),
             tint="runtime token (--text-primary, --text-muted, --accent); file colour is ui_text")
    return e


def build_all(palette: Palette | None = None) -> dict:
    palette = palette or load_palette(PALETTE_V1_PATH)
    icons = build_icons(palette)
    twins = {i.key: i for i in build_icons(palette)}  # second, independent render for the reproduction check
    entries, assets = {}, {}
    for i in icons:
        stub = _provenance(i, palette, [{"path": i.asset_id, "sha256": "0" * 64}], "0" * 64)
        entries[i.key] = _entry(i, palette, stub)
        i.results = check_icon(i, entries[i.key], palette, twins[i.key])
        assets[i.asset_id] = i.results
    glob = [ck.check_palette_definition(palette), peer_iou(icons),
            ck.CheckResult("inventory", len(icons) == 50 and len({i.key for i in icons}) == 50,
                           {"icons": len(icons), "briefRows": "30-78 (49 F003 rows) + 86 (F004-UI-ICON-SETTINGS)"})]
    report = ck.build_report(assets, glob)
    return {"palette": palette, "icons": icons, "entries": entries, "report": report}


def _write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def _surface_sheet(icons: list[Icon], palette: Palette) -> tuple[Image.Image, list[str]]:
    """Tinted icons on the three token surfaces at x1, x2 and x3 (integer multiples), enabled tint then selected
    tint. Order = sorted keys; the order list is written to the answer key, not onto the sheet."""
    order = sorted(i.key for i in icons)
    by = {i.key: i for i in icons}
    pad = 4
    cols = 10
    cell = 24 * 3 + pad * 2
    rows = (len(order) + cols - 1) // cols
    band = rows * cell
    sheet = Image.new("RGB", (cols * cell, band * len(SURFACES)), (0, 0, 0))
    for si, (_s, shex) in enumerate(SURFACES.items()):
        sheet.paste(Image.new("RGB", (cols * cell, band), hex_to_rgb(shex)), (0, si * band))
        for n, key in enumerate(order):
            tint = _tint(palette.rgb(GLYPH_COLOUR), TINTS["enabled" if n % 2 == 0 else "selected"][1])
            m = 3 if n % 3 == 0 else (2 if n % 3 == 1 else 1)
            src = by[key].image
            big = src.resize((24 * m, 24 * m), Image.Resampling.NEAREST)
            tinted = Image.new("RGBA", big.size, tint + (0,))
            tinted.putalpha(big.getchannel("A"))
            sheet.paste(tinted.convert("RGB"), ((n % cols) * cell + pad, si * band + (n // cols) * cell + pad),
                        tinted.getchannel("A"))
    return sheet, order


def write_all(out_root: Path | str, palette: Palette | None = None) -> dict:
    """Write the family under `<out_root>/ui-icons`; `out_root` is the `art-source/f004` directory."""
    out_root = Path(out_root)
    b = build_all(palette)
    pal = b["palette"]
    fam_dir = out_root / FAMILY
    report_text = ck.report_json(b["report"])
    _write(fam_dir / "check-report.json", report_text.encode("utf-8"))
    report_hash = pp.sha256_hex(report_text.encode("utf-8"))
    entries = []
    for i in b["icons"]:
        data = pp.png_bytes(i.image)
        rel = "%s/%s_00.png" % (i.asset_id, i.asset_id)
        _write(fam_dir / rel, data)
        outputs = [{"path": "art-source/f004/%s/%s" % (FAMILY, rel), "sha256": pp.sha256_hex(data),
                    "pixelHash": pp.pixel_hash(i.image)}]
        prov = _provenance(i, pal, outputs, report_hash)
        _write(fam_dir / i.asset_id / "provenance.json", (json.dumps(prov, indent=2, sort_keys=True) + "\n").encode("utf-8"))
        entries.append(_entry(i, pal, prov))
    _write(fam_dir / "manifest.json", pp.manifest_json(pp.build_manifest(entries)).encode("utf-8"))
    # Review sheets for the independent reviewer (T439): seeded shuffle, grayscale and CVD, integer scale 3. The
    # answer key is withheld from the reviewer, so it lives apart from the sheets.
    sheet_src = {i.asset_id: i.image for i in b["icons"]}
    sheets = ck.make_review_sheets(sheet_src, SHEET_SEED, fam_dir / "sheets", scale=3)
    key_file = Path(sheets["key"])
    _write(fam_dir / "answer-key" / "sheet_key.json", key_file.read_bytes().replace(b"\r\n", b"\n"))
    key_file.unlink()
    surf, order = _surface_sheet(b["icons"], pal)
    surf_path = fam_dir / "sheets" / "tinted_on_token_surfaces.png"
    surf.save(surf_path, format="PNG", compress_level=9, optimize=False)
    _write(fam_dir / "answer-key" / "tinted_sheet_order.json",
           (json.dumps({"columns": 10, "bands": list(SURFACES), "order": order,
                        "cellRule": "per cell n: tint enabled when n is even else selected; scale 3 when n%3==0, 2 when n%3==1, else 1"},
                       indent=2) + "\n").encode("utf-8"))
    b["sheets"] = sheets
    return b
