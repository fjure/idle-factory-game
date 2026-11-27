"""Known-answer tests for the art pipeline v2 checks (T613). Fixtures are drawn in code, so every expected value is exact."""
import json
import struct

import pytest
from PIL import Image, ImageDraw

from art_pipeline import checks_v2 as c2


def rect_img(size, box, rgba=(100, 120, 140, 255)):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).rectangle(box, fill=rgba)
    return img


# --- pass alignment -----------------------------------------------------------------------------------------------

def aligned():
    beauty = rect_img((20, 20), (0, 0, 19, 19))
    shadow = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rectangle((10, 10, 19, 19), fill=(0, 0, 0, 90))
    mask = rect_img((20, 20), (5, 5, 9, 9), (255, 255, 255, 255))
    return {"beauty": beauty, "shadow": shadow, "emissiveMask": mask}


def test_alignment_passes_for_union_trimmed_passes():
    assert c2.check_pass_alignment(aligned()).passed


def test_alignment_fails_when_emissive_leaves_the_beauty_alpha():
    p = aligned()
    p["beauty"] = rect_img((20, 20), (0, 0, 19, 19))
    ImageDraw.Draw(p["beauty"]).rectangle((5, 5, 9, 9), fill=(0, 0, 0, 0))   # hole under the emissive area
    r = c2.check_pass_alignment(p)
    assert not r.passed and r.details["emissiveOutsideBeauty"] == 25


def test_alignment_fails_on_size_mismatch_and_pivot_mismatch_and_separate_trim():
    p = aligned()
    p["shadow"] = p["shadow"].crop((0, 0, 19, 20))
    assert not c2.check_pass_alignment(p).passed
    assert not c2.check_pass_alignment(aligned(), {"beauty": (10.0, 5.0), "shadow": (10.5, 5.0)}).passed
    q = aligned()
    q["beauty"] = rect_img((20, 20), (2, 2, 17, 17))     # every pass trimmed on its own: union is smaller than the canvas
    q["shadow"] = rect_img((20, 20), (3, 3, 16, 16))
    q["emissiveMask"] = rect_img((20, 20), (5, 5, 9, 9))
    assert not c2.check_pass_alignment(q).passed
    assert c2.check_pass_alignment(q, padding=2).passed


def test_alignment_tolerates_one_level_of_alpha_rounding_only():
    p = aligned()
    p["beauty"] = rect_img((20, 20), (0, 0, 19, 19))
    ImageDraw.Draw(p["beauty"]).rectangle((5, 5, 9, 9), fill=(100, 120, 140, 254))
    p["emissiveMask"] = rect_img((20, 20), (5, 5, 9, 9), (255, 255, 255, 255))
    assert c2.check_pass_alignment(p).passed
    ImageDraw.Draw(p["beauty"]).rectangle((5, 5, 9, 9), fill=(100, 120, 140, 252))
    assert not c2.check_pass_alignment(p).passed


# --- key colour --------------------------------------------------------------------------------------------------------

def test_key_colour_absence():
    ok = rect_img((8, 8), (0, 0, 7, 7), (255, 0, 254, 255))
    assert c2.check_key_colour_absent([ok]).passed
    bad = rect_img((8, 8), (2, 2, 3, 3), (255, 0, 255, 255))
    r = c2.check_key_colour_absent([ok, bad])
    assert not r.passed and r.details["hits"] == [{"image": 1, "pixels": 4}]
    hidden = Image.new("RGBA", (4, 4), (255, 0, 255, 0))                # transparent pixels do not count
    assert c2.check_key_colour_absent([hidden]).passed
    assert not c2.check_key_colour_absent([ok], key=(255, 0, 254)).passed   # the key is a parameter


# --- reproducibility ------------------------------------------------------------------------------------------------------

def test_identical_images_reproduce_exactly():
    a = rect_img((32, 32), (4, 4, 27, 27))
    r = c2.check_reproducibility(a, a.copy())
    assert r.passed and r.details["ssim"] == 1.0 and r.details["worstDeltaE"] == 0.0 and r.details["pixelShare"] == 1.0


