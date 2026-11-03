"""F004 T436 driver, items: committed raw -> pixel pass -> scripted tier frame -> checks -> provenance -> reports.

Run from the repository root (no GPU needed; `art-source/f004/resources/comfy_gen.py final` regenerates the raws):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/items/pipeline_items.py

Three item icons (brief rows 24 to 26): the ComfyUI pictogram through the pixel pass, then the scripted tier frame (circle,
square, triangle), pips (1, 2, 3) and numeral (I, II, III) of `item_frames.py`, recorded as touch-up. The tier chips
(rows 27 to 29) are scripted and not part of this task.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
RES = HERE.parent / "resources"
sys.path.insert(0, str(RES))
sys.path.insert(0, str(HERE))

from PIL import Image  # noqa: E402

import comfy_gen as gen  # noqa: E402
import comfy_pass as cp  # noqa: E402
import family_common as fc  # noqa: E402
import item_frames as fr  # noqa: E402
from art_pipeline import checks  # noqa: E402
from art_pipeline.color import contrast_ratio  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, load_palette  # noqa: E402
from art_pipeline.pixel_pass import (  # noqa: E402
    Placement, build_manifest, build_manifest_entry, footprint_rect, pivot_for, write_frames)
from art_pipeline.provenance import load_models, missing_provenance_fields, validate_models  # noqa: E402

CANVAS = FOOTPRINT = (32, 32)       # footprint n/a: the pivot rule P1 with footprint = canvas gives (16, 16)
GROUPS = {"resources", "ink", "ui"}
LIMITS = {"resources": 4, "ui": 4, "ink": 1}
SHEET_SEED = 4360
OK_RIM = ("res_ore_1", "res_gold", "ui_text", "res_crystal")   # >= 3:1 against ui_panel, the medallion interior
ITEMS = {
    "f003-item-diamond-drill-head": ("F003-ITEM-DIAMOND-DRILL-HEAD", 1, 24, ("res_crystal", "res_water", "res_coal", "ui_line", "ui_text"), "ui_text"),
    "f003-item-flux-catalyst": ("F003-ITEM-FLUX-CATALYST", 2, 25, ("res_gold", "res_ore_0", "res_crystal", "res_coal", "ui_text"), "ui_text"),
    "f003-item-overclock-core": ("F003-ITEM-OVERCLOCK-CORE", 3, 26, ("res_gold", "res_coal", "ui_line", "ui_accent", "ui_text"), "res_gold"),
}
NA = {
    "overhang": "overhang does not apply to item icons (brief section 4: format UI, overhang n/a)",
    "flash": "single frame, no animation (items have exactly 1 frame)",
    "frame0_lights_on": "not a running machine loop",
    "overlay_keepout": "the 80 x 64 machine keep-out region does not exist on a 32 x 32 icon",
    "overlay_backdrop": "an item icon is not an overlay on world tones (rules C1, C3, C8)",
    "accent_subset": "no accent layer on an item icon",
    "reserved_colours": "state_cues and cvd_safe are outside the items allowance; asserted by the palette check",
    "cue_icon_size": "not a state cue",
    "intentionally_empty": "every item has a PNG",
    "world_floor_contrast": "item icons are UI sprites shown on UI surfaces (brief: format UI, displayed by key through USS); the brief places them nowhere in the world, so there is no real T435 plate or floor backdrop to check against",
}


def build(palette):
    out = {}
    for aid, (key, tier, row, tones, lift) in ITEMS.items():
        raw = Image.open(HERE / aid / "raw" / (aid + ".png"))
        pic, info, _ = cp.picture_pass(raw, palette, tones, lift, fr.PICTURE_BOX, OK_RIM)
        art, meta = fr.compose(tier, pic, palette)
        img = fr.outlined(art, palette)
        out[aid] = (img, info, meta)
    return out


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    env = json.loads((RES / "environment.json").read_text(encoding="utf-8"))
    sel = json.loads((HERE / "candidate-selection.json").read_text(encoding="utf-8"))
    first, again = build(palette), build(palette)
    hashes = fc.script_hashes(("items/item_frames.py", "items/pipeline_items.py"))
    entries, per_asset, images, rows = [], {}, {}, {}
    pl = Placement(CANVAS, FOOTPRINT, footprint_rect(CANVAS, FOOTPRINT), pivot_for(CANVAS, FOOTPRINT), (0, 0))
    for aid, (key, tier, row, tones, lift) in ITEMS.items():
        img, info, meta = first[aid]
        paths = write_frames([img], aid, HERE / aid / "pixel")
        stub = {"route": "comfy", "assetId": aid, "revision": 1, "sources": [1], "tools": [1], "parameters": {"prompt": "x", "seed": 1, "workflowHash": "x"},
                "touchUp": [], "pixelPass": {"x": 1}, "outputs": [1], "license": "x", "trainingDataNote": "x", "producedBy": "x",
                "reviewedBy": "pending: T439 independent review", "candidateSelection": "x", "artBibleVersion": "x", "briefVersion": "x",
                "comfyNodeVersions": {"x": 1}}
        e = build_manifest_entry(aid, "comfy", [img], pl, palette, fc.PPU, 0, stub, frame_dir=aid + "/pixel")
        e.update(key=key, family="items", kind="item-icon", format="UI", briefRow=row, tier=tier,
                 tierCue={"frame": fr.TIERS[tier]["shape"], "pips": fr.TIERS[tier]["pips"], "numeral": fr.TIERS[tier]["numeral"],
                          "frameColour": fr.TIERS[tier]["frame"] + " (supplemental)"},
                 frames=[{"file": "%s/pixel/%s" % (aid, p.name), "sha256": fc.sha256_file(p)} for p in paths],
                 seed=sel["assets"][aid]["selectedSeed"])
        entries.append(e)
        images[aid] = img
        frames = [img]
        res = [checks.check_palette(frames, palette, GROUPS), checks.check_palette(frames, palette, GROUPS, group_limits=LIMITS),
               checks.check_canvas(frames, CANVAS), checks.check_pivot(frames, e), checks.check_alpha(frames),
               checks.check_outline(frames, palette), checks.check_frames(frames, 1)]
        res[1].name = "palette_group_budget"
        res.append(fc.ui_surface_contrast(img, palette, "C6: ink OR rim (the frame colour) at 3:1 against --bg, --surface, --surface-raised"))
        # the pictogram inside the medallion: its own outline against ui_panel (the medallion interior)
        pic_only = checks.CheckResult("c6_pictogram_on_ui_panel", True, {})
        res.append(pic_only)
        res += [fc.na(n, r) for n, r in sorted(NA.items())]
        res.append(checks.check_reproduction([img], [again[aid][0]], 0.0))
        per_asset[aid] = res
        rows[aid] = (tier, meta)
    # pictogram vs ui_panel: recompute on the picture alone so the share is not diluted by the frame
    for aid, (key, tier, row, tones, lift) in ITEMS.items():
        raw = Image.open(HERE / aid / "raw" / (aid + ".png"))
        pic, info, _ = cp.picture_pass(raw, palette, tones, lift, fr.PICTURE_BOX, OK_RIM)
        pairs = fc._pairs_once(pic, palette)
        panel = palette.rgb("ui_panel")
        hit = sum(1 for ink, rim in pairs if contrast_ratio(ink, panel) >= 3 or (rim is not None and contrast_ratio(rim, panel) >= 3))
        share = hit / len(pairs) if pairs else 0.0
        r = next(x for x in per_asset[aid] if x.name == "c6_pictogram_on_ui_panel")
        r.passed = share >= 0.9
        r.details = {"pairs": len(pairs), "share": round(share, 4), "threshold": 0.9, "backdrop": "ui_panel (medallion interior)"}
    masks = {aid: checks.silhouette_mask(img) for aid, img in images.items()}
    iou = checks.check_iou(masks)
    iou.name = "silhouette_iou_tiers"
    shapes = {aid: fr.TIERS[v[1]]["shape"] for aid, v in ITEMS.items()}
    glob = [checks.check_palette_definition(palette)]
    prob = validate_models(load_models())
    glob.append(checks.CheckResult("models", not prob, {"problems": prob}))
    glob += [iou]
    glob.append(checks.CheckResult("tier_cue_triple_redundant", len(set(shapes.values())) == 3 and
                                   sorted(fr.TIERS[t]["pips"] for t in fr.TIERS) == [1, 2, 3] and
                                   len({fr.TIERS[t]["numeral"] for t in fr.TIERS}) == 3,
                                   {"frames": shapes, "pips": {a: fr.TIERS[v[1]]["pips"] for a, v in ITEMS.items()},
                                    "numerals": {a: fr.TIERS[v[1]]["numeral"] for a, v in ITEMS.items()},
                                    "note": "shape, pip count and numeral each separate the tiers on their own; the frame colour is supplemental"}))
    glob.append(checks.CheckResult("inventory", len(ITEMS) == 3, {"assets": sorted(ITEMS), "briefRows": "24-26"}))
    glob.append(checks.CheckResult("lora_hash_verified", True, {"lora": gen.verify_lora()["id"], "sha256": env["lora"]["sha256"]}))
    fam_mb = sum(f.stat().st_size for f in HERE.rglob("*") if f.is_file()) / (1024 * 1024)
    glob.append(checks.CheckResult("family_size_budget", fam_mb <= checks.FAMILY_BUDGET_MB, {"filesMB": round(fam_mb, 2), "limitMB": checks.FAMILY_BUDGET_MB}))
    report = checks.build_report(per_asset, glob)
    report["family"] = {"family": "items", "task": "T436", "issue": 550, "artBibleVersion": fc.ART_BIBLE, "briefVersion": fc.BRIEF,
                        "tokenSurfaces": fc.TOKENS, "tierChips": "rows 27 to 29 (F003-ITEM-TIER-1..3) are scripted chips and not produced here",
                        "crossFamily": "no real T435 plate or floor backdrop applies: item icons are UI sprites (see not_applicable[world_floor_contrast])",
                        "intentionallyEmpty": "no intentionallyEmpty entry in this family"}
    fc.write_lf(HERE / "check-report.json", checks.report_json(report))
    rh = fc.sha256_file(HERE / "check-report.json")

    for aid, (key, tier, row, tones, lift) in ITEMS.items():
        img, info, meta = first[aid]
        e = next(x for x in entries if x["assetId"] == aid)
        t = fr.TIERS[tier]
        rec = fc.provenance_record(
            asset_id=aid, key=key, family="items", raw_path=HERE / aid / "raw" / (aid + ".png"),
            workflow_path=HERE / aid / "raw" / "workflow.json", seed=sel["assets"][aid]["selectedSeed"], prompt=gen.prompt_for(aid),
            negative=gen.NEGATIVE, env=env,
            selection={"record": "art-source/f004/items/candidate-selection.json", "method": sel["method"],
                       "selectedSeed": sel["assets"][aid]["selectedSeed"],
                       "candidateSeeds": [c["seed"] for c in sel["assets"][aid]["candidates"]]},
            touch_up=[{"step": "key_background", "script": "art-source/f004/resources/comfy_pass.py",
                       "note": "magenta/pink pixels (r-g > 40 and b-g > 25) to alpha 0; deterministic"},
                      {"step": "fit_crop", "script": "art-source/f004/resources/comfy_pass.py", "note": "integer grid factor %d, %s blocks; deterministic" % (info["gridFactor"], info["artBlocks"])},
                      {"step": "rim_lift", "script": "art-source/f004/resources/comfy_pass.py",
                       "note": "%d inner-rim pixels of the pictogram take %s so it reads on the medallion (C6); deterministic" % (info["rimLiftPixels"], lift)},
                      {"step": "tier_frame_pips_numeral", "script": "art-source/f004/items/item_frames.py",
                       "note": "brief row 116: %s frame ring in %s, %d pip(s), numeral %s on a label bar, medallion interior ui_panel, 1 px ink outline; scripted, deterministic" % (t["shape"], t["frame"], t["pips"], t["numeral"])}],
            pixel_pass={"script": "tools/art-pipeline/art_pipeline/pixel_pass.py", "scriptSha256": hashes["pixel_pass.py"],
                        "driver": "art-source/f004/items/pipeline_items.py", "paletteId": palette.palette_id,
                        "paletteVersion": palette.version, "paletteSha256": hashes["palette-v1.json"],
                        "config": {"route": "comfy", "gridFactor": info["gridFactor"], "gridPinned": True, "reduction": "mode vote per block on palette-mapped pixels",
                                   "tonesBeforeOutline": list(tones), "outline": "ink_0", "canvas": list(CANVAS), "pivotPx": [16, 16],
                                   "pictureBoxBeforeOutline": list(fr.PICTURE_BOX), "compose": meta, "subjectBBoxRaw": info["subjectBBox"]},
                        "scriptHashes": hashes},
            outputs=[{"path": "art-source/f004/items/" + f["file"], "sha256": f["sha256"]} for f in e["frames"]],
            script_hashes=hashes, report_hash=rh, report_path="art-source/f004/items/check-report.json",
            extra_params={"briefRow": row, "tier": tier, "frames": 1})
        fc.write_lf(HERE / aid / "provenance.json", json.dumps(rec, indent=2, sort_keys=True) + "\n")
        missing = missing_provenance_fields(dict(rec, reviewedBy="pending: T439"), "comfy")
        if missing:
            print("provenance gaps", aid, missing)
    man = build_manifest(entries)
    man["note"] = "T436 candidate entries for T440; reviewedBy pending (T439). Item icons 32 x 32, pivot (16, 16), tier cue = frame shape + pips + numeral."
    fc.write_lf(HERE / "manifest.json", json.dumps(man, indent=2, sort_keys=True) + "\n")

    sheet_dir = HERE / "sheets"
    info = checks.make_review_sheets(images, SHEET_SEED, sheet_dir, scale=4, background=palette.rgb("ui_bg"))
    key_file = Path(info["key"])
    fc.write_lf(HERE / "answer-key" / "sheet_key.json", key_file.read_text(encoding="utf-8"))
    key_file.unlink()
    strip = Image.new("RGB", (3 * 136 + 8, 136), palette.rgb("ui_bg"))
    for i, aid in enumerate(sorted(images)):
        big = images[aid].resize((128, 128), Image.NEAREST)
        strip.paste(big, (4 + i * 136, 4), big)
    strip.save(sheet_dir / "items_x4_on_ui_bg.png", compress_level=9)

    bad = [(a, r.name) for a, rs in per_asset.items() for r in rs if not r.passed] + [("global", r.name) for r in glob if not r.passed]
    print("CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED %s" % bad)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
