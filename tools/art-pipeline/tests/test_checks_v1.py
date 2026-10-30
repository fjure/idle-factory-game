"""T462 art-pipeline v1 checks: positive and negative cases for every check (FR-021a, FR-022)."""
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline import checks as ck
from art_pipeline import checks_v1 as v1
from art_pipeline.palette import PALETTE_V1_PATH, load_palette
from conftest import solid

REPO = Path(__file__).resolve().parents[3]
SPIKE = REPO / "art-source" / "f004" / "spike"


@pytest.fixture(scope="module")
def pal():
    return load_palette(PALETTE_V1_PATH)


def _img(size, pixels):
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    for (x, y), rgb in pixels.items():
        im.putpixel((x, y), tuple(rgb) + (255,))
    return im


def _block(pal, outline, rim, fill, size=(12, 12), box=(3, 3, 9, 9)):
    """Body on a transparent canvas: 1 px `outline` ring with chamfered corners (so every outline pixel has a
    non-outline inward neighbour, as in the spike sprites), 1 px `rim` ring inside it, `fill` interior."""
    x0, y0, x1, y1 = box
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    for y in range(y0, y1):
        for x in range(x0, x1):
            if x in (x0, x1 - 1) and y in (y0, y1 - 1):
                continue
            edge = x in (x0, x1 - 1) or y in (y0, y1 - 1)
            ring = x in (x0 + 1, x1 - 2) or y in (y0 + 1, y1 - 2)
            im.putpixel((x, y), pal.rgb(outline if edge else rim if ring else fill) + (255,))
    return im


# --- integral pivot (rule P1) -------------------------------------------------------------------

def _rest(canvas, x=3):
    im = Image.new("RGBA", canvas, (0, 0, 0, 0))
    im.putpixel((x, canvas[1] - 1), (1, 1, 1, 255))
    return [im]


def test_pivot_integral_accepts_even_footprints_and_the_resource_tolerance():
    # 80 x 64 / 68 x 30: pivot (40, 15), normalised (0.5, 0.234375): exact
    machine = {"canvas": [80, 64], "footprintPx": [68, 30], "pivot": [0.5, 0.234375]}
    r = ck.check_pivot(_rest((80, 64)), machine)
    assert r.passed and r.details["integral"] and r.details["pivotPx"] == [40.0, 15.0]
    # 32 x 24 / 20 x 10: pivot (16, 5) = 0.5, 0.208333 (not float-exact, inside the 1e-3 tolerance)
    res = {"canvas": [32, 24], "footprintPx": [20, 10], "pivot": [0.5, 0.208333]}
    assert ck.check_pivot(_rest((32, 24)), res).passed


def test_pivot_integral_rejects_the_spike_odd_footprint_and_half_pixels():
    # the D24 spike ore: footprint 20 x 9 puts the pivot at y = 4.5 (a half pixel)
    odd = {"canvas": [32, 24], "footprintPx": [20, 9], "pivot": [0.5, 0.1875]}
    r = ck.check_pivot(_rest((32, 24)), odd)
    assert not r.passed and r.details["integral"] is False and r.details["pivotPx"][1] == 4.5
    # D16-style half pixel in x: odd canvas width
    half_x = {"canvas": [53, 24], "footprintPx": [20, 10], "pivot": [0.5, 0.208333]}
    assert not ck.check_pivot(_rest((53, 24)), half_x).passed
    # tolerance: strictly below 1e-3 px passes, a tenth of a pixel fails
    assert ck.pivot_is_integral({"canvas": [100, 100], "pivot": [0.5, 0.5]})
    assert ck.pivot_is_integral({"canvas": [24, 24], "pivot": [0.5, 0.208333]})
    assert not ck.pivot_is_integral({"canvas": [1000, 100], "pivot": [0.5001, 0.5]})  # 500.1 px


# --- id-level palette allowance -----------------------------------------------------------------

