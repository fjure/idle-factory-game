"""palette-v1.json is the frozen Art Bible v1.0 section 5 palette (T462, owner decision R9)."""
import json
import re
from pathlib import Path

from art_pipeline.checks import check_palette_definition
from art_pipeline.color import contrast_ratio
from art_pipeline.palette import DEFAULT_PALETTE_PATH, PALETTE_V1_PATH, load_palette, validate_palette

REPO = Path(__file__).resolve().parents[3]
BIBLE = REPO / "docs" / "art" / "F004-art-bible-v1.md"


def _bible_section5():
    text = BIBLE.read_text(encoding="utf-8")
    sec = text.split("## 5. Palette v1", 1)[1].split("**Family allowances**", 1)[0]
    groups = {}
    for row in re.finditer(r"^\| (\w+) \((\d+)(?:, reserved)?\) \| (.+?) \|", sec, re.M):
        gid, budget, body = row.group(1), int(row.group(2)), row.group(3)
        groups[gid] = (budget, re.findall(r"`(\w+)` (#[0-9a-fA-F]{6})", body))
    return groups


def test_palette_v1_matches_art_bible_section_5():
    groups = _bible_section5()
    assert set(groups) == {"ink", "machine", "floor", "resources", "ui", "state_cues", "cvd_safe"}
    doc = json.loads(PALETTE_V1_PATH.read_text(encoding="utf-8"))
    assert doc["version"] == "1.0.0" and doc["paletteId"] == "f004-palette" and doc["cap"] == 48
    assert [g["id"] for g in doc["groups"]] == list(groups)  # group order preserved
    for g in doc["groups"]:
        budget, colors = groups[g["id"]]
        assert g["budget"] == budget
        assert [(c["id"], c["hex"].lower()) for c in g["colors"]] == [(i, h.lower()) for i, h in colors], g["id"]


def test_palette_v1_records_its_source_and_equals_v0_values():
    doc = json.loads(PALETTE_V1_PATH.read_text(encoding="utf-8"))
    assert doc["source"]["document"] == "docs/art/F004-art-bible-v1.md" and doc["source"]["section"].startswith("5.")
    assert doc["source"]["derivedFromSha256"] == "2d2ca24f42242bf5dacdd9d842fd94ee67dea4a820944e2bdb6539604f004afb"
    v0, v1 = load_palette(DEFAULT_PALETTE_PATH), load_palette(PALETTE_V1_PATH)
    assert [(c.id, c.rgb, c.group, c.emissive) for c in v0.colors] == [(c.id, c.rgb, c.group, c.emissive) for c in v1.colors]


def test_palette_v1_is_valid_and_at_cap():
    p = load_palette(PALETTE_V1_PATH)
    assert p.version == "1.0.0" and len(p.colors) == 48 and p.cap == 48
    assert validate_palette(p) == [] and check_palette_definition(p).passed
    assert {c.id for c in p.colors if c.emissive} == {"lit_0", "lit_1"}


def test_c1_overlay_backdrop_set_derives_from_the_palette():
    """Section 9.4 rule C1: T is allowed when, for all five badge fills and both marker fills,
    max(ratio(ink_0, T), ratio(fill, T)) >= 3. The forbidden eight are the tones where one of the seven fails."""
    from art_pipeline.checks_v1 import A_OV
    p = load_palette(PALETTE_V1_PATH)
    fills = ["cue_warn", "cue_alert", "cue_info", "cue_neutral", "cue_dim", "cvd_green", "cvd_pink"]
    ink = p.rgb("ink_0")
    allowed = {c.id for c in p.colors if all(max(contrast_ratio(ink, c.rgb), contrast_ratio(p.rgb(f), c.rgb)) >= 3
                                              for f in fills)}
    forbidden = {"steel_0", "copper_0", "teal_0", "dim_1", "warn_0", "floor_2", "plate_0", "res_ore_0"}
    assert forbidden.isdisjoint(A_OV)
    assert forbidden <= {c.id for c in p.colors} - allowed
    assert set(A_OV) <= allowed
