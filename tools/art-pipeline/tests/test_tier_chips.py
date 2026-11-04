"""T464: scripted production of the three item tier chips (F004-AB v1.0 rows 27 to 29)."""
import hashlib
import json
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline import checks as ck
from art_pipeline import pixel_pass as pp
from art_pipeline import tier_chips as tc
from art_pipeline.palette import PALETTE_V1_PATH, load_palette
from art_pipeline.provenance import missing_provenance_fields

REPO = Path(__file__).resolve().parents[3]
ITEMS = REPO / "art-source" / "f004" / "items"
BRIEF = REPO / "docs" / "art" / "F004-asset-briefs-v1.md"


@pytest.fixture(scope="module")
def pal():
    return load_palette(PALETTE_V1_PATH)


@pytest.fixture(scope="module")
def build(pal):
    return tc.build_all(pal)


def test_inventory_equals_the_brief_rows():
    text = BRIEF.read_text(encoding="utf-8")
    rows = [l for l in text.splitlines() if l.startswith("| 2") and "F003-ITEM-TIER-" in l]
    assert len(rows) == 3
    for row, (aid, (key, tier, brief_row)) in zip(rows, tc.CHIPS.items()):
        cells = [c.strip() for c in row.strip("|").split("|")]
        assert cells[0] == str(brief_row) and cells[1] == "`%s`" % key
        assert cells[4] == "32 x 16" and cells[6] == "(16, 8)" and cells[7] == "scripted" and cells[8] == "items"


def test_every_check_passes(build):
    report = build["report"]
    failing = [(n, r["name"]) for n, rs in report["assets"].items() for r in rs if not r["passed"]]
    failing += [("global", r["name"]) for r in report["global"] if not r["passed"]]
    assert not failing, failing
    assert len(report["assets"]) == 3


def test_required_checks_run_on_every_chip(build):
    need = {"palette", "palette_group_budget", "canvas", "pivot", "alpha", "outline", "frames", "tier_cue_geometry",
            "contrast_internal", "c6_ui_surface_outline_contrast", "reproduction",
            "not_applicable[overlay_backdrop]", "not_applicable[overlay_keepout]", "not_applicable[world_floor_contrast]"}
    for rs in build["report"]["assets"].values():
        names = {r["name"] for r in rs}
        assert need <= names
        for r in rs:
            if r["name"].startswith("not_applicable"):
                assert r["details"]["reason"]


def test_canvas_pivot_and_single_frame(build, pal):
    for c in build["chips"]:
        assert c.image.size == (32, 16)
        e = build["entries"][c.asset_id]
        assert tuple(ck.pivot_px(e)) == (16.0, 8.0)
        assert e["canvas"] == [32, 16] and len(e["frames"]) == 1


def test_tier_cue_matches_the_item_frames(build):
    import item_frames as fr
    for c in build["chips"]:
        t = fr.TIERS[c.tier]
        e = build["entries"][c.asset_id]
        assert e["tierCue"]["frame"] == t["shape"] and e["tierCue"]["pips"] == t["pips"] and e["tierCue"]["numeral"] == t["numeral"]
        assert c.meta["pipCells"] == 4 * t["pips"] and c.meta["pipsOnBar"]
        ring = tc.fr.ring(tc.badge_mask(t["shape"]))
        px = c.image.load()
        pal = build["palette"]
        assert all(px[x + 1, y + 1][:3] == pal.rgb(t["frame"]) for x, y in ring)   # +1: the outline offset
    assert tc.fr is fr   # one glyph source: item_frames is imported, not copied


def test_tiers_are_pairwise_different(build):
    imgs = [c.image for c in build["chips"]]
    for i in range(3):
        for j in range(i + 1, 3):
            assert list(imgs[i].getdata()) != list(imgs[j].getdata())
    glob = {r["name"]: r for r in build["report"]["global"]}
    assert glob["tier_cue_triple_redundant"]["passed"]


def test_rendering_is_deterministic(pal, build):
    again = tc.build_chips(pal)
    for a, b in zip(build["chips"], again):
        assert pp.pixel_hash(a.image) == pp.pixel_hash(b.image)
        assert pp.png_bytes(a.image) == pp.png_bytes(b.image)


def test_committed_outputs_equal_a_rerun(build):
    """The committed art-source files equal the generator output (PNG bytes, manifest and reports)."""
    for c in build["chips"]:
        committed = ITEMS / c.asset_id / "pixel" / ("%s_00.png" % c.asset_id)
        assert committed.read_bytes() == pp.png_bytes(c.image), c.asset_id
        prov = json.loads((ITEMS / c.asset_id / "provenance.json").read_text(encoding="utf-8"))
        assert prov["outputs"][0]["sha256"] == hashlib.sha256(committed.read_bytes()).hexdigest()
        assert prov["pixelPass"]["scriptSha256"] == tc.script_sha256()
        assert not missing_provenance_fields(dict(prov, reviewedBy="pending: T439"), "scripted", for_manifest=True)
    report = (ITEMS / "check-report-t464.json").read_text(encoding="utf-8")
    assert report == ck.report_json(build["report"])
    man = json.loads((ITEMS / "manifest-t464.json").read_text(encoding="utf-8"))
    assert sorted(e["assetId"] for e in man["entries"]) == sorted(tc.CHIPS)
    for e in man["entries"]:
        png = ITEMS / e["frames"][0]["file"]
        assert e["frames"][0]["sha256"] == hashlib.sha256(png.read_bytes()).hexdigest()