def test_id_level_palette_allowance(pal):
    groups = {"resources", "ink"}
    grey = [solid((2, 2), pal.rgb("steel_2")), solid((2, 2), pal.rgb("steel_3"))]
    assert not ck.check_palette(grey, pal, groups).passed  # machine group is not allowed for resources
    r = ck.check_palette(grey, pal, groups, allowed_ids={"steel_2", "steel_3"})
    assert r.passed and r.details["idAllowances"] == ["steel_2", "steel_3"]
    # only the listed ids are lifted: steel_1 from the same group is still rejected
    mixed = grey + [solid((2, 2), pal.rgb("steel_1"))]
    r = ck.check_palette(mixed, pal, groups, allowed_ids={"steel_2", "steel_3"})
    assert not r.passed and r.details["disallowedGroups"] == ["machine"]
    # group budgets still count the lifted colours
    assert not ck.check_palette(grey, pal, groups, group_limits={"machine": 1}, allowed_ids={"steel_2", "steel_3"}).passed
    # an allowance never lifts the reserved cue group unless that id is listed
    cue = [solid((2, 2), pal.rgb("cue_warn"))]
    assert not ck.check_palette(cue, pal, groups, allowed_ids={"steel_2"}).passed


# --- overlay keep-out ---------------------------------------------------------------------------

def test_overlay_keepout_region():
    ok = _img((80, 64), {(14, 0): (1, 1, 1), (72, 33): (1, 1, 1), (40, 34): (1, 1, 1), (15, 34): (1, 1, 1)})
    assert v1.check_overlay_keepout([ok]).passed  # just outside columns 15..71 / rows 0..33
    for pos in ((15, 0), (71, 33), (40, 20)):  # corners and middle of the region
        r = v1.check_overlay_keepout([ok, _img((80, 64), {pos: (1, 1, 1)})])
        assert not r.passed and r.details["badFrames"][0]["frame"] == 1 and r.details["badFrames"][0]["first"] == list(pos)
    assert not v1.check_overlay_keepout([_img((80, 64), {(15, 33): (1, 1, 1)})]).passed  # inclusive bounds
    assert v1.check_overlay_keepout([_img((80, 64), {})]).passed


def test_spike_miner_respects_the_keepout():
    body = Image.open(SPIKE / "s3-s4" / "pixel" / "s3-lightB-body-run_00.png")
    assert v1.check_overlay_keepout([body]).passed


# --- outline / rim contrast on the composite (section 9.3) --------------------------------------

def test_outline_pairs_count_corners_and_off_canvas_neighbours(pal):
    one = _img((3, 3), {(1, 1): pal.rgb("ink_0")})
    recs = list(v1.outline_pairs(one, None, pal))
    assert len(recs) == 8 and {t for _i, _r, t in recs} == {"floor_0", "floor_1"}  # 4 pairs, each on 2 floors
    assert all(rim is None for _i, rim, _t in recs)
    edge = _img((2, 1), {(0, 0): pal.rgb("ink_0"), (1, 0): pal.rgb("ink_0")})  # off-canvas neighbours count
    assert len(list(v1.outline_pairs(edge, None, pal))) == 2 * 3 * 2  # two px x 3 outward neighbours x 2 floors


def test_rim_is_the_first_inward_non_outline_neighbour_in_order_right_left_down_up(pal):
    # 5x5 block: (1,1) is a corner outline pixel whose inward neighbours are right (2,1) = ring and down (1,2)
    # = ring, both outline too, so a 3-wide ring needs depth; use a 7x7 block with a 1 px outline.
    body = _block(pal, "ink_0", "steel_3", "steel_1", size=(9, 9), box=(1, 1, 8, 8))
    # left edge pixel (1,4): inward right neighbour (2,4) is the ring tone steel_3
    recs = [r for r in v1.outline_pairs(body, None, pal) if r[1] is not None]
    assert recs and {r[1] for r in recs} == {pal.rgb("steel_3")}
    # a left-edge pixel with both a right and a down inward neighbour picks right: build it explicitly
    probe = _img((5, 4), {(1, 1): pal.rgb("ink_0"), (2, 1): pal.rgb("steel_3"), (1, 2): pal.rgb("copper_2"),
                          (2, 2): pal.rgb("steel_1"), (3, 1): pal.rgb("ink_0"), (3, 2): pal.rgb("ink_0"),
                          (1, 3): pal.rgb("ink_0"), (2, 3): pal.rgb("ink_0"), (3, 3): pal.rgb("ink_0")})
    # (2,2) is interior (all four neighbours opaque): rim of (1,2) is its right neighbour (2,2) = steel_1
    rims = {(i, r) for i, r, t in v1.outline_pairs(probe, None, pal) if t == "floor_0"}
    assert (pal.rgb("ink_0"), pal.rgb("steel_1")) in rims


