"""F006 T521: the committed PROP and EDGE families (art-source/f006) obey the Art Bible 21.2 allowance and the 21.4 table, the stage
rules of 21.1 and the keep-out proof of 21.5 (source level)."""
import hashlib
import json
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline.palette import PALETTE_V1_PATH, load_palette

ART = Path(__file__).resolve().parents[3] / "art-source" / "f006"
RAILING = {"ink_0", "ink_1", "floor_0", "steel_1", "steel_2", "steel_3"}
WALL = {"ink_0", "ink_1", "floor_0", "floor_1", "steel_0", "steel_1", "steel_2"}
GANTRY = {"ink_0", "ink_1", "floor_0", "steel_0", "steel_1", "steel_2", "steel_3", "dim_0", "copper_1"}
PROP = {"ink_0", "ink_1", "steel_0", "steel_1", "steel_2", "steel_3", "copper_0", "copper_1", "copper_2", "dim_0", "dim_1"}
SPRITES = {}
for _s in ("fl", "fr", "corner", "quiet-fl", "quiet-fr"):
    SPRITES["f006-edge-railing-" + _s] = ((70, 66), RAILING)
for _s in ("fl", "fr", "corner"):
    SPRITES["f006-edge-wall-" + _s] = ((70, 78), WALL)
    SPRITES["f006-edge-gantry-" + _s] = ((70, 84), GANTRY)
SPRITES.update({"f006-prop-crate-small": ((24, 20), PROP), "f006-prop-crate-stack": ((30, 24), PROP),
                "f006-prop-lamp-post": ((14, 24), PROP), "f006-prop-pallet": ((30, 16), PROP)})


def _fam(asset):
    return "edge" if "-edge-" in asset else "prop"


def _img(asset):
    return Image.open(ART / _fam(asset) / asset / (asset + "_00.png")).convert("RGBA")


def test_table_21_4_is_complete():
    assert len(SPRITES) == 15
    assert sum(1 for a in SPRITES if "-edge-" in a) == 11 and sum(1 for a in SPRITES if "-prop-" in a) == 4


@pytest.mark.parametrize("asset", sorted(SPRITES))
def test_sprite_allowance_canvas_and_hash(asset):
    canvas, allow = SPRITES[asset]
    palette = load_palette(PALETTE_V1_PATH)
    img = _img(asset)
    assert img.size == canvas
    ids = {}
    for p in img.getdata():
        assert p[3] in (0, 255)
        if p[3]:
            i = palette.id_of(tuple(p[:3]))
            ids[i] = ids.get(i, 0) + 1
    assert set(ids) <= allow, set(ids) - allow
    assert not any(i.startswith(("lit_", "teal_", "plate_", "floor_2")) for i in ids)
    assert len(ids) <= 12
    man = json.loads((ART / _fam(asset) / "manifest.json").read_text(encoding="utf-8"))
    entry = next(e for e in man["entries"] if e["assetId"] == asset)
    png = ART / _fam(asset) / asset / (asset + "_00.png")
    assert entry["frames"][0]["sha256"] == hashlib.sha256(png.read_bytes()).hexdigest()
    prov = json.loads((ART / _fam(asset) / asset / "provenance.json").read_text(encoding="utf-8"))
    assert prov["artBibleVersion"] == "F004-PAB v1.1.0" and prov["route"] == "blender" and prov["worldBibleVersion"] == "F006-WB v0.1"
    assert prov["briefVersion"] == "F006-PE v0.1" and prov["interactiveSteps"] and prov["license"]
    assert (ART / _fam(asset) / "model" / (prov["sources"][2]["path"].rsplit("/", 1)[1])).exists()


@pytest.mark.parametrize("fam", ["prop", "edge"])
def test_family_check_report_passed(fam):
    report = json.loads((ART / fam / "check-report.json").read_text(encoding="utf-8"))
    assert report["passed"] is True


@pytest.mark.parametrize("asset", [a for a in SPRITES if "-prop-" in a])
def test_props_are_at_most_24_art_px_tall(asset):
    bb = _img(asset).getchannel("A").getbbox()
    assert bb[3] - bb[1] <= 24
    ids = {tuple(p[:3]) for p in _img(asset).getdata() if p[3]}
    palette = load_palette(PALETTE_V1_PATH)
    assert not any(palette.id_of(c).startswith(("teal_", "lit_")) for c in ids)


def test_lamp_has_copper_2_head_and_steel_3_cap_and_no_lit():
    palette = load_palette(PALETTE_V1_PATH)
    ids = {palette.id_of(tuple(p[:3])) for p in _img("f006-prop-lamp-post").getdata() if p[3]}
    assert {"copper_2", "steel_3"} <= ids
    assert not any(i.startswith(("lit_", "teal_")) for i in ids)


def _steel_px(asset):
    palette = load_palette(PALETTE_V1_PATH)
    return sum(1 for p in _img(asset).getdata() if p[3] and palette.id_of(tuple(p[:3])).startswith("steel_"))


def test_quiet_railing_has_fewer_posts():
    assert _steel_px("f006-edge-railing-quiet-fl") < _steel_px("f006-edge-railing-fl")
    assert _steel_px("f006-edge-railing-quiet-fr") < _steel_px("f006-edge-railing-fr")


def test_stage_one_railing_has_no_wall_or_gantry_tones():
    """No wall in stage 1 (Art Bible 21.5): the railing sprites carry no wall-only or gantry-only tone (steel_0, floor_1, dim_0, copper_1)."""
    palette = load_palette(PALETTE_V1_PATH)
    for asset in SPRITES:
        if "-railing-" in asset:
            ids = {palette.id_of(tuple(p[:3])) for p in _img(asset).getdata() if p[3]}
            assert not ids & {"steel_0", "floor_1", "dim_0", "copper_1"}, asset


def test_stage_triptych_sheet_exists():
    sheet = Image.open(ART / "edge" / "sheets" / "edge-stage-triptych.png")
    assert sheet.width > 1000


def test_keepout_proof_front_margin_clear_for_all_layouts():
    proof = json.loads((ART / "edge" / "keepout-proof.json").read_text(encoding="utf-8"))
    assert set(proof["layouts"]) == {"3x4", "4x3", "6", "20", "L"}
    for name, lay in proof["layouts"].items():
        front = lay["sides"]["front"]
        assert front["allClear"] and front["wallGantryClear"], name
        for asset, v in front["sprites"].items():
            assert all(n == 0 for n in v["overlapPx"].values()), (name, asset)
            assert v["overlapPx"]["ringK0"] == 0 and v["overlapPx"]["ringK4"] == 0
        # a wall or gantry that overlaps a mask on an edge is not drawn on that edge (21.8 item 1): the back row fails
        assert not lay["sides"]["back"]["wallGantryClear"], name
    assert proof["summary"]["frontMarginRowClearForAllLayouts"] is True
