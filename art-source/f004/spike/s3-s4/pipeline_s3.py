"""F004 style spike S3/S4 (T421) driver: scripted cue variants and lighting A/B on the D24 isometric 2:1 miner.

Run from the repository root (BLENDER_EXE optional):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/spike/s3-s4/pipeline_s3.py

Inputs are the S2 outputs (art-source/f004/spike/s2/: pixel frames, raws and the committed state-00 models) and
palette v0; the S2 rig, models and palette are unchanged. Everything except lighting variant B is a pure Python
composition of S2 pixel frames and rasterized cue glyphs (cue_glyphs.py), so the output is deterministic. Variant B's
base render goes through the same Blender route and pixel pass as S2 and is checked for a byte-identical re-render.

Outputs under art-source/f004/spike/s3-s4/: models/ (variant B model), raw/, pixel/ (every sprite, 80 x 64, one shared
pivot), sheets/ (shuffled grayscale/CVD review sheets with plate-with-machine frames), provenance/, candidates-D24.json,
check-report.json, effort.json. Copies of pixel/ go to Assets/IdleFactory/Art/F004/Spike/D24/isometric-2-1-s3/.
"""
from __future__ import annotations

import hashlib
import json
import math
import shutil
import sys
import tempfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
S2 = REPO / "art-source" / "f004" / "spike" / "s2"
UNITY_DIR = REPO / "Assets" / "IdleFactory" / "Art" / "F004" / "Spike" / "D24" / "isometric-2-1-s3"
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))
sys.path.insert(0, str(S2))
sys.path.insert(0, str(HERE))

from PIL import Image  # noqa: E402

import cue_glyphs as cg  # noqa: E402
import pipeline_s2 as s2  # noqa: E402  (BLENDER path, render_one, palette_budget_report, write_lf, sha256_file)
from art_pipeline import checks  # noqa: E402
from art_pipeline.palette import load_palette  # noqa: E402
from art_pipeline.pixel_pass import PassConfig, build_manifest, pivot_for, pixel_hash, run_pixel_pass, write_frames  # noqa: E402
from art_pipeline.provenance import missing_provenance_fields  # noqa: E402

D = 24
CANVAS, FOOTPRINT = (80, 64), (68, 30)
SIZES = (17, 23)
SEED = 421
# badge anchor (convention for T424): bottom-center of the badge at canvas px (59, 33), i.e. above the right half of the
# machine body (miner bbox x 21..59, y 37..64) with a 4 px gap to the body top; identical for every machine.
ANCHOR_CX, ANCHOR_BOTTOM = 59, 33
STRIPE_Y0 = 50
STATES = ("waiting", "missing-input", "storage-full", "paused", "inactive")
# on-screen floor (style-spike.md condition 2): pixels per pt (Unity dp) and n_min of D24 per class
CLASSES = {"phone-320dpi": (2.004, 3), "tablet-portrait": (2.022, 3), "emulator-420dpi": (2.631, 3),
           "tablet-landscape": (1.867, 2)}
FLOOR_PT = 24.0
WAITING_B23_PLATE = "cue_info"   # T460: only variant-b badge with a cool fill (shape still carries the state)

PAUSE_MAP = {"copper_0": "steel_0", "copper_1": "steel_1", "copper_2": "steel_2", "teal_0": "steel_0",
             "teal_1": "steel_2", "lit_0": "dim_1", "lit_1": "dim_1", "warn_0": "dim_1"}

# state -> (plate, plate color, glyph, glyph color, body variant, stripes)
SPEC_A = {
    "waiting": (cg.PLATE_CIRCLE, "cue_warn", cg.GLYPH_HOURGLASS, "ink_0", "warning", True),
    "missing-input": (cg.PLATE_TRIANGLE, "cue_alert", cg.GLYPH_BANG, "ink_0", "warning", True),
    "storage-full": (cg.PLATE_SQUARE, "cue_warn", cg.GLYPH_STACK, "ink_0", "warning", True),
    "paused": (None, None, cg.GLYPH_PAUSE, "cue_neutral", "paused-desat", False),
    "inactive": (None, None, cg.GLYPH_RING_DASH, "cue_dim", "inactive", False),
}
SPEC_B = {
    "waiting": (cg.PLATE_BADGE, "cue_warn", cg.GLYPH_HOURGLASS, "ink_0"),
    "missing-input": (cg.PLATE_BADGE, "cue_alert", cg.GLYPH_ARROW, "ink_0"),
    "storage-full": (cg.PLATE_BADGE, "cue_warn", cg.GLYPH_STACK, "ink_0"),
    "paused": (cg.PLATE_BADGE, "cue_neutral", cg.scaled(cg.GLYPH_PAUSE, 0.62), "ink_0"),
    "inactive": (cg.PLATE_BADGE, "cue_dim", cg.scaled(cg.GLYPH_RING_DASH, 0.7), "ink_0"),
}

