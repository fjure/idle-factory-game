"""Art pipeline v2 checks for the `render-hd` and `mesh-export` routes (contracts/art-pipeline-v2.md, FR-019, FR-034).

Every check returns the same `CheckResult` as the F004 checks, so reports compose. The F004 checks that still apply
(canvas, frames, flash, silhouette IoU, contrast ratio) are reused; this module adds the checks that only exist in v2:
pass alignment, key-colour absence, tolerance reproducibility, indicator anchor and contrast over the machine's own
pixels, the texture-memory estimate, the prompt lint and the mesh-export checks. Pure Python plus Pillow, no numpy.
"""
from __future__ import annotations

import json
import math
import re
import struct
from pathlib import Path

from PIL import Image

from .checks import CheckResult, check_canvas, check_flash, check_frames, check_iou, silhouette_mask
from .color import contrast_ratio, rgb_to_lab

#: Capture key colour (SC-006 / QA-8). Assumption: the capture driver (T615) owns the final value; until then this is the
#: reserved magenta, and the check takes the key as a parameter so a different value needs no code change.
CAPTURE_KEY_RGB = (255, 0, 255)

#: Reproducibility (TA-6): at least 99 % of covered pixels within CIE76 delta E 2.
REPRO_DELTA_E_MAX = 2.0
REPRO_PIXEL_SHARE_MIN = 0.99
#: SSIM floor, set from the two-render test of the reference asset (see tools/art-pipeline/README-v2.md for the measured values).
REPRO_SSIM_MIN = 0.995

#: Alpha rounding allowed between the emissive mask and the beauty alpha (8-bit PNG round trip).
ALIGNMENT_ALPHA_TOL = 1
CONTRAST_NONTEXT = 3.0

#: Bits per pixel of the formats the importer may select (ASTC block sizes: 128 bits per block).
BITS_PER_PIXEL = {
    "RGBA32": 32.0, "R8": 8.0, "A8": 8.0, "BC7": 8.0, "DXT5": 8.0,
    "ASTC_4x4": 8.0, "ASTC_6x6": 128.0 / 36.0, "ASTC_8x8": 2.0,
}
MIP_FACTOR = 4.0 / 3.0


def _alpha(img: Image.Image) -> Image.Image:
    return img.convert("RGBA").getchannel("A")


# --- pass alignment (TA-4) ----------------------------------------------------------------------------------------

def check_pass_alignment(passes: dict[str, Image.Image], pivots: dict[str, tuple[float, float]] | None = None,
                         alpha_tol: int = ALIGNMENT_ALPHA_TOL, padding: int = 0) -> CheckResult:
    """All passes share one size and one pivot, the union bbox of their content is the shared canvas minus `padding` on every
    side (so no pass was trimmed on its own), and the emissive mask is a subset of the beauty alpha."""
    problems: list[str] = []
    names = sorted(passes)
    sizes = {n: passes[n].size for n in names}
    if len(set(sizes.values())) > 1:
        problems.append("pass sizes differ: " + json.dumps({n: list(s) for n, s in sizes.items()}))
    if pivots is not None and len({tuple(round(v, 4) for v in p) for p in pivots.values()}) > 1:
        problems.append("passes carry different pivots")
    union = None
    for n in names:
        box = _alpha(passes[n]).getbbox()
        if box:
            union = box if union is None else (min(union[0], box[0]), min(union[1], box[1]), max(union[2], box[2]), max(union[3], box[3]))
    size = passes[names[0]].size if names else (0, 0)
    if union is None:
        problems.append("no pass has any content")
    elif union != (padding, padding, size[0] - padding, size[1] - padding):
        problems.append("union bbox %s is not the pass canvas %s minus padding %d (passes were trimmed separately or not at all)" % (list(union), list(size), padding))
    leaked = 0
    if "beauty" in passes and "emissiveMask" in passes and passes["beauty"].size == passes["emissiveMask"].size:
        b = _alpha(passes["beauty"]).load()
        e = _alpha(passes["emissiveMask"]).load()
        w, h = passes["beauty"].size
        for y in range(h):
            for x in range(w):
                if e[x, y] > b[x, y] + alpha_tol:
                    leaked += 1
        if leaked:
            problems.append("%d emissive pixels lie outside the beauty alpha" % leaked)
    return CheckResult("pass_alignment", not problems, {"problems": problems, "emissiveOutsideBeauty": leaked, "alphaTolerance": alpha_tol})