def test_delta_e_share_is_a_known_fraction():
    a = rect_img((20, 20), (0, 0, 19, 19), (100, 100, 100, 255))
    b = a.copy()
    ImageDraw.Draw(b).rectangle((0, 0, 3, 19), fill=(160, 100, 100, 255))   # 4 of 20 columns clearly different
    d = c2.delta_e_share(a, b)
    assert d["pixels"] == 400 and d["share"] == pytest.approx(0.8)
    assert not c2.check_reproducibility(a, b).passed


def test_one_percent_budget_boundary():
    a = rect_img((100, 100), (0, 0, 99, 99), (100, 100, 100, 255))
    ok = a.copy()
    ImageDraw.Draw(ok).rectangle((0, 0, 99, 0), fill=(200, 100, 100, 255))   # exactly 100 of 10000 px = 1 %
    d = c2.delta_e_share(a, ok)
    assert d["share"] == pytest.approx(0.99)
    bad = a.copy()
    ImageDraw.Draw(bad).rectangle((0, 0, 99, 1), fill=(200, 100, 100, 255))  # 2 %
    assert c2.delta_e_share(a, bad)["share"] == pytest.approx(0.98)


def test_small_tone_difference_stays_within_delta_e_two():
    a = rect_img((16, 16), (0, 0, 15, 15), (100, 100, 100, 255))
    b = rect_img((16, 16), (0, 0, 15, 15), (101, 100, 100, 255))
    r = c2.check_reproducibility(a, b)
    assert r.passed and 0 < r.details["worstDeltaE"] < 2.0


def test_alpha_difference_counts_and_size_mismatch_fails():
    a = rect_img((16, 16), (0, 0, 15, 15))
    b = rect_img((16, 16), (0, 0, 15, 15), (100, 120, 140, 128))
    assert not c2.check_reproducibility(a, b).passed
    assert not c2.check_reproducibility(a, Image.new("RGBA", (8, 8))).passed


def test_ssim_known_answer_drops_for_structural_change():
    a = Image.new("RGBA", (32, 32), (128, 128, 128, 255))
    ImageDraw.Draw(a).rectangle((8, 8, 23, 23), fill=(220, 220, 220, 255))
    shifted = Image.new("RGBA", (32, 32), (128, 128, 128, 255))
    ImageDraw.Draw(shifted).rectangle((12, 8, 27, 23), fill=(220, 220, 220, 255))
    assert c2.ssim(a, a) == 1.0
    assert c2.ssim(a, shifted) < 0.95


# --- flash, silhouettes ------------------------------------------------------------------------------------------------------

def test_flash_composite_counts_emissive_pulses():
    body = [rect_img((8, 8), (0, 0, 7, 7), (40, 40, 40, 255))] * 4
    on = rect_img((8, 8), (0, 0, 7, 7), (255, 255, 255, 255))
    off = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
    steady = c2.check_flash_composite(body, [on] * 4, 4)
    assert steady.passed
    strobe = c2.check_flash_composite(body * 2, [on, off] * 4, 8)    # 8 frames/s alternating: 8 transitions per second
    assert not strobe.passed and strobe.details["maxFlashesPerSecond"] == 8
    assert not c2.check_flash_composite(body, [on], 4).passed


def test_composite_additive_known_pixel():
    c = c2.composite_additive(rect_img((2, 2), (0, 0, 1, 1), (100, 50, 0, 255)), rect_img((2, 2), (0, 0, 1, 1), (100, 100, 100, 128)))
    assert c.getpixel((0, 0)) == (150, 100, 50, 255)


def test_silhouette_iou_at_scale():
    tall = rect_img((40, 40), (10, 0, 29, 39))
    wide = rect_img((40, 40), (0, 10, 39, 29))
    same = tall.copy()
    assert not c2.check_silhouette_iou_at_scale({"a": tall, "b": same}, 0.5).passed
    assert c2.check_silhouette_iou_at_scale({"a": tall, "b": wide}, 0.5).passed     # IoU 0.333 at both scales


# --- indicator ------------------------------------------------------------------------------------------------------------------

