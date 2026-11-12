"""F006 T522: LINK tiles and FX sprites (Art Bible 21.2, 21.4, 21.5, 21.7, 21.8)."""
import json
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline import checks as ck
from art_pipeline.f006 import link_fx as L
from art_pipeline.palette import PALETTE_V1_PATH, load_palette

REPO = Path(__file__).resolve().parents[3]
OUT = REPO / "art-source" / "f006"


@pytest.fixture(scope="module")
def built():
    return L.build_all(load_palette(PALETTE_V1_PATH))


@pytest.fixture(scope="module")
def palette():
    return load_palette(PALETTE_V1_PATH)


def _by_id(built):
    return {s.sprite_id: s for s in built["link"] + built["fx"]}


def test_every_check_passes(built):
    assert built["report_link"]["passed"], [(a, r["name"]) for a, rs in built["report_link"]["assets"].items() for r in rs if not r["passed"]]
    assert built["report_fx"]["passed"], [(a, r["name"]) for a, rs in built["report_fx"]["assets"].items() for r in rs if not r["passed"]]
    for rep in (built["report_link"], built["report_fx"]):
        assert all(g["passed"] for g in rep["global"]), [g["name"] for g in rep["global"] if not g["passed"]]


def test_sprite_table_21_4(built):
    sp = _by_id(built)
    assert [s.sprite_id for s in built["link"]] == ["f006-link-trunk", "f006-link-junction", "f006-link-branch", "f006-link-branch-active"]
    for s in built["link"]:
        assert len(s.frames) == 1 and s.frames[0].size == (70, 54)
    sizes = {"completion-pop": (68, 36), "branch-pip": (14, 10), "spark": (20, 16), "steam": (24, 32), "build-dust": (48, 28),
             "upgrade-glint": (24, 24)}
    for name, size in sizes.items():
        s = sp["f006-fx-" + name]
        assert len(s.frames) == 6 and s.frames[0].size == size
    hl = sp["f006-fx-completion-highlight"]
    assert len(hl.frames) == 1 and hl.frames[0].size == (68, 36) and hl.fps == 0


def test_no_lit_teal_cue_anywhere(built, palette):
    for s in built["link"] + built["fx"]:
        ids = L._ids(s.frames, palette)
        assert not [i for i in ids if i.startswith(("lit_", "teal_", "cue_", "cvd_"))], (s.sprite_id, ids)
        assert ids <= s.allowed, (s.sprite_id, ids - s.allowed)
        assert len(ids) <= L.MAX_COLOURS


def test_link_uses_only_set_r_so_the_trunk_is_ring_safe(built, palette):
    for s in built["link"]:
        ids = L._ids(s.frames, palette)
        assert ids <= L.SET_R and not [i for i in ids if i.startswith("steel_")], (s.sprite_id, ids)
    proof = next(g for g in built["report_link"]["global"] if g["name"] == "c8_ring_link_layer")
    assert proof["passed"]
    assert {r["layout"] for r in proof["details"]["rows"]} == {"3x4", "4x3", "6", "20", "L"}
    assert all(r["ringPxNotInSetR"] == 0 and r["ringSteelPx"] == 0 for r in proof["details"]["rows"])


def test_link_tiles_join_and_lie_in_the_row_gap(built):
    sp = _by_id(built)
    trunk = sp["f006-link-trunk"].frames[0]
    px = trunk.load()
    for y in range(54):                       # the trunk occupies rows 8..12 only (a = 27..31 above the machine pivot) and joins left to right
        opaque = [px[x, y][3] != 0 for x in range(70)]
        assert (all(opaque) if 8 <= y <= 12 else not any(opaque)), y
    branch = sp["f006-link-branch"].frames[0].load()
    for y in range(13, 20):                   # the branch lies at a = 20..26 on x 34..36 and nowhere else below the trunk
        assert [x for x in range(70) if branch[x, y][3]] == [34, 35, 36]
    assert not any(branch[x, y][3] for x in range(70) for y in range(20, 54))
    junction = sp["f006-link-junction"].frames[0].load()
    assert all(junction[x, y][3] for x in (34, 35, 36) for y in range(54))      # the spine runs the whole tile height
    active = sp["f006-link-branch-active"].frames[0]
    assert active.tobytes() != sp["f006-link-branch"].frames[0].tobytes()


def test_fx_keep_out_and_headroom(built):
    sp = _by_id(built)
    for name in ("completion-pop", "spark", "steam", "upgrade-glint"):
        s = sp["f006-fx-" + name]
        assert s.machine_anchored
        r = L.check_keepout(s)
        assert r.passed, (name, r.details)
        assert s.anchor is not None


def test_flash_evidence_table(built):
    ev = built["evidence"]
    assert ev["pacesFps"] == [6, 8, 10]
    assert ev["passed"] and ev["worstFlashesPerSecond"] <= 2
    caps = ev["paceCaps"]
    assert set(caps) == set(L.MACHINES)
    assert all(c["paceCapFps"] in (6, 8, 10) for c in caps.values())
    assert caps["assembler"]["paceCapFps"] <= 8
    for row in ev["rows"] + ev["stacks"]:
        assert set(row["flashes"]) == {"6", "8", "10"}


def test_flash_check_is_the_repository_check(built):
    # the composite of a family without FX has the F004 baseline (research R12: the Assembler is the near-limit family)
    frames = L._machine_frames("assembler")
    assert ck.flash_rate(frames, 8)["maxFlashesPerSecond"] <= ck.FLASH_MAX_PER_SECOND
    pop = _by_id(built)["f006-fx-completion-pop"]
    assert max(ck.flash_rate(L.composite(frames, [pop]), fps)["maxFlashesPerSecond"] for fps in (6, 8, 10)) <= L.FLASH_TARGET


def test_deterministic_and_committed_output_is_current(tmp_path, built):
    L.write_all(tmp_path, built["palette"])
    for fam in ("link", "fx"):
        for f in sorted((tmp_path / fam).rglob("*")):
            if not f.is_file():
                continue
            committed = OUT / f.relative_to(tmp_path)
            assert committed.exists(), "not committed: %s" % committed
            a, b = f.read_bytes(), committed.read_bytes()
            if f.suffix == ".json":
                a, b = a.replace(b"\r\n", b"\n"), b.replace(b"\r\n", b"\n")
            assert a == b, "stale committed output: %s (rerun python -m art_pipeline.f006.link_fx --out art-source/f006)" % committed


def test_manifest_provenance_and_stage_cost(built):
    for fam in ("link", "fx"):
        man = json.loads((OUT / fam / "manifest.json").read_text(encoding="utf-8"))
        for e in man["entries"]:
            prov = json.loads((OUT / fam / e["assetId"] / "provenance.json").read_text(encoding="utf-8"))
            assert prov["route"] == "scripted" and prov["artBibleVersion"] == "F004-PAB v1.1.0"
            assert prov["pixelPass"]["scriptSha256"] and prov["outputs"]
            assert not any(f["file"].count("lit_") for f in e["frames"])
    fx = json.loads((OUT / "fx" / "manifest.json").read_text(encoding="utf-8"))
    assert all("reducedMotion" in e for e in fx["entries"])
    still = {e["assetId"]: e["stillFrame"] for e in fx["entries"]}
    assert still["f006-fx-completion-pop"] == 3
    for e in Image.open(OUT / "fx" / "sheets" / "contact_x4.png"), Image.open(OUT / "link" / "sheets" / "contact_x3.png"):
        assert e.size[0] > 0