# --- render not clipped (AD review N5) ----------------------------------------------------------------------------

def check_not_clipped(passes: dict[str, Image.Image], margin: int = 1) -> CheckResult:
    """No raw pass may have content within `margin` px of the render canvas edge: such a pass was cut off by the camera
    frame, and a trim cannot restore it. Run on the raw renders, before the union trim."""
    touching = []
    for name in sorted(passes):
        box = _alpha(passes[name]).getbbox()
        if box is None:
            continue
        w, h = passes[name].size
        if box[0] < margin or box[1] < margin or box[2] > w - margin or box[3] > h - margin:
            touching.append({"pass": name, "bbox": list(box), "canvas": [w, h]})
    return CheckResult("render_not_clipped", not touching, {"margin": margin, "clipped": touching})


# --- key colour absence (SC-006)----------------------------------------------------------------------------------

def check_key_colour_absent(images: list[Image.Image], key: tuple[int, int, int] = CAPTURE_KEY_RGB) -> CheckResult:
    """The capture key colour must not appear in any pixel with non-zero alpha, in any image."""
    hits = []
    for i, img in enumerate(images):
        px = img.convert("RGBA").load()
        n = 0
        for y in range(img.height):
            for x in range(img.width):
                p = px[x, y]
                if p[3] and p[:3] == tuple(key):
                    n += 1
        if n:
            hits.append({"image": i, "pixels": n})
    return CheckResult("key_colour_absent", not hits, {"key": list(key), "hits": hits})


# --- reproducibility (TA-6) ---------------------------------------------------------------------------------------

def _over_grey(img: Image.Image) -> Image.Image:
    """Composite over mid grey so alpha differences register as colour differences."""
    base = Image.new("RGBA", img.size, (128, 128, 128, 255))
    return Image.alpha_composite(base, img.convert("RGBA")).convert("RGB")


def delta_e_share(a: Image.Image, b: Image.Image, max_de: float = REPRO_DELTA_E_MAX) -> dict:
    """CIE76 delta E over the pixels covered (alpha > 0) in either image, composited over grey."""
    if a.size != b.size:
        return {"share": 0.0, "pixels": 0, "maxDeltaE": float("inf"), "sizeMismatch": True}
    aa, ab = _alpha(a).load(), _alpha(b).load()
    ca, cb = _over_grey(a).load(), _over_grey(b).load()
    n = ok = 0
    worst = 0.0
    for y in range(a.height):
        for x in range(a.width):
            if not (aa[x, y] or ab[x, y]):
                continue
            la, lb = rgb_to_lab(ca[x, y]), rgb_to_lab(cb[x, y])
            de = math.sqrt((la[0] - lb[0]) ** 2 + (la[1] - lb[1]) ** 2 + (la[2] - lb[2]) ** 2)
            n += 1
            ok += de <= max_de
            worst = max(worst, de)
    return {"share": (ok / n) if n else 1.0, "pixels": n, "maxDeltaE": round(worst, 4), "sizeMismatch": False}


def ssim(a: Image.Image, b: Image.Image, window: int = 8, step: int = 4) -> float:
    """Mean structural similarity (Wang et al. 2004; C1 = (0.01 L)^2, C2 = (0.03 L)^2, L = 255) of the grey-composited luma,
    uniform window and stride so it stays in pure Python. Identical images give 1.0. Windows with no content are skipped."""
    if a.size != b.size:
        return 0.0
    pa, pb = _over_grey(a).convert("L").load(), _over_grey(b).convert("L").load()
    ca, cb = _alpha(a).load(), _alpha(b).load()
    c1, c2 = (0.01 * 255) ** 2, (0.03 * 255) ** 2
    w, h = a.size
    window = min(window, w, h)
    total, count = 0.0, 0
    for y0 in range(0, h - window + 1, step):
        for x0 in range(0, w - window + 1, step):
            xs = [(x, y) for y in range(y0, y0 + window) for x in range(x0, x0 + window)]
            if not any(ca[x, y] or cb[x, y] for x, y in xs):
                continue
            va = [pa[x, y] for x, y in xs]
            vb = [pb[x, y] for x, y in xs]
            n = len(xs)
            ma, mb = sum(va) / n, sum(vb) / n
            sa = sum((v - ma) ** 2 for v in va) / (n - 1)
            sb = sum((v - mb) ** 2 for v in vb) / (n - 1)
            sab = sum((p - ma) * (q - mb) for p, q in zip(va, vb)) / (n - 1)
            total += ((2 * ma * mb + c1) * (2 * sab + c2)) / ((ma * ma + mb * mb + c1) * (sa + sb + c2))
            count += 1
    return total / count if count else 1.0