def test_indicator_anchor_inside_body_and_hit_area():
    body = rect_img((40, 40), (10, 10, 29, 29))
    hit = [(5, 5), (35, 5), (35, 35), (5, 35)]
    assert c2.check_indicator_anchor(body, (20, 20), hit).passed
    assert not c2.check_indicator_anchor(body, (3, 3), hit).passed            # outside body and hit area
    assert not c2.check_indicator_anchor(body, (7, 7), hit).passed            # in the hit area, off the body
    assert not c2.check_indicator_anchor(body, (20, 20), [(25, 25), (35, 25), (35, 35)]).passed   # on the body, off the hit area
    assert not c2.check_indicator_anchor(body, (50, 50), hit).passed


def test_indicator_contrast_known_ratios():
    dark = rect_img((40, 40), (0, 0, 39, 39), (20, 20, 20, 255))
    assert c2.check_indicator_contrast(dark, (20, 20), 4, (255, 255, 255)).passed
    r = c2.check_indicator_contrast(dark, (20, 20), 4, (40, 40, 40), (0, 0, 0))
    assert not r.passed and r.details["worstFill"] < 3.0
    assert c2.check_indicator_contrast(dark, (20, 20), 4, (40, 40, 40), (255, 255, 255)).passed   # the outline carries it
    gradient = dark.copy()
    ImageDraw.Draw(gradient).rectangle((20, 20, 22, 22), fill=(255, 255, 255, 255))   # one white pixel breaks white fill
    assert not c2.check_indicator_contrast(gradient, (20, 20), 4, (255, 255, 255)).passed
    assert not c2.check_indicator_contrast(Image.new("RGBA", (40, 40)), (20, 20), 4, (255, 255, 255)).passed


# --- texture memory -------------------------------------------------------------------------------------------------------------

def test_texture_bytes_known_answers():
    assert c2.texture_bytes(1024, 1024, "RGBA32", False) == 4 * 1024 * 1024
    assert c2.texture_bytes(1024, 1024, "BC7", False) == 1024 * 1024
    assert c2.texture_bytes(1296, 1296, "ASTC_6x6", False) == 1296 * 1296 * 128 // 36 // 8     # 36 px per 16-byte block
    assert c2.texture_bytes(1024, 1024, "A8", True) == 1398102   # ceil(1 MiB x 4/3)
    with pytest.raises(ValueError):
        c2.texture_bytes(8, 8, "PVRTC", False)


def test_texture_memory_budget_and_setpass_estimate():
    rows = [{"name": "body", "width": 512, "height": 512, "format": "RGBA32", "frames": 2, "material": "body"},
            {"name": "shadow", "width": 256, "height": 256, "format": "A8", "frames": 2, "material": "shadow-multiply"}]
    est = c2.estimate_family_memory(rows)
    assert est["bytes"] == 2 * 1024 * 1024 + 2 * 65536 and est["setPassMaterials"] == ["body", "shadow-multiply"]
    assert c2.check_texture_memory(rows, 2 * 1024 * 1024 + 131072).passed
    assert not c2.check_texture_memory(rows, 2 * 1024 * 1024).passed
    assert not c2.check_texture_memory(rows, 10 ** 9, max_setpass_materials=1).passed


# --- geometry -------------------------------------------------------------------------------------------------------------------------

def test_v2_geometry_allows_a_fractional_pivot_but_not_one_outside_the_canvas():
    frames = [rect_img((20, 10), (0, 0, 19, 9))] * 2
    ok = {"canvas": [20, 10], "pivot": [0.5125, 0.1], "frames": 2}
    assert all(r.passed for r in c2.check_v2_geometry(frames, ok))
    assert not all(r.passed for r in c2.check_v2_geometry(frames, {**ok, "pivot": [1.2, 0.1]}))
    assert not all(r.passed for r in c2.check_v2_geometry(frames, {**ok, "canvas": [21, 10]}))
    assert not all(r.passed for r in c2.check_v2_geometry(frames, {**ok, "frames": 3}))


# --- prompt lint ------------------------------------------------------------------------------------------------------------------------

