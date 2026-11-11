"""F006 T506 concept sprites: deterministic, palette-exact, lattice-sized, flash-safe."""
import json

import pytest
from PIL import Image

from art_pipeline import concept_f006 as c
from art_pipeline.palette import PALETTE_V1_PATH, load_palette

IDS = ["deck-tile", "hatch-tile", "edge-tile", "depot", "vein", "prop-crate", "branch-pip", "trunk-branch",
       "completion-pop"]


@pytest.fixture(scope="module")
def pal():
    return load_palette(PALETTE_V1_PATH)


@pytest.mark.parametrize("key", ["a", "b"])
def test_all_checks_pass_and_inventory(pal, key):
    sprites = {s.sprite_id: s for s in c.build_direction(pal, key)}
    for need in IDS:
        assert need in sprites
    assert len([i for i in sprites if i.startswith("prop-")]) == 2
    for s in sprites.values():
        for r in c.check_sprite(s, pal):
            assert r.passed, (s.sprite_id, r.name, r.details)


@pytest.mark.parametrize("key", ["a", "b"])
def test_lattice_sizes(pal, key):
    sp = {s.sprite_id: s for s in c.build_direction(pal, key)}
    for name in ("deck-tile", "hatch-tile", "trunk-branch"):
        assert sp[name].frames[0].size == c.PITCH
    assert len(sp["branch-pip"].frames) == 6 and len(sp["completion-pop"].frames) == 6


def test_byte_identical_rerun(tmp_path, pal):
    c.write_all(tmp_path / "1")
    c.write_all(tmp_path / "2")
    for p in (tmp_path / "1").rglob("*.png"):
        assert p.read_bytes() == (tmp_path / "2" / p.relative_to(tmp_path / "1")).read_bytes()
    m = json.loads((tmp_path / "1" / "direction-a" / "manifest.json").read_text(encoding="utf-8"))
    assert m["worldBible"] == "F006-WB v0.1" and m["artBible"] == "F004-PAB v1.0.4"
