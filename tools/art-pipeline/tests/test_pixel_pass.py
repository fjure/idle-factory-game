import hashlib

import pytest
from PIL import Image

from art_pipeline import pixel_pass as pp
from art_pipeline.color import rgb_to_lab
from conftest import FIXTURES, solid

INK = (20, 18, 28)


def test_alpha_is_thresholded_to_0_or_255():
    img = Image.new("RGBA", (4, 1))
    for x, a in enumerate((0, 127, 128, 255)):
        img.putpixel((x, 0), (10, 20, 30, a))
    out = pp.threshold_alpha(img)
    assert [out.getpixel((x, 0))[3] for x in range(4)] == [0, 0, 255, 255]
    assert out.getpixel((1, 0)) == (0, 0, 0, 0)


def test_quantization_maps_only_to_palette_and_is_deterministic(palette):
    img = Image.new("RGBA", (64, 4))
    for x in range(64):
        for y in range(4):
            img.putpixel((x, y), (x * 4, 255 - x * 4, 128, 255))
    a, b = pp.quantize(img, palette), pp.quantize(img, palette)
    assert pp.pixel_hash(a) == pp.pixel_hash(b)
    colors = {a.getpixel((x, y))[:3] for x in range(64) for y in range(4)}
    assert colors <= set(palette.rgbs())


def test_quantization_has_no_dithering(palette):
    out = pp.quantize(solid((16, 16), (100, 120, 90)), palette)
    assert len({out.getpixel((x, y)) for x in range(16) for y in range(16)}) == 1


def test_quantization_uses_cielab(palette):
    rgbs = palette.rgbs()
    probe = (200, 40, 40)
    lab = rgb_to_lab(probe)
    d = [sum((a - b) ** 2 for a, b in zip(lab, rgb_to_lab(c))) for c in rgbs]
    assert pp.nearest_palette_index(probe, rgbs) == d.index(min(d))


def test_tie_break_is_lowest_palette_index():
    same = [(10, 10, 10), (10, 10, 10), (200, 200, 200)]
    assert pp.nearest_palette_index((50, 50, 50), same) == 0
    assert pp.nearest_palette_index((50, 50, 50), [same[2], same[0], same[1]]) == 1


def test_quantization_restricts_to_groups(palette):
    out = pp.quantize(solid((2, 2), (240, 180, 50)), palette, {"ink"})
    assert out.getpixel((0, 0))[:3] in {c.rgb for c in palette.colors_in("ink")}


def test_outline_is_one_pixel_and_ink():
    out = pp.add_outline(solid((2, 2), (200, 200, 200)), INK)
    assert out.size == (4, 4)
    ring = [(x, y) for x in range(4) for y in range(4) if out.getpixel((x, y)) == INK + (255,)]
    assert len(ring) == 8  # 4-connected ring, no corners
    assert out.getpixel((0, 0))[3] == 0
    assert all(out.getpixel((x, y))[:3] == (200, 200, 200) for x in (1, 2) for y in (1, 2))


def test_footprint_anchored_canvas_and_pivot():
    assert pp.footprint_rect((48, 60), (48, 48)) == (0, 12, 48, 48)
    assert pp.pivot_for((48, 60), (48, 48)) == (0.5, 0.4)
    assert pp.pivot_for((50, 60), (48, 48)) == (0.5, 0.4)


def test_all_frames_share_one_canvas_offset():
    a = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
    b = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
    for x in range(2, 8):
        for y in range(2, 8):
            a.putpixel((x, y), (200, 0, 0, 255))
    for x in range(4, 6):
        for y in range(4, 6):
            b.putpixel((x, y), (0, 200, 0, 255))
    (pa, pb), placement = pp.place_on_canvas([a, b], (16, 20), (12, 12))
    assert pa.size == pb.size == (16, 20)
    assert pp.opaque_bbox(pa) == (5, 14, 11, 20)
    # frame b keeps its position relative to frame a: not re-trimmed per frame
    assert pp.opaque_bbox(pb) == (7, 16, 9, 18)
    assert placement.content_offset == (3, 12)
    assert placement.pivot == pp.pivot_for((16, 20), (12, 12))