def check_reproducibility(first: Image.Image, second: Image.Image, max_de: float = REPRO_DELTA_E_MAX,
                          min_share: float = REPRO_PIXEL_SHARE_MIN, min_ssim: float = REPRO_SSIM_MIN) -> CheckResult:
    d = delta_e_share(first, second, max_de)
    s = ssim(first, second)
    ok = (not d["sizeMismatch"]) and d["share"] >= min_share and s >= min_ssim
    return CheckResult("reproducibility", ok, {
        "deltaEMax": max_de, "pixelShareMin": min_share, "ssimMin": min_ssim,
        "pixelShare": round(d["share"], 6), "coveredPixels": d["pixels"], "worstDeltaE": d["maxDeltaE"], "ssim": round(s, 6)})


# --- animation, silhouettes ---------------------------------------------------------------------------------------

def composite_additive(beauty: Image.Image, emissive: Image.Image, gain: float = 1.0) -> Image.Image:
    """Body plus emissive composite for the flash check: the emissive pass adds its colour weighted by its alpha."""
    b, e = beauty.convert("RGBA"), emissive.convert("RGBA")
    out = Image.new("RGBA", b.size)
    pb, pe, po = b.load(), e.load(), out.load()
    for y in range(b.height):
        for x in range(b.width):
            r, g, bl, a = pb[x, y]
            er, eg, eb, ea = pe[x, y]
            k = gain * ea / 255.0
            po[x, y] = (min(255, round(r + er * k)), min(255, round(g + eg * k)), min(255, round(bl + eb * k)), a)
    return out


def check_flash_composite(body_frames: list[Image.Image], emissive_frames: list[Image.Image], fps: int, gain: float = 1.0) -> CheckResult:
    if len(body_frames) != len(emissive_frames):
        return CheckResult("flash", False, {"problem": "body and emissive frame counts differ"})
    return check_flash([composite_additive(b, e, gain) for b, e in zip(body_frames, emissive_frames)], fps)


def check_silhouette_iou_at_scale(sprites: dict[str, Image.Image], scale: float, max_iou: float = 0.70) -> CheckResult:
    """Silhouette IoU after scaling every sprite by the phone fit-all scale (device px per art px)."""
    masks = {}
    for name, img in sprites.items():
        m = silhouette_mask(img)
        size = (max(1, round(m.width * scale)), max(1, round(m.height * scale)))
        masks[name] = m.resize(size, Image.Resampling.BOX).point(lambda v: 255 if v >= 128 else 0)
    r = check_iou(masks, max_iou)
    return CheckResult("silhouette_iou_at_scale", r.passed, {**r.details, "scale": scale})


# --- indicator anchor and contrast --------------------------------------------------------------------------------

def point_in_polygon(pt: tuple[float, float], poly: list[tuple[float, float]]) -> bool:
    x, y = pt
    inside = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            inside = not inside
    return inside


def check_indicator_anchor(beauty: Image.Image, anchor: tuple[float, float], hit_polygon: list[tuple[float, float]]) -> CheckResult:
    """The anchor (art px, top-left origin) lies on an opaque body pixel and inside the location's hit area."""
    x, y = anchor
    in_canvas = 0 <= x < beauty.width and 0 <= y < beauty.height
    body = in_canvas and _alpha(beauty).getpixel((int(x), int(y))) == 255
    hit = point_in_polygon(anchor, [tuple(p) for p in hit_polygon])
    return CheckResult("indicator_anchor", bool(body and hit), {"anchor": list(anchor), "insideBody": bool(body), "insideHitArea": hit})


