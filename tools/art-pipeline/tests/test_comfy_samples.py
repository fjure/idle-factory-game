"""Grid detection and mode reduction on generative-style samples (task T404; research R07, R08).

The samples are synthetic (see make_comfy_samples.py): off-grid jitter, soft edges, noise and
non-integer upscale factors, not output of a generative model.
"""
import json

import pytest
from PIL import Image

import make_comfy_samples as ms
from art_pipeline import pixel_pass as pp
from conftest import FIXTURES

DIR = FIXTURES / "comfy" / "samples"
INDEX = json.loads((FIXTURES / "comfy" / "samples.json").read_text(encoding="utf-8"))
AUTO = sorted(n for n, e in INDEX.items() if e["expectGrid"])
CFG_KW = dict(ms.CFG)


def _raw(name):
    return Image.open(DIR / f"{name}_raw.png").convert("RGBA")


def _cfg(name):
    return pp.PassConfig(grid=INDEX[name]["pinnedGrid"], **CFG_KW)


def _clean_reference(name, palette):
    """The same motif without any generative degradation, pushed through the scripted route."""
    art = ms.SAMPLES[name]["art"]()
    cfg = pp.PassConfig(route="scripted", canvas=CFG_KW["canvas"], footprint=CFG_KW["footprint"],
                        quant_groups=CFG_KW["quant_groups"])
    return pp.run_pixel_pass([art], palette, cfg)[0][0]


def _agreement(a, b):
    pa, pb = a.convert("RGBA").load(), b.convert("RGBA").load()
    n = same = 0
    for y in range(a.height):
        for x in range(a.width):
            if pa[x, y][3] == 0 and pb[x, y][3] == 0:
                continue
            n += 1
            same += pa[x, y] == pb[x, y]
    return same / n


def test_every_sample_is_committed_with_matching_hashes():
    assert set(INDEX) == set(ms.SAMPLES)
    for name, e in INDEX.items():
        assert pp.pixel_hash(_raw(name)) == e["rawHash"], name
        assert list(_raw(name).size) == e["rawSize"], name


def test_raw_samples_regenerate_from_the_seeded_script():
    for name, spec in ms.SAMPLES.items():
        assert pp.pixel_hash(ms.build(spec)) == INDEX[name]["rawHash"], name


def test_samples_really_are_off_grid(palette):
    mapper = pp.make_mapper(palette, CFG_KW["quant_groups"])
    for name in ("s01_gem_k4_soft", "s03_coin_k8", "s05_bars_k6_wobble"):
        raw = _raw(name)
        k = INDEX[name]["expectGrid"]
        assert pp.grid_uniformity(raw, k) < 0.9, f"{name}: raw colours should not already be blockwise equal"
        assert pp.grid_uniformity(raw, k, mapper) > 0.9, name
    assert len({p for p in _raw("s01_gem_k4_soft").getdata()}) > 200  # noise: many distinct colours


@pytest.mark.parametrize("name", AUTO)
def test_grid_is_detected_and_reduced_to_the_art_size(name, palette):
    mapper = pp.make_mapper(palette, CFG_KW["quant_groups"])
    raw = _raw(name)
    e = INDEX[name]
    assert pp.detect_grid(raw, mapper=mapper) == e["expectGrid"]
    reduced = pp.reduce_for_route(raw, "comfy", mapper=mapper, expected_size=tuple(e["expectSize"]))
    assert reduced.size == tuple(e["expectSize"])


@pytest.mark.parametrize("name", sorted(n for n, e in INDEX.items() if "goldenHash" in e))
def test_golden_reproduction_and_palette_alpha_invariants(name, palette):
    e = INDEX[name]
    out, placement = pp.run_pixel_pass([_raw(name)], palette, _cfg(name))
    golden = Image.open(DIR / f"{name}_golden.png").convert("RGBA")
    assert pp.pixel_hash(out[0]) == e["goldenHash"] == pp.pixel_hash(golden)
    assert out[0].size == CFG_KW["canvas"] == placement.canvas
    allowed = set(palette.rgbs())
    for p in out[0].getdata():
        assert p[3] in (0, 255)
        if p[3]:
            assert p[:3] in allowed
    again, _ = pp.run_pixel_pass([_raw(name)], palette, _cfg(name))
    assert pp.png_bytes(again[0]) == pp.png_bytes(out[0])


@pytest.mark.parametrize("name", sorted(n for n, e in INDEX.items() if "goldenHash" in e))
def test_output_recovers_the_clean_motif(name, palette):
    out, _ = pp.run_pixel_pass([_raw(name)], palette, _cfg(name))
    floor = ms.SAMPLES[name].get("min_agreement", 1.0)
    assert _agreement(out[0], _clean_reference(name, palette)) >= floor


def test_non_integer_upscale_is_refused_not_guessed(palette):
    mapper = pp.make_mapper(palette, CFG_KW["quant_groups"])
    raw = _raw("s06_gem_nonint_4p5")
    assert raw.size == (108, 108)
    assert pp.detect_grid(raw, mapper=mapper) != 4.5  # integer factors only
    with pytest.raises(pp.PixelPassError, match="expected"):
        pp.reduce_for_route(raw, "comfy", mapper=mapper, expected_size=(24, 24))


def test_fully_blended_upscale_needs_a_pinned_grid(palette):
    """Bilinear upscaling blends every pixel; auto detection is not asserted, a brief-pinned grid must work."""
    mapper = pp.make_mapper(palette, CFG_KW["quant_groups"])
    raw = _raw("s07_gem_bilinear4")
    assert pp.reduce_for_route(raw, "comfy", grid=4, mapper=mapper, expected_size=(24, 24)).size == (24, 24)
