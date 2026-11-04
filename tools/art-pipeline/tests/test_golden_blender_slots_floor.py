"""F-14 (T440 review, T470): golden hashes of the Blender-route plates and floor of F004.

The committed PNGs are the final headless-batch renders and their pixel passes. Any deliberate re-render changes these
digests and must update them here together with the family provenance and check report. The floor pixel pass is also
re-run on the committed raw render (no Blender needed) and must reproduce the committed tile byte for byte.
"""
import hashlib
import importlib.util
import json
from pathlib import Path

import pytest
from PIL import Image

ART = Path(__file__).resolve().parents[3] / "art-source" / "f004"

GOLDEN = {
    "floor/pixel/f004-floor-tile_00.png": "5184552d396fe34b757aac9fd05294e6a0fb41917a346c402c473a7710e6d425",
    "floor/raw/floor-tile.png": "e7f62bf488ce540e185387e2b44d6becdf8e90e659d600a33fb04ca7587efe8b",
    "slots/plates/f003-slot-empty/pixel/f003-slot-empty_00.png": "fe208d7ff3d40c7f9ca92412a3e289c9d5f4fbe9e65b16c937ae3de898f5ebae",
    "slots/plates/f003-slot-locked-l2/pixel/f003-slot-locked-l2_00.png": "79a0554d99f2144a3066f4efd3b5c229d1d25ab11dc6a70412ba6ec058a0177c",
    "slots/plates/f003-slot-locked-l3/pixel/f003-slot-locked-l3_00.png": "79a0554d99f2144a3066f4efd3b5c229d1d25ab11dc6a70412ba6ec058a0177c",
    "slots/plates/f003-slot-locked-l4/pixel/f003-slot-locked-l4_00.png": "79a0554d99f2144a3066f4efd3b5c229d1d25ab11dc6a70412ba6ec058a0177c",
    "slots/plates/f003-slot-locked-l5/pixel/f003-slot-locked-l5_00.png": "79a0554d99f2144a3066f4efd3b5c229d1d25ab11dc6a70412ba6ec058a0177c",
    "slots/plates/f003-slot-occupied-base/pixel/f003-slot-occupied-base_00.png": "d902d895a99f6f0384527565fb4b9e9de801e634688cdab9a75515dbaea26c6c",
    "slots/source/empty/raw/empty.png": "76a7e48e1295a249a9992c17ed419f10dfd38179519437db4eb948f898cc5830",
    "slots/source/locked/raw/locked.png": "b806fd29e3d8c7ecc126b9fac73854ad924ebfeb4ccf7d87cdf5a8301ded2304",
    "slots/source/occupied/raw/occupied.png": "7be6bb3509f8ea6b7fcc87faaf103f8051a44c4a60d71381e4a706a22fc44caf",
}


def _sha(rel):
    return hashlib.sha256((ART / rel).read_bytes()).hexdigest()


@pytest.mark.parametrize("rel", sorted(GOLDEN))
def test_committed_png_matches_golden_hash(rel):
    assert _sha(rel) == GOLDEN[rel]


def test_floor_manifest_and_provenance_hash_the_golden_tile():
    rel = "floor/pixel/f004-floor-tile_00.png"
    man = json.loads((ART / "floor" / "manifest.json").read_text(encoding="utf-8"))
    assert man["entries"][0]["frames"][0]["sha256"] == GOLDEN[rel]
    prov = json.loads((ART / "floor" / "provenance.json").read_text(encoding="utf-8"))
    assert prov["outputs"][0]["sha256"] == GOLDEN[rel]
    assert prov["rigVersion"] == "rig-v2"
    assert man["entries"][0]["canvas"] == [70, 54]


def test_floor_pixel_pass_reproduces_committed_tile(palette):
    spec = importlib.util.spec_from_file_location("pipeline_floor_t470", ART / "floor" / "pipeline_floor.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    raw = Image.open(ART / "floor" / "raw" / "floor-tile.png").convert("RGBA")
    tile = mod.cut_tile(raw, palette)
    committed = Image.open(ART / "floor" / "pixel" / "f004-floor-tile_00.png").convert("RGBA")
    assert tile.size == (70, 54)
    assert tile.tobytes() == committed.tobytes()