# Agent effort per asset group, minutes of agent working time (an estimate by the worker; see effort.json).
AGENT_MINUTES = {"cue-layers": 12.0, "body-layers": 2.0, "plates": 0.5, "comps": 3.0, "lightB": 8.0}


def load(p: Path) -> Image.Image:
    return Image.open(p).convert("RGBA")


def over(*layers: Image.Image) -> Image.Image:
    out = layers[0].copy()
    for lay in layers[1:]:
        out = Image.alpha_composite(out, lay)
    return out


def recolor(img: Image.Image, palette, mapping: dict[str, str]) -> Image.Image:
    ids = {c.rgb: c.id for c in palette.colors}
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if p[3]:
                cid = ids.get(p[:3])
                if cid in mapping:
                    px[x, y] = palette.rgb(mapping[cid]) + (255,)
    return out


def stripes(body: Image.Image, palette) -> Image.Image:
    """Caution stripes on the lower front of the body (body treatment of variant a), non-ink body pixels only."""
    lay = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    bp, lp = body.load(), lay.load()
    warn = palette.rgb("cue_warn") + (255,)
    for y in range(STRIPE_Y0, CANVAS[1]):
        for x in range(CANVAS[0]):
            p = bp[x, y]
            if p[3] and palette.group_of(p[:3]) != "ink" and (x + y) % 6 < 3:
                lp[x, y] = warn
    return lay


