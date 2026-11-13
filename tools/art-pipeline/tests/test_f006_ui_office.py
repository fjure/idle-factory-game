"""F006 T554: the committed F006-UI glyph `f006-ui-office` (art-source/f006/ui) obeys the F003 UI-icon rules."""
import hashlib
import json
from pathlib import Path

from PIL import Image

from art_pipeline.palette import PALETTE_V1_PATH, load_palette

ROOT = Path(__file__).resolve().parents[3]
UI = ROOT / "art-source" / "f006" / "ui"
CONTRACTS = ROOT / "art-source" / "f004" / "ui-icons" / "f003-ui-icon-tab-contracts" / "f003-ui-icon-tab-contracts_00.png"
ID = "f006-ui-office"


def _entry():
    man = json.loads((UI / "manifest.json").read_text(encoding="utf-8"))
    assert [e["assetId"] for e in man["entries"]] == [ID]
    return man["entries"][0]


def test_glyph_png_matches_manifest_and_rules():
    entry = _entry()
    png = UI / entry["frames"][0]["file"]
    assert entry["frames"][0]["sha256"] == entry["sha256"] == hashlib.sha256(png.read_bytes()).hexdigest()
    img = Image.open(png).convert("RGBA")
    assert img.size == (24, 24) and tuple(entry["canvas"]) == (24, 24)
    palette = load_palette(PALETTE_V1_PATH)
    ids = {palette.id_of(tuple(p[:3])) for p in img.getdata() if p[3]}
    assert all(p[3] in (0, 255) for p in img.getdata())
    assert ids == {"ui_text"}


def test_glyph_differs_from_contracts_glyph():
    a = Image.open(UI / _entry()["frames"][0]["file"]).convert("RGBA")
    b = Image.open(CONTRACTS).convert("RGBA")
    ma = {(x, y) for y in range(24) for x in range(24) if a.getpixel((x, y))[3]}
    mb = {(x, y) for y in range(24) for x in range(24) if b.getpixel((x, y))[3]}
    assert len(ma & mb) / len(ma | mb) < 0.5


def test_provenance_declares_scripted_route_without_model():
    prov = json.loads((UI / ID / "provenance.json").read_text(encoding="utf-8"))
    assert prov["route"] == "scripted" and prov["license"].startswith("authored")
    assert "no generative model" in prov["trainingDataNote"] and prov["artBibleVersion"] == "F004-PAB v1.1.0"


def test_check_report_passed():
    assert json.loads((UI / "check-report.json").read_text(encoding="utf-8"))["passed"] is True
