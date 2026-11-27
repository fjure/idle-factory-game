"""render-hd post steps, v2 provenance and the reference-asset reproducibility test (T613)."""
import json
import os
from pathlib import Path

import pytest
from PIL import Image, ImageDraw

from art_pipeline import checks_v2 as c2
from art_pipeline import render_hd as rh
from art_pipeline.provenance import license_allows_commercial, v2_provenance_problems

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
GOLDEN = ROOT / "f007" / "reference"
PARAMS = ROOT / "f007" / "reference-machine.params.json"


def rect_img(size, box, rgba):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).rectangle(box, fill=rgba)
    return img


# --- post ------------------------------------------------------------------------------------------------------------

def raw_passes():
    return {"beauty": rect_img((40, 40), (10, 12, 29, 31), (100, 120, 140, 255)),
            "shadow": rect_img((40, 40), (20, 25, 35, 33), (0, 0, 0, 90)),
            "emissiveMask": rect_img((40, 40), (12, 14, 15, 17), (255, 255, 255, 255))}


def test_union_bbox_and_trim_share_one_box_and_one_pivot():
    raw = raw_passes()
    assert rh.union_bbox(raw) == (10, 12, 36, 34)
    trimmed, pivot = rh.trim_to_union(raw, padding=2)
    assert {i.size for i in trimmed.values()} == {(30, 26)}
    assert pivot == (20 - 8, 20 - 10)     # the canvas centre (the world origin) minus the crop origin
    assert c2.check_pass_alignment(trimmed, padding=2).passed
    assert trimmed["emissiveMask"].getpixel((4, 4)) == (255, 255, 255, 255)   # (12 - 8, 14 - 10)


def test_trim_rejects_empty_passes():
    with pytest.raises(ValueError):
        rh.trim_to_union({"beauty": Image.new("RGBA", (4, 4))})


def test_dilate_colours_transparent_edge_pixels_without_touching_alpha():
    img = rect_img((9, 9), (3, 3, 5, 5), (200, 100, 50, 255))
    out = rh.dilate_colour(img, 2)
    assert out.getpixel((2, 4)) == (200, 100, 50, 0)       # one px out: colour bled in, alpha still 0
    assert out.getpixel((1, 4)) == (200, 100, 50, 0)       # two px out
    assert out.getpixel((0, 4)) == (0, 0, 0, 0)            # beyond the radius
    assert out.getpixel((4, 4)) == (200, 100, 50, 255)
    assert out.getchannel("A").getbbox() == (3, 3, 6, 6)


def test_premultiply_known_pixel():
    img = Image.new("RGBA", (1, 1), (200, 100, 50, 128))
    assert rh.premultiply(img).getpixel((0, 0)) == (100, 50, 25, 128)


def test_frame_strip_layout():
    a, b = Image.new("RGBA", (3, 2), (1, 2, 3, 255)), Image.new("RGBA", (3, 2), (9, 9, 9, 255))
    s = rh.frame_strip([a, b])
    assert s.size == (6, 2) and s.getpixel((0, 0))[:3] == (1, 2, 3) and s.getpixel((3, 0))[:3] == (9, 9, 9)


def test_manifest_row_known_answer():
    passes = {"beauty": Image.new("RGBA", (200, 100)), "shadow": Image.new("RGBA", (200, 100)), "emissiveMask": Image.new("RGBA", (200, 100))}
    params = {"machineDefinitionId": "m1", "look": "F007-B", "stage": 1, "indicatorAnchor": [0, 40], "effectAnchors": [[10, 20]],
              "frames": 2, "fps": 6, "footprint": [150, 80], "textureFormat": {"android": "ASTC_6x6", "windows": "BC7"}}
    row = rh.manifest_row(params, passes, (100.0, 75.0), "p.json")
    assert row["canvas"] == [200, 100] and row["pivot"] == [0.5, 0.25] and row["pivotPx"] == [100.0, 75.0]
    assert row["indicatorAnchor"] == [100.0, 35.0] and row["effectAnchors"] == [[110.0, 55.0]]
    assert row["passes"] == ["body", "shadow", "emissiveMask"] and row["provenanceRef"] == "p.json" and row["stage"] == 1