def test_outline_contrast_passes_with_ink_over_a_light_plate(pal):
    body = _block(pal, "ink_0", "steel_1", "steel_1")
    plate = Image.new("RGBA", body.size, pal.rgb("plate_2") + (255,))
    r = v1.check_outline_contrast(body, plate, pal)
    assert r.passed and r.details["tones"]["plate_2"]["inkShare"] == 1.0


def test_outline_contrast_passes_with_a_light_rim_over_bare_floor(pal):
    body = _block(pal, "ink_0", "steel_3", "steel_1")  # ink fails on floor (1.4-1.7), steel_3 rim passes (6.5-7.9)
    r = v1.check_outline_contrast(body, None, pal)
    assert r.passed
    for t in ("floor_0", "floor_1"):
        d = r.details["tones"][t]
        assert d["inkShare"] == 0.0 and d["rimShare"] == 1.0 and d["share"] == 1.0


def test_outline_contrast_fails_with_a_dark_rim_over_bare_floor(pal):
    body = _block(pal, "ink_0", "steel_1", "steel_1")
    r = v1.check_outline_contrast(body, None, pal)
    assert not r.passed and r.details["failingTones"] == ["floor_0", "floor_1"]


def test_outline_contrast_threshold_comparison_is_at_least_and_default_is_90_percent(pal):
    assert v1.SHARE_THRESHOLD == 0.90
    # light rim on three sides, dark rim on one: the share over bare floor is strictly between 0 and 1
    body = _block(pal, "ink_0", "steel_3", "steel_1")
    for y in range(4, 8):
        body.putpixel((4, y), pal.rgb("steel_1") + (255,))  # left rim column goes dark
    share = v1.outline_contrast_shares(body, None, pal)["floor_0"]["share"]
    assert 0.0 < share < 0.9
    assert not v1.check_outline_contrast(body, None, pal).passed  # default 90 %
    assert v1.check_outline_contrast(body, None, pal, threshold=share).passed  # at-least semantics
    assert not v1.check_outline_contrast(body, None, pal, threshold=share + 0.001).passed


def test_outline_contrast_tone_restriction_ignores_tones_outside_the_set(pal):
    body = _block(pal, "ink_0", "steel_1", "steel_1")
    plate = Image.new("RGBA", body.size, pal.rgb("plate_1") + (255,))
    plate.putpixel((2, 5), pal.rgb("dim_1") + (255,))  # outward neighbour of the left edge pixel (3,5)
    r_all = v1.check_outline_contrast(body, plate, pal)
    assert not r_all.passed and "dim_1" in r_all.details["failingTones"]
    r = v1.check_outline_contrast(body, plate, pal, tones=v1.BODY_BACKDROP_TONES)
    assert r.passed and r.details["ignoredTones"] == ["dim_1"]
    # restricting to a set that matches nothing is not a pass
    assert not v1.check_outline_contrast(body, plate, pal, tones={"ui_bg"}).passed