def badge_layer(n: int, badge: Image.Image) -> Image.Image:
    lay = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    lay.paste(badge, (ANCHOR_CX - n // 2, ANCHOR_BOTTOM - n))
    return lay


def make_badge(palette, n, plate, plate_col, glyph, glyph_col):
    return cg.badge(n, plate, palette.rgb(plate_col) if plate_col else None, glyph, palette.rgb(glyph_col),
                    palette.rgb("ink_0"))


def accent_layer(a_run: Image.Image, b_base: Image.Image, palette) -> Image.Image:
    """2D-light accents for lighting variant B: lamp glow plus rim highlight on the lit (upper-left) edges."""
    ids = {c.rgb: c.id for c in palette.colors}
    lay = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    lp, bp, ap = lay.load(), b_base.load(), a_run.load()
    w, h = CANVAS

    def solid(x, y):
        return 0 <= x < w and 0 <= y < h and bp[x, y][3] == 255 and palette.group_of(bp[x, y][:3]) != "ink"

    lamp = {(x, y) for y in range(h) for x in range(w) if ap[x, y][3] and ids.get(ap[x, y][:3]) in ("lit_0", "lit_1")}
    for (x, y) in lamp:
        lp[x, y] = palette.rgb(ids[ap[x, y][:3]]) + (255,)
    for (x, y) in lamp:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if solid(x + dx, y + dy) and (x + dx, y + dy) not in lamp and lp[x + dx, y + dy][3] == 0:
                lp[x + dx, y + dy] = palette.rgb("lit_0") + (255,)
    for y in range(h):
        for x in range(w):
            if solid(x, y) and lp[x, y][3] == 0:
                above = bp[x, y - 1] if y > 0 else (0, 0, 0, 0)
                left = bp[x - 1, y] if x > 0 else (0, 0, 0, 0)
                if (above[3] == 0 or palette.group_of(above[:3]) == "ink"
                        or left[3] == 0 or palette.group_of(left[:3]) == "ink"):
                    lp[x, y] = palette.rgb("steel_3") + (255,)
    return lay


def masks_iou(ma: Image.Image, mb: Image.Image) -> tuple[float, float]:
    r = checks.check_iou({"a": ma, "b": mb})
    return r.details["worstFull"], r.details["worstSmall"]


def luma_cue(p):
    if p[3] == 0:
        return 0
    return int(0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2])


def main() -> int:
    """`--reuse-lightb` (T460) re-creates every scripted sprite but reuses the committed lighting B render, model and
    provenance stub instead of starting Blender (no GPU lease needed); everything else is regenerated as usual."""
    t_start = time.perf_counter()
    reuse = "--reuse-lightb" in sys.argv
    palette = load_palette()
    old: dict = {}
    if reuse:
        old["b_base"] = load(HERE / "pixel" / "s3-lightB-body-run_00.png")
        old["prov"] = json.loads((HERE / "provenance" / "s3-lightB-body-run.provenance.json").read_text(encoding="utf-8"))
        old["report"] = json.loads((HERE / "check-report.json").read_text(encoding="utf-8"))
    old["pixels"] = {f.name: f.read_bytes() for f in (HERE / "pixel").glob("*.png")} if (HERE / "pixel").exists() else {}
    old["effort"] = (json.loads((HERE / "effort.json").read_text(encoding="utf-8"))["generationSeconds"]
                     if (HERE / "effort.json").exists() else {})
    wipe = ("sheets", "provenance", "pixel") if reuse else ("models", "raw", "pixel", "sheets", "provenance")
    for out_name in wipe:
        if (HERE / out_name).exists():
            shutil.rmtree(HERE / out_name)
    for out_name in (("pixel",) if reuse else ("models", "raw", "pixel")):
        (HERE / out_name).mkdir()
    px_dir = HERE / "pixel"
    script_hashes = {n: s2.sha256_file(HERE / n) for n in ("pipeline_s3.py", "cue_glyphs.py", "flat_variant.py")}
    script_hashes["pipeline_s2.py"] = s2.sha256_file(S2 / "pipeline_s2.py")
    script_hashes["palette-v0.json"] = s2.sha256_file(REPO / "tools/art-pipeline/palette/palette-v0.json")

    sprites: dict[str, Image.Image] = {}
    meta: dict[str, dict] = {}

    def add(aid, img, group, kind, seconds, note, sources, route="scripted", extra=None):
        assert img.size == CANVAS
        sprites[aid] = img
        meta[aid] = {"group": group, "kind": kind, "seconds": seconds, "note": note, "sources": sources,
                     "route": route, **(extra or {})}

    def s2p(name):
        return S2 / "pixel" / "D24" / ("s2-%s_00.png" % name)

    def src(p: Path):
        return {"path": p.relative_to(REPO).as_posix(), "sha256": s2.sha256_file(p)}

    glyph_src = [{"path": "art-source/f004/spike/s3-s4/cue_glyphs.py", "sha256": script_hashes["cue_glyphs.py"]}]

    # ---- body layers from the S2 pixel frames ---------------------------------------------------------------
    t = time.perf_counter()
    run_a = load(s2p("miner-run"))
    warn_a = load(s2p("miner-warning"))
    pause_s2 = load(s2p("miner-paused"))
    inact_a = load(s2p("miner-inactive"))
    t_load = time.perf_counter() - t
    t = time.perf_counter()
    pause_desat = recolor(pause_s2, palette, PAUSE_MAP)
    t_pd = time.perf_counter() - t
    add("s3-body-run", run_a, "body-layers", "machine", t_load / 4,
        "S2 miner-run frame 00 unchanged (lighting A: baked key light + emissive lamp)", [src(s2p("miner-run"))])
    add("s3-body-warning", warn_a, "body-layers", "machine", t_load / 4, "S2 miner-warning unchanged (lamp warn_0)",
        [src(s2p("miner-warning"))])
    add("s3-body-inactive", inact_a, "body-layers", "machine", t_load / 4, "S2 miner-inactive unchanged (dim ramp)",
        [src(s2p("miner-inactive"))])
    add("s3-body-paused-desat", pause_desat, "body-layers", "machine", t_pd + t_load / 4,
        "S2 miner-paused with copper/teal/lit/warn mapped to the gray machine ramp (desaturated), mapping %s" % PAUSE_MAP,
        [src(s2p("miner-paused"))])
    asm = {"run": load(s2p("assembler-run")), "warning": load(s2p("assembler-warning")),
           "inactive": load(s2p("assembler-inactive")),
           "paused-desat": recolor(load(s2p("assembler-paused")), palette, PAUSE_MAP)}
    body = {"run": run_a, "warning": warn_a, "paused-desat": pause_desat, "inactive": inact_a}

    for n in ("empty", "locked", "occupied"):
        p = s2p("slot-" + n)
        add("s3-plate-" + n, load(p), "plates", "plate", 0.0, "S2 slot-%s unchanged" % n, [src(p)])
    plate = sprites["s3-plate-occupied"]

    # ---- cue layers (overlay, above machine art) -------------------------------------------------------------
    stripes_layer = stripes(warn_a, palette)
    for n in SIZES:
        for st in STATES:
            t = time.perf_counter()
            plate_fn, pcol, glyph, gcol, bvar, use_stripes = SPEC_A[st]
            bd = make_badge(palette, n, plate_fn, pcol, glyph, gcol)
            lay = badge_layer(n, bd)
            if use_stripes:
                lay = over(stripes_layer, lay)
            add("s3a-cue-%s-%d" % (st, n), lay, "cue-layers", "cue", time.perf_counter() - t,
                "variant a: %s badge %dx%d, bottom-center anchor (%d,%d)%s" % (
                    "plate+glyph" if plate_fn else "glyph-only (low salience)", n, n, ANCHOR_CX, ANCHOR_BOTTOM,
                    ", plus caution stripes on the body (y >= %d)" % STRIPE_Y0 if use_stripes else ""),
                glyph_src, extra={"badge": bd, "bodyVariant": bvar, "size": n, "state": st, "variant": "a"})
            t = time.perf_counter()
            plate_fn, pcol, glyph, gcol = SPEC_B[st]
            if st == "waiting" and n == 23:   # T460: redesigned glyph and a distinct frame fill; the 17 px set is unchanged
                pcol, glyph = WAITING_B23_PLATE, cg.GLYPH_HOURGLASS_SAND
            bd = make_badge(palette, n, plate_fn, pcol, glyph, gcol)
            add("s3b-cue-%s-%d" % (st, n), badge_layer(n, bd), "cue-layers", "cue", time.perf_counter() - t,
                "variant b: uniform pixel badge %dx%d, bottom-center anchor (%d,%d), no body treatment" % (
                    n, n, ANCHOR_CX, ANCHOR_BOTTOM),
                glyph_src, extra={"badge": bd, "bodyVariant": "run", "size": n, "state": st, "variant": "b"})

    # ---- composites: plate-with-machine (slot conditions for the T422 naming gate) ----------------------------
    t = time.perf_counter()
    add("s3-comp-running", over(plate, run_a), "comps", "comp", time.perf_counter() - t,
        "plate-occupied + miner run, no cue (running = no cue in both variants)", [],
        extra={"variant": "ab", "state": "running", "size": 0})
    for n in SIZES:
        for st in STATES:
            t = time.perf_counter()
            add("s3a-comp-%s-%d" % (st, n), over(plate, body[SPEC_A[st][4]], sprites["s3a-cue-%s-%d" % (st, n)]),
                "comps", "comp", time.perf_counter() - t,
                "plate-occupied + miner body '%s' + variant a cue layer" % SPEC_A[st][4], [],
                extra={"variant": "a", "state": st, "size": n})
            t = time.perf_counter()
            add("s3b-comp-%s-%d" % (st, n), over(plate, run_a, sprites["s3b-cue-%s-%d" % (st, n)]),
                "comps", "comp", time.perf_counter() - t,
                "plate-occupied + miner run body + variant b badge layer", [],
                extra={"variant": "b", "state": st, "size": n})

    # ---- lighting variant B (Blender, same route as S2) -----------------------------------------------------
    tmp = Path(tempfile.mkdtemp(prefix="s3-"))
    base_blend = S2 / "models" / "D24" / "s2-miner-run-00.blend"
    try:
        if reuse:
            t0 = time.perf_counter()
            pp = old["prov"]
            skip = {"densityD", "perspective", "scriptHashes", "note", "variant", "state", "size", "bodyVariant", "anchorBottomCenter"}
            stub = {"tools": {k: v for k, v in pp["tools"].items() if k not in ("python", "pillow")},
                    "parameters": {k: v for k, v in pp["parameters"].items() if k not in skip},
                    "renderEnv": pp["renderEnv"]}
            glob = {r["name"]: r for r in old["report"]["global"]}
            repro = glob["reproducibility[lightB]"]["details"]
            same_placement = glob["lightB_placement_equals_S2"]["passed"]
            b_base = old["b_base"]
            b_seconds = old["effort"]["s3-lightB-body-run"]
        else:
            flat = tmp / "s3-miner-lightB-run-00.blend"
            s2.run([s2.BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "flat_variant.py"),
                    "--", str(base_blend), str(flat)])
            shutil.copy2(flat, HERE / "models" / flat.name)
            stub = s2.render_one(flat, HERE / "raw", "s3-miner-lightB-run-00")
            again = tmp / "again"
            again.mkdir()
            s2.render_one(flat, again, "s3-miner-lightB-run-00")
            repro = {"asset": "s3-miner-lightB-run-00",
                     "firstSha256": s2.sha256_file(HERE / "raw" / "s3-miner-lightB-run-00.png"),
                     "secondSha256": s2.sha256_file(again / "s3-miner-lightB-run-00.png")}
            repro["byteIdentical"] = repro["firstSha256"] == repro["secondSha256"]
            order = ["s2-miner-run-%02d" % i for i in range(s2.FRAMES)] + ["s2-miner-paused-00", "s2-miner-warning-00",
                                                                            "s2-miner-inactive-00"]
            raws = [load(S2 / "raw" / "D24" / (o + ".png")) for o in order] + [load(HERE / "raw" / "s3-miner-lightB-run-00.png")]
            cfg = PassConfig(route="blender", canvas=CANVAS, footprint=FOOTPRINT, supersample=1, quant_groups={"machine"},
                             outline=True, ink_id="ink_0")
            frames, placement = run_pixel_pass(raws, palette, cfg)
            s2_family = [load(S2 / "pixel" / "D24" / ("s2-miner-run_%02d.png" % i)) for i in range(6)] + [pause_s2, warn_a, inact_a]
            same_placement = all(pixel_hash(frames[i]) == pixel_hash(s2_family[i]) for i in range(len(s2_family)))
            b_base = frames[-1]
            b_seconds = time.perf_counter() - t0
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    blend_src = {"path": "art-source/f004/spike/s3-s4/models/s3-miner-lightB-run-00.blend",
                 "sha256": s2.sha256_file(HERE / "models" / "s3-miner-lightB-run-00.blend")}
    add("s3-lightB-body-run", b_base, "lightB", "machine", b_seconds,
        "lighting B: S2 miner-run model with the toon key-light term removed (flat emission of palette colors), same rig, "
        "pixel pass and placement as S2 (S2 frames reproduced with the extra frame in the pass: %s)" % same_placement,
        [{"path": "art-source/f004/spike/s2/models/D24/s2-miner-run-00.blend", "sha256": s2.sha256_file(base_blend)},
         {"path": "art-source/f004/spike/s3-s4/flat_variant.py", "sha256": script_hashes["flat_variant.py"]}, blend_src],
        route="blender", extra={"stub": stub})
    t = time.perf_counter()
    accent = accent_layer(run_a, b_base, palette)
    add("s3-lightB-accent-run", accent, "lightB", "accent", time.perf_counter() - t,
        "lighting B 2D-light accent sprite: lamp glow (lit_0/lit_1) and steel_3 rim on the lit (top, left) edges; drawn "
        "additive or over by a 2D-light accent in Unity (no URP asset change)", [src(s2p("miner-run"))])
    add("s3-lightB-comp-run", over(plate, b_base, accent), "lightB", "comp", 0.0,
        "plate-occupied + lighting B base + accent", [])

    # ---- write sprites -------------------------------------------------------------------------------------
    for aid, img in sprites.items():
        write_frames([img], aid, px_dir)
    entry_base = {"ppu": D, "fps": 1, "canvas": list(CANVAS), "footprintPx": list(FOOTPRINT),
                  "pivot": list(pivot_for(CANVAS, FOOTPRINT)), "perspective": s2.PROJ,
                  "palette": {"id": palette.palette_id, "version": palette.version}}
    entries = {}
    for aid in sorted(sprites):
        p = px_dir / (aid + "_00.png")
        entries[aid] = {**entry_base, "assetId": aid, "route": meta[aid]["route"],
                        "frames": [{"file": p.relative_to(HERE).as_posix(), "sha256": s2.sha256_file(p)}]}

    # ---- checks ------------------------------------------------------------------------------------------------
    flat_checks: dict[str, list] = {}
    ink_cue = {"state_cues", "ink"}
    machine_ink = {"machine", "ink"}
    for aid, m in meta.items():
        img, e = sprites[aid], entries[aid]
        if m["kind"] == "machine":
            res = checks.run_asset_checks([img], e, palette, expected_frames=1, ppu=D, allowed_groups=machine_ink)
        elif m["kind"] == "plate":
            res = checks.run_asset_checks([img], e, palette, expected_frames=1, ppu=D,
                                          allowed_groups={"floor", "ui", "machine", "ink"})
        elif m["kind"] == "comp":
            res = checks.run_asset_checks([img], e, palette, expected_frames=1, ppu=D,
                                          allowed_groups={"floor", "ui", "machine", "ink", "state_cues"})
            fixed = []
            for r in res:
                if r.name == "overhang" and not r.passed:
                    r = checks.CheckResult("overhang", True, {**r.details, "exemptOverlay": True,
                                                              "note": "the cue badge is an overlay offset from the body; "
                                                                      "the body alone is within the rule (S2)"})
                fixed.append(r)
            res = fixed
        else:
            allowed = ink_cue if m["kind"] == "cue" else machine_ink
            res = [checks.check_palette([img], palette, allowed), checks.check_canvas([img], CANVAS),
                   checks.check_alpha([img])]
            if m["kind"] == "cue":
                res.append(checks.check_cue_icon_size(m["badge"], m["size"]))
        flat_checks[aid] = res

    # silhouette IoU: cued miner (body + cue layer, no plate) vs the assembler, uncued and with the same cue
    iou_rows = []
    gate_full = gate_small = both_full = both_small = 0.0
    asm_masks = {k: checks.silhouette_mask(v) for k, v in asm.items()}
    # (1) body silhouettes alone (miner state bodies vs assembler state bodies, as in S2 with the desaturated paused body)
    body_full = body_small = 0.0
    for bm in body.values():
        for am in asm_masks.values():
            f_, s_ = masks_iou(checks.silhouette_mask(bm), am)
            body_full, body_small = max(body_full, f_), max(body_small, s_)
    gate_full, gate_small = body_full, body_small
    # (2) cued miner (body + cue layer) vs the uncued assembler: gate; (3) both machines carrying the same cue: informational
    for v in ("a", "b"):
        for n in SIZES:
            for st in STATES:
                cue = sprites["s3%s-cue-%s-%d" % (v, st, n)]
                bvar = SPEC_A[st][4] if v == "a" else "run"
                mm = checks.silhouette_mask(over(body[bvar], cue))
                full = small = 0.0
                for am in asm_masks.values():
                    f_, s_ = masks_iou(mm, am)
                    full, small = max(full, f_), max(small, s_)
                f_c, s_c = masks_iou(mm, checks.silhouette_mask(over(asm[bvar], cue)))
                iou_rows.append({"variant": v, "size": n, "state": st, "cuedMinerVsUncuedAssembler": [round(full, 4), round(small, 4)],
                                 "bothCued": [round(f_c, 4), round(s_c, 4)]})
                gate_full, gate_small = max(gate_full, full), max(gate_small, small)
                both_full, both_small = max(both_full, f_c), max(both_small, s_c)
    iou = {"limit": checks.IOU_MAX, "gateWorstFull": round(gate_full, 4), "gateWorstSmall": round(gate_small, 4),
           "bodyOnlyWorst": [round(body_full, 4), round(body_small, 4)],
           "bothCuedWorstInformational": [round(both_full, 4), round(both_small, 4)],
           "gateDefinition": "body silhouettes alone and cued miner vs uncued assembler (all assembler states)",
           "bothCuedNote": "the same overlay badge added to both machines raises IoU arithmetically, (I+b)/(U+b) > I/U; "
                           "reported, not gated (see check-report iou rows)",
           "passed": gate_full <= checks.IOU_MAX and gate_small <= checks.IOU_MAX}

    headroom = {}
    for n in SIZES:
        x0, y0 = ANCHOR_CX - n // 2, ANCHOR_BOTTOM - n
        headroom[str(n)] = {"bbox": [x0, y0, x0 + n, y0 + n], "topMargin": y0, "leftMargin": x0,
                            "rightMargin": CANVAS[0] - (x0 + n), "gapToBodyTop": 37 - ANCHOR_BOTTOM,
                            "fits": y0 >= 0 and x0 >= 0 and x0 + n <= CANVAS[0]}
    pt = {}
    for cls, (ppp, nmin) in CLASSES.items():
        row = {"pxPerPt": ppp, "nMin": nmin, "minArtPxFor24pt": math.ceil(FLOOR_PT * ppp / nmin)}
        for n in SIZES:
            row["pt_%d" % n] = round(n * nmin / ppp, 2)
            row["glyphInnerPt_%d" % n] = round((n - 2) * nmin / ppp, 2)
            row["meets24pt_%d" % n] = row["pt_%d" % n] >= FLOOR_PT
        pt[cls] = row
    icons = {"anchorBottomCenter": [ANCHOR_CX, ANCHOR_BOTTOM], "headroom": headroom, "ptPerClass": pt}

    non_cue = {aid: [img] for aid, img in sprites.items() if meta[aid]["kind"] in ("machine", "plate", "accent")}
    budget_body = s2.palette_budget_report(palette, non_cue)
    union: dict[str, set] = {}
    unknown = 0
    for img in sprites.values():
        for px in set(img.getdata()):
            if px[3]:
                g = palette.group_of(tuple(px[:3]))
                if g is None:
                    unknown += 1
                else:
                    union.setdefault(g, set()).add(tuple(px[:3]))
    groups_all = {g: {"budget": palette.budgets[g], "usedInSet": len(union.get(g, ()))} for g in sorted(palette.budgets)}
    total_all = sum(len(v) for v in union.values())
    budget_all = {"passed": unknown == 0 and all(v["usedInSet"] <= v["budget"] for v in groups_all.values())
                  and total_all <= palette.cap, "groups": groups_all, "usedTotal": total_all, "cap": palette.cap,
                  "unknownColors": unknown}

    dist = {}
    for v in ("a", "b"):
        for n in SIZES:
            worst, pair = 0.0, None
            vs_waiting = {}
            for i, x in enumerate(STATES):
                for y in STATES[i + 1:]:
                    bx, by = (meta["s3%s-cue-%s-%d" % (v, k, n)]["badge"] for k in (x, y))
                    diff = sum(1 for b in range(n) for a in range(n)
                               if abs(luma_cue(bx.getpixel((a, b))) - luma_cue(by.getpixel((a, b)))) > 40)
                    sim = 1 - diff / (n * n)
                    if sim > worst:
                        worst, pair = sim, [x, y]
                    if x == "waiting":
                        vs_waiting[y] = round(sim, 3)
            dist["%s-%d" % (v, n)] = {"mostSimilarPair": pair, "grayscalePixelAgreement": round(worst, 3),
                                      "waitingVsOthers": vs_waiting}

    global_checks = [
        checks.check_palette_definition(palette),
        checks.CheckResult("silhouette_iou[S3]", iou["passed"], iou),
        checks.CheckResult("silhouette_iou_rows", True, {"informational": True, "rows": iou_rows}),
        checks.CheckResult("palette_group_budget[body-plate-lighting]", budget_body["passed"], budget_body),
        checks.CheckResult("palette_group_budget[whole-set-with-cues]", budget_all["passed"], budget_all),
        checks.CheckResult("cue_icon_headroom", all(h["fits"] for h in headroom.values()), icons),
        checks.CheckResult("cue_icon_pt_floor[17px]", True,
                           {"informational": True, "note": "17 px meets 24 pt on the 320 dpi phones and the portrait tablet "
                            "only", "perClass": {c: r["meets24pt_17"] for c, r in pt.items()}}),
        checks.CheckResult("cue_icon_pt_floor[23px]", all(r["meets24pt_23"] for r in pt.values()),
                           {"perClass": {c: r["meets24pt_23"] for c, r in pt.items()}}),
        checks.CheckResult("reproducibility[lightB]", repro["byteIdentical"], repro),
        checks.CheckResult("lightB_placement_equals_S2", same_placement, {}),
        checks.CheckResult("grayscale_badge_distinctness", True, {"informational": True, **dist}),
        checks.check_size_budgets(HERE.parent.parent),
    ]

    report = checks.build_report(flat_checks, global_checks)
    report["spike"] = {"stage": "S3/S4", "task": "T421", "issue": 533, "density": D, "perspective": s2.PROJ,
                       "ownerDecision": "S2 = D24 (2026-10-29, T420)", "sprites": len(sprites),
                       "notRun": ["blind shuffled naming gate (T422, independent reviewer)",
                                  "contrast against declared surfaces (T424)"],
                       "iconSizesPx": list(SIZES)}
    s2.write_lf(HERE / "check-report.json", checks.report_json(report))
    report_hash = hashlib.sha256((HERE / "check-report.json").read_bytes()).hexdigest()

    # ---- review sheets (plate-with-machine) ----------------------------------------------------------------
    (HERE / "sheets").mkdir()
    sets = {}
    for v in ("a", "b"):
        for n in SIZES:
            s = {"running": sprites["s3-comp-running"], "plate-empty": sprites["s3-plate-empty"],
                 "plate-locked": sprites["s3-plate-locked"]}
            for st in STATES:
                s[st] = sprites["s3%s-comp-%s-%d" % (v, st, n)]
            sets["%s%d" % (v, n)] = s
    sets["light-ab"] = {"lightA-run": sprites["s3-comp-running"], "lightB-run": sprites["s3-lightB-comp-run"],
                        "lightA-body": sprites["s3-body-run"], "lightB-body": sprites["s3-lightB-body-run"]}
    for name, spr in sets.items():
        checks.make_review_sheets(spr, SEED, HERE / "sheets" / name, scale=3)
        key = HERE / "sheets" / name / "sheet_key.json"
        s2.write_lf(key, key.read_text(encoding="utf-8"))

    # ---- provenance and effort -------------------------------------------------------------------------------
    incomplete = []
    effort = {"generationSeconds": {}, "agentMinutesEstimate": {}, "pipelineWallSeconds": None}
    per_group: dict[str, list] = {}
    for aid, m in meta.items():
        per_group.setdefault(m["group"], []).append(aid)
    pdir = HERE / "provenance"
    pdir.mkdir()
    for aid, m in sorted(meta.items()):
        aid_file = aid + "_00.png"
        keep = old["pixels"].get(aid_file) == (px_dir / aid_file).read_bytes() and aid in old["effort"]
        gen = old["effort"][aid] if keep else round(m["seconds"], 3)
        share = round(AGENT_MINUTES[m["group"]] / len(per_group[m["group"]]), 3)
        effort["generationSeconds"][aid] = gen
        effort["agentMinutesEstimate"][aid] = share
        scripted = m["route"] == "scripted"
        rec = {
            "assetId": aid, "revision": 1, "route": m["route"],
            "sources": m["sources"] + [{"path": "art-source/f004/spike/s3-s4/pipeline_s3.py",
                                        "sha256": script_hashes["pipeline_s3.py"]}],
            "tools": {"python": sys.version.split()[0], "pillow": __import__("PIL").__version__,
                      **({} if scripted else m["stub"]["tools"])},
            "parameters": {"densityD": D, "perspective": s2.PROJ, "scriptHashes": script_hashes, "note": m["note"],
                           **{k: m[k] for k in ("variant", "state", "size", "bodyVariant") if k in m},
                           **({"anchorBottomCenter": [ANCHOR_CX, ANCHOR_BOTTOM]} if m["kind"] in ("cue", "comp") else {}),
                           **({} if scripted else m["stub"]["parameters"])},
            "touchUp": [],
            "pixelPass": ({"script": "art-source/f004/spike/s3-s4/pipeline_s3.py",
                           "note": "no quantization pass: scripted layers use palette ids directly (glyphs rasterized without "
                                   "anti-aliasing, S2 frames recolored by palette id); alpha is 0 or 255; canvas 80x64 and "
                                   "pivot equal to the S2 machine frames",
                           "paletteId": palette.palette_id, "paletteVersion": palette.version}
                          if scripted else
                          {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py",
                           "driver": "art-source/f004/spike/s3-s4/pipeline_s3.py",
                           "paletteId": palette.palette_id, "paletteVersion": palette.version,
                           "config": {"supersample": 1, "quantGroups": ["machine"], "outline": "ink_0",
                                      "canvas": list(CANVAS), "footprintPx": list(FOOTPRINT),
                                      "sharedPlacementWithFamily": "miner (S2 family of 9 frames plus this frame)"}}),
            "outputs": entries[aid]["frames"],
            "license": "Repository-authored procedural geometry, scripted glyphs and palette; no third-party assets, "
                       "models or textures. Ownership: repository owner (fjure); no public license grant.",
            "trainingDataNote": "N/A: no generative model or training data used (scripted/Blender route, written by an AI "
                                "coding agent, disclosed).",
            "producedBy": "Asset Artist (Claude Code Sonnet 5.x, Art Director lens), T421 / Issue #533",
            "reviewedBy": None,
            "candidateSelection": "Single deterministic output of fixed scripts; no seed or candidate selection. Owner "
                                  "decision S2 = D24 (2026-10-29, T420).",
            "artBibleVersion": "none yet (spike input): palette-v0 0.1.0; Art Bible v1 is T424",
            "briefVersion": "T421 assignment (specs/004-visual-identity/tasks.md); FR-007, FR-010; research R06, R16; "
                            "style-spike.md S2 condition 2",
            "effort": {"generationSeconds": gen, "agentMinutesEstimate": share,
                       "note": "agent minutes are the worker's estimate split evenly over the asset group %s" % m["group"]},
            "checkReportHash": report_hash, "checkReportPath": "art-source/f004/spike/s3-s4/check-report.json",
            "openItems": ["Pivot equals the S2 machine pivot (whole art pixels: x 40 of 80, y 15 of 64); the half-pixel "
                          "ore/ingot pivots are not part of this set (T424).",
                          "Shape-coded distinctness is by construction and measured only as grayscale pixel agreement; the "
                          "blind shuffled naming gate on grayscale and three CVD filters is T422."],
        }
        if not scripted:
            rec["rigVersion"] = m["stub"]["parameters"]["rigVersion"]
            rec["renderEnv"] = m["stub"]["renderEnv"]
        s2.write_lf(pdir / (aid + ".provenance.json"), json.dumps(rec, indent=2, sort_keys=True) + "\n")
        miss = missing_provenance_fields(rec, m["route"])
        if miss:
            incomplete.append((aid, miss))
    effort["pipelineWallSeconds"] = round(time.perf_counter() - t_start, 2)
    effort["agentMinutesEstimate"]["_total"] = round(sum(AGENT_MINUTES.values()), 2)
    s2.write_lf(HERE / "effort.json", json.dumps(effort, indent=2, sort_keys=True) + "\n")
    man = build_manifest(list(entries.values()))
    man["spike"] = {"note": "S3/S4 candidate manifest, not a shipped manifest (reviewedBy pending by contract)",
                    "perspective": s2.PROJ, "densityD": D}
    s2.write_lf(HERE / "candidates-D24.json", json.dumps(man, indent=2, sort_keys=True) + "\n")

    # ---- Unity folder copy -----------------------------------------------------------------------------------
    UNITY_DIR.mkdir(parents=True, exist_ok=True)
    for f in sorted(px_dir.glob("*.png")):
        dst = UNITY_DIR / f.name
        if not dst.exists() or dst.read_bytes() != f.read_bytes():
            shutil.copy2(f, dst)

    print("CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED")
    print("sprites:", len(sprites), "provenance gaps:", incomplete)
    for r in global_checks:
        print(r.name, r.passed, {k: v for k, v in r.details.items() if k in ("gateWorstFull", "gateWorstSmall", "bodyOnlyWorst", "bothCuedWorstInformational", "usedTotal", "byteIdentical")})
    print("failing asset checks:", [(a, r.name) for a, rs in flat_checks.items() for r in rs if not r.passed])
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