# --- provenance v2 --------------------------------------------------------------------------------------------------------

def good_prov(**over):
    rec = {"look": "F007-B", "pipeline": "v2-prototype", "route": "render-hd", "renderPasses": ["beauty", "shadow"],
           "rigVersion": {"version": "rig-v3", "sha256": "a" * 64}, "textureFormat": {"android": "ASTC_6x6", "windows": "BC7"},
           "generatedTextures": [], "artDirectionBrief": {"path": "x.md", "version": "none (prototype brief)"},
           "artBibleVersion": "none (prototype brief)"}
    rec.update(over)
    return rec


def test_v2_provenance_prototype_accepts_the_placeholder_bible():
    assert v2_provenance_problems(good_prov()) == []


@pytest.mark.parametrize("field,value", [
    ("pipeline", "v3"), ("route", "pixel"), ("renderPasses", []), ("renderPasses", ["shadow"]), ("renderPasses", ["beauty", "depth"]),
    ("rigVersion", {"version": "rig-v3", "sha256": "xyz"}), ("textureFormat", {"android": "ASTC_6x6"}),
    ("artDirectionBrief", {"path": "x"}), ("artBibleVersion", ""), ("look", ""), ("generatedTextures", None)])
def test_v2_provenance_rejects_each_broken_field(field, value):
    assert v2_provenance_problems(good_prov(**{field: value}))


def test_v2_provenance_generated_textures_need_full_fields_and_a_commercial_licence():
    tex = {"model": "m", "license": "Apache-2.0", "prompt": "steel plate", "seed": 0, "workflowHash": "abc"}
    assert v2_provenance_problems(good_prov(generatedTextures=[tex])) == []
    assert v2_provenance_problems(good_prov(generatedTextures=[{**tex, "license": "CC-BY-NC-4.0"}]))
    assert v2_provenance_problems(good_prov(generatedTextures=[{k: v for k, v in tex.items() if k != "workflowHash"}]))
    assert not license_allows_commercial("research-only") and not license_allows_commercial("") and license_allows_commercial("MIT")


def test_v2_final_pipeline_needs_a_real_bible_version_and_a_review():
    final = good_prov(pipeline="v2", artBibleVersion="F007-AB v2.0")
    assert any("reviewedBy" in p for p in v2_provenance_problems(final))
    assert v2_provenance_problems(final, for_manifest=True) == []
    reviewed = {**final, "reviewedBy": {"role": "Art Director", "session": "s1", "date": "2026-12-01"}}
    assert v2_provenance_problems(reviewed) == []
    assert v2_provenance_problems({**reviewed, "artBibleVersion": "none (prototype brief)"})
    assert v2_provenance_problems({**reviewed, "artBibleVersion": "F007-AB v1.0"})
    assert v2_provenance_problems(good_prov(artBibleVersion="whatever"))


# --- reference asset: golden passes and a real re-render ----------------------------------------------------------------------

def load(directory, name):
    return Image.open(Path(directory) / ("reference-machine.%s.png" % name)).convert("RGBA")


def test_golden_reference_passes_are_internally_consistent():
    raw = {n: load(GOLDEN, n) for n in ("beauty", "shadow", "emissiveMask")}
    trimmed, _pivot = rh.trim_to_union(raw, 4)
    assert c2.check_pass_alignment(trimmed, padding=4).passed
    assert c2.check_key_colour_absent(list(raw.values())).passed


def blender_path():
    exe = rh.blender_exe()
    return exe if Path(exe).exists() else None


@pytest.mark.skipif(blender_path() is None, reason="Blender 5.2.1 not installed; the golden-fixture test above still runs")
def test_rerender_of_the_reference_asset_reproduces_the_golden_passes(tmp_path):
    info = rh.run_blender(PARAMS, tmp_path / "again", blender_path(), threads=2)
    assert info["tools"]["blender"]["version"].startswith(rh.BLENDER_PIN)
    assert info["parameters"]["denoiser"] is False and info["parameters"]["seed"] == 0
    for n in ("beauty", "shadow", "emissiveMask", "normal"):
        r = c2.check_reproducibility(load(GOLDEN, n), load(tmp_path / "again", n))
        assert r.passed, (n, r.details)
