"""T473 (Issue #657, FR-022): the slots pipeline works at the rectangular 70 x 54 lattice of Art Bible 3.5 and never
writes the floor family (`art-source/f004/floor/`), which `floor/pipeline_floor.py` owns. A re-run of the slots driver
must not overwrite the floor check report, manifest or provenance with 60 x 30 values."""
import ast
import importlib.util
import json
import sys
from pathlib import Path

import pytest
from PIL import Image

ART = Path(__file__).resolve().parents[3] / "art-source" / "f004"
PERIOD = (70, 54)                                   # Art Bible 3.5 (v1.0.3)
SLOTS_DRIVER = ART / "slots" / "pipeline_slots.py"
FLOOR_OUTPUT_CALLS = {"run", "render_one", "write_lf", "write_frames", "rmtree", "copy2", "unlink", "mkdir", "make_lattice_sheet", "clean",
                      "write_text", "write_bytes", "save"}


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    sys.path.insert(0, str(path.parent))
    try:
        spec.loader.exec_module(mod)
    finally:
        sys.path.remove(str(path.parent))
    return mod


@pytest.fixture(scope="module")
def cf():
    return _load("cross_family_t473", ART / "slots" / "cross_family.py")


@pytest.fixture(scope="module")
def fl():
    return _load("pipeline_floor_t473", ART / "floor" / "pipeline_floor.py")


def test_shared_lattice_is_the_art_bible_period(cf, fl):
    assert fl.PERIOD == PERIOD
    assert cf.LATTICE == ((PERIOD[0], 0), (0, PERIOD[1]))
    assert cf.LATTICE == fl.LATTICE
    assert tuple(cf.PHASES) == tuple(fl.PHASES)
    assert (0, 0) in cf.PHASES and (PERIOD[0] // 2, PERIOD[1] // 2) in cf.PHASES


def test_lattice_floor_places_tiles_on_the_rectangular_pitch(cf):
    tile = Image.new("RGBA", PERIOD, (0, 0, 0, 0))
    tile.putpixel((PERIOD[0] // 2, PERIOD[1] // 2), (255, 0, 0, 255))          # one marker at the tile centre
    for phase in cf.PHASES:
        got = cf.lattice_floor(tile, phase)
        px = {(x, y) for y in range(got.size[1]) for x in range(got.size[0]) if got.getpixel((x, y))[3]}
        cx, cy = cf.CENTRE[0] + phase[0], cf.CENTRE[1] + phase[1]
        want = {(cx + a * PERIOD[0], cy + b * PERIOD[1]) for a in range(-3, 4) for b in range(-3, 4)
                if 0 <= cx + a * PERIOD[0] < got.size[0] and 0 <= cy + b * PERIOD[1] < got.size[1]}
        assert px == want and px


def test_floor_driver_does_not_patch_the_shared_module():
    src = (ART / "floor" / "pipeline_floor.py").read_text(encoding="utf-8")
    assert "cf.lattice_floor =" not in src and "cf.PHASES =" not in src and "rect_lattice_floor" not in src


def test_slots_driver_has_no_60x30_lattice():
    for p in (SLOTS_DRIVER, ART / "slots" / "cross_family.py"):
        src = p.read_text(encoding="utf-8")
        for bad in ("[60, 30]", "60 x 30", "(30, 15)", "(30, -15)", "latticePeriodPx"):
            assert bad not in src, "%s still contains %r" % (p.name, bad)


def test_slots_driver_writes_no_floor_output():
    tree = ast.parse(SLOTS_DRIVER.read_text(encoding="utf-8"))
    offenders = []
    for node in ast.walk(tree):
        if not isinstance(node, ast.Call):
            continue
        fn = node.func
        name = fn.attr if isinstance(fn, ast.Attribute) else getattr(fn, "id", "")
        if name not in FLOOR_OUTPUT_CALLS:
            continue
        text = ast.unparse(node)
        if "floor" in text.lower() or "fl." in text:
            offenders.append(text)
    assert not offenders, offenders
    # the floor family is not rendered or checked here either
    src = SLOTS_DRIVER.read_text(encoding="utf-8")
    for bad in ("fl.cut_tile", "fl.tiling_checks", "fl.clean", '"floor-tile"'):
        assert bad not in src, bad


def test_floor_cross_checks_reproduce_the_committed_floor_report(fl, palette):
    tile = Image.open(ART / "floor" / "pixel" / "f004-floor-tile_00.png").convert("RGBA")
    cross, _ = fl.cross_checks(tile, palette)
    report = json.loads((ART / "floor" / "check-report.json").read_text(encoding="utf-8"))
    committed = {c["name"]: c["passed"] for c in report["global"]}
    assert {c.name: c.passed for c in cross} == {n: committed[n] for n in (c.name for c in cross)}
    assert all(c.passed for c in cross)
