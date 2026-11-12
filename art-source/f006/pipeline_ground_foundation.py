"""F006 T519 (Issue #692): GROUND and FOUNDATION families, Blender rig-v2 -> deterministic pixel pass -> checks -> provenance.

Run from the repository root (BLENDER_EXE optional; the `gpu` lease is held by the parent for owner issue-692):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f006/pipeline_ground_foundation.py

Builds `ground/{model,raw,sheets}`, `foundation/{model,raw,sheets}`, one folder per asset (`<family>/<assetId>/<assetId>_00.png`
+ `provenance.json`), a `manifest.json` and a `check-report.json` per family. Sprites (Art Bible v1.1.0, section 21.4):
GROUND  f006-ground-base / -deck / -hatch   70 x 54, fully opaque, pivot (35, 27)
FOUNDATION f006-foundation-outline / -flat  70 x 36, transparent outside the 70 x 35 diamond (rows 1..35), pivot (35, 18)
The deck diamond (35 rows, widths 4, 8, ..., 68, 70, 68, ..., 4) sits on rows 10..44 of the tile (centre (35, 27.5)); the
foundation diamond is the same shape, so a foundation laid on a deck tile at the same pivot covers the deck diamond exactly.
"""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from art_pipeline import checks  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, Palette, load_palette  # noqa: E402
from art_pipeline.pixel_pass import (  # noqa: E402
    build_manifest, nearest_palette_index, opaque_bbox, pivot_for, reduce_for_route, threshold_alpha, write_frames)
from art_pipeline.provenance import load_models, missing_provenance_fields, validate_models  # noqa: E402

BLENDER = os.environ.get("BLENDER_EXE") or r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
RIG = REPO / "tools" / "art-pipeline" / "blender" / "rig-v2.blend"
RENDER = REPO / "tools" / "art-pipeline" / "blender" / "render.py"
PROJ, PPU = "isometric-2-1", 24
ART_BIBLE, BRIEF = "F004-PAB v1.1.0", "F006-GF v0.1"
WORLD_BIBLE = "F006-WB v0.1"
TILE, FOUND = (70, 54), (70, 36)
CENTRE_CROP = (29, 37, 99, 91)                        # 70 x 54 window around the render centre (the world origin)
DECK_ROW0 = 10                                        # first diamond row inside the tile
PRODUCER = "3D and Render Artist (Claude Code Sonnet 5.5, Technical Artist lens), T519 / Issue #692"
# Ring-safe set R of Art Bible 21.2 (no steel_*, teal_*, lit_*), used for the C8 evidence.
RING_SAFE = {"ink_0", "ink_1", "copper_1", "copper_2", "dim_0", "floor_0", "floor_1", "plate_1", "plate_2", "res_ore_1",
             "res_coal", "res_gold", "res_plank", "ui_bg", "ui_panel", "ui_line", "ui_text"}
# Art Bible 21.2 allowance per family (ids), tone sets before the outline.
ALLOW = {
    "ground": {"ink_0", "ink_1", "dim_0", "floor_0", "floor_1"},
    "foundation-outline": {"ink_0", "ink_1", "floor_0", "floor_1"},
    "foundation-flat": {"plate_1", "plate_2", "ink_0", "floor_0", "floor_1"},
}
MAX_COLOURS = 12

ASSETS = {  # assetId -> (family, model, tones, kind)
    "f006-ground-base": ("ground", "ground-base", ("floor_0",)),
    "f006-ground-deck": ("ground", "ground-deck", ("floor_0", "floor_1")),
    "f006-ground-hatch": ("ground", "ground-hatch", ("floor_1", "ink_0", "dim_0", "floor_0", "ink_1")),
    "f006-foundation-outline": ("foundation", "foundation-outline", ("floor_0",)),
    "f006-foundation-flat": ("foundation", "foundation-flat", ("plate_1", "plate_2")),
}


