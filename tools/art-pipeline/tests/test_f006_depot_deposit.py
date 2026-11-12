"""F006 T520: the committed DEPOT and DEPOSIT families (art-source/f006) obey the Art Bible 21.2 allowance and 21.4 table."""
import hashlib
import json
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline import checks
from art_pipeline.palette import PALETTE_V1_PATH, load_palette

ART = Path(__file__).resolve().parents[3] / "art-source" / "f006"
DEPOT_ALLOW = {"ink_0", "ink_1", "floor_1", "steel_0", "steel_1", "steel_2", "steel_3", "copper_0", "copper_1", "copper_2",
               "dim_0", "dim_1", "res_ore_0", "res_ore_1", "res_coal"}
DEPOSIT_ALLOW = {"ink_0", "res_ore_0", "res_ore_1", "res_coal", "floor_1", "steel_3"}
CHIP_ALLOW = {"res_ore_1", "res_ore_0", "steel_3", "ink_0"}
DEPOTS = ["f006-depot-%s-%s" % (s, f) for s in ("s1", "s2", "s3") for f in ("empty", "low", "high", "full")]
DEPOSITS = ["f006-deposit-rest-s1", "f006-deposit-rest-s2", "f006-deposit-rest-s3", "f006-deposit-highlight"]
CHIP = "f006-fx-vein-chip"
FORBIDDEN = ("lit_", "teal_", "plate_", "cue_", "cvd_")


def _ids(img, palette):
    ids = set()
    for p in img.getdata():
        assert p[3] in (0, 255)
        if p[3]:
            ids.add(palette.id_of(tuple(p[:3])))
    return ids


def _check(fam, asset, allow, canvas, count):
    palette = load_palette(PALETTE_V1_PATH)
    man = json.loads((ART / fam / "manifest.json").read_text(encoding="utf-8"))
    entry = next(e for e in man["entries"] if e["assetId"] == asset)
    assert len(entry["frames"]) == count and tuple(entry["canvas"]) == canvas
    for fr in entry["frames"]:
        png = ART / fam / fr["file"]
        assert fr["sha256"] == hashlib.sha256(png.read_bytes()).hexdigest()
        img = Image.open(png).convert("RGBA")
        assert img.size == canvas
        ids = _ids(img, palette)
        assert ids <= allow, ids - allow
        assert not any(k.startswith(FORBIDDEN) for k in ids)
        assert len(ids) <= 12
    prov = json.loads((ART / fam / asset / "provenance.json").read_text(encoding="utf-8"))
    assert prov["artBibleVersion"] == "F004-PAB v1.1.0" and prov["route"] == "blender"
    return entry


@pytest.mark.parametrize("asset", DEPOTS)
def test_depot_sprite(asset):
    _check("depot", asset, DEPOT_ALLOW, (80, 76), 1)
    img = Image.open(ART / "depot" / asset / (asset + "_00.png")).convert("RGBA")
    assert img.getchannel("A").getbbox()[1] >= 2, "lamp tip needs 2 px of headroom (Art Bible 21.7)"


@pytest.mark.parametrize("asset", DEPOSITS)
def test_deposit_sprite(asset):
    _check("deposit", asset, DEPOSIT_ALLOW, (48, 34), 1)


def test_chip_loop_and_flash():
    entry = _check("deposit", CHIP, CHIP_ALLOW, (48, 34), 6)
    frames = [Image.open(ART / "deposit" / fr["file"]).convert("RGBA") for fr in entry["frames"]]
    assert entry["fps"] == 8 and entry["reducedMotionStill"] == "f006-deposit-highlight"
    assert checks.check_flash(frames, 8).passed


@pytest.mark.parametrize("fam", ["depot", "deposit"])
def test_family_check_report_passed(fam):
    assert json.loads((ART / fam / "check-report.json").read_text(encoding="utf-8"))["passed"] is True
