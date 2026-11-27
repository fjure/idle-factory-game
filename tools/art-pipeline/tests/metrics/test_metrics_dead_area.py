"""Known-answer tests for the SC-006 dead-area script (plan review QA-8)."""
import pytest
from PIL import Image

from metrics.common import KEY_COLOUR, LayoutError
from metrics.dead_area import dead_area

WORLD = (90, 60, 40)
UI = (200, 200, 220)


def layout(**extra):
    base = {"schema": 1, "width": 100, "height": 100, "safeArea": {"x": 0, "y": 0, "w": 100, "h": 100}, "uiRects": []}
    base.update(extra)
    return base


def canvas():
    return Image.new("RGB", (100, 100), KEY_COLOUR)


def paint(img, x, y, w, h, colour):
    img.paste(Image.new("RGB", (w, h), colour), (x, y))


def test_all_key_is_fully_dead():
    r = dead_area(canvas(), layout())
    assert r["deadPx"] == 10000 and r["deadAreaShare"] == 1.0


def test_no_key_is_not_dead():
    img = Image.new("RGB", (100, 100), WORLD)
    assert dead_area(img, layout())["deadAreaShare"] == 0.0


def test_quarter_key_world_elsewhere():
    img = Image.new("RGB", (100, 100), WORLD)
    paint(img, 0, 0, 50, 50, KEY_COLOUR)  # 2500 key pixels
    r = dead_area(img, layout())
    assert r["deadPx"] == 2500 and r["deadAreaShare"] == pytest.approx(0.25)


def test_ui_rectangle_hides_key_pixels():
    img = canvas()
    r = dead_area(img, layout(uiRects=[{"name": "top", "x": 0, "y": 0, "w": 100, "h": 20}]))  # 2000 px of key under UI
    assert r["deadPx"] == 8000 and r["uiInSafePx"] == 2000
    assert r["deadAreaShare"] == pytest.approx(0.8)
    assert r["deadShareOfNonUi"] == pytest.approx(1.0)


def test_overlapping_ui_rectangles_count_once():
    ui = [{"x": 0, "y": 0, "w": 50, "h": 50}, {"x": 25, "y": 25, "w": 50, "h": 50}]  # 2500 + 2500 - 625 overlap = 4375
    r = dead_area(canvas(), layout(uiRects=ui))
    assert r["uiInSafePx"] == 4375 and r["deadPx"] == 10000 - 4375


def test_card_hidden_area_counts_as_ui():
    r = dead_area(canvas(), layout(cardHiddenRects=[{"name": "card", "x": 0, "y": 50, "w": 100, "h": 50}]))
    assert r["deadPx"] == 5000 and r["deadAreaShare"] == pytest.approx(0.5)


def test_key_outside_the_safe_area_is_not_counted():
    img = canvas()
    r = dead_area(img, layout(safeArea={"x": 10, "y": 10, "w": 80, "h": 80}))
    assert r["safeAreaPx"] == 6400 and r["deadPx"] == 6400 and r["deadAreaShare"] == 1.0
    assert r["keyPxInSafe"] == 6400


def test_near_key_colour_is_not_key():
    img = Image.new("RGB", (100, 100), (0, 254, 0))
    assert dead_area(img, layout())["deadPx"] == 0


def test_ui_covering_everything_gives_zero_dead_and_no_division_error():
    r = dead_area(canvas(), layout(uiRects=[{"x": 0, "y": 0, "w": 100, "h": 100}]))
    assert r["deadPx"] == 0 and r["deadShareOfNonUi"] == 0.0


def test_size_mismatch_is_rejected():
    with pytest.raises(ValueError):
        dead_area(Image.new("RGB", (50, 50), KEY_COLOUR), layout())


def test_bad_layout_is_rejected():
    with pytest.raises(LayoutError):
        dead_area(canvas(), {"schema": 2, "width": 100, "height": 100, "safeArea": {"x": 0, "y": 0, "w": 1, "h": 1}})
    with pytest.raises(LayoutError):
        dead_area(canvas(), layout(uiRects=[{"x": 0, "y": 0, "w": -1, "h": 5}]))
