import hashlib
import json

import pytest
from PIL import Image

from art_pipeline import checks as ck
from art_pipeline import cues
from art_pipeline import pixel_pass as pp
from art_pipeline.__main__ import main as cli_main
from art_pipeline.provenance import (ModelHashError, load_models, missing_provenance_fields, validate_models,
                                     verify_model_file)
from conftest import solid


def test_cue_icons_are_at_least_12x12_and_palette_exact(palette):
    for name in cues.cue_names():
        img = cues.render_cue(name, palette)
        assert ck.check_cue_icon_size(img).passed, name
        colors = {img.getpixel((x, y))[:3] for x in range(img.width) for y in range(img.height) if img.getpixel((x, y))[3]}
        assert colors <= {c.rgb for c in palette.colors_in("state_cues")}
        assert ck.check_alpha([img]).passed


def test_cue_integer_factor_scales_exactly(palette):
    one, two = cues.render_cue("pause", palette), cues.render_cue("pause", palette, scale=2)
    assert two.size == (24, 24) and two.resize((12, 12), Image.Resampling.NEAREST).tobytes() == one.tobytes()


def test_cue_silhouettes_are_distinct(palette):
    masks = {n: ck.silhouette_mask(cues.render_cue(n, palette)) for n in cues.cue_names()}
    r = ck.check_iou(masks)
    assert r.passed, r.details


def test_cues_are_deterministic(palette):
    for n in cues.cue_names():
        assert pp.pixel_hash(cues.render_cue(n, palette)) == pp.pixel_hash(cues.render_cue(n, palette))


def test_markers_frame_and_digits(palette):
    for n in cues.marker_names():
        assert cues.render_marker(n, palette).size == (8, 8)
    assert cues.render_selection_frame(10, 8, palette).getpixel((5, 4))[3] == 0
    assert cues.render_selection_frame(10, 8, palette).getpixel((0, 0))[3] == 255
    assert cues.render_digits("42", palette).size == (7, 5)
    assert cues.render_digits("8", palette, scale=2).size == (6, 10)


# --- sheets ------------------------------------------------------------------

def _sprites(palette):
    return {n: cues.render_cue(n, palette) for n in cues.cue_names()}


def test_review_sheets_are_seeded_shuffled_and_reproducible(palette, tmp_path):
    a = ck.make_review_sheets(_sprites(palette), 11, tmp_path / "a")
    b = ck.make_review_sheets(_sprites(palette), 11, tmp_path / "b")
    c = ck.make_review_sheets(_sprites(palette), 12, tmp_path / "c")
    assert a["order"] == b["order"] and sorted(a["order"]) == cues.cue_names()
    assert set(a["sheets"]) == {"grayscale", "protanopia", "deuteranopia", "tritanopia"}
    for mode in a["sheets"]:
        assert a["sheets"][mode].read_bytes() == b["sheets"][mode].read_bytes()
    gray = Image.open(a["sheets"]["grayscale"]).convert("RGB")
    assert all(r == g == bl for r, g, bl in list(gray.getdata())[:500])
    prot = Image.open(a["sheets"]["protanopia"]).convert("RGB")
    assert prot.tobytes() != gray.tobytes()
    key = json.loads(a["key"].read_text())
    assert key["seed"] == 11 and key["order"] == a["order"]
    assert a["key"].parent == a["sheets"]["grayscale"].parent  # key is a separate file, withheld by the caller


def test_shuffle_actually_depends_on_seed(palette, tmp_path):
    sprites = {f"s{i}": solid((4, 4), (i * 20, 0, 0)) for i in range(10)}
    orders = {tuple(ck.make_review_sheets(sprites, s, tmp_path / str(s))["order"]) for s in range(5)}
    assert len(orders) > 1


# --- budgets -----------------------------------------------------------------

def _fill(path, mb):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"\0" * int(mb * 1024 * 1024))


def test_family_and_total_budgets(tmp_path):
    _fill(tmp_path / "machines" / "a.bin", 9)
    _fill(tmp_path / "machines" / "b.bin", 9)
    _fill(tmp_path / "_rejected" / "x.bin", 40)  # gitignored archive does not count
    assert ck.check_size_budgets(tmp_path).passed
    _fill(tmp_path / "machines" / "c.bin", 8)  # 26 MB
    r = ck.check_size_budgets(tmp_path)
    assert not r.passed and "machines" in r.details["overFamilyBudget"]
    assert ck.check_size_budgets(tmp_path, family_mb=30).passed
    assert not ck.check_size_budgets(tmp_path, family_mb=30, total_mb=20).passed


def test_total_budget_150_mb_default(tmp_path):
    assert ck.FAMILY_BUDGET_MB == 25 and ck.TOTAL_BUDGET_MB == 150
    assert ck.check_size_budgets(tmp_path / "missing").passed


def test_large_source_file_needs_decision(tmp_path):
    _fill(tmp_path / "f" / "big.blend", 11)
    r = ck.check_size_budgets(tmp_path)
    assert not r.passed and r.details["undecidedLargeFiles"] == ["f/big.blend"]
    assert ck.check_size_budgets(tmp_path, decided_large={"f/big.blend"}).passed


# --- provenance / models -----------------------------------------------------

def test_missing_provenance_fields_by_route():
    assert "license" in missing_provenance_fields({}, "scripted")
    assert "rigVersion" in missing_provenance_fields({}, "blender", for_manifest=True)
    assert "parameters.seed" in missing_provenance_fields({"parameters": {"prompt": "p"}}, "comfy", for_manifest=True)
    assert "checkReportHash" in missing_provenance_fields({}, "scripted")
    assert "checkReportHash" not in missing_provenance_fields({}, "scripted", for_manifest=True)


def test_models_json_schema_and_hash_verification(tmp_path):
    doc = load_models()
    assert validate_models(doc) == []
    assert doc["models"] == []  # nothing needing a download is registered
    payload = b"model-bytes"
    f = tmp_path / "secret-dir" / "m.safetensors"
    f.parent.mkdir()
    f.write_bytes(payload)
    entry = {"id": "m", "kind": "model", "sourceUrl": "https://example.invalid/m", "version": "1", "license": "Apache-2.0",
             "commercialUse": True, "sha256": hashlib.sha256(payload).hexdigest(), "trainingDataNote": "unknown"}
    assert validate_models({"schemaVersion": 1, "models": [entry]}) == []
    verify_model_file(entry, f)
    with pytest.raises(ModelHashError) as e:
        verify_model_file({**entry, "sha256": "0" * 64}, f)
    assert "secret-dir" not in str(e.value) and str(tmp_path) not in str(e.value)


def test_models_validation_rejects_bad_entries():
    bad = {"id": "x", "kind": "weird", "commercialUse": False, "sha256": "abc"}
    problems = validate_models({"schemaVersion": 1, "models": [bad]})
    assert any("commercial" in p for p in problems) and any("sha256" in p for p in problems)
    assert any("kind" in p for p in problems) and any("missing sourceUrl" in p for p in problems)


def test_cli_checks_passes(tmp_path, capsys):
    rc = cli_main(["checks", "--art-root", str(tmp_path), "--report", str(tmp_path / "r.json")])
    assert rc == 0 and json.loads((tmp_path / "r.json").read_text())["passed"]
