"""T438: scripted production of the 50 UI glyph icons (F004-PAB v1.0, brief section 5)."""
import json
import re
from pathlib import Path

import pytest
from PIL import Image

from art_pipeline import __main__ as cli
from art_pipeline import checks as ck
from art_pipeline import pixel_pass as pp
from art_pipeline import ui_icons as ui
from art_pipeline import ui_icons_draw as draw
from art_pipeline import ui_icons_glyphs as gl
from art_pipeline.color import contrast_ratio, hex_to_rgb
from art_pipeline.palette import PALETTE_V1_PATH, load_palette

REPO = Path(__file__).resolve().parents[3]
ART = REPO / "art-source" / "f004"
BRIEF = REPO / "docs" / "art" / "F004-asset-briefs-v1.md"


@pytest.fixture(scope="module")
def pal():
    return load_palette(PALETTE_V1_PATH)


@pytest.fixture(scope="module")
def build(pal):
    return ui.build_all(pal)


def _brief_rows():
    text = BRIEF.read_text(encoding="utf-8")
    section = text.split("## 5. UI icons per surface")[1].split("## 6.")[0]
    rows = []
    for m in re.finditer(r"^\| (\d+|new) \| `([A-Z0-9-]+)` \|", section, re.M):
        rows.append((m.group(1), m.group(2)))
    return rows


def test_inventory_equals_the_brief_rows():
    rows = _brief_rows()
    assert len(rows) == 50  # rows 30 to 78 (49) plus the Settings gear
    assert [k for _n, k in rows] == [r[1] for r in gl.ROWS]
    assert [n for n, _k in rows[:-1]] == [str(r[0]) for r in gl.ROWS[:-1]]
    assert rows[-1] == ("new", "F004-UI-ICON-SETTINGS")


def test_every_check_of_every_icon_passes(build):
    report = build["report"]
    failing = [(n, r["name"]) for n, rs in report["assets"].items() for r in rs if not r["passed"]]
    failing += [("global", r["name"]) for r in report["global"] if not r["passed"]]
    assert not failing, failing
    assert report["passed"]
    assert len(report["assets"]) == 50


def test_required_checks_run_on_every_icon(build):
    need = {"palette", "c4_glyph_colour", "canvas", "pivot", "alpha", "frames", "glyph_bounds", "stroke_width",
            "primitives", "integer_display", "contrast", "c4_token_contrast", "accent_subset", "reproduction",
            "not_applicable[outline]", "not_applicable[outline_contrast]", "not_applicable[overlay_backdrop]",
            "not_applicable[overlay_keepout]"}
    for name, results in build["report"]["assets"].items():
        assert need <= {r["name"] for r in results}, name
    for r in build["report"]["assets"]["f003-ui-icon-cash"]:
        if r["name"].startswith("not_applicable"):
            assert r["details"]["reason"]


def test_geometry_one_colour_hard_alpha_and_pivot(build, pal):
    for icon in build["icons"]:
        assert icon.image.size == (24, 24)
        assert ui._colour_ids(icon.image, pal) == {"ui_text"}
        assert all(a in (0, 255) for a in icon.image.getchannel("A").getdata())
        assert 1 <= icon.primitives <= 6, icon.key
        e = build["entries"][icon.key]
        assert e["canvas"] == [24, 24] and e["pivot"] == [0.5, 0.5] and ck.pivot_is_integral(e)
        assert e["ppu"] == 24 and e["route"] == "scripted" and len(e["frames"]) == 1
        bb = pp.opaque_bbox(icon.image)
        assert bb[0] >= 0 and bb[1] >= 0 and bb[2] <= 24 and bb[3] <= 24


def test_stroke_width_rule_rejects_one_pixel_lines():
    c = draw.Canvas()
    c.rect(2, 10, 20, 11)  # 1 px tall bar
    img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    for (x, y) in c.px:
        img.putpixel((x, y), (255, 255, 255, 255))
    assert not ui.check_stroke_width(img).passed
    c2 = draw.Canvas().rect(2, 10, 20, 12)
    img2 = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    for (x, y) in c2.px:
        img2.putpixel((x, y), (255, 255, 255, 255))
    assert ui.check_stroke_width(img2).passed


def test_even_strokes_on_integer_coordinates_are_exact():
    c = draw.Canvas().line(2, 10, 20, 10, 2)
    assert {y for (_x, y) in c.px} == {9, 10}
    c3 = draw.Canvas().line(2.5, 10.5, 20.5, 10.5, 3)
    assert {y for (_x, y) in c3.px} == {9, 10, 11}


def test_c4_contrast_on_the_three_token_surfaces(build, pal):
    r = ui.check_token_contrast(pal)
    assert r.passed
    pairs = r.details["pairs"]
    assert len(pairs) == 9  # three tints x three surfaces
    for p in pairs:
        assert p["ratio"] >= 3.0
        tinted = ui._tint(pal.rgb("ui_text"), p["tintHex"])
        assert round(contrast_ratio(tinted, hex_to_rgb(p["surfaceHex"])), 3) == p["ratio"]
    enabled = [p for p in pairs if p["state"] == "enabled"]
    assert all(p["meets4_5"] for p in enabled)


