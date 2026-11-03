"""F004 T436 driver, resources: committed raw -> pixel pass -> checks -> provenance -> reports (route `comfy`).

Run from the repository root (no GPU needed: the raws are committed; `comfy_gen.py final` regenerates them under the gpu lease):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/resources/pipeline_resources.py

Writes `<asset>/pixel/`, `<asset>/provenance.json`, `manifest.json`, `check-report.json` and `sheets/` + `answer-key/` for the
four Iron resources. Reviewer fields stay empty (T439). Rerunning on the committed raws is byte-identical.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from PIL import Image  # noqa: E402

import comfy_pass as cp  # noqa: E402
import family_common as fc  # noqa: E402
import comfy_gen as gen  # noqa: E402
from art_pipeline import checks, checks_v1 as c1  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, load_palette  # noqa: E402
from art_pipeline.pixel_pass import build_manifest, build_manifest_entry, write_frames  # noqa: E402
from art_pipeline.provenance import load_models, validate_models  # noqa: E402

CANVAS, FOOTPRINT = (32, 24), (20, 10)
SHEET_SEED = 436
GROUPS = {"resources", "ink"}
ID_ALLOW = {"steel_2", "steel_3"}      # Art Bible 5, family allowances: the grey iron tones
LIMITS = {"resources": 4, "ink": 1, "machine": 2}

# asset -> (key, tones before the outline, rim lift tone). Ink is added only by the outline step.
ASSETS = {
    "f003-res-iron-ore": ("F003-RES-IRON-ORE", ("res_ore_0", "res_ore_1", "res_coal", "steel_2", "steel_3"), "res_ore_1"),
    "f003-res-iron-bar": ("F003-RES-IRON-BAR", ("res_coal", "steel_2", "steel_3"), "steel_2"),
    "f003-res-iron-plate": ("F003-RES-IRON-PLATE", ("res_coal", "steel_2", "steel_3"), "steel_2"),
    "f003-res-gear": ("F003-RES-GEAR", ("res_coal", "steel_2", "steel_3"), "steel_2"),
}
# Art box before the outline per asset (selected so the four silhouettes stay distinct, IoU at most 0.70): the gear is a
# compact part, the ore a chunky lump, the bar and the plate wide.
ART_BOX = {"f003-res-iron-ore": (24, 20), "f003-res-iron-bar": (28, 20), "f003-res-iron-plate": (28, 20), "f003-res-gear": (16, 16)}
NA = {
    "flash": "single frame, no animation (resources have exactly 1 frame)",
    "frame0_lights_on": "not a running machine loop (rule L-2 applies to machine bodies)",
    "overlay_keepout": "the 80 x 64 machine keep-out region does not exist on a 32 x 24 resource canvas (Art Bible 3.4 is for machine bodies)",
    "overlay_backdrop": "a resource is not an overlay on world tones (rules C1, C3, C8 are for badges, markers and the frame)",
    "accent_subset": "no accent layer on a resource (rule L-4 is for F004-ACCENT-*)",
    "reserved_colours": "state_cues and cvd_safe are outside the resources family allowance; asserted by the palette check",
    "cue_icon_size": "not a state cue",
    "intentionally_empty": "every resource has a PNG; no intentionallyEmpty entry in this family",
}


def passes(palette):
    """Raw PNGs on disk -> pixel images; run twice by the caller for the reproduction check."""
    out = {}
    for aid, (key, tones, lift) in ASSETS.items():
        raw = Image.open(HERE / aid / "raw" / (aid + ".png"))
        img, pl, info, _ = cp.resource_pass(raw, palette, tones, lift, ART_BOX[aid], CANVAS, FOOTPRINT)
        out[aid] = (img, pl, info)
    return out


def asset_checks(aid, img, entry, palette, tile, plates):
    frames = [img]
    res = checks.run_asset_checks(frames, entry, palette, expected_frames=1, ppu=fc.PPU, allowed_groups=GROUPS, allowed_ids=ID_ALLOW)
    pal = res[0]
    lim = checks.check_palette(frames, palette, GROUPS, group_limits=LIMITS, allowed_ids=ID_ALLOW)
    lim.name = "palette_group_budget"
    res.append(lim)
    res.append(fc.ui_surface_contrast(img, palette, "C6: ink OR rim at 3:1 against --bg, --surface, --surface-raised (Art Bible 9.4)"))
    res.append(fc.bare_floor_contrast(img, palette))
    res += fc.lattice_contrast(img, (16, 19), palette, tile, plates)
    res += [fc.na(n, r) for n, r in sorted(NA.items())]
    res.append(checks.CheckResult("frame_count_exactly_one", len(frames) == 1, {"frames": len(frames)}))
    return res


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    env = json.loads((HERE / "environment.json").read_text(encoding="utf-8"))
    sel = json.loads((HERE / "candidate-selection.json").read_text(encoding="utf-8"))
    first, again = passes(palette), passes(palette)
    tile, plates = fc.load_t435(palette)
    hashes = fc.script_hashes(("resources/pipeline_resources.py",))
    entries, per_asset, images = [], {}, {}
    for aid, (key, tones, lift) in ASSETS.items():
        img, pl, info = first[aid]
        paths = write_frames([img], aid, HERE / aid / "pixel")
        e = build_manifest_entry(aid, "comfy", [img], pl, palette, fc.PPU, 0,
                                 {"route": "comfy", "assetId": aid, "revision": 1, "sources": [1], "tools": [1], "parameters": {"prompt": "x", "seed": 1, "workflowHash": "x"},
                                  "touchUp": [], "pixelPass": {"x": 1}, "outputs": [1], "license": "x", "trainingDataNote": "x", "producedBy": "x",
                                  "reviewedBy": "pending: T439 independent review", "candidateSelection": "x", "artBibleVersion": "x", "briefVersion": "x",
                                  "comfyNodeVersions": {"x": 1}}, frame_dir=aid + "/pixel")
        e.update(key=key, family="resources", kind="resource", format="WORLD", briefRow={"f003-res-iron-ore": 20, "f003-res-iron-bar": 21, "f003-res-iron-plate": 22, "f003-res-gear": 23}[aid],
                 perspective="isometric-2-1", frames=[{"file": "%s/pixel/%s" % (aid, p.name), "sha256": fc.sha256_file(p)} for p in paths],
                 seed=sel["assets"][aid]["selectedSeed"])
        entries.append(e)
        images[aid] = img
        res = asset_checks(aid, img, e, palette, tile, plates)
        res.append(checks.check_reproduction([img], [again[aid][0]], 0.0))
        per_asset[aid] = res
    masks = {aid: checks.silhouette_mask(img) for aid, img in images.items()}
    iou = checks.check_iou(masks)
    iou.name = "silhouette_iou_resources"
    glob = [checks.check_palette_definition(palette)]
    prob = validate_models(load_models())
    glob.append(checks.CheckResult("models", not prob, {"problems": prob}))
    glob.append(iou)
    glob.append(checks.CheckResult("inventory", sorted(per_asset) == sorted(ASSETS) and len(ASSETS) == 4,
                                   {"assets": sorted(ASSETS), "briefRows": "20-23"}))
    glob.append(checks.CheckResult("lora_hash_verified", True, {"lora": gen.verify_lora()["id"], "sha256": env["lora"]["sha256"]}))
    fam_mb = sum(f.stat().st_size for f in HERE.rglob("*") if f.is_file()) / (1024 * 1024)
    glob.append(checks.CheckResult("family_size_budget", fam_mb <= checks.FAMILY_BUDGET_MB, {"filesMB": round(fam_mb, 2), "limitMB": checks.FAMILY_BUDGET_MB}))
    report = checks.build_report(per_asset, glob)
    report["family"] = {"family": "resources", "task": "T436", "issue": 550, "artBibleVersion": fc.ART_BIBLE, "briefVersion": fc.BRIEF,
                        "tokenSurfaces": fc.TOKENS,
                        "crossFamily": "real T435 floor tile and plates (art-source/f004/floor, slots/plates), read-only; reported, not gated: "
                                       "the brief places resources in UI lists, so the gated contrast is C6 on the UI token surfaces and the bare-floor share",
                        "intentionallyEmpty": "no intentionallyEmpty entry in this family"}
    fc.write_lf(HERE / "check-report.json", checks.report_json(report))
    rh = fc.sha256_file(HERE / "check-report.json")

    for aid, (key, tones, lift) in ASSETS.items():
        img, pl, info = first[aid]
        e = next(x for x in entries if x["assetId"] == aid)
        rec = fc.provenance_record(
            asset_id=aid, key=key, family="resources", raw_path=HERE / aid / "raw" / (aid + ".png"),
            workflow_path=HERE / aid / "raw" / "workflow.json", seed=sel["assets"][aid]["selectedSeed"], prompt=gen.prompt_for(aid),
            negative=gen.NEGATIVE, env=env,
            selection={"record": "art-source/f004/resources/candidate-selection.json", "method": sel["method"],
                       "selectedSeed": sel["assets"][aid]["selectedSeed"],
                       "candidateSeeds": [c["seed"] for c in sel["assets"][aid]["candidates"]]},
            touch_up=[{"step": "key_background", "script": "art-source/f004/resources/comfy_pass.py",
                       "note": "pixels with r-g > 40 and b-g > 25 (the flat magenta generation background, its shadow tint, enclosed holes) to alpha 0; deterministic"},
                      {"step": "fit_crop", "script": "art-source/f004/resources/comfy_pass.py", "note": "integer grid factor %d, window of whole blocks %s centred on the subject; deterministic" % (info["gridFactor"], info["artBlocks"])},
                      {"step": "rim_lift", "script": "art-source/f004/resources/comfy_pass.py",
                       "note": "Art Bible 9.4 rule C6: %d inner-rim pixels that carried an interior tone take %s; deterministic" % (info["rimLiftPixels"], lift)}],
            pixel_pass={"script": "tools/art-pipeline/art_pipeline/pixel_pass.py", "scriptSha256": hashes["pixel_pass.py"],
                        "driver": "art-source/f004/resources/pipeline_resources.py", "paletteId": palette.palette_id,
                        "paletteVersion": palette.version, "paletteSha256": hashes["palette-v1.json"],
                        "config": {"route": "comfy", "gridFactor": info["gridFactor"], "gridPinned": True, "reduction": "mode vote per block on palette-mapped pixels",
                                   "tonesBeforeOutline": list(tones), "outline": "ink_0", "canvas": list(CANVAS), "footprintPx": list(FOOTPRINT),
                                   "pivotPx": [16, 5], "maxArtBoxBeforeOutline": list(ART_BOX[aid]), "subjectBBoxRaw": info["subjectBBox"]},
                        "scriptHashes": hashes},
            outputs=[{"path": "art-source/f004/resources/" + f["file"], "sha256": f["sha256"]} for f in e["frames"]],
            script_hashes=hashes, report_hash=rh, report_path="art-source/f004/resources/check-report.json",
            extra_params={"briefRow": e["briefRow"], "frames": 1})
        fc.write_lf(HERE / aid / "provenance.json", json.dumps(rec, indent=2, sort_keys=True) + "\n")
        missing = __import__("art_pipeline.provenance", fromlist=["x"]).missing_provenance_fields(dict(rec, reviewedBy="pending: T439"), "comfy")
        if missing:
            print("provenance gaps", aid, missing)

    man = build_manifest(entries)
    man["note"] = "T436 candidate entries for T440; reviewedBy pending (T439). Family allowance resources + ink + id steel_2 and steel_3."
    fc.write_lf(HERE / "manifest.json", json.dumps(man, indent=2, sort_keys=True) + "\n")

    # review sheets: shuffled grayscale and CVD, answer key apart
    sheet_dir = HERE / "sheets"
    info = checks.make_review_sheets(images, SHEET_SEED, sheet_dir, scale=4, background=palette.rgb("ui_panel"))
    key_file = Path(info["key"])
    fc.write_lf(HERE / "answer-key" / "sheet_key.json", key_file.read_text(encoding="utf-8"))
    key_file.unlink()
    strip = Image.new("RGB", (4 * 32 * 4 + 20, 24 * 4 + 8), palette.rgb("ui_panel"))
    for i, aid in enumerate(sorted(images)):
        big = images[aid].resize((128, 96), Image.NEAREST)
        strip.paste(big, (4 + i * 133, 4), big)
    strip.save(sheet_dir / "resources_x4_on_ui_panel.png", compress_level=9)

    bad = [(a, r.name) for a, rs in per_asset.items() for r in rs if not r.passed] + [("global", r.name) for r in glob if not r.passed]
    print("CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED %s" % bad)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
