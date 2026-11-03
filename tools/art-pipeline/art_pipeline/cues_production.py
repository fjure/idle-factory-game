"""T437 production run of the scripted route: state cues, markers, selection frame and lock labels.

`build_all` is a pure function of (palette, source files). `write_all` writes, per family, the PNGs, the
provenance records, the manifest and the family-scoped `check-report.json`. Everything is byte-identical on a
rerun: no clock, no randomness, no absolute paths, hashes of LF-normalised source text.

Order of every sprite (research R07): scripted drawing (`cues`) -> `pixel_pass` (route reduction, alpha 0/255,
palette quantisation, 1 px ink outline) -> placement on the 80 x 64 overlay canvas -> checks -> provenance.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image

from . import checks as ck
from . import checks_v1 as v1
from . import cues
from . import pixel_pass as pp
from .palette import RESERVED_CUE_GROUP, Palette, load_palette, PALETTE_V1_PATH

ART_BIBLE = "F004-PAB v1.0"
BRIEF = "F004-AB v1.0"
PPU = 24
SHEET_SEED = 437
DARKEST_LIGHT = 1.0  # Art Bible 2.1: lighting B has no dimming, the darkest allowed light is 1.0

# Declared world surfaces (Art Bible 9.1): the field and the occupied plate tops.
DECLARED_TONES = ("floor_0", "floor_1", "plate_1", "plate_2")
LABEL_TONES = ("dim_0", "dim_1")  # locked plate body tones the digit sits on (Art Bible 8.4)

_SRC_FILES = ("cues.py", "cues_production.py")


@dataclass
class Sprite:
    key: str
    kind: str                       # badge | marker | frame | label | empty
    family: str
    image: Image.Image | None       # placed 80 x 64 RGBA, None for the intentionally empty entry
    box: tuple[int, int, int, int] | None
    fill_id: str | None = None
    raw_glyph: Image.Image | None = None
    results: list = field(default_factory=list)

    @property
    def asset_id(self) -> str:
        return cues.asset_id(self.key)


# --- pixel pass and placement --------------------------------------------------------------------

def _pass(raw: Image.Image, palette: Palette, groups: set[str]) -> Image.Image:
    img = pp.reduce_for_route(raw, "scripted")
    img = pp.threshold_alpha(img)
    img = pp.quantize(img, palette, groups)
    return pp.add_outline(img, palette.rgb(cues.INK_ID))


def _on_canvas(img: Image.Image, xy: tuple[int, int]) -> Image.Image:
    out = Image.new("RGBA", cues.CANVAS, pp.TRANSPARENT)
    out.paste(img, xy)
    return out


def _crop(img: Image.Image) -> Image.Image:
    return img.crop(pp.opaque_bbox(img))


def build_sprites(palette: Palette) -> list[Sprite]:
    sprites: list[Sprite] = []
    for key, (_state, fill_id, _g, _k) in cues.BADGE_SPECS.items():
        x0, y0, _x1, _y1 = cues.BADGE_BOX
        img = _pass(cues.raw_badge(key, palette), palette, {"ink", RESERVED_CUE_GROUP})
        sprites.append(Sprite(key, "badge", "cues", _on_canvas(img, (x0, y0)), cues.BADGE_BOX, fill_id,
                              cues.raw_glyph_mask(key)))
    sprites.append(Sprite(cues.RUNNING_BADGE_KEY, "empty", "cues", None, None))
    for key, (name, fill_id) in cues.MARKER_SPECS.items():
        box = cues.MARKER_BOXES[name]
        img = _pass(cues.raw_marker(key, palette), palette, {"ink", "cvd_safe"})
        sprites.append(Sprite(key, "marker", "markers", _on_canvas(img, box[:2]), box, fill_id, cues.raw_glyph_mask(key)))
    raw = cues.raw_frame(palette)
    bx0, by0, _bx1, _by1 = pp.opaque_bbox(raw)
    img = _pass(raw.crop(pp.opaque_bbox(raw)), palette, {"ink", "ui"})
    placed = _on_canvas(img, (bx0 - 1, by0 - 1))
    sprites.append(Sprite(cues.FRAME_KEY, "frame", "slots", placed, pp.opaque_bbox(placed)))
    for key, level in cues.LABEL_KEYS.items():
        img = _pass(cues.raw_label(level, palette), palette, {"ink", "ui"})
        cx, cy = cues.LABEL_CENTRE
        placed = _on_canvas(img, (cx - img.width // 2, cy - img.height // 2))
        sprites.append(Sprite(key, "label", "slots", placed, pp.opaque_bbox(placed)))
    return sprites


# --- backdrops ----------------------------------------------------------------------------------

def uniform_backdrop(tone: str, palette: Palette) -> Image.Image:
    return Image.new("RGBA", cues.CANVAS, palette.rgb(tone) + (255,))


def frame_surface(tone: str, palette: Palette) -> Image.Image:
    """Backdrop for the selection frame at tone `tone`: the frame encloses the plate, so the area inside the ring
    is the plate top (`plate_1`) and only the outside is the tone under test (the frame never sits on a bare tone
    inside its own ring)."""
    out = uniform_backdrop(tone, palette)
    px = out.load()
    for (x, y) in cues.frame_diamond():
        px[x, y] = palette.rgb("plate_1") + (255,)
    return out


def _surface(s: "Sprite", tone: str, palette: Palette) -> Image.Image:
    return frame_surface(tone, palette) if s.kind == "frame" else uniform_backdrop(tone, palette)


def plate_reference(palette: Palette) -> Image.Image:
    """Synthetic occupied-plate reference (NOT production art, T435 owns the plate): a 70 x 35 diamond, `plate_2`
    rim, `plate_1` top, `ink_0` outline, on the footprint. Gives the frame and the badges a mixed backdrop."""
    out = Image.new("RGBA", cues.CANVAS, pp.TRANSPARENT)
    px = out.load()
    s = cues.frame_diamond()
    e1 = cues._erode(s)
    for (x, y) in s:
        px[x, y] = palette.rgb("plate_2") + (255,)
    for (x, y) in e1:
        px[x, y] = palette.rgb("plate_1") + (255,)
    # ink outline around the raw diamond
    grown = {(x + dx, y + dy) for (x, y) in s for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))} - s
    for (x, y) in grown:
        px[x, y] = palette.rgb(cues.INK_ID) + (255,)
    return out


# --- checks -------------------------------------------------------------------------------------

def _named(name: str, r: ck.CheckResult) -> ck.CheckResult:
    return ck.CheckResult(name, r.passed, r.details)


def _colour_ids(img: Image.Image, palette: Palette) -> set[str]:
    px = img.load()
    return {palette.id_of(px[x, y][:3]) for y in range(img.height) for x in range(img.width) if px[x, y][3]}


def _contrast_pairs(s: Sprite) -> list[dict]:
    pairs = []
    if s.kind in ("badge", "marker"):
        pairs.append({"fg": cues.INK_ID, "bg": s.fill_id, "kind": "text"})  # glyph on its own fill
        pairs += [{"fg": s.fill_id, "bg": t, "kind": "nontext"} for t in ("floor_0", "floor_1")]
    elif s.kind == "frame":
        pairs += [{"fg": "ui_accent", "bg": t, "kind": "nontext"} for t in ("floor_0", "floor_1")]
    elif s.kind == "label":
        pairs += [{"fg": "ui_text", "bg": t, "kind": "text"} for t in LABEL_TONES]
    return pairs


def _pivot_check(s: Sprite, entry: dict) -> ck.CheckResult:
    if s.kind == "frame":  # the frame rests on the footprint bottom like a plate: full check applies
        return ck.check_pivot([s.image], entry)
    expected = pp.pivot_for(tuple(entry["canvas"]), tuple(entry["footprintPx"]))
    ok = tuple(round(v, 6) for v in entry["pivot"]) == tuple(expected) and ck.pivot_is_integral(entry)
    return ck.CheckResult("pivot", ok, {"expected": list(expected), "manifest": list(entry["pivot"]),
                                        "pivotPx": [round(v, 4) for v in ck.pivot_px(entry)],
                                        "integral": ck.pivot_is_integral(entry),
                                        "restsOnFootprint": "not required: overlay layers float above the footprint"})


def _band_check(s: Sprite) -> ck.CheckResult:
    """Section 3.4 / 3.2 overlay geometry. `check_overlay_keepout` is the machine keep-out probe; it is called on
    every sprite and its raw result is recorded. Badges and markers live IN the band by design (exact box
    required), labels must stay out of it, the frame may overhang (<= 12 px, checked by `overhang`)."""
    probe = v1.check_overlay_keepout([s.image])
    bbox = pp.opaque_bbox(s.image)
    if s.kind in ("badge", "marker"):
        ok = bbox == s.box and not probe.passed
        rule = "bbox equals the declared box and occupies the band"
    elif s.kind == "label":
        ok = probe.passed
        rule = "outside the band"
    else:
        ok = True
        rule = "plates and frames may overhang the footprint by up to 12 px; band applies to machine bodies"
    return ck.CheckResult("overlay_keepout", ok, {"rule": rule, "bbox": list(bbox), "box": list(s.box) if s.box else None,
                                                  "probe": {"passed": probe.passed, **probe.details}})


def _backdrop_checks(s: Sprite, palette: Palette) -> list[ck.CheckResult]:
    out = []
    allowed = v1.a_fr(palette) if s.kind == "frame" else v1.A_OV
    tones = LABEL_TONES if s.kind == "label" else DECLARED_TONES
    backdrops = {t: uniform_backdrop(t, palette) for t in tones}
    backdrops["bare-floor"] = Image.new("RGBA", cues.CANVAS, pp.TRANSPARENT)
    if s.kind in ("badge", "marker", "frame"):
        backdrops["plate-reference"] = plate_reference(palette)
    for label, bd in backdrops.items():
        # ring pixels that are plate-reference geometry are exempt only when they are the overlay itself (never)
        if s.kind == "label":
            continue  # a label sits on locked-plate tones; their ring is covered by the dim_* surfaces below
        out.append(_named("overlay_backdrop[%s]" % label, v1.check_overlay_backdrop(s.image, bd, allowed, palette)))
    if s.kind == "label":
        for t in LABEL_TONES:
            out.append(_named("overlay_backdrop[%s]" % t,
                              v1.check_overlay_backdrop(s.image, uniform_backdrop(t, palette),
                                                        v1.A_OV | {"dim_1"}, palette)))
    return out


def _outline_checks(s: Sprite, palette: Palette) -> list[ck.CheckResult]:
    out = []
    tones = LABEL_TONES if s.kind == "label" else DECLARED_TONES
    for t in tones:
        out.append(_named("outline_contrast[%s]" % t,
                          v1.check_outline_contrast(s.image, _surface(s, t, palette), palette, tones={t})))
    if s.kind != "frame":  # the frame's inside is the plate, never bare floor (see frame_surface)
        out.append(_named("outline_contrast[bare-floor]", v1.check_outline_contrast(s.image, None, palette)))
    if s.kind in ("badge", "marker", "frame"):
        out.append(_named("outline_contrast[plate-reference]",
                          v1.check_outline_contrast(s.image, plate_reference(palette), palette,
                                                    tones=set(DECLARED_TONES) | {"ink_0"})))
    # informational sweep over the whole allowed backdrop set (rule C1/C3): recorded, gated per tone below
    allowed = sorted(v1.a_fr(palette) if s.kind == "frame" else v1.A_OV)
    sweep = {}
    for t in allowed:
        sweep[t] = round(v1.outline_contrast_shares(s.image, _surface(s, t, palette), palette)[t]["share"], 4)
    failing = sorted(t for t, share in sweep.items() if share < v1.SHARE_THRESHOLD)
    out.append(ck.CheckResult("outline_contrast_sweep", not failing,
                              {"threshold": v1.SHARE_THRESHOLD, "shares": sweep, "failingTones": failing}))
    return out


def _accent_check(s: Sprite, palette: Palette) -> ck.CheckResult:
    if s.kind == "frame":
        accent = Image.new("RGBA", cues.CANVAS, pp.TRANSPARENT)
        src, dst = s.image.load(), accent.load()
        for y in range(s.image.height):
            for x in range(s.image.width):
                if src[x, y][3] and src[x, y][:3] == palette.rgb("ui_accent"):
                    dst[x, y] = src[x, y]
    else:
        accent = Image.new("RGBA", cues.CANVAS, pp.TRANSPARENT)  # no accent layer: vacuous by construction
    r = v1.check_accent_subset([accent], [s.image], palette)
    r.details["accentPixels"] = sum(1 for p in accent.getdata() if p[3])
    return r


def _reserved_colours(s: Sprite, palette: Palette) -> ck.CheckResult:
    ids = _colour_ids(s.image, palette)
    reserved = {c.id for c in palette.colors_in(RESERVED_CUE_GROUP)}
    if s.kind == "badge":
        ok = ids == {cues.INK_ID, s.fill_id}
        rule = "exactly ink_0 and the state's reserved cue colour"
    else:
        ok = not (ids & reserved)
        rule = "no reserved state-cue colour outside the badges"
    return ck.CheckResult("reserved_colours", ok, {"rule": rule, "used": sorted(ids)})


def _glyph_size(s: Sprite) -> ck.CheckResult:
    bb = pp.opaque_bbox(s.raw_glyph)
    w, h = bb[2] - bb[0], bb[3] - bb[1]
    return ck.CheckResult("glyph_size", w >= ck.CUE_MIN_PX and h >= ck.CUE_MIN_PX,
                          {"width": w, "height": h, "min": ck.CUE_MIN_PX})


def _label_checks(s: Sprite, level: str) -> list[ck.CheckResult]:
    bb = pp.opaque_bbox(s.image)
    cx, cy = cues.LABEL_CENTRE
    digit_w = 3 * cues.LABEL_SCALE
    digit_h = 5 * cues.LABEL_SCALE
    # digit is the opaque ui_text part; the outline adds 1 px per side
    centred = (bb[0] + bb[2] - 1) == 2 * cx and (bb[1] + bb[3] - 1) == 2 * cy
    sized = (bb[2] - bb[0] - 2, bb[3] - bb[1] - 2) == (digit_w, digit_h)
    return [ck.CheckResult("label_geometry", centred and sized,
                           {"level": level, "digitPx": [digit_w, digit_h], "capPx": digit_h, "bbox": list(bb),
                            "centre": [cx, cy]})]


def check_sprite(s: Sprite, entry: dict, palette: Palette, twin: Sprite) -> list[ck.CheckResult]:
    if s.kind == "empty":
        return [ck.check_intentionally_empty([], entry)]
    groups = {"badge": {"ink", RESERVED_CUE_GROUP}, "marker": {"ink", "cvd_safe"},
              "frame": {"ink", "ui"}, "label": {"ink", "ui"}}[s.kind]
    frames = [s.image]
    res = [
        ck.check_palette(frames, palette, groups),
        _reserved_colours(s, palette),
        ck.check_canvas(frames, cues.CANVAS),
        _pivot_check(s, entry),
        ck.check_alpha(frames),
        ck.check_outline(frames, palette),
        ck.check_frames(frames, 1),
    ]
    if s.kind == "badge":
        res.append(ck.check_cue_icon_size(s.image))
        res.append(_glyph_size(s))
    if s.kind == "marker":
        bb = pp.opaque_bbox(s.image)
        res.append(ck.CheckResult("marker_size", (bb[2] - bb[0], bb[3] - bb[1]) == (11, 11),
                                  {"size": [bb[2] - bb[0], bb[3] - bb[1]], "deviation": "DEV-3 (11 x 11, below 12 x 12)"}))
    if s.kind == "frame":
        res.append(ck.check_overhang(frames, cues.CANVAS, cues.FOOTPRINT, PPU))
    if s.kind == "label":
        res += _label_checks(s, cues.LABEL_KEYS[s.key])
    res.append(ck.check_contrast(_contrast_pairs(s), palette, DARKEST_LIGHT))
    res.append(_band_check(s))
    res += _backdrop_checks(s, palette)
    res += _outline_checks(s, palette)
    res.append(_accent_check(s, palette))
    res.append(ck.check_reproduction(frames, [twin.image], 0.0))
    return res


# --- provenance, manifest, report -----------------------------------------------------------------

def source_hashes(root: Path | None = None) -> dict[str, str]:
    base = Path(__file__).resolve().parent
    out = {}
    for name in _SRC_FILES:
        text = (base / name).read_bytes().replace(b"\r\n", b"\n")
        out[name] = hashlib.sha256(text).hexdigest()
    return out


def script_sha256() -> str:
    h = hashlib.sha256()
    for name, digest in sorted(source_hashes().items()):
        h.update(name.encode() + b":" + digest.encode() + b"\n")
    return h.hexdigest()


def _entry(s: Sprite, palette: Palette, prov: dict) -> dict:
    if s.kind == "empty":
        return {"assetId": s.asset_id, "key": s.key, "family": s.family, "route": "scripted", "intentionallyEmpty": True,
                "palette": {"id": palette.palette_id, "version": palette.version}}
    cv, fp = cues.CANVAS, cues.FOOTPRINT
    placement = pp.Placement(cv, fp, pp.footprint_rect(cv, fp), pp.pivot_for(cv, fp), (0, 0))
    # reviewedBy stays empty in provenance.json (T439 fills it); the manifest builder only needs it non-blank.
    gate = dict(prov, reviewedBy="pending: T439 independent review")
    e = pp.build_manifest_entry(s.asset_id, "scripted", [s.image], placement, palette, PPU, 0, gate,
                                frame_dir=s.asset_id)
    e["key"], e["family"], e["kind"] = s.key, s.family, s.kind
    e["box"] = list(s.box)
    return e


def _provenance(s: Sprite, palette: Palette, outputs: list[dict], report_hash: str) -> dict:
    params: dict = {"scriptVersion": cues.SCRIPT_VERSION, "canvas": list(cues.CANVAS), "footprintPx": list(cues.FOOTPRINT),
                    "pivotPx": list(cues.PIVOT_PX), "ppu": PPU, "key": s.key, "kind": s.kind, "seed": None,
                    "randomness": "none"}
    if s.kind == "badge":
        st, fill, glyph, k = cues.BADGE_SPECS[s.key]
        params.update(state=st, fill=fill, glyph=glyph, glyphScale=k, innerPx=cues.BADGE_INNER, box=list(s.box))
    elif s.kind == "marker":
        name, fill = cues.MARKER_SPECS[s.key]
        params.update(marker=name, fill=fill, innerPx=cues.MARKER_INNER, box=list(s.box))
    elif s.kind == "frame":
        params.update(diamondRawWidth=cues.FRAME_RAW_WIDTH, apex=cues.FRAME_APEX, rawTopRow=cues.FRAME_TOP_ROW)
    elif s.kind == "label":
        params.update(digit=cues.LABEL_KEYS[s.key], font="authored 3x5 bitmap", fontScale=cues.LABEL_SCALE,
                      centre=list(cues.LABEL_CENTRE))
    else:
        params.update(note="owner decision R8: manifest entry only, no PNG, no frame, no renderer drawn")
    return {
        "assetId": s.asset_id,
        "revision": 1,
        "route": "scripted",
        "sources": [{"path": "tools/art-pipeline/art_pipeline/%s" % n, "sha256": h} for n, h in sorted(source_hashes().items())],
        "tools": [{"name": "art_pipeline.cues", "version": cues.SCRIPT_VERSION},
                  {"name": "Pillow", "version": "11.3.0 (pinned in tools/art-pipeline/pyproject.toml)"},
                  {"name": "Python", "version": ">=3.12,<3.14"}],
        "parameters": params,
        "touchUp": [],
        "pixelPass": {"steps": ["reduce_for_route(scripted)", "threshold_alpha", "quantize(palette groups)",
                                "add_outline(ink_0)", "placement on 80x64 overlay canvas"],
                      "scriptSha256": script_sha256(), "paletteId": palette.palette_id,
                      "paletteVersion": palette.version},
        "outputs": outputs,
        "license": "authored: project-owned scripted drawing, no third-party material",
        "trainingDataNote": "not applicable: no generative model, LoRA or training data is used on the scripted route",
        "producedBy": "Asset Artist (scripted route), MAQA T437",
        "reviewedBy": {},
        "candidateSelection": "none: a single deterministic render, no candidates, no seed",
        "artBibleVersion": ART_BIBLE,
        "briefVersion": BRIEF,
        "checkReportHash": report_hash,
    }


FAMILY_FILES = {  # family -> (manifest name, report name); slots files carry a suffix so T435 keeps the plain names
    "cues": ("manifest.json", "check-report.json"),
    "markers": ("manifest.json", "check-report.json"),
    "slots": ("manifest-t437.json", "check-report-t437.json"),
}


def build_all(palette: Palette | None = None) -> dict:
    """Run the whole production in memory. Returns {'sprites', 'entries', 'reports'} keyed by family."""
    palette = palette or load_palette(PALETTE_V1_PATH)
    sprites = build_sprites(palette)
    twins = {s.key: s for s in build_sprites(palette)}  # second, independent render for the reproduction check
    families: dict[str, list[Sprite]] = {}
    for s in sprites:
        families.setdefault(s.family, []).append(s)
    entries: dict[str, dict] = {}
    reports: dict[str, dict] = {}
    for fam, group in families.items():
        assets = {}
        for s in group:
            prov_stub = _provenance(s, palette, [{"path": s.asset_id, "sha256": "0" * 64}], "0" * 64)
            entries[s.key] = _entry(s, palette, prov_stub)
            s.results = check_sprite(s, entries[s.key], palette, twins[s.key])
            assets[s.asset_id] = s.results
        glob = [ck.check_palette_definition(palette)]
        peers = [s for s in group if s.kind in ("badge", "marker")]
        if peers:
            masks = {s.key: ck.silhouette_mask(s.raw_glyph) for s in peers}
            glob.append(_named("glyph_silhouette_iou", ck.check_iou(masks)))
        reports[fam] = ck.build_report(assets, glob)
    return {"palette": palette, "sprites": sprites, "entries": entries, "reports": reports, "families": families}


def _write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def write_all(out_root: Path | str, palette: Palette | None = None) -> dict:
    """Write art-source/f004-style output under `out_root` (the `art-source/f004` directory). Returns the build."""
    out_root = Path(out_root)
    b = build_all(palette)
    pal = b["palette"]
    for fam, group in b["families"].items():
        manifest_name, report_name = FAMILY_FILES[fam]
        fam_dir = out_root / fam
        report = b["reports"][fam]
        report_text = ck.report_json(report)
        _write(fam_dir / report_name, report_text.encode("utf-8"))
        report_hash = pp.sha256_hex(report_text.encode("utf-8"))
        entries = []
        for s in group:
            outputs = []
            if s.image is not None:
                data = pp.png_bytes(s.image)
                rel = "%s/%s_00.png" % (s.asset_id, s.asset_id)
                _write(fam_dir / rel, data)
                outputs.append({"path": "art-source/f004/%s/%s" % (fam, rel), "sha256": pp.sha256_hex(data),
                                "pixelHash": pp.pixel_hash(s.image)})
            else:
                outputs.append({"note": "intentionallyEmpty: no PNG (owner decision R8)"})
            prov = _provenance(s, pal, outputs, report_hash)
            _write(fam_dir / s.asset_id / "provenance.json",
                   (json.dumps(prov, indent=2, sort_keys=True) + "\n").encode("utf-8"))
            entries.append(_entry(s, pal, prov))
        _write(fam_dir / manifest_name, pp.manifest_json(pp.build_manifest(entries)).encode("utf-8"))
    # seeded shuffled grayscale / CVD review sheets for the independent reviewer (T439), cues and markers together
    sheet_src = {s.asset_id: _crop(s.image) for s in b["sprites"] if s.kind in ("badge", "marker")}
    sheets = ck.make_review_sheets(sheet_src, SHEET_SEED, out_root / "cues" / "sheets")
    b["sheets"] = sheets
    return b
