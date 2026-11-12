"""F006 T524: the production pointer tail of the F004 state badges (OD-12, anchor K4; Art Bible v1.1.0 section 21.8 item 4).

The badge keeps its anchor (bottom-centre (59, 33), box x 48..70, rows 10..32 of the 80 x 64 overlay canvas) and gains a 4 art px tail
below the box. The tail is a triangle of rows 7, 5, 3 and 1 px wide, centred on canvas column 59, drawn in `ink_0` (the two edges and the tip)
and the badge's own reserved cue fill (inside), palette-exact. The box's `ink_0` bottom row turns to fill under the 7 px tail so that the tail
joins the body. The 23 px box is never altered otherwise (Art Bible 4.3). Route: scripted (research R07). Order per sprite: the F004
`cues_production` badge (scripted drawing, pixel pass, placement) -> this tail step -> checks -> provenance. No clock, no randomness, no absolute
paths; every output is byte-identical on a rerun.

Output goes under `art-source/f006/cue/` (family `cue-tail`). It does not replace the imported F004 sprites in `Assets/IdleFactory/Art/F004/`:
that swap is an integration step after the Art Director's pixel review (T525).
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image

from .. import checks as ck
from .. import cues
from .. import cues_production as cp
from .. import pixel_pass as pp
from ..palette import Palette, PALETTE_V1_PATH, load_palette

ART_BIBLE = "F004-PAB v1.1.0"
BRIEF = "F006-CUE-TAIL v0.1 (art-source/f006/cue/brief-tail.md)"
SCRIPT_VERSION = "t524-1"
ANCHOR = (59, 33)            # bottom-centre of the box, canvas pixels (x, row from the top)
TAIL_ROWS = 4
TAIL_BASE_HALF = 3           # row 0 is 7 px wide: half width 3, then 2, 1, 0
SHEET_SEED = 524
BOX_WITH_TAIL = (48, 10, 71, 37)   # opaque bounding box (x0, y0, x1, y1 exclusive): 23 x 27


def apply_tail(img: Image.Image, palette: Palette, fill_id: str) -> Image.Image:
    """Return a copy of the placed 80 x 64 badge with the pointer tail. Pure function of its inputs."""
    out = img.copy()
    px = out.load()
    cx, bottom = ANCHOR[0], ANCHOR[1] - 1          # `bottom` = last opaque row of the box (row 32)
    fill = palette.rgb(fill_id) + (255,)
    ink = palette.rgb(cues.INK_ID) + (255,)
    for x in range(cx - TAIL_BASE_HALF, cx + TAIL_BASE_HALF + 1):
        px[x, bottom] = fill
    for i in range(TAIL_ROWS):
        half = TAIL_BASE_HALF - i
        for x in range(cx - half, cx + half + 1):
            px[x, bottom + 1 + i] = ink if (x in (cx - half, cx + half) or i == TAIL_ROWS - 1) else fill
    return out


def build_sprites(palette: Palette) -> list[cp.Sprite]:
    sprites = []
    for s in cp.build_sprites(palette):
        if s.kind != "badge":
            continue
        sprites.append(cp.Sprite(s.key, "badge", "cue-tail", apply_tail(s.image, palette, s.fill_id), BOX_WITH_TAIL, s.fill_id, s.raw_glyph))
    return sprites


# --- checks ---------------------------------------------------------------------------------------

def _tail_geometry(tailed: Image.Image, base: Image.Image, palette: Palette, fill_id: str) -> ck.CheckResult:
    """The tail is exactly the specified triangle, the box above its bottom row is untouched and nothing else changed."""
    t, b = tailed.load(), base.load()
    cx, bottom = ANCHOR[0], ANCHOR[1] - 1
    fill = palette.rgb(fill_id) + (255,)
    ink = palette.rgb(cues.INK_ID) + (255,)
    errors = []
    widths = []
    for i in range(TAIL_ROWS):
        y = bottom + 1 + i
        xs = [x for x in range(tailed.width) if t[x, y][3]]
        widths.append(len(xs))
        half = TAIL_BASE_HALF - i
        if xs != list(range(cx - half, cx + half + 1)):
            errors.append("row %d extent %s" % (i, xs[:1] + xs[-1:]))
        for x in xs:
            want = ink if (x in (cx - half, cx + half) or i == TAIL_ROWS - 1) else fill
            if t[x, y] != want:
                errors.append("row %d x %d colour" % (i, x))
    changed_box = sorted((x, y) for y in range(tailed.height) for x in range(tailed.width)
                         if y <= bottom and t[x, y] != b[x, y])
    allowed = {(x, bottom) for x in range(cx - TAIL_BASE_HALF, cx + TAIL_BASE_HALF + 1)}
    if not set(changed_box) <= allowed:
        errors.append("box pixels changed outside the joint row: %s" % sorted(set(changed_box) - allowed)[:4])
    bb = pp.opaque_bbox(tailed)
    if bb != BOX_WITH_TAIL:
        errors.append("bbox %s != %s" % (bb, BOX_WITH_TAIL))
    return ck.CheckResult("tail_geometry", not errors, {"rowWidths": widths, "jointPixels": len(changed_box), "bbox": list(bb), "errors": errors})


def check_sprite(s: cp.Sprite, base: cp.Sprite, entry: dict, palette: Palette, twin: cp.Sprite) -> list[ck.CheckResult]:
    res = cp.check_sprite(s, entry, palette, twin)
    res.append(_tail_geometry(s.image, base.image, palette, s.fill_id))
    return res


# --- provenance, manifest, report -----------------------------------------------------------------

def source_hashes() -> dict[str, str]:
    out = dict(cp.source_hashes())
    text = (Path(__file__).resolve().parent / "cue_tail.py").read_bytes().replace(b"\r\n", b"\n")
    out["f006/cue_tail.py"] = hashlib.sha256(text).hexdigest()
    return out


def _provenance(s: cp.Sprite, palette: Palette, outputs: list[dict], report_hash: str) -> dict:
    prov = cp._provenance(s, palette, outputs, report_hash)
    digest = hashlib.sha256()
    for name, h in sorted(source_hashes().items()):
        digest.update(name.encode() + b":" + h.encode() + b"\n")
    prov["sources"] = [{"path": "tools/art-pipeline/art_pipeline/%s" % n, "sha256": h} for n, h in sorted(source_hashes().items())]
    prov["tools"] = [{"name": "art_pipeline.f006.cue_tail", "version": SCRIPT_VERSION}] + prov["tools"]
    prov["parameters"].update(tailRows=TAIL_ROWS, tailRowWidths=[2 * (TAIL_BASE_HALF - i) + 1 for i in range(TAIL_ROWS)],
                              anchor=list(ANCHOR), boxWithTail=list(BOX_WITH_TAIL), od="OD-12 (anchor K4)")
    prov["pixelPass"]["steps"] = prov["pixelPass"]["steps"] + ["pointer tail (7, 5, 3, 1 px rows, ink_0 edges and tip, badge fill inside)"]
    prov["pixelPass"]["scriptSha256"] = digest.hexdigest()
    prov["producedBy"] = "Asset Artist (scripted route), MAQA T524"
    prov["artBibleVersion"] = ART_BIBLE
    prov["briefVersion"] = BRIEF
    prov["reviewedBy"] = {"status": "pending: T525 Art Director pixel review (the producer does not approve its own art)"}
    return prov


def _entry(s: cp.Sprite, palette: Palette, prov: dict) -> dict:
    e = cp._entry(s, palette, prov)
    e["artBibleVersion"] = ART_BIBLE
    return e


def build_all(palette: Palette | None = None) -> dict:
    palette = palette or load_palette(PALETTE_V1_PATH)
    sprites = build_sprites(palette)
    twins = {s.key: s for s in build_sprites(palette)}
    bases = {s.key: s for s in cp.build_sprites(palette) if s.kind == "badge"}
    entries, assets = {}, {}
    for s in sprites:
        stub = _provenance(s, palette, [{"path": s.asset_id, "sha256": "0" * 64}], "0" * 64)
        entries[s.key] = _entry(s, palette, stub)
        s.results = check_sprite(s, bases[s.key], entries[s.key], palette, twins[s.key])
        assets[s.asset_id] = s.results
    masks = {s.key: ck.silhouette_mask(s.raw_glyph) for s in sprites}
    glob = [ck.check_palette_definition(palette), cp._named("glyph_silhouette_iou", ck.check_iou(masks))]
    report = ck.build_report(assets, glob)
    return {"palette": palette, "sprites": sprites, "entries": entries, "report": report}


def _write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def write_all(out_root: Path | str, palette: Palette | None = None) -> dict:
    """Write the tail sprites, provenance, manifest, check report and review sheets under `out_root` (art-source/f006/cue)."""
    out_root = Path(out_root)
    b = build_all(palette)
    pal = b["palette"]
    report_text = ck.report_json(b["report"])
    _write(out_root / "check-report.json", report_text.encode("utf-8"))
    report_hash = pp.sha256_hex(report_text.encode("utf-8"))
    entries = []
    for s in b["sprites"]:
        data = pp.png_bytes(s.image)
        rel = "%s/%s_00.png" % (s.asset_id, s.asset_id)
        _write(out_root / rel, data)
        outputs = [{"path": "art-source/f006/cue/%s" % rel, "sha256": pp.sha256_hex(data), "pixelHash": pp.pixel_hash(s.image)}]
        prov = _provenance(s, pal, outputs, report_hash)
        _write(out_root / s.asset_id / "provenance.json", (json.dumps(prov, indent=2, sort_keys=True) + "\n").encode("utf-8"))
        entries.append(_entry(s, pal, prov))
    _write(out_root / "manifest.json", pp.manifest_json(pp.build_manifest(entries)).encode("utf-8"))
    sheet_src = {s.asset_id: s.image.crop(pp.opaque_bbox(s.image)) for s in b["sprites"]}
    b["sheets"] = ck.make_review_sheets(sheet_src, SHEET_SEED, out_root / "sheets")
    return b
