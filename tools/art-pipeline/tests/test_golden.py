"""Golden reproduction per route (research R09) on committed fixtures, plus determinism."""
import json

import pytest
from PIL import Image

import make_fixtures as mf
from art_pipeline import checks as ck
from art_pipeline import pixel_pass as pp
from conftest import FIXTURES, load_frames

ROUTES = {
    "blender": (mf.BLENDER_CFG, 0.005),   # within 0.5 % of opaque pixels per frame
    "comfy": (mf.COMFY_CFG, 0.005),
    "scripted": (mf.SCRIPTED_CFG, 0.0),   # pixel-identical
}
GOLDENS = json.loads((FIXTURES / "goldens.json").read_text())


@pytest.mark.parametrize("route", sorted(ROUTES))
def test_golden_reproduction(route, palette):
    cfg, tol = ROUTES[route]
    raw = load_frames(route, "raw")
    assert [pp.pixel_hash(f) for f in raw] == GOLDENS[route]["raw"]
    out, placement = pp.run_pixel_pass(raw, palette, cfg)
    golden = load_frames(route, "golden")
    assert ck.check_reproduction(out, golden, tol).passed
    assert [pp.pixel_hash(f) for f in out] == GOLDENS[route]["golden"]
    assert placement.canvas == cfg.canvas


@pytest.mark.parametrize("route", sorted(ROUTES))
def test_output_is_deterministic_run_twice(route, palette):
    cfg, _ = ROUTES[route]
    raw = load_frames(route, "raw")
    a, _ = pp.run_pixel_pass(raw, palette, cfg)
    b, _ = pp.run_pixel_pass(raw, palette, cfg)
    assert [pp.sha256_hex(pp.png_bytes(f)) for f in a] == [pp.sha256_hex(pp.png_bytes(f)) for f in b]


def test_scripted_route_regenerates_raw_from_code(palette):
    regenerated = mf.scripted_raw(palette)
    assert pp.pixel_hash(regenerated) == GOLDENS["scripted"]["raw"][0]


def test_fixture_raw_inputs_regenerate(palette):
    assert pp.pixel_hash(mf.blender_raw(0)) == GOLDENS["blender"]["raw"][0]
    assert pp.pixel_hash(mf.blender_raw(1)) == GOLDENS["blender"]["raw"][1]
    assert pp.pixel_hash(mf.comfy_raw()) == GOLDENS["comfy"]["raw"][0]


def test_golden_outputs_pass_asset_checks(palette):
    frames = load_frames("blender", "golden")
    entry = {"canvas": [48, 60], "footprintPx": [48, 48], "pivot": list(pp.pivot_for((48, 60), (48, 48))), "fps": 8}
    results = ck.run_asset_checks(frames, entry, palette, expected_frames=2, ppu=24, running_loop=True)
    assert all(r.passed for r in results), [r.to_dict() for r in results if not r.passed]
    comfy = load_frames("comfy", "golden")
    centry = {"canvas": [32, 32], "footprintPx": [28, 24], "pivot": list(pp.pivot_for((32, 32), (28, 24))), "fps": 1}
    cres = ck.run_asset_checks(comfy, centry, palette, expected_frames=1, ppu=24,
                               allowed_groups={"resources", "ink"})
    assert all(r.passed for r in cres), [r.to_dict() for r in cres if not r.passed]
    cue = load_frames("scripted", "golden")
    assert ck.check_cue_icon_size(cue[0]).passed
    assert ck.check_outline(cue, palette).passed


def test_reproduction_tolerance_boundaries(palette):
    golden = load_frames("blender", "golden")
    changed = [f.copy() for f in golden]
    opaque = sum(1 for x in range(48) for y in range(60) if changed[0].getpixel((x, y))[3])
    n = int(opaque * 0.003)  # 0.3 % of opaque pixels altered: inside the 0.5 % tolerance
    count = 0
    for y in range(60):
        for x in range(48):
            if changed[0].getpixel((x, y))[3] and count < n:
                changed[0].putpixel((x, y), (1, 2, 3, 255))
                count += 1
    assert ck.check_reproduction(changed, golden, 0.005).passed
    assert not ck.check_reproduction(changed, golden, 0.0).passed
    for y in range(60):
        for x in range(48):
            if changed[1].getpixel((x, y))[3] and (x + y) % 20 == 0:
                changed[1].putpixel((x, y), (1, 2, 3, 255))
    assert not ck.check_reproduction(changed, golden, 0.005).passed
    assert not ck.check_reproduction(golden[:1], golden, 0.005).passed
