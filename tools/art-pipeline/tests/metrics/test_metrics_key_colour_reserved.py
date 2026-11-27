"""The reserved key colour never appears in art (plan review QA-8). Every PNG under art-source/ and Assets/ is scanned for an exact match."""
from pathlib import Path

from PIL import Image

from metrics.common import KEY_COLOUR

REPO = Path(__file__).resolve().parents[4]


def test_key_colour_is_pure_green():
    assert KEY_COLOUR == (0, 255, 0)


def test_no_art_png_contains_the_key_colour():
    offenders = []
    scanned = 0
    for root in ("art-source", "Assets"):
        for path in sorted((REPO / root).rglob("*.png")):
            if "reviews" in path.parts:
                continue
            scanned += 1
            img = Image.open(path).convert("RGBA")
            colours = img.getcolors(img.width * img.height) or []
            if any(c[:3] == KEY_COLOUR and c[3] > 0 for _, c in colours):
                offenders.append(str(path.relative_to(REPO)))
    assert scanned > 100, "the scan found no art; the repository layout changed"
    assert not offenders, f"the key colour {KEY_COLOUR} appears in art: {offenders[:10]}"