def test_content_too_large_for_canvas_is_refused():
    with pytest.raises(pp.PixelPassError):
        pp.place_on_canvas([solid((30, 10), (1, 2, 3))], (16, 20), (12, 12))


def test_reduce_mode_picks_modal_value_with_deterministic_tie_break():
    img = Image.new("RGBA", (2, 2), (0, 0, 0, 0))
    for pos, c in {(0, 0): (9, 9, 9), (1, 0): (9, 9, 9), (0, 1): (1, 1, 1), (1, 1): (5, 5, 5)}.items():
        img.putpixel(pos, c + (255,))
    assert pp.reduce_mode(img, 2).getpixel((0, 0)) == (9, 9, 9, 255)
    tie = Image.new("RGBA", (2, 2), (9, 9, 9, 255))
    tie.putpixel((0, 0), (3, 3, 3, 255))
    tie.putpixel((1, 0), (3, 3, 3, 255))
    assert pp.reduce_mode(tie, 2).getpixel((0, 0)) == (3, 3, 3, 255)


def test_reduce_box_averages_and_rounds_deterministically():
    img = Image.new("RGBA", (2, 2), (0, 0, 0, 255))
    img.putpixel((0, 0), (255, 255, 255, 255))
    assert pp.reduce_box(img, 2).getpixel((0, 0)) == (64, 64, 64, 255)
    with pytest.raises(pp.PixelPassError):
        pp.reduce_box(img, 3)


def test_route_reductions(palette):
    raw = Image.open(FIXTURES / "comfy" / "raw_00.png")
    mapper = pp.make_mapper(palette, {"resources", "ink"})
    assert pp.detect_grid(raw, mapper=mapper) == 4
    assert pp.reduce_for_route(raw, "comfy", mapper=mapper, expected_size=(24, 24)).size == (24, 24)
    assert pp.reduce_for_route(solid((8, 8), (1, 2, 3)), "scripted").size == (8, 8)
    assert pp.reduce_for_route(solid((12, 12), (1, 2, 3)), "blender", supersample=3).size == (4, 4)
    with pytest.raises(pp.PixelPassError):
        pp.reduce_for_route(solid((12, 12), (1, 2, 3)), "blender", supersample=3, expected_size=(5, 5))
    with pytest.raises(pp.PixelPassError):
        pp.reduce_for_route(solid((4, 4), (1, 2, 3)), "unknown")


COMPLETE_PROV = {
    "assetId": "a", "revision": 1, "route": "scripted", "sources": ["s.py"], "tools": [{"name": "pillow"}],
    "parameters": {"p": 1}, "touchUp": [], "pixelPass": {"commit": "x", "paletteVersion": "0.1.0"},
    "outputs": ["a_00.png"], "license": "authored", "trainingDataNote": "n/a", "producedBy": "Asset Artist",
    "reviewedBy": {"role": "Art Director", "session": "s2", "date": "2026-10-27"},
    "candidateSelection": "single", "artBibleVersion": "1.0", "briefVersion": "1.0",
}


def _entry(palette, prov):
    frames, placement = pp.place_on_canvas([solid((4, 4), (1, 2, 3))], (8, 8), (6, 6))
    return pp.build_manifest_entry("a", "scripted", frames, placement, palette, 24, 1, prov, "f")


def test_manifest_entry_has_png_hashes_and_schema(palette):
    manifest = pp.build_manifest([_entry(palette, COMPLETE_PROV)])
    assert manifest["schemaVersion"] == 1
    e = manifest["entries"][0]
    assert e["canvas"] == [8, 8] and e["pivot"] == list(pp.pivot_for((8, 8), (6, 6)))
    assert e["frames"][0]["file"] == "f/a_00.png"
    frames, _ = pp.place_on_canvas([solid((4, 4), (1, 2, 3))], (8, 8), (6, 6))
    assert e["frames"][0]["sha256"] == hashlib.sha256(pp.png_bytes(frames[0])).hexdigest()
    again = pp.build_manifest([_entry(palette, COMPLETE_PROV)])
    assert pp.manifest_json(manifest) == pp.manifest_json(again)


def test_manifest_refuses_incomplete_provenance(palette):
    bad = dict(COMPLETE_PROV)
    del bad["license"]
    with pytest.raises(pp.PixelPassError, match="license"):
        _entry(palette, bad)