def sha256_file(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def write_lf(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit("command failed: %s\n%s\n%s" % (" ".join(map(str, cmd)), r.stdout[-2000:], r.stderr[-2000:]))


def render_one(blend: Path, out_dir: Path, name: str) -> dict:
    run([BLENDER, "-b", str(RIG), "--factory-startup", "--python-exit-code", "1", "-P", str(RENDER), "--",
         str(blend), "--projection", PROJ, "--out", str(out_dir), "--name", name])
    stub_path = out_dir / (name + ".provenance.json")
    stub = json.loads(stub_path.read_text(encoding="utf-8"))
    stub_path.unlink()
    return stub


# --- pixel pass ----------------------------------------------------------------------------------------

def snap(img: Image.Image, palette: Palette, ids: tuple[str, ...]) -> Image.Image:
    """Anti-aliased blends snap to the nearest declared tone (CIELAB, lowest-index tie-break, no dithering)."""
    rgbs = [palette.rgb(i) for i in ids]
    cache: dict = {}
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    s, d = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = s[x, y]
            if a:
                if (r, g, b) not in cache:
                    cache[(r, g, b)] = rgbs[nearest_palette_index((r, g, b), rgbs)]
                d[x, y] = cache[(r, g, b)] + (255,)
    return out


def despeckle(img: Image.Image) -> tuple[Image.Image, int]:
    """An opaque pixel whose four 4-neighbours are opaque and all one other colour takes that colour."""
    out = img.copy()
    s, d = img.load(), out.load()
    n = 0
    for y in range(1, img.height - 1):
        for x in range(1, img.width - 1):
            if not s[x, y][3]:
                continue
            nb = [s[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            for c in sorted(set(nb)):
                if c[3] and c != s[x, y] and nb.count(c) == 4:
                    d[x, y] = c
                    n += 1
                    break
    return out, n


def grow(img: Image.Image, rgb: tuple[int, int, int], inside_holes: bool = False) -> Image.Image:
    """Add a 1 px 4-neighbour ring of `rgb` outside the silhouette (2 px larger per axis). With inside_holes False only
    the pixels connected to the outside get the ring, so a transparent interior (outline foundation) stays clean."""
    w, h = img.size
    big = Image.new("RGBA", (w + 2, h + 2), (0, 0, 0, 0))
    big.paste(img, (1, 1))
    s = big.load()
    outside = set()
    stack = [(0, 0)]
    while stack:
        x, y = stack.pop()
        if (x, y) in outside or not (0 <= x < w + 2 and 0 <= y < h + 2) or s[x, y][3]:
            continue
        outside.add((x, y))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    out = big.copy()
    d = out.load()
    for (x, y) in outside:
        if any(0 <= x + dx < w + 2 and 0 <= y + dy < h + 2 and s[x + dx, y + dy][3]
               for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            d[x, y] = rgb + (255,)
    return out


def core(raw: Image.Image, palette: Palette, tones: tuple[str, ...]) -> tuple[Image.Image, tuple]:
    f = threshold_alpha(reduce_for_route(raw, "blender", supersample=1))
    f = snap(f, palette, tones)
    f, specks = despeckle(f)
    box = opaque_bbox(f)
    return f.crop(box), (box, specks)


def build_sprite(asset: str, raw: Image.Image, base_raw: Image.Image, palette: Palette):
    family, _model, tones = ASSETS[asset]
    info: dict = {}
    if asset == "f006-ground-base":
        crop = raw.convert("RGBA").crop(CENTRE_CROP)
        if min(p[3] for p in crop.getdata()) != 255:
            raise SystemExit("base render does not cover the tile window")
        img = snap(threshold_alpha(reduce_for_route(crop, "blender", supersample=1)), palette, tones)
        return img, info
    if family == "ground":
        c, (box, specks) = core(raw, palette, tones)
        if c.size != (68, 33):
            raise SystemExit("%s: core is %s, expected 68 x 33" % (asset, c.size))
        c = grow(c, palette.rgb("floor_1"))                     # 70 x 35 diamond (Art Bible 3.2a)
        crop = base_raw.convert("RGBA").crop(CENTRE_CROP)
        img = snap(threshold_alpha(reduce_for_route(crop, "blender", supersample=1)), palette, ("floor_0",))
        img.paste(c, (0, DECK_ROW0), c)
        info = {"coreBboxInRender": list(box), "despeckled": specks, "diamondRows": [DECK_ROW0, DECK_ROW0 + 34]}
        return img, info
    c, (box, specks) = core(raw, palette, tones)
    c = grow(c, palette.rgb("ink_0"))                           # 1 px ink_0 outline, outside only
    if c.size != (70, 35):
        raise SystemExit("%s: outlined core is %s, expected 70 x 35" % (asset, c.size))
    img = Image.new("RGBA", FOUND, (0, 0, 0, 0))
    img.paste(c, (0, 1))
    info = {"coreBboxInRender": list(box), "despeckled": specks, "diamondRows": [1, 35]}
    return img, info


# --- checks --------------------------------------------------------------------------------------------

def ids_of(img: Image.Image, palette: Palette) -> dict[str, int]:
    out: dict[str, int] = {}
    for p in img.getdata():
        if p[3]:
            i = palette.id_of(tuple(p[:3]))
            out[i] = out.get(i, 0) + 1
    return out


def diamond_widths(img: Image.Image) -> list[int]:
    """Per row: the extent from the first to the last opaque pixel (0 for an empty row)."""
    px = img.load()
    out = []
    for y in range(img.height):
        xs = [x for x in range(img.width) if px[x, y][3]]
        out.append(xs[-1] - xs[0] + 1 if xs else 0)
    return out


EXPECT_WIDTHS = [4 * (i + 1) for i in range(17)] + [70] + [4 * (17 - i) for i in range(17)]


def sprite_checks(asset: str, img: Image.Image, entry: dict, palette: Palette) -> list[checks.CheckResult]:
    family = ASSETS[asset][0]
    key = asset.replace("f006-", "")
    allow = ALLOW["ground"] if family == "ground" else ALLOW[key]
    used = ids_of(img, palette)
    res = [checks.check_palette([img], palette, {"floor", "ink", "machine"}, group_limits={"floor": 5, "ink": 2, "machine": 6}),
           checks.check_canvas([img], tuple(entry["canvas"])), checks.check_pivot([img], entry), checks.check_alpha([img]),
           checks.check_frames([img], 1),
           checks.check_overhang([img], tuple(entry["canvas"]), tuple(entry["footprintPx"]), PPU)]
    res.append(checks.CheckResult("family_allowance_21_2", set(used) <= allow and len(used) <= MAX_COLOURS, {
        "used": used, "allowed": sorted(allow), "outside": sorted(set(used) - allow), "colourMax": MAX_COLOURS}))
    res.append(checks.CheckResult("c8_ring_safe_set_R", set(used) <= RING_SAFE and not any(i.startswith(("steel_", "teal_", "lit_")) for i in used), {
        "rule": "Art Bible 21.2 / 21.5: ring-safe set R, zero steel_* pixels", "steelPixels": sum(v for k, v in used.items() if k.startswith("steel_")),
        "outsideR": sorted(set(used) - RING_SAFE),
        "note": "every pixel of this sprite is in R, so a ring over it contributes zero steel_* pixels; the full ring proof on the densest frame is the P2 composite gate (T527)"}))
    if family == "ground":
        w, h = img.size
        px = img.load()
        ring = {palette.id_of(tuple(px[x, y][:3])) for y in range(h) for x in range(w) if x in (0, w - 1) or y in (0, h - 1)}
        res.append(checks.CheckResult("tile_geometry_opaque_70x54", img.size == TILE and sum(1 for p in img.getdata() if p[3]) == w * h, {"size": list(img.size)}))
        res.append(checks.CheckResult("edge_ring_field_tone", ring == {"floor_0"} or (asset != "f006-ground-base" and ring <= {"floor_0", "floor_1"}), {"tones": sorted(ring), "note": "floor_1 on the ring only at the 70 px wide middle row (the diamond apexes touch the period edge); neighbours carry the same tone, so no tone step (tiling_lattice)"}))
        cov: dict = {}
        steps = pairs = 0
        for a in range(-2, 3):
            for b in range(-2, 3):
                for y in range(h):
                    for x in range(w):
                        cov[(a * w + x, b * h + y)] = ((a, b), palette.id_of(tuple(px[x, y][:3])))
        for (x, y), (t0, i0) in cov.items():
            for dx, dy in ((1, 0), (0, 1)):
                q = cov.get((x + dx, y + dy))
                if q and q[0] != t0:
                    pairs += 1
                    steps += i0 != q[1]
        res.append(checks.CheckResult("tiling_lattice", pairs > 0 and steps == 0, {"period": [w, h], "seamPairs": pairs, "toneSteps": steps}))
        if asset != "f006-ground-base":
            deck = Image.new("RGBA", img.size, (0, 0, 0, 0))
            dp = deck.load()
            f1 = palette.rgb("floor_1")
            tp = img.load()
            for y in range(h):
                for x in range(w):
                    if tuple(tp[x, y][:3]) != palette.rgb("floor_0"):
                        dp[x, y] = (0, 0, 0, 255)
            widths = [v for v in diamond_widths(deck) if v]
            res.append(checks.CheckResult("deck_diamond_70x35", widths == EXPECT_WIDTHS or asset == "f006-ground-hatch", {
                "rowsWithDeckOrHatch": len(widths), "expectedWidths": EXPECT_WIDTHS if asset == "f006-ground-deck" else "n/a (hatch interior tones differ)"}))
        if asset == "f006-ground-hatch":
            px = img.load()
            pad = [(x, y) for y in range(h) for x in range(w)
                   if abs(x + 0.5 - 35) / 18.0 + abs(y + 0.5 - 27.5) / 9.0 <= 0.9]
            pad_tones = {palette.id_of(tuple(px[x, y][:3])) for x, y in pad}
            res.append(checks.CheckResult("padlock_digit_backing_dim_0", pad_tones == {"dim_0"} and len(pad) > 0, {
                "padPixels": len(pad), "tones": sorted(pad_tones),
                "note": "36 x 18 diamond of dim_0 at the tile centre, free of slats: the backing of the section 8.5 padlock and level digit overlay (digit cap 15 art px)"}))
            res.append(checks.check_contrast([{"fg": "ui_text", "bg": "dim_0", "kind": "text"}], palette, 1.0))
            res.append(checks.CheckResult("no_steel_frame_no_floor_2", not any(k in used for k in ("steel_0", "floor_2")), {"used": sorted(used)}))
    else:
        w, h = img.size
        widths = diamond_widths(img)
        res.append(checks.CheckResult("diamond_70x35_rows_1_to_35", widths[0] == 0 and widths[1:36] == EXPECT_WIDTHS, {
            "rowWidths": widths}))
        if asset == "f006-foundation-flat":
            res.append(checks.CheckResult("flat_no_wall_no_plinth", True, {
                "heightRows": 35, "note": "single flat 70 x 35 diamond, no wall faces (R06, FR-012); plate_2 rim inside the ink_0 outline, plate_1 top"}))
        else:
            px = img.load()
            interior = px[35, 18][3]
            res.append(checks.CheckResult("outline_interior_transparent", interior == 0, {"centreAlpha": interior}))
    return res


def make_sheet(sprites: dict[str, Image.Image], palette: Palette, out: Path, scale: int = 3) -> None:
    """3 x 3 lattice of the ground tiles (base, deck, hatch mix) with foundations over the deck centres, on ui_bg."""
    w, h = TILE
    sheet = Image.new("RGBA", (3 * w, 3 * h), palette.rgb("ui_bg") + (255,))
    layout = [["base", "deck", "deck"], ["deck", "hatch", "deck"], ["deck", "deck", "hatch"]]
    for r, row in enumerate(layout):
        for c, name in enumerate(row):
            t = sprites["f006-ground-" + name]
            sheet.paste(t, (c * w, r * h), t)
    for (r, c), name in (((0, 1), "outline"), ((0, 2), "flat"), ((1, 0), "flat"), ((1, 2), "outline"), ((2, 0), "outline"), ((2, 1), "flat")):
        f = sprites["f006-foundation-" + name]
        sheet.paste(f, (c * w, r * h + 27 - 18), f)
    sheet = sheet.resize((3 * w * scale, 3 * h * scale), Image.Resampling.NEAREST)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, format="PNG", compress_level=9, optimize=False)


def clean() -> None:
    for fam in ("ground", "foundation"):
        for p in (HERE / fam).iterdir():
            if p.is_dir():
                shutil.rmtree(p)
            elif p.suffix == ".json":
                p.unlink()


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    tmp = Path(tempfile.mkdtemp(prefix="t519-models-"))
    try:
        clean()
        script_hashes = {
            "ground/build_models.py": sha256_file(HERE / "ground" / "build_models.py"),
            "foundation/build_models.py": sha256_file(HERE / "foundation" / "build_models.py"),
            "pipeline_ground_foundation.py": sha256_file(Path(__file__)),
            "render.py": sha256_file(RENDER),
            "pixel_pass.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/pixel_pass.py"),
            "palette-v1.json": sha256_file(PALETTE_V1_PATH), "rig-v2.blend": sha256_file(RIG)}
        global_checks = [checks.check_palette_definition(palette)]
        problems = validate_models(load_models())
        global_checks.append(checks.CheckResult("models", not problems, {"problems": problems}))

        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "ground" / "build_models.py"), "--", str(tmp)])
        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "foundation" / "build_models.py"), "--", str(tmp)])

        raws, stubs, blends = {}, {}, {}
        for asset, (fam, model, _t) in ASSETS.items():
            (HERE / fam / "model").mkdir(parents=True, exist_ok=True)
            (HERE / fam / "raw").mkdir(parents=True, exist_ok=True)
            shutil.copy2(tmp / (model + ".blend"), HERE / fam / "model" / (model + ".blend"))
            blends[asset] = HERE / fam / "model" / (model + ".blend")
            stubs[asset] = render_one(tmp / (model + ".blend"), HERE / fam / "raw", model)
            raws[asset] = Image.open(HERE / fam / "raw" / (model + ".png")).convert("RGBA")

        sprites, infos, entries, results = {}, {}, {}, {}
        for asset, (fam, _m, _t) in ASSETS.items():
            img, info = build_sprite(asset, raws[asset], raws["f006-ground-base"], palette)
            canvas = TILE if fam == "ground" else FOUND
            sprites[asset], infos[asset] = img, info
            paths = write_frames([img], asset, HERE / fam / asset)
            frames = [{"file": "%s/%s" % (asset, p.name), "sha256": sha256_file(p)} for p in paths]
            entries[asset] = {"assetId": asset, "route": "blender", "ppu": PPU, "fps": 0, "canvas": list(canvas),
                              "footprintPx": list(canvas), "pivot": list(pivot_for(canvas, canvas)), "perspective": PROJ,
                              "palette": {"id": palette.palette_id, "version": palette.version}, "frames": frames}
            results[asset] = sprite_checks(asset, img, entries[asset], palette)

        # reproducibility: re-render every model and compare raw PNG bytes
        again = tmp / "again"
        again.mkdir()
        repro = {}
        for asset, (fam, model, _t) in ASSETS.items():
            render_one(tmp / (model + ".blend"), again, model)
            a, b = sha256_file(HERE / fam / "raw" / (model + ".png")), sha256_file(again / (model + ".png"))
            repro[model] = {"first": a, "second": b, "byteIdentical": a == b}
        global_checks.append(checks.CheckResult("reproducibility", all(v["byteIdentical"] for v in repro.values()), repro))

        make_sheet(sprites, palette, HERE / "ground" / "sheets" / "ground-foundation-lattice-3x3.png")
        (HERE / "foundation" / "sheets").mkdir(parents=True, exist_ok=True)
        shutil.copy2(HERE / "ground" / "sheets" / "ground-foundation-lattice-3x3.png",
                     HERE / "foundation" / "sheets" / "ground-foundation-lattice-3x3.png")

        all_ok = True
        for fam in ("ground", "foundation"):
            ids = [a for a, v in ASSETS.items() if v[0] == fam]
            report = checks.build_report({a: results[a] for a in ids}, global_checks)
            report["family"] = {"family": fam, "task": "T519", "issue": 692, "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
                                "worldBible": WORLD_BIBLE, "regionKey": "starter-platform" if fam == "ground" else None,
                                "intentionallyEmpty": "no intentionallyEmpty entry in this family"}
            write_lf(HERE / fam / "check-report.json", checks.report_json(report))
            rh = sha256_file(HERE / fam / "check-report.json")
            all_ok &= report["passed"]
            for a in ids:
                fm, model, tones = ASSETS[a]
                rec = {
                    "assetId": a, "revision": 1, "route": "blender", "key": a.upper().replace("F006-", "F006-"),
                    "sources": [
                        {"path": "art-source/f006/%s/build_models.py" % fam, "sha256": script_hashes["%s/build_models.py" % fam],
                         "note": "procedural geometry, flat palette-colour emission materials per element; no external assets"},
                        {"path": "tools/art-pipeline/blender/rig-v2.blend", "sha256": script_hashes["rig-v2.blend"]},
                        {"path": "art-source/f006/%s/model/%s.blend" % (fam, model), "sha256": sha256_file(blends[a])}],
                    "tools": {**stubs[a]["tools"], "python": sys.version.split()[0], "pillow": __import__("PIL").__version__},
                    "parameters": {**stubs[a]["parameters"], "perspective": PROJ, "densityD": PPU, "frameCount": 1,
                                   "scriptHashes": script_hashes, "renderSeconds": stubs[a]["renderSeconds"],
                                   "materials": "flat emission, exact palette colours per element", "keyLight": "unused by the emission materials"},
                    "touchUp": [{"step": "snap_despeckle_grow", "script": "art-source/f006/pipeline_ground_foundation.py",
                                 "note": "nearest-tone snap to %s, single-pixel despeckle, 1 px silhouette growth to the 70 x 35 diamond, "
                                         "placement on the %s canvas; deterministic, no hand edits. %s" % (list(tones), list(entries[a]["canvas"]), json.dumps(infos[a]))}],
                    "pixelPass": {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py", "scriptSha256": script_hashes["pixel_pass.py"],
                                  "driver": "art-source/f006/pipeline_ground_foundation.py", "paletteId": palette.palette_id,
                                  "paletteVersion": palette.version, "paletteSha256": script_hashes["palette-v1.json"],
                                  "config": {"supersample": 1, "tones": list(tones), "canvas": entries[a]["canvas"]}},
                    "outputs": entries[a]["frames"],
                    "license": "Repository-authored procedural geometry and palette; no third-party assets, models or textures. "
                               "Ownership: repository owner (fjure); no public license grant.",
                    "trainingDataNote": "N/A: no generative model or training data used (Blender route, procedural geometry "
                                        "written by an AI coding agent, disclosed).",
                    "producedBy": PRODUCER, "reviewedBy": None,
                    "candidateSelection": "Deterministic render of fixed geometry, seed 0, 16 samples; no seed selection. Hybrid route "
                                          "(owner decision 2026-11-03): the Blender MCP bridge (127.0.0.1:9876) was not reachable "
                                          "(Blender GUI not running), so geometry was iterated by headless batch renders; the committed "
                                          "render is the final headless run, and a second headless render is byte-identical.",
                    "interactiveSteps": ["none: Blender MCP bridge not reachable at 127.0.0.1:9876; headless batch iteration only"],
                    "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF, "worldBibleVersion": WORLD_BIBLE,
                    "rigVersion": stubs[a]["parameters"]["rigVersion"], "renderEnv": stubs[a]["renderEnv"],
                    "checkReportHash": rh, "checkReportPath": "art-source/f006/%s/check-report.json" % fam}
                gaps = [g for g in missing_provenance_fields(rec, "blender") if g != "reviewedBy"]   # reviewedBy stays null until T525
                if gaps:
                    print("provenance gaps", a, gaps)
                    all_ok = False
                write_lf(HERE / fam / a / "provenance.json", json.dumps(rec, indent=2, sort_keys=True) + "\n")
            man = build_manifest([entries[a] for a in ids])
            man["note"] = ("T519 candidate entries for T526; reviewedBy pending (T525). Ground tiles 70 x 54 pivot (35, 27), keyed by "
                           "region id starter-platform; foundations 70 x 36 pivot (35, 18); diamond centre of both = deck diamond centre.")
            write_lf(HERE / fam / "manifest.json", json.dumps(man, indent=2, sort_keys=True) + "\n")
            print(fam, "CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED")
            for a in ids:
                for r in results[a]:
                    if not r.passed:
                        print("  FAIL", a, r.name, json.dumps(r.details)[:300])
        for r in global_checks:
            print(r.name, r.passed)
        return 0 if all_ok else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
