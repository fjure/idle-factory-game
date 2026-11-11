"""F006 T517: the A-03 anchor measurement (research R17). Facts here are the measured numbers quoted in reviews/a03-anchor.md."""
import json

import pytest

from art_pipeline import checks as ck
from art_pipeline.f006 import a03_anchor as a


@pytest.fixture(scope="module")
def inp():
    return a.load_inputs()


@pytest.fixture(scope="module")
def data(inp):
    return a.measure_all(inp)


def cand(data, cid):
    return next(c for c in data["candidates"] if c["id"] == cid)


def test_baseline_is_the_f004_box(inp):
    # (59,33) = +19 right, +16 up: the export's badge box 7.5..30.5 x 16..39 above the pivot
    bx, by, bw, bh = inp.geo["badgeBoxPx"]
    assert (a.BASE_ANCHOR[0] - bw / 2, a.BASE_ANCHOR[1]) == (bx, by) or (a.BASE_ANCHOR[0] - 11.5, a.BASE_ANCHOR[1]) == (bx, by)
    assert a.BAND_BELOW + a.BAND_ABOVE == inp.geo["pitchPx"][1] == 54


def test_baseline_has_no_overlap_but_is_ambiguous(data):
    for lay in cand(data, "K0")["layouts"]:
        for k in ("rowBandSpillPx", "ownPlatePx", "foreignPlatePx", "foreignHitPx", "ownFrameHiddenPx", "ownBodyPx", "ownMarkerPx"):
            assert lay[k] == 0, k
        assert lay["gaps"]["ownPlate"] == lay["gaps"]["behindBody"] == 2
        assert lay["c8"]["ringFractionInAov"] == 1.0


def test_centred_collides(data):
    lay = cand(data, "K1")["layouts"][0]
    assert lay["ownMarkerPx"] > 0 and lay["foreignHitPx"] > 0 and lay["branchPx"] > 0 and lay["ownFrameHiddenPx"] > 0


def test_tail_keeps_every_baseline_guarantee(data):
    for lay in cand(data, "K4")["layouts"]:
        for k in ("rowBandSpillPx", "ownPlatePx", "foreignPlatePx", "foreignHitPx", "ownFrameHiddenPx", "ownMarkerPx", "extentOverflowPx"):
            assert lay[k] == 0, k
        assert lay["badgePx"] == cand(data, "K0")["layouts"][0]["badgePx"] + 16


def test_no_candidate_leaves_the_row_band_or_extent(data):
    for c in data["candidates"]:
        for lay in c["layouts"]:
            assert lay["rowBandSpillPx"] == 0 and lay["extentOverflowPx"] == 0
            assert lay["c8"]["ringFractionInAov"] == 1.0


def test_legibility_numbers(data):
    rows = {r["name"]: r for r in data["legibility"]}
    assert rows["phone-portrait 1080x2400 420dpi"]["badgePt"] == 26.23
    assert rows["phone-portrait 720x1280 xhdpi"]["badgePt"] == 22.95 and not rows["phone-portrait 720x1280 xhdpi"]["meets24pt"]
    assert rows["tablet-portrait 1536x2048 264dpi"]["badgePt"] == 34.12


@pytest.mark.parametrize("cid", ["K0", "K4", "K5"])
def test_candidate_sprites_are_palette_exact(inp, cid):
    c = next(x for x in a.CANDIDATES if x.cid == cid)
    for kind in ("blocked", "faulted"):
        res = ck.check_palette([a.candidate_sprite(inp, c, kind)], inp.palette)
        assert res.passed, res.details


def test_deterministic(tmp_path):
    inp = a.load_inputs()
    d1 = a.measure_all(inp)
    d2 = a.measure_all(inp)
    assert json.dumps(d1) == json.dumps(d2)