def test_declared_confusion_peers_have_distinct_silhouettes(build):
    r = [g for g in build["report"]["global"] if g["name"] == "peer_silhouette_iou"][0]
    assert r["passed"] and r["details"]["worstDeclared"] <= 0.70
    assert r["details"]["declaredPairs"] >= 30
    keys = {i.key for i in build["icons"]}
    for icon in build["icons"]:
        assert set(icon.peers) <= keys, icon.key


def test_all_icons_are_pairwise_distinct(build):
    hashes = [pp.pixel_hash(i.image) for i in build["icons"]]
    assert len(set(hashes)) == 50


def test_toggles_differ_by_fill_and_knob_side(build, pal):
    by = {i.key: i.image for i in build["icons"]}
    on, off = by["F003-UI-ICON-TOGGLE-ON"], by["F003-UI-ICON-TOGGLE-OFF"]
    count = lambda im: sum(1 for p in im.getdata() if p[3])
    assert count(on) > count(off) * 1.4  # filled versus outlined
    # the knob sits on the right when on, on the left when off (centre of mass of the inner area)
    assert on.getpixel((16, 12))[3] == 255 and on.getpixel((16, 9))[3] == 0
    assert off.getpixel((8, 12))[3] == 255 and off.getpixel((16, 12))[3] == 0


def test_gear_variants_differ_in_teeth_and_plus(build):
    by = {i.key: i for i in build["icons"]}
    assert by["F003-UI-ICON-INDUSTRY"].image.getpixel((12, 12))[3] == 255  # the plus
    assert by["F004-UI-ICON-SETTINGS"].image.getpixel((12, 12))[3] == 0     # open hub, no plus
    assert "radial" in by["F004-UI-ICON-SETTINGS"].kinds


def _without_review(data: bytes, name: str) -> bytes:
    """T440: committed provenance files carry the T439 reviewer record; a generator run writes it empty."""
    if name != "provenance.json":
        return data
    record = json.loads(data.decode("utf-8"))
    reviewed = record.get("reviewedBy")
    if isinstance(reviewed, str) and reviewed.startswith("Art Director review (independent agent"):
        record["revision"] -= 1  # T441: a review record raises the revision by one; the generator writes the base revision
    record["reviewedBy"] = {}
    return (json.dumps(record, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode("utf-8")


def test_deterministic_rerun_and_committed_files_are_byte_identical(tmp_path, pal):
    a, b = tmp_path / "a", tmp_path / "b"
    ui.write_all(a, pal)
    ui.write_all(b, pal)
    files_a = sorted(p.relative_to(a) for p in a.rglob("*") if p.is_file())
    files_b = sorted(p.relative_to(b) for p in b.rglob("*") if p.is_file())
    assert files_a == files_b and files_a
    for rel in files_a:
        assert (a / rel).read_bytes() == (b / rel).read_bytes(), rel
    committed = ART / "ui-icons"
    assert committed.is_dir(), "run: python -m art_pipeline ui-icons --out ../../art-source/f004"
    on_disk = sorted(p.relative_to(committed) for p in committed.rglob("*") if p.is_file())
    assert on_disk == [p.relative_to(a / "ui-icons") for p in sorted((a / "ui-icons").rglob("*")) if p.is_file()]
    for rel in on_disk:
        assert _without_review((committed / rel).read_bytes(), rel.name) == _without_review((a / "ui-icons" / rel).read_bytes(), rel.name), rel


def test_manifest_provenance_and_answer_key_layout(tmp_path, pal):
    ui.write_all(tmp_path, pal)
    fam = tmp_path / "ui-icons"
    man = json.loads((fam / "manifest.json").read_text(encoding="utf-8"))
    assert len(man["entries"]) == 50
    for e in man["entries"]:
        png = fam / e["frames"][0]["file"]
        assert pp.sha256_hex(png.read_bytes()) == e["frames"][0]["sha256"]
        prov = json.loads((fam / e["assetId"] / "provenance.json").read_text(encoding="utf-8"))
        assert prov["reviewedBy"] == {} and prov["route"] == "scripted" and prov["checkReportHash"]
        assert prov["pixelPass"]["paletteVersion"] == pal.version
        assert e["displayMultiples"] == [1, 2, 3] and e["pivotPx"] == [12, 12]
    assert (fam / "check-report.json").is_file()
    assert (fam / "sheets" / "sheet_grayscale.png").is_file()
    assert not (fam / "sheets" / "sheet_key.json").exists()      # the key is withheld from the reviewer's folder
    assert (fam / "answer-key" / "sheet_key.json").is_file()
    assert (fam / "sheets" / "tinted_on_token_surfaces.png").is_file()


def test_cli_ui_icons_subcommand(tmp_path, capsys):
    assert cli.main(["ui-icons", "--out", str(tmp_path)]) == 0
    assert "ui-icons: 50 icons, all checks pass" in capsys.readouterr().out