def check_indicator_contrast(beauty: Image.Image, anchor: tuple[float, float], radius: float,
                             fill: tuple[int, int, int], outline: tuple[int, int, int] | None = None,
                             need: float = CONTRAST_NONTEXT) -> CheckResult:
    """The indicator needs at least 3:1 (fill or outline) against every opaque machine pixel under its footprint."""
    px = beauty.convert("RGBA").load()
    worst_fill = worst_outline = float("inf")
    n = 0
    for y in range(max(0, int(anchor[1] - radius)), min(beauty.height, int(anchor[1] + radius) + 1)):
        for x in range(max(0, int(anchor[0] - radius)), min(beauty.width, int(anchor[0] + radius) + 1)):
            if (x - anchor[0]) ** 2 + (y - anchor[1]) ** 2 > radius * radius:
                continue
            r, g, b, a = px[x, y]
            if a < 255:
                continue
            n += 1
            worst_fill = min(worst_fill, contrast_ratio(tuple(fill), (r, g, b)))
            if outline is not None:
                worst_outline = min(worst_outline, contrast_ratio(tuple(outline), (r, g, b)))
    if not n:
        return CheckResult("indicator_contrast", False, {"problem": "no opaque machine pixels under the indicator footprint"})
    best = max(worst_fill, worst_outline if outline is not None else 0.0)
    return CheckResult("indicator_contrast", best >= need, {
        "need": need, "worstFill": round(worst_fill, 3),
        "worstOutline": None if outline is None else round(worst_outline, 3), "pixels": n})


# --- texture memory (TA-5) ----------------------------------------------------------------------------------------

def texture_bytes(width: int, height: int, fmt: str, mips: bool) -> int:
    if fmt not in BITS_PER_PIXEL:
        raise ValueError("unknown texture format " + fmt)
    return int(math.ceil(width * height * BITS_PER_PIXEL[fmt] / 8.0 * (MIP_FACTOR if mips else 1.0)))


def estimate_family_memory(rows: list[dict]) -> dict:
    """rows: {name, width, height, format, mips, frames, material}. `setPassMaterials` lists the distinct blend materials
    (body, multiply shadow, additive emissive) the family needs; they enter the SetPass estimate."""
    total = 0
    per = []
    materials = set()
    for r in rows:
        b = texture_bytes(r["width"], r["height"], r["format"], bool(r.get("mips", False))) * int(r.get("frames", 1))
        total += b
        per.append({"name": r["name"], "bytes": b})
        materials.add(r.get("material", "body"))
    return {"bytes": total, "rows": per, "setPassMaterials": sorted(materials)}


def check_texture_memory(rows: list[dict], budget_bytes: int, max_setpass_materials: int = 3) -> CheckResult:
    est = estimate_family_memory(rows)
    ok = est["bytes"] <= budget_bytes and len(est["setPassMaterials"]) <= max_setpass_materials
    return CheckResult("texture_memory", ok, {**est, "budgetBytes": budget_bytes, "maxSetPassMaterials": max_setpass_materials})


# --- canvas, pivot, frames ----------------------------------------------------------------------------------------

def check_v2_geometry(frames: list[Image.Image], entry: dict) -> list[CheckResult]:
    """canvas, frame count and the single pivot. The pivot of B and C is the projected world origin, so a fractional art
    pixel is legal (the integral rule is direction A only); it must lie inside the canvas."""
    cw, ch = entry["canvas"]
    px, py = entry["pivot"][0] * cw, entry["pivot"][1] * ch
    return [check_canvas(frames, (cw, ch)), check_frames(frames, entry["frames"]),
            CheckResult("pivot", 0 <= px <= cw and 0 <= py <= ch, {"pivotPx": [round(px, 4), round(py, 4)], "canvas": [cw, ch]})]


# --- prompt lint (AD-10) ------------------------------------------------------------------------------------------

DENYLIST_PATH = Path(__file__).resolve().parent.parent / "prompt-denylist.json"
FORBIDDEN_NODE_TYPES = ("LoadImage", "VAEEncode", "ControlNet", "IPAdapter", "Img2Img", "InpaintModelConditioning")


def load_denylist(path: Path | str = DENYLIST_PATH) -> list[str]:
    return [t.lower() for t in json.loads(Path(path).read_text(encoding="utf-8"))["terms"]]


def check_prompt_lint(record: dict, denylist: list[str] | None = None) -> CheckResult:
    """record: {prompt, seed, workflow?}. Rejects game or artist names, an unlogged prompt or seed, and a workflow graph
    that feeds a reference image (img2img, ControlNet, IP-Adapter)."""
    terms = load_denylist() if denylist is None else [t.lower() for t in denylist]
    problems = []
    prompt = record.get("prompt")
    if not isinstance(prompt, str) or not prompt.strip():
        problems.append("prompt is not logged")
    else:
        low = prompt.lower()
        for t in terms:
            if re.search(r"(?<![a-z0-9])" + re.escape(t) + r"(?![a-z0-9])", low):
                problems.append("forbidden name in prompt: " + t)
    seed = record.get("seed")
    if not isinstance(seed, int) or isinstance(seed, bool):
        problems.append("seed is not logged")
    graph = record.get("workflow")
    if isinstance(graph, dict):
        for node_id, node in sorted(graph.items()):
            ctype = node.get("class_type", "") if isinstance(node, dict) else ""
            if any(f.lower() in ctype.lower() for f in FORBIDDEN_NODE_TYPES):
                problems.append("reference-image node %s (%s)" % (node_id, ctype))
    return CheckResult("prompt_lint", not problems, {"problems": problems})


