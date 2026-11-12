"""F006 T519: the committed GROUND and FOUNDATION families (art-source/f006) obey the Art Bible 21.2 allowance and 21.4 table."""
import hashlib
import json
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline.palette import PALETTE_V1_PATH, load_palette

ART = Path(__file__).resolve().parents[3] / "art-source" / "f006"
ALLOW = {
    "f006-ground-base": {"floor_0"},
    "f006-ground-deck": {"floor_0", "floor_1"},
    "f006-ground-hatch": {"ink_0", "ink_1", "dim_0", "floor_0", "floor_1"},
    "f006-foundation-outline": {"ink_0", "ink_1", "floor_0", "floor_1"},
    "f006-foundation-flat": {"plate_1", "plate_2", "ink_0", "floor_0", "floor_1"},
}
CANVAS = {"ground": (70, 54), "foundation": (70, 36)}


def _assets():
    return [(a, "ground" if "ground" in a else "foundation") for a in sorted(ALLOW)]


@pytest.mark.parametrize("asset,fam", _assets())
def test_sprite_allowance_canvas_and_hash(asset, fam):
    palette = load_palette(PALETTE_V1_PATH)
    png = ART / fam / asset / (asset + "_00.png")
    img = Image.open(png).convert("RGBA")
    assert img.size == CANVAS[fam]
    ids = set()
    for p in img.getdata():
        assert p[3] in (0, 255)
        if p[3]:
            ids.add(palette.id_of(tuple(p[:3])))
    assert ids <= ALLOW[asset], ids - ALLOW[asset]
    assert not any(i.startswith(("steel_", "teal_", "lit_", "floor_2")) for i in ids)
    assert len(ids) <= 12
    man = json.loads((ART / fam / "manifest.json").read_text(encoding="utf-8"))
    entry = next(e for e in man["entries"] if e["assetId"] == asset)
    assert entry["frames"][0]["sha256"] == hashlib.sha256(png.read_bytes()).hexdigest()
    prov = json.loads((ART / fam / asset / "provenance.json").read_text(encoding="utf-8"))
    assert prov["artBibleVersion"] == "F004-PAB v1.1.0" and prov["route"] == "blender"


@pytest.mark.parametrize("fam", ["ground", "foundation"])
def test_family_check_report_passed(fam):
    report = json.loads((ART / fam / "check-report.json").read_text(encoding="utf-8"))
    assert report["passed"] is True


def test_ground_tiles_are_opaque_and_foundations_are_flat():
    for a in ("f006-ground-base", "f006-ground-deck", "f006-ground-hatch"):
        img = Image.open(ART / "ground" / a / (a + "_00.png")).convert("RGBA")
        assert all(p[3] == 255 for p in img.getdata())
    flat = Image.open(ART / "foundation" / "f006-foundation-flat" / "f006-foundation-flat_00.png").convert("RGBA")
    assert flat.getchannel("A").getbbox() == (0, 1, 70, 36)