def test_prompt_lint_known_answers():
    ok = {"prompt": "riveted steel boiler, warm dusk rim light, orthographic", "seed": 4242}
    assert c2.check_prompt_lint(ok).passed
    assert not c2.check_prompt_lint({**ok, "prompt": "a boiler in the style of Greg Rutkowski"}).passed
    assert not c2.check_prompt_lint({**ok, "prompt": "factorio style belts"}).passed
    assert c2.check_prompt_lint({**ok, "prompt": "factorial design notes"}).passed        # whole-word match only
    assert not c2.check_prompt_lint({"seed": 1}).passed                                  # prompt not logged
    assert not c2.check_prompt_lint({"prompt": "boiler"}).passed                         # seed not logged
    assert not c2.check_prompt_lint({**ok, "seed": True}).passed
    img2img = {**ok, "workflow": {"3": {"class_type": "KSampler"}, "7": {"class_type": "LoadImage"}}}
    r = c2.check_prompt_lint(img2img)
    assert not r.passed and "reference-image node 7 (LoadImage)" in r.details["problems"]
    assert not c2.check_prompt_lint({**ok, "workflow": {"1": {"class_type": "ControlNetApply"}}}).passed
    assert c2.check_prompt_lint({**ok, "workflow": {"3": {"class_type": "KSampler"}}}).passed
    assert not c2.check_prompt_lint({**ok, "prompt": "custom term"}, denylist=["custom term"]).passed


def test_default_denylist_loads_and_is_lowercase():
    terms = c2.load_denylist()
    assert "factorio" in terms and all(t == t.lower() for t in terms)


# --- mesh export -------------------------------------------------------------------------------------------------------------------------

def write_gltf(path, tris=12, materials=1, lo=(-0.8, 0.0, -0.6), hi=(0.8, 0.9, 0.6), scale=(1, 1, 1)):
    doc = {"asset": {"version": "2.0"},
           "nodes": [{"mesh": 0, "scale": list(scale)}],
           "meshes": [{"primitives": [{"attributes": {"POSITION": 0}, "indices": 1}]}],
           "accessors": [{"count": 8, "min": list(lo), "max": list(hi)}, {"count": tris * 3}],
           "materials": [{} for _ in range(materials)]}
    path.write_text(json.dumps(doc), encoding="utf-8")
    return path


def test_mesh_export_known_answers(tmp_path):
    side = {"unitsPerWorldUnit": 1.0, "variants": 2, "shadowCaster": True}
    s = c2.read_gltf_summary(write_gltf(tmp_path / "a.gltf"))
    assert s["triangles"] == 12 and s["materials"] == 1
    assert all(r.passed for r in c2.check_mesh_export(s, side))
    names = {r.name: r for r in c2.check_mesh_export(c2.read_gltf_summary(write_gltf(tmp_path / "b.gltf", tris=5000)), side)}
    assert not names["mesh_triangles"].passed
    names = {r.name: r for r in c2.check_mesh_export(c2.read_gltf_summary(write_gltf(tmp_path / "c.gltf", materials=4)), side)}
    assert not names["mesh_materials_variants"].passed
    names = {r.name: r for r in c2.check_mesh_export(s, {**side, "variants": 9})}
    assert not names["mesh_materials_variants"].passed
    off = c2.read_gltf_summary(write_gltf(tmp_path / "d.gltf", lo=(-0.8, 0.2, -0.6)))
    assert not {r.name: r for r in c2.check_mesh_export(off, side)}["mesh_pivot"].passed
    big = c2.read_gltf_summary(write_gltf(tmp_path / "e.gltf", scale=(100, 100, 100)))
    assert not {r.name: r for r in c2.check_mesh_export(big, side)}["mesh_unit_scale"].passed
    assert not {r.name: r for r in c2.check_mesh_export(s, {**side, "unitsPerWorldUnit": 0.01})}["mesh_unit_scale"].passed
    assert not {r.name: r for r in c2.check_mesh_export(s, {**side, "shadowCaster": None})}["mesh_shadow_caster"].passed


def test_glb_reader_matches_gltf(tmp_path):
    g = write_gltf(tmp_path / "f.gltf")
    body = g.read_bytes()
    body += b" " * (-len(body) % 4)
    glb = struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(body)) + struct.pack("<II", len(body), 0x4E4F534A) + body
    (tmp_path / "f.glb").write_bytes(glb)
    assert c2.read_gltf_summary(tmp_path / "f.glb") == c2.read_gltf_summary(g)
    (tmp_path / "bad.glb").write_bytes(b"nope" + b"\0" * 20)
    with pytest.raises(ValueError):
        c2.read_gltf_summary(tmp_path / "bad.glb")
