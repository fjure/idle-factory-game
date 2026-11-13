"""F006 T554 (Issue #752): the "Office" hub entry glyph `f006-ui-office`, scripted F003 UI-icon route.

Run from the repository root (no Unity, no GPU, no generative model):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f006/ui/pipeline_ui_office.py

One 24 x 24 art px glyph, one colour (`ui_text`, tinted by the F003 tokens at runtime), no outline, stroke at least
2 px, at most 6 primitives. It reuses the T438 toolkit (`ui_icons_draw`, `ui_icons`) unchanged: same pixel pass, same
check set (including rule C4 on the token surfaces), plus the pairwise silhouette IoU against all 50 catalog icons.
Motif: an in-tray (a desk tray) holding a stack of three cards. It says "the place where everything lands", and is
deliberately not a single sheet of paper like `F003-UI-ICON-TAB-CONTRACTS` (a clipboard / folded document).
Display: integer multiples only (1, 2, 3 pt per art px); the F003 convention ships no @2x file.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from art_pipeline import checks as ck  # noqa: E402
from art_pipeline import pixel_pass as pp  # noqa: E402
from art_pipeline import ui_icons as ui  # noqa: E402
from art_pipeline.color import hex_to_rgb  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, load_palette  # noqa: E402
from art_pipeline.ui_icons_draw import Canvas  # noqa: E402

SCRIPT_VERSION = "t554-1"
ART_BIBLE = "F004-PAB v1.1.0"
KEY = "F006-UI-OFFICE"
ASSET_ID = "f006-ui-office"
GLYPH = "desk in-tray holding a stack of three cards"
SURFACE = "hub entry (Office)"
PEERS = ["F003-UI-ICON-TAB-CONTRACTS", "F003-UI-ICON-TAB-ITEMS", "F003-UI-ICON-STORAGE", "F003-UI-ICON-REMOVE"]
DATE = "2026-11-13"
SHEET_TABS = ["F003-UI-ICON-TAB-CONTRACTS", "F003-UI-ICON-TAB-FACTORY", "F003-UI-ICON-TAB-ITEMS",
              "F003-UI-ICON-TAB-MARKET", "F003-UI-ICON-TAB-PRESTIGE", "F003-UI-ICON-TAB-RESEARCH"]


def office(c: Canvas) -> None:
    """Five primitives: tray body (U), tray front lip, front card (open bottom), middle card edge, back card edge."""
    c.polyline([(3, 12), (3, 21), (21, 21), (21, 12)])
    c.line(3, 17, 21, 17)
    c.rects([(8, 8, 16, 10)])
    c.polyline([(7, 15), (7, 5), (17, 5), (17, 15)])
    c.line(9, 2, 15, 2)


def build_office(palette) -> ui.Icon:
    c = Canvas()
    office(c)
    return ui.Icon(0, KEY, SURFACE, GLYPH, PEERS, ui._pass(ui._raw(c, palette), palette), c.prims, list(c.kinds))


def _sources() -> list[dict]:
    files = [HERE / "pipeline_ui_office.py"] + [REPO / "tools/art-pipeline/art_pipeline" / n for n in ui._SRC_FILES]
    return [{"path": str(p.relative_to(REPO)).replace("\\", "/"),
             "sha256": pp.sha256_hex(p.read_bytes().replace(b"\r\n", b"\n"))} for p in sorted(files)]


def _provenance(icon, palette, outputs, report_hash) -> dict:
    prov = ui._provenance(icon, palette, outputs, report_hash)
    prov["sources"] = _sources()
    prov["tools"][0] = {"name": "art-source/f006/ui/pipeline_ui_office.py", "version": SCRIPT_VERSION}
    prov["tools"].insert(1, {"name": "art_pipeline.ui_icons (toolkit, unchanged)", "version": ui.SCRIPT_VERSION})
    prov["parameters"].update(scriptVersion=SCRIPT_VERSION, briefRow=None, taskId="T554", issue=752,
                              date=DATE, route="scripted F003 UI-icon pipeline, no generative model",
                              artBibleSection="21.2 UI row (owner decision P2-F9 -> P2-F22), 21.4, 21.9 item 6",
                              distinctFrom="F003-UI-ICON-TAB-CONTRACTS")
    prov["pixelPass"]["scriptSha256"] = pp.sha256_hex(json.dumps(prov["sources"], sort_keys=True).encode())
    prov["producedBy"] = "Asset Artist (scripted route, Art Director lens pending), MAQA T554 / Issue #752, " + DATE
    prov["reviewedBy"] = "pending: Art Director pixel review in a fresh session (T554 acceptance)"
    prov["artBibleVersion"] = ART_BIBLE
    prov["briefVersion"] = "none: one sprite added by owner decision P2-F9, spec in Art Bible 21.2 UI row"
    return prov


def _tinted(img: Image.Image, palette, tint_hex: str, scale: int) -> Image.Image:
    big = img.resize((img.width * scale, img.height * scale), Image.Resampling.NEAREST)
    tint = ui._tint(palette.rgb(ui.GLYPH_COLOUR), tint_hex)
    out = Image.new("RGBA", big.size, tint + (0,))
    out.putalpha(big.getchannel("A"))
    return out


def contact_sheet(office_img: Image.Image, palette, others: dict) -> tuple[Image.Image, dict]:
    """Columns: contracts first, the other five tab icons, then the Office glyph (enabled tint) and the Office glyph
    (selected tint). Rows: 1x on --surface, 4x on --surface, 4x on --bg. Order is recorded, not drawn."""
    cols = [(k, others[k], "enabled") for k in SHEET_TABS] + [(KEY, office_img, "enabled"), (KEY, office_img, "selected")]
    cell1, cell4 = 24 + 12, 96 + 12
    w = len(cols) * cell4
    rows = [("--surface", "#1F2937", 1, cell1), ("--surface", "#1F2937", 4, cell4), ("--bg", "#111827", 4, cell4)]
    sheet = Image.new("RGB", (w, sum(r[3] for r in rows)), (0, 0, 0))
    y = 0
    for _name, hexc, scale, ch in rows:
        sheet.paste(Image.new("RGB", (w, ch), hex_to_rgb(hexc)), (0, y))
        for n, (_k, img, state) in enumerate(cols):
            t = _tinted(img, palette, ui.TINTS[state][1], scale)
            sheet.paste(t.convert("RGB"), (n * cell4 + (cell4 - 24 * scale) // 2, y + (ch - 24 * scale) // 2), t.getchannel("A"))
        y += ch
    order = {"columns": [f"{k} ({s})" for k, _i, s in cols],
             "rows": [f"{n} at {s}x" for n, _h, s, _c in rows],
             "note": "column 1 is the contracts glyph, the two last columns are the new Office glyph"}
    return sheet, order


def icon_rows(icon) -> list[str]:
    px = icon.image.load()
    return ["".join("#" if px[x, y][3] else "." for x in range(24)) for y in range(24)]


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    catalog = ui.build_icons(palette)
    icon, twin = build_office(palette), build_office(palette)
    assert icon.asset_id == ASSET_ID, icon.asset_id
    stub = _provenance(icon, palette, [{"path": ASSET_ID, "sha256": "0" * 64}], "0" * 64)
    entry = ui._entry(icon, palette, stub)
    icon.results = ui.check_icon(icon, entry, palette, twin)
    glob = [ck.check_palette_definition(palette), ui.peer_iou(catalog + [icon]),
            ck.CheckResult("inventory", len(catalog) == 50 and icon.primitives <= ui.MAX_PRIMITIVES,
                           {"catalogIcons": len(catalog), "newIcons": 1,
                            "note": "the F003 50-icon inventory is untouched; F006-UI count is 1 (Art Bible 21.2/21.4)"})]
    report = ck.build_report({ASSET_ID: icon.results}, glob)
    report_text = ck.report_json(report)
    report_hash = pp.sha256_hex(report_text.encode("utf-8"))

    data = pp.png_bytes(icon.image)
    rel = f"{ASSET_ID}/{ASSET_ID}_00.png"
    outputs = [{"path": f"art-source/f006/ui/{rel}", "sha256": pp.sha256_hex(data), "pixelHash": pp.pixel_hash(icon.image)}]
    prov = _provenance(icon, palette, outputs, report_hash)
    entry = ui._entry(icon, palette, prov)
    entry.update(family="ui", briefRow=None, key=KEY, id=ASSET_ID, f006Task="T554", sha256=outputs[0]["sha256"],
                 distinctFrom="F003-UI-ICON-TAB-CONTRACTS",
                 palette={"id": palette.palette_id, "version": palette.version, "ids": [ui.GLYPH_COLOUR]},
                 displayMultiples=list(ui.DISPLAY_MULTIPLES),
                 retina="none: integer display multiples only, no @2x file (F003 convention)",
                 checkReport="art-source/f006/ui/check-report.json", checkReportHash=report_hash,
                 provenance={"route": "scripted F003 UI-icon pipeline", "scriptVersion": SCRIPT_VERSION, "date": DATE,
                             "generativeModel": "none", "license": prov["license"],
                             "script": "art-source/f006/ui/pipeline_ui_office.py"})
    manifest = pp.build_manifest([entry])

    def w(path: Path, b: bytes) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b)

    w(HERE / rel, data)
    w(HERE / ASSET_ID / "provenance.json", (json.dumps(prov, indent=2, sort_keys=True) + "\n").encode())
    w(HERE / "manifest.json", pp.manifest_json(manifest).encode())
    w(HERE / "check-report.json", report_text.encode())
    sheet, order = contact_sheet(icon.image, palette, {i.key: i.image for i in catalog})
    sp = HERE / "sheets" / "contact_office_vs_tabs.png"
    sp.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(sp, format="PNG", compress_level=9, optimize=False)
    w(HERE / "sheets" / "contact_order.json", (json.dumps(order, indent=2) + "\n").encode())
    print("passed" if report["passed"] else "FAILED")
    print("\n".join(icon_rows(icon)))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