# --- mesh export (TA-8, Direction C) ------------------------------------------------------------------------------

MESH_LIMITS = {"maxTriangles": 4000, "maxMaterials": 3, "maxVariants": 4, "unitsPerWorldUnit": 1.0, "unitTolerance": 1e-3, "pivotTolerance": 0.025}


def read_gltf_summary(path: Path | str) -> dict:
    """Minimal .gltf / .glb reader: triangle count, material count, bounding box from accessor min/max, node scales."""
    p = Path(path)
    data = p.read_bytes()
    if p.suffix.lower() == ".glb":
        magic, _ver, _len = struct.unpack_from("<4sII", data, 0)
        if magic != b"glTF":
            raise ValueError("not a GLB file")
        clen, ctype = struct.unpack_from("<II", data, 12)
        if ctype != 0x4E4F534A:
            raise ValueError("GLB first chunk is not JSON")
        doc = json.loads(data[20:20 + clen].decode("utf-8"))
    else:
        doc = json.loads(data.decode("utf-8"))
    tris = 0
    lo = [math.inf] * 3
    hi = [-math.inf] * 3
    accessors = doc.get("accessors", [])
    for mesh in doc.get("meshes", []):
        for prim in mesh.get("primitives", []):
            idx = prim.get("indices")
            pos = accessors[prim["attributes"]["POSITION"]]
            count = accessors[idx]["count"] if idx is not None else pos["count"]
            if prim.get("mode", 4) == 4:
                tris += count // 3
            for i in range(3):
                lo[i] = min(lo[i], pos["min"][i])
                hi[i] = max(hi[i], pos["max"][i])
    return {"triangles": tris, "materials": len(doc.get("materials", [])), "min": lo, "max": hi,
            "nodeScales": [n.get("scale", [1, 1, 1]) for n in doc.get("nodes", [])]}


def check_mesh_export(summary: dict, sidecar: dict, limits: dict | None = None) -> list[CheckResult]:
    """summary: `read_gltf_summary`; sidecar: {unitsPerWorldUnit, variants, shadowCaster} written by the export step.
    glTF is Y-up: the pivot is the footprint centre on the ground (x and z at 0, ground at min y 0)."""
    lim = {**MESH_LIMITS, **(limits or {})}
    unit = sidecar.get("unitsPerWorldUnit")
    scales_ok = all(all(abs(v - 1.0) < lim["unitTolerance"] for v in s) for s in summary["nodeScales"])
    out = [CheckResult("mesh_unit_scale", unit is not None and abs(unit - lim["unitsPerWorldUnit"]) <= lim["unitTolerance"] and scales_ok,
                       {"unitsPerWorldUnit": unit, "nodeScalesAreUnit": scales_ok})]
    lo, hi = summary["min"], summary["max"]
    cx, cz = (lo[0] + hi[0]) / 2.0, (lo[2] + hi[2]) / 2.0
    tol = lim["pivotTolerance"]
    out.append(CheckResult("mesh_pivot", abs(cx) <= tol and abs(cz) <= tol and abs(lo[1]) <= tol,
                           {"footprintCentre": [round(cx, 5), round(cz, 5)], "groundY": round(lo[1], 5)}))
    out.append(CheckResult("mesh_triangles", summary["triangles"] <= lim["maxTriangles"], {"triangles": summary["triangles"], "max": lim["maxTriangles"]}))
    variants = sidecar.get("variants", 1)
    out.append(CheckResult("mesh_materials_variants", summary["materials"] <= lim["maxMaterials"] and variants <= lim["maxVariants"],
                           {"materials": summary["materials"], "maxMaterials": lim["maxMaterials"], "variants": variants, "maxVariants": lim["maxVariants"]}))
    caster = sidecar.get("shadowCaster")
    out.append(CheckResult("mesh_shadow_caster", isinstance(caster, bool), {"shadowCaster": caster}))
    return out
