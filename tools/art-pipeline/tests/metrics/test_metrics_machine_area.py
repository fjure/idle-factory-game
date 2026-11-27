"""Known-answer tests for the SC-004 machine-area script (plan review QA-8)."""
import pytest
from PIL import Image

from metrics.common import KEY_COLOUR
from metrics.machine_area import machine_areas

BODY = (120, 120, 130)


def mask(size=(200, 100)):
    return Image.new("RGB", size, KEY_COLOUR)


def paint(img, x, y, w, h):
    img.paste(Image.new("RGB", (w, h), BODY), (x, y))


def layout(machines, width=200, height=100):
    return {"schema": 1, "width": width, "height": height, "safeArea": {"x": 0, "y": 0, "w": width, "h": height}, "machines": machines}


def rect(x, y, w, h):
    return {"x": x, "y": y, "w": w, "h": h}


def test_body_area_is_the_pixel_count_of_the_body():
    img = mask()
    paint(img, 10, 10, 20, 30)  # 600 px
    r = machine_areas(img, layout([{"slot": 0, "machine": "miner", "cell": rect(5, 5, 40, 50), "body": rect(8, 8, 25, 35)}]))
    assert r["machines"][0]["bodyAreaPx"] == 600 and r["machines"][0]["overhangPx"] == 0


def test_overhang_beyond_the_cell_is_reported_separately():
    img = mask()
    paint(img, 10, 10, 20, 30)  # 600 px; the cell covers x 10..24, so x 25..29 (5 columns x 30 rows = 150 px) overhangs
    r = machine_areas(img, layout([{"slot": 0, "machine": "miner", "cell": rect(10, 10, 15, 30), "body": rect(8, 8, 25, 35)}]))
    assert r["machines"][0]["bodyAreaPx"] == 600 and r["machines"][0]["overhangPx"] == 150


def test_median_of_three_machines():
    img = mask()
    paint(img, 0, 0, 10, 10)    # 100
    paint(img, 50, 0, 20, 10)   # 200
    paint(img, 100, 0, 30, 30)  # 900
    ms = [
        {"slot": 0, "machine": "miner", "cell": rect(0, 0, 40, 40), "body": rect(0, 0, 12, 12)},
        {"slot": 1, "machine": "smelter", "cell": rect(45, 0, 40, 40), "body": rect(48, 0, 24, 12)},
        {"slot": 2, "machine": "press", "cell": rect(95, 0, 40, 40), "body": rect(98, 0, 34, 34)},
    ]
    r = machine_areas(img, layout(ms))
    assert [m["bodyAreaPx"] for m in r["machines"]] == [100, 200, 900]
    assert r["medianBodyAreaPx"] == 200 and r["machineCount"] == 3


def test_median_of_an_even_count_is_the_mean_of_the_middle_pair():
    img = mask()
    paint(img, 0, 0, 10, 10)   # 100
    paint(img, 50, 0, 10, 30)  # 300
    ms = [
        {"slot": 0, "machine": "miner", "cell": rect(0, 0, 40, 40), "body": rect(0, 0, 12, 12)},
        {"slot": 1, "machine": "smelter", "cell": rect(45, 0, 40, 40), "body": rect(48, 0, 14, 34)},
    ]
    assert machine_areas(img, layout(ms))["medianBodyAreaPx"] == 200


def test_shadow_is_absent_from_the_mask_so_it_is_not_counted():
    # The mask frame never draws a shadow: anything the rig draws as shadow is key colour here. Paint only the body.
    img = mask()
    paint(img, 10, 10, 10, 10)
    r = machine_areas(img, layout([{"slot": 0, "machine": "miner", "cell": rect(0, 0, 40, 40), "body": rect(0, 0, 40, 40)}]))
    assert r["machines"][0]["bodyAreaPx"] == 100


def test_pixels_inside_two_bounds_are_ambiguous_and_counted_for_neither():
    img = mask()
    paint(img, 10, 10, 20, 10)  # 200 px; x 20..29 lies inside both bounds
    ms = [
        {"slot": 0, "machine": "miner", "cell": rect(0, 0, 40, 40), "body": rect(8, 8, 15, 15)},   # x 8..22
        {"slot": 1, "machine": "smelter", "cell": rect(20, 0, 40, 40), "body": rect(20, 8, 15, 15)},  # x 20..34
    ]
    r = machine_areas(img, layout(ms))
    assert r["machines"][0]["ambiguousPx"] == 30 and r["machines"][1]["ambiguousPx"] == 30  # x 20..22 (3) x 10 rows
    assert r["machines"][0]["bodyAreaPx"] == 100  # x 10..19 only
    assert r["machines"][1]["bodyAreaPx"] == 70    # x 23..29 only
    assert sum(m["bodyAreaPx"] for m in r["machines"]) + r["machines"][0]["ambiguousPx"] == 200


def test_machine_with_nothing_drawn_is_listed_as_empty():
    r = machine_areas(mask(), layout([{"slot": 3, "machine": "miner", "cell": rect(0, 0, 10, 10), "body": rect(0, 0, 10, 10)}]))
    assert r["emptyBodies"] == [3] and r["medianBodyAreaPx"] == 0


def test_size_mismatch_is_rejected():
    with pytest.raises(ValueError):
        machine_areas(mask((10, 10)), layout([]))
