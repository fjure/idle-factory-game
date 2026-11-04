"""T437: scripted production of state cues, markers, selection frame and lock labels (F004-PAB v1.0)."""
import json
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline import checks as ck
from art_pipeline import checks_v1 as v1
from art_pipeline import cues
from art_pipeline import cues_production as cp
from art_pipeline import pixel_pass as pp
from art_pipeline.palette import PALETTE_V1_PATH, RESERVED_CUE_GROUP, load_palette

REPO = Path(__file__).resolve().parents[3]
ART = REPO / "art-source" / "f004"


@pytest.fixture(scope="module")
def pal():
    return load_palette(PALETTE_V1_PATH)


@pytest.fixture(scope="module")
def build(pal):
    return cp.build_all(pal)


def _without_review(data: bytes, name: str) -> bytes:
    """T440: the committed provenance files carry the reviewer record that T439 produced; a generator run writes it empty."""
    if name != "provenance.json":
        return data
    record = json.loads(data.decode("utf-8"))
    record["reviewedBy"] = {}
    return (json.dumps(record, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode("utf-8")


def _by_key(build):
    return {s.key: s for s in build["sprites"]}


def test_inventory_matches_the_brief(build):
    keys = set(_by_key(build))
    assert keys == {"F003-BADGE-RUNNING", "F003-BADGE-WAITING", "F003-BADGE-MISSING-INPUT", "F003-BADGE-STORAGE-FULL",
                    "F003-BADGE-PAUSED", "F003-BADGE-INACTIVE", "F003-MARKER-BOOSTED", "F003-MARKER-OVERDRIVE",
                    "F003-SLOT-SELECTED-FRAME", "F004-LABEL-LOCK-L2", "F004-LABEL-LOCK-L3", "F004-LABEL-LOCK-L4",
                    "F004-LABEL-LOCK-L5"}


def test_every_check_of_every_sprite_passes(build):
    for fam, report in build["reports"].items():
        failing = [(n, r["name"]) for n, rs in report["assets"].items() for r in rs if not r["passed"]]
        assert not failing, (fam, failing)
        assert report["passed"]


def test_required_v1_checks_run_on_every_png_sprite(build):
    need = {"outline_contrast[floor_0]", "overlay_keepout", "accent_subset", "outline", "cue_icon_size"}
    for fam, report in build["reports"].items():
        for name, results in report["assets"].items():
            names = {r["name"] for r in results}
            if name == "f003-badge-running":
                assert names == {"intentionally_empty"}
                continue
            assert any(n.startswith("overlay_backdrop[") for n in names), name
            assert any(n.startswith("outline_contrast[") for n in names), name
            assert {"overlay_keepout", "accent_subset", "outline", "contrast", "palette", "pivot"} <= names, name
            if name.startswith("f003-badge"):
                assert "cue_icon_size" in names, name
    del need


def test_running_badge_is_an_intentionally_empty_entry(build):
    s = _by_key(build)["F003-BADGE-RUNNING"]
    assert s.image is None
    e = build["entries"]["F003-BADGE-RUNNING"]
    assert e["intentionallyEmpty"] is True and not any(k in e for k in ("frames", "files", "png", "path"))
    assert ck.check_intentionally_empty([], e).passed


def test_badge_geometry_fill_semantics_and_size(build, pal):
    fills = {"F003-BADGE-WAITING": "cue_info", "F003-BADGE-MISSING-INPUT": "cue_alert",
             "F003-BADGE-STORAGE-FULL": "cue_warn", "F003-BADGE-PAUSED": "cue_neutral", "F003-BADGE-INACTIVE": "cue_dim"}
    for key, fill in fills.items():
        s = _by_key(build)[key]
        assert s.image.size == (80, 64)
        assert pp.opaque_bbox(s.image) == (48, 10, 71, 33)  # 23 x 23, bottom-centre anchor (59, 33)
        assert cp._colour_ids(s.image, pal) == {"ink_0", fill}
        assert ck.check_cue_icon_size(s.image).passed
        assert ck.check_alpha([s.image]).passed


def test_reserved_cue_colours_only_in_badges(build, pal):
    reserved = {c.id for c in pal.colors_in(RESERVED_CUE_GROUP)}
    for s in build["sprites"]:
        if s.image is None:
            continue
        used = cp._colour_ids(s.image, pal)
        assert bool(used & reserved) == (s.kind == "badge"), s.key


def test_overlay_boxes_do_not_overlap_and_stay_in_the_band(build):
    boxes = [pp.opaque_bbox(s.image) for k, s in _by_key(build).items() if s.kind in ("badge", "marker") and s.image]
    boxes = [pp.opaque_bbox(_by_key(build)[k].image) for k in ("F003-BADGE-WAITING", "F003-MARKER-BOOSTED", "F003-MARKER-OVERDRIVE")]
    for i, a in enumerate(boxes):
        assert a[1] >= 0 and a[3] <= 34 and v1.KEEPOUT_COLS[0] <= a[0] and a[2] - 1 <= v1.KEEPOUT_COLS[1]
        for b in boxes[i + 1:]:
            assert a[2] <= b[0] or b[2] <= a[0]
    assert pp.opaque_bbox(_by_key(build)["F003-MARKER-BOOSTED"].image) == (24, 16, 35, 27)
    assert pp.opaque_bbox(_by_key(build)["F003-MARKER-OVERDRIVE"].image) == (36, 16, 47, 27)


def test_markers_are_11x11_with_ink_glyph_on_cvd_fill(build, pal):
    for key, fill in (("F003-MARKER-BOOSTED", "cvd_green"), ("F003-MARKER-OVERDRIVE", "cvd_pink")):
        s = _by_key(build)[key]
        assert cp._colour_ids(s.image, pal) == {"ink_0", fill}
        assert pp.opaque_bbox(s.image)[2] - pp.opaque_bbox(s.image)[0] == 11


def test_selection_frame_follows_the_plate_diamond(build, pal):
    s = _by_key(build)["F003-SLOT-SELECTED-FRAME"]
    assert pp.opaque_bbox(s.image) == (5, 29, 75, 64)  # 70 x 35, bottom on the canvas bottom
    assert ck.check_overhang([s.image], cues.CANVAS, cues.FOOTPRINT, 24).passed
    assert cp._colour_ids(s.image, pal) == {"ink_0", "ui_accent"}
    assert s.image.getpixel((40, 46))[3] == 0  # the ring is hollow


def test_lock_labels_are_scale_3_digits_centred_on_the_footprint(build, pal):
    for key, level in cues.LABEL_KEYS.items():
        s = _by_key(build)[key]
        bb = pp.opaque_bbox(s.image)
        assert (bb[2] - bb[0] - 2, bb[3] - bb[1] - 2) == (9, 15)  # cap 15 art px (owner decision R4)
        assert cp._colour_ids(s.image, pal) == {"ink_0", "ui_text"}
        assert (bb[0] + bb[2] - 1, bb[1] + bb[3] - 1) == (80, 98)  # centre (40, 49)
    digits = {key: _by_key(build)[key].image.tobytes() for key in cues.LABEL_KEYS}
    assert len(set(digits.values())) == 4


def test_contrast_rules_hold(build, pal):
    for s in build["sprites"]:
        if s.image is None:
            continue
        r = ck.check_contrast(cp._contrast_pairs(s), pal, cp.DARKEST_LIGHT)
        assert r.passed, (s.key, r.details)


def test_outline_contrast_sweeps_cover_the_allowed_backdrop_sets(build):
    for s in build["sprites"]:
        if s.image is None or s.kind == "label":
            continue
        sweep = next(r for r in s.results if r.name == "outline_contrast_sweep")
        assert sweep.passed and not sweep.details["failingTones"], s.key
        assert set(sweep.details["shares"]) == (set(v1.a_fr(build["palette"])) if s.kind == "frame" else set(v1.A_OV))


def test_generation_is_deterministic(pal, build):
    again = cp.build_all(pal)
    for a, b in zip(build["sprites"], again["sprites"]):
        assert a.key == b.key
        if a.image is not None:
            assert pp.pixel_hash(a.image) == pp.pixel_hash(b.image)
            assert pp.png_bytes(a.image) == pp.png_bytes(b.image)
    assert ck.report_json(build["reports"]["cues"]) == ck.report_json(again["reports"]["cues"])


def test_write_all_twice_is_byte_identical(pal, tmp_path):
    cp.write_all(tmp_path / "a", pal)
    cp.write_all(tmp_path / "b", pal)
    fa = sorted(p.relative_to(tmp_path / "a") for p in (tmp_path / "a").rglob("*") if p.is_file())
    fb = sorted(p.relative_to(tmp_path / "b") for p in (tmp_path / "b").rglob("*") if p.is_file())
    assert fa == fb and fa
    for rel in fa:
        assert (tmp_path / "a" / rel).read_bytes() == (tmp_path / "b" / rel).read_bytes(), rel


def test_committed_art_source_matches_a_fresh_run(pal, tmp_path):
    """The committed art-source/f004 files for T437 equal a rerun of the generator (provenance hashes included)."""
    cp.write_all(tmp_path, pal)
    files = [p for p in tmp_path.rglob("*") if p.is_file()]
    assert files
    for p in files:
        rel = p.relative_to(tmp_path)
        committed = ART / rel
        assert committed.exists(), rel
        got = _without_review(committed.read_bytes().replace(b"\r\n", b"\n"), p.name)
        assert got == _without_review(p.read_bytes().replace(b"\r\n", b"\n"), p.name), rel


def test_provenance_is_complete_except_for_the_review_fields():
    for fam in ("cues", "markers", "slots"):
        for prov_path in (ART / fam).glob("*/provenance.json"):
            rec = json.loads(prov_path.read_text(encoding="utf-8"))
            from art_pipeline.provenance import missing_provenance_fields
            reviewed = rec["reviewedBy"]
            if reviewed:
                # T440 filled the reviewer record from the T439 review: the record is then complete.
                assert missing_provenance_fields(rec, "scripted") == [], prov_path
                assert reviewed["verdict"] in ("APPROVE", "APPROVE WITH CONDITIONS"), prov_path
            else:
                assert {m.split(".")[0] for m in missing_provenance_fields(rec, "scripted")} == {"reviewedBy"}, prov_path
            assert rec["route"] == "scripted"


def test_manifest_entries_have_whole_pixel_pivots_and_png_hashes():
    for fam, name in (("cues", "manifest.json"), ("markers", "manifest.json"), ("slots", "manifest-t437.json")):
        doc = json.loads((ART / fam / name).read_text(encoding="utf-8"))
        for e in doc["entries"]:
            if e.get("intentionallyEmpty"):
                assert fam == "cues" and e["assetId"] == "f003-badge-running"
                continue
            assert e["canvas"] == [80, 64] and e["footprintPx"] == [68, 30] and ck.pivot_is_integral(e)
            assert (e["pivot"][0] * 80, e["pivot"][1] * 64) == (40, 15)
            f = e["frames"][0]
            assert pp.sha256_hex((ART / fam / f["file"]).read_bytes()) == f["sha256"]
            assert Image.open(ART / fam / f["file"]).size == (80, 64)


def test_glyph_peers_are_distinct_shapes(build):
    r = next(g for g in build["reports"]["cues"]["global"] if g["name"] == "glyph_silhouette_iou")
    assert r["passed"] and r["details"]["worstFull"] <= 0.70