def test_spike_miner_over_bare_floor_fails_the_90_percent_check(pal):
    """Regression for the section 9.3 re-measurement (owner decision R1): `s3-lightB-body-run` over
    `s2-slot-occupied` (D24). Bare-floor pairs: 56 on floor_0 (35 pass, 62 %) and 58 on floor_1 (35 pass,
    60 %); the plate tones pass. The check must FAIL at 90 %."""
    body_p = SPIKE / "s3-s4" / "pixel" / "s3-lightB-body-run_00.png"
    plate_p = SPIKE / "s2" / "pixel" / "D24" / "s2-slot-occupied_00.png"
    assert body_p.exists() and plate_p.exists()
    body, plate = Image.open(body_p), Image.open(plate_p)
    shares = v1.outline_contrast_shares(body, plate, pal)
    assert (shares["floor_0"]["pairs"], shares["floor_0"]["either"]) == (56, 35)
    assert (shares["floor_1"]["pairs"], shares["floor_1"]["either"]) == (58, 35)
    assert round(shares["floor_0"]["share"] * 100) == 62 and round(shares["floor_1"]["share"] * 100) == 60
    assert shares["floor_0"]["ink"] == 0 and shares["floor_1"]["ink"] == 0
    assert (shares["plate_1"]["pairs"], shares["plate_1"]["share"]) == (68, 1.0)
    assert (shares["plate_2"]["pairs"], shares["plate_2"]["share"]) == (3, 1.0)
    assert (shares["ink_0"]["pairs"], shares["ink_0"]["share"]) == (2, 1.0)
    assert (shares["dim_1"]["pairs"], shares["dim_1"]["share"]) == (1, 0.0)
    assert sum(1 for i, _r, _t in v1.outline_pairs(body, plate, pal) if i != pal.rgb("ink_0")) == 0  # all ink_0
    r = v1.check_outline_contrast(body, plate, pal)
    assert not r.passed and {"floor_0", "floor_1"} <= set(r.details["failingTones"])
    # rule C5's tone set does not rescue it: bare floor is part of the set
    assert not v1.check_outline_contrast(body, plate, pal, tones=v1.BODY_BACKDROP_TONES).passed
    # the same body on plate tones only passes: the ink outline carries it
    on_plate = Image.new("RGBA", body.size, pal.rgb("plate_1") + (255,))
    assert v1.check_outline_contrast(body, on_plate, pal).passed


# --- backdrop under overlays (C1, C3, C8) -------------------------------------------------------

def test_overlay_backdrop_ring_allowed_and_forbidden(pal):
    badge = _img((10, 10), {(x, y): pal.rgb("cue_warn") for x in range(4, 7) for y in range(4, 7)})
    good = Image.new("RGBA", (10, 10), pal.rgb("plate_1") + (255,))
    r = v1.check_overlay_backdrop(badge, good, v1.A_OV, pal)
    assert r.passed and r.details["ringPixels"] == 12
    bad = good.copy()
    bad.putpixel((3, 5), pal.rgb("dim_1") + (255,))  # forbidden tone in the ring
    r = v1.check_overlay_backdrop(badge, bad, v1.A_OV, pal)
    assert not r.passed and r.details["forbiddenTones"] == {"dim_1": 1}
    far = good.copy()
    far.putpixel((0, 0), pal.rgb("dim_1") + (255,))  # forbidden but outside the 1 px ring: ignored
    assert v1.check_overlay_backdrop(badge, far, v1.A_OV, pal).passed
    # a ring pixel covered by another overlay is excluded
    other = _img((10, 10), {(3, 5): pal.rgb("cue_info")})
    assert v1.check_overlay_backdrop(badge, bad, v1.A_OV, pal, exclude=other).passed


def test_overlay_backdrop_transparent_is_bare_floor_and_frame_set(pal):
    badge = _img((6, 6), {(2, 2): pal.rgb("cue_info")})
    empty = Image.new("RGBA", (6, 6), (0, 0, 0, 0))
    assert v1.check_overlay_backdrop(badge, empty, v1.A_OV, pal).passed  # floor_0 and floor_1 are both in A_ov
    assert not v1.check_overlay_backdrop(badge, empty, {"floor_0"}, pal).passed  # floor_1 not allowed
    fr = v1.a_fr(pal)
    assert "warn_0" not in fr and "plate_0" not in fr and "res_ore_0" not in fr and len(fr) == len(pal.colors) - 3
    odd = Image.new("RGBA", (6, 6), (1, 2, 3, 255))  # not a palette tone
    r = v1.check_overlay_backdrop(badge, odd, fr, pal)
    assert not r.passed and "unknown:#010203" in r.details["forbiddenTones"]
    assert not v1.check_overlay_backdrop(badge, Image.new("RGBA", (5, 5)), fr, pal).passed  # size mismatch
    assert not v1.check_overlay_backdrop(Image.new("RGBA", (6, 6)), empty, fr, pal).passed  # nothing sampled


