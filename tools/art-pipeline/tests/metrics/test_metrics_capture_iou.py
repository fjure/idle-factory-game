"""Known-answer tests for the SC-003 silhouette IoU script (plan review QA-8)."""
import pytest
from PIL import Image

from metrics.capture_iou import capture_iou, pairwise_iou, silhouettes
from metrics.common import KEY_COLOUR

BODY = (150, 150, 160)


def rect(x, y, w, h):
    return {"x": x, "y": y, "w": w, "h": h}


def mask_of(w, h, boxes):
    img = Image.new("L", (w, h), 0)
    for x, y, bw, bh in boxes:
        img.paste(255, (x, y, x + bw, y + bh))
    return img


def test_identical_silhouettes_have_iou_one():
    a = mask_of(10, 10, [(0, 0, 10, 10)])
    r = pairwise_iou({"a": a, "b": a.copy()})
    assert r["pairs"][0]["full"] == 1.0 and not r["passesLimit"]


def test_disjoint_silhouettes_have_iou_zero():
    a = mask_of(20, 10, [(0, 0, 10, 10)])
    b = mask_of(20, 10, [(10, 0, 10, 10)])
    assert pairwise_iou({"a": a, "b": b})["pairs"][0]["full"] == 0.0


def test_half_overlap_is_one_third():
    # a = x 0..9, b = x 5..14 on a 15 x 10 canvas: intersection 50, union 150.
    a = mask_of(15, 10, [(0, 0, 10, 10)])
    b = mask_of(15, 10, [(5, 0, 10, 10)])
    assert pairwise_iou({"a": a, "b": b})["pairs"][0]["full"] == pytest.approx(0.3333, abs=1e-4)


def test_different_sizes_are_aligned_at_the_bottom_centre():
    a = mask_of(10, 10, [(0, 0, 10, 10)])
    b = mask_of(10, 4, [(0, 0, 10, 4)])  # a 10 x 4 slab sits on the bottom of the 10 x 10 square: IoU 40 / 100
    assert pairwise_iou({"a": a, "b": b})["pairs"][0]["full"] == pytest.approx(0.4)


def test_limit_is_zero_point_seven():
    a = mask_of(10, 10, [(0, 0, 10, 10)])
    b = mask_of(10, 10, [(0, 0, 10, 7)])  # IoU 0.7 exactly passes (the limit is inclusive)
    r = pairwise_iou({"a": a, "b": b})
    assert r["pairs"][0]["full"] == pytest.approx(0.7) and r["passesLimit"]


def test_silhouettes_are_cut_from_the_capture_per_machine_type():
    img = Image.new("RGB", (60, 30), KEY_COLOUR)
    img.paste(Image.new("RGB", (10, 20), BODY), (2, 5))    # miner: tall
    img.paste(Image.new("RGB", (20, 10), BODY), (30, 10))  # smelter: wide
    img.paste(Image.new("RGB", (10, 20), BODY), (45, 5))   # a second miner is ignored
    layout = {"schema": 1, "width": 60, "height": 30, "safeArea": rect(0, 0, 60, 30), "machines": [
        {"slot": 0, "machine": "miner", "cell": rect(0, 0, 25, 30), "body": rect(0, 0, 25, 30)},
        {"slot": 1, "machine": "smelter", "cell": rect(28, 0, 17, 30), "body": rect(28, 0, 17, 30)},
        {"slot": 2, "machine": "miner", "cell": rect(43, 0, 17, 30), "body": rect(43, 0, 17, 30)},
    ]}
    s = silhouettes(img, layout)
    assert sorted(s) == ["miner", "smelter"] and s["miner"].size == (10, 20)
    # The smelter body is clipped by its bounds to x 28..44 (15 columns), so the cut-out is 15 x 10.
    assert s["smelter"].size == (15, 10)
    r = capture_iou(img, layout)
    assert r["types"] == ["miner", "smelter"] and len(r["pairs"]) == 1


def test_narrow_silhouette_is_centred_not_left_aligned():
    # a: 10 x 10 canvas with only x 0..4 set; b: a 4 wide slab. Centred, b sits at x 3..6 and overlaps 2 columns (20 px): IoU 20 / (50 + 40 - 20).
    a = mask_of(10, 10, [(0, 0, 5, 10)])
    b = mask_of(4, 10, [(0, 0, 4, 10)])
    assert pairwise_iou({"a": a, "b": b})["pairs"][0]["full"] == pytest.approx(20 / 70, abs=1e-4)