# --- accent subset of body (L-4) ----------------------------------------------------------------

def test_accent_subset_of_body(pal):
    body = _block(pal, "ink_0", "steel_1", "steel_2")
    inside = _img((12, 12), {(5, 5): pal.rgb("lit_0"), (6, 6): pal.rgb("lit_1")})
    assert v1.check_accent_subset([inside], [body], pal).passed
    outside = _img((12, 12), {(0, 0): pal.rgb("lit_0")})  # not on the body
    r = v1.check_accent_subset([outside], [body], pal)
    assert not r.passed and r.details["badFrames"][0]["outsideBody"] == 1
    on_ink = _img((12, 12), {(4, 3): pal.rgb("lit_0")})  # top-edge outline pixel (ink, boundary)
    r = v1.check_accent_subset([on_ink], [body], pal)
    assert not r.passed and r.details["badFrames"][0]["onInk"] == 1 and r.details["badFrames"][0]["onBoundary"] == 1
    boundary_non_ink = _block(pal, "steel_1", "steel_1", "steel_2")  # boundary pixels not ink: still rejected
    r = v1.check_accent_subset([on_ink], [boundary_non_ink], pal)
    assert not r.passed and r.details["badFrames"][0]["onBoundary"] == 1 and r.details["badFrames"][0]["onInk"] == 0
    assert not v1.check_accent_subset([inside, inside], [body], pal).passed  # frame count mismatch
    assert v1.check_accent_subset([_img((12, 12), {})], [body], pal).passed  # empty accent frame


def test_spike_accent_is_a_subset_of_the_spike_body(pal):
    body = Image.open(SPIKE / "s3-s4" / "pixel" / "s3-lightB-body-run_00.png")
    accent = Image.open(SPIKE / "s3-s4" / "pixel" / "s3-lightB-accent-run_00.png")
    assert v1.check_accent_subset([accent], [body], pal).passed  # Art Bible L-4: 95 of 95 inside, 0 on ink


# --- intentionallyEmpty manifest flag (R8) ------------------------------------------------------

def test_intentionally_empty_entry_has_no_png_and_passes(pal):
    entry = {"id": "F003-BADGE-RUNNING", "intentionallyEmpty": True}
    results = ck.run_asset_checks([], entry, pal, expected_frames=1, ppu=24)
    assert [r.name for r in results] == ["intentionally_empty"] and all(r.passed for r in results)
    assert ck.build_report({"F003-BADGE-RUNNING": results}, [])["passed"]
    # with canvas/pivot fields present the PNG checks are still skipped
    full = {**entry, "canvas": [80, 64], "footprintPx": [68, 30], "pivot": [0.5, 0.23]}
    assert all(r.passed for r in ck.run_asset_checks([], full, pal, expected_frames=1, ppu=24))


def test_intentionally_empty_contradictions_fail(pal):
    entry = {"intentionallyEmpty": True}
    with_png = ck.run_asset_checks([solid((4, 4), pal.rgb("ink_0"))], entry, pal, expected_frames=1, ppu=24)
    assert not with_png[0].passed and with_png[0].details["frames"] == 1
    stray = ck.run_asset_checks([], {**entry, "png": "x.png"}, pal, expected_frames=1, ppu=24)
    assert not stray[0].passed and stray[0].details["strayFields"] == ["png"]
    # a non-empty entry without frames is not exempt: it fails the regular checks
    normal = {"canvas": [80, 64], "footprintPx": [68, 30], "pivot": [0.5, 0.234375], "fps": 8}
    assert not all(r.passed for r in ck.run_asset_checks([], normal, pal, expected_frames=1, ppu=24))
    assert not ck.check_intentionally_empty([], {"intentionallyEmpty": "yes"}).passed  # flag must be literally true
