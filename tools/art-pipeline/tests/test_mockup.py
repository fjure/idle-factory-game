"""T505 (Feature 006, FR-040, FR-041; research R03, R07): the geometry derivation, the lane and keep-out rules, and the deterministic
mockup compositor. Sprites here are simple placeholder test sprites; the real concept sprites arrive with T506."""
import json
from dataclasses import replace

import pytest
from PIL import Image, ImageDraw

from art_pipeline import mockup, mockup_geometry as mg
from art_pipeline.__main__ import main as cli_main
from art_pipeline.palette import PALETTE_V1_PATH, load_palette

GEO = mg.load_geometry()
CONSTS = mg.consts_of(GEO)
PALETTE = load_palette(PALETTE_V1_PATH)


# ------------------------------------------------------------------------------------------------ placeholder sprites

def _save(path, img):
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)


def _diamond(size, color):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    w, h = size
    ImageDraw.Draw(img).polygon([(w // 2, 0), (w - 1, h // 2), (w // 2, h - 1), (0, h // 2)], fill=PALETTE.rgb(color) + (255,))
    return img


def _plate(color):
    """A 70 x 35 plate diamond on the 80 x 64 layer canvas: bottom on the canvas bottom, centred (pivot 15 px above the bottom)."""
    img = Image.new("RGBA", (80, 64), (0, 0, 0, 0))
    img.alpha_composite(_diamond((70, 35), color), (5, 64 - 35))
    return img


def _box(size, color, lit_x=None):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((size[0] // 2 - 10, size[1] - 28, size[0] // 2 + 9, size[1] - 16), fill=PALETTE.rgb(color) + (255,))
    if lit_x is not None:
        d.rectangle((size[0] // 2 - 10 + lit_x * 3, size[1] - 28, size[0] // 2 - 8 + lit_x * 3, size[1] - 26), fill=PALETTE.rgb("lit_0") + (255,))
    return img


def make_direction(tmp_path, name="dir-test", machine_colors=None, extra=None):
    d = tmp_path / name
    _save(d / "tile_margin.png", _diamond((70, 54), "floor_0"))
    _save(d / "tile_floor.png", _diamond((70, 54), "floor_1"))
    _save(d / "plate_occupied.png", _plate("plate_1"))
    _save(d / "plate_empty.png", _plate("plate_0"))
    _save(d / "plate_locked.png", _plate("dim_0"))
    for i in range(6):
        _save(d / f"machine_{i}.png", _box((80, 64), "steel_2" if machine_colors is None else machine_colors[i], lit_x=i))
    _save(d / "depot.png", _box((80, 64), "copper_1"))
    _save(d / "vein.png", _box((80, 64), "res_ore_1"))
    doc = {"name": name, "ui": {"hubLabel": "Hub", "siteName": "Test site"}, "laneColors": {"pip": "res_gold"},
           "sprites": {"tile_margin": "tile_margin.png", "tile_floor": "tile_floor.png", "plate_occupied": "plate_occupied.png",
                       "plate_empty": "plate_empty.png", "plate_locked": "plate_locked.png",
                       "machine": [f"machine_{i}.png" for i in range(6)], "depot": "depot.png", "vein": "vein.png"}}
    if extra:
        doc.update(extra)
    (d / "direction.json").write_text(json.dumps(doc), encoding="utf-8")
    return d / "direction.json"


# ------------------------------------------------------------------------------------------------------- geometry

@pytest.fixture(scope="module")
def lattices():
    return {name: mg.build_lattice(layout, CONSTS) for name, layout in mg.TEST_LAYOUTS.items()}


def test_geometry_export_matches_the_lattice_rules():
    assert GEO["schema"] == mg.SCHEMA and CONSTS.density == 24
    assert (CONSTS.pitch_x, CONSTS.pitch_y, CONSTS.plate_w, CONSTS.plate_h, CONSTS.row_gap) == (70, 54, 70, 35, 19)
    for layout in GEO["layouts"]:
        cols, rows = layout["columns"], layout["rows"]
        assert layout["extentPx"] == [(cols - 1) * 70 + 70, (rows - 1) * 54 + 15 + 39]
        assert len(layout["slots"]) == 12 and layout["rowGapsPx"] == [19] * (rows - 1)
        for slot in layout["slots"]:
            pv = mg.pivot_of(CONSTS, rows, slot["column"], slot["row"])
            assert slot["pivotPx"] == list(pv)
            assert slot["plateRectPx"] == [pv[0] - 35, pv[1] - 15, 70, 35]
    # the screen rows: n_fit >= n_min everywhere and the R03 sheet classes exist
    for row in GEO["pointScreens"] + GEO["devices"]:
        assert row["nFit"] >= row["nMin"] >= 1
    for _sheet, section, name in mockup.SHEETS:
        assert any(r["name"] == name for r in GEO[section])


@pytest.mark.parametrize("name", list(mg.TEST_LAYOUTS))
def test_lane_geometry_and_keep_out_on_test_layouts(lattices, name):
    lat = lattices[name]
    assert mg.lane_violations(lat) == []
    layout = mg.TEST_LAYOUTS[name]
    # one trunk per row in the 19 px gap behind the row's plates
    assert sorted(t.row for t in lat.trunks) == list(range(lat.rows))
    for t in lat.trunks:
        py = mg.pivot_of(CONSTS, lat.rows, 0, t.row)[1]
        assert py + 20 <= t.rect[1] and t.rect[3] <= py + 39 and t.rect[3] - t.rect[1] == mg.TRUNK_WIDTH
        assert t.rect[2] > t.rect[0]
    # one branch per occupied location, from the rear apex to the row trunk, never between machines
    assert len(lat.branches) == len(layout.cells)
    for b in lat.branches:
        cell = lat.location(b.row, b.column)
        trunk = next(t for t in lat.trunks if t.row == b.row)
        assert b.rect[1] == cell.pivot[1] + 20 and b.rect[3] == trunk.rect[1]
        assert b.rect[0] <= cell.pivot[0] < b.rect[2]
    # the deck covers all locations plus a margin ring
    m = lat.params.margin_cells
    assert len(lat.cells) == (lat.columns + 2 * m) * (lat.rows + 2 * m)
    assert sum(c.kind == "location" for c in lat.cells) == len(layout.cells)
    assert lat.depot.kind == "margin" and lat.deposit.kind == "margin" and lat.depot is not lat.deposit
    assert all(mg.rect_touches(t.rect, lat.spine.rect) for t in lat.trunks)


def test_layout_shapes():
    assert [(l.columns, l.rows) for l in (mg.build_lattice(mg.TEST_LAYOUTS[k], CONSTS) for k in ("3x4", "4x3", "6", "20"))] == [(3, 4), (4, 3), (3, 2), (4, 5)]
    lat = mg.build_lattice(mg.TEST_LAYOUTS["L"], CONSTS)
    assert sum(c.kind == "filler" for c in lat.cells) == 4          # holes inside the bounding box are deck filler, not locations
    assert len(lat.branches) == 5
    # a trunk of a short row still reaches the depot across the hole
    top = next(t for t in lat.trunks if t.row == 0)
    assert top.rect[2] == lat.depot.pivot[0] + 1


def test_only_occupied_locations_get_branches():
    layout = mg.rect_layout("3x4-mixed", 3, 4, states={(3, 1): "empty", (3, 2): "locked"})
    lat = mg.build_lattice(layout, CONSTS)
    assert len(lat.branches) == 10 and mg.lane_violations(lat) == []
    assert {(b.row, b.column) for b in lat.branches}.isdisjoint({(3, 1), (3, 2)})


def test_depot_side_and_margin_parameters_mirror_world_composition_settings():
    left = replace(mg.CompositionParams(), depot_side=(("portrait", "left"), ("landscape", "left")), depot_row="front", margin_cells=2)
    lat = mg.build_lattice(mg.TEST_LAYOUTS["3x4"], CONSTS, left)
    assert lat.depot.column == -2 and lat.depot.row == 3 and len(lat.cells) == 7 * 8
    assert mg.lane_violations(lat) == []
    with pytest.raises(ValueError):
        mg.build_lattice(mg.TEST_LAYOUTS["3x4"], CONSTS, replace(mg.CompositionParams(), margin_cells=0))


def test_keep_out_detects_violations(lattices):
    lat = lattices["3x4"]
    cell = lat.location(1, 1)
    bad_lane = mg.Lane("trunk", 1, None, (cell.pivot[0] - 5, cell.pivot[1] - 3, cell.pivot[0] + 5, cell.pivot[1] + 3))
    broken = replace(lat, trunks=lat.trunks + [bad_lane])
    problems = mg.lane_violations(broken)
    assert any("plate box" in p for p in problems) and any("hit diamond" in p for p in problems) and any("plate diamond" in p for p in problems)
    # a lane over the deposit cell
    dep = lat.deposit
    over = mg.Lane("trunk", 0, None, (dep.pivot[0] - 5, dep.pivot[1], dep.pivot[0] + 5, dep.pivot[1] + 5))
    assert any("deposit" in p for p in mg.lane_violations(replace(lat, trunks=lat.trunks + [over])))
    # a trunk outside its row gap
    lifted = [mg.Lane("trunk", t.row, None, (t.rect[0], t.rect[1] + 20, t.rect[2], t.rect[3] + 20)) if t.row == 0 else t for t in lat.trunks]
    assert any("leaves the row gap" in p for p in mg.lane_violations(replace(lat, trunks=lifted)))


# ------------------------------------------------------------------------------------------------------- compositor

def test_run_is_byte_identical_across_two_runs(tmp_path):
    direction_path = make_direction(tmp_path)
    reports = []
    for i in (1, 2):
        reports.append(mockup.run_direction(mockup.load_direction(direction_path), GEO, tmp_path / f"out{i}", PALETTE))
    assert reports[0]["passed"], [g for g in reports[0]["gates"] if not g["passed"]]
    names = sorted(reports[0]["files"])
    assert names == sorted(reports[1]["files"])
    for n in names:
        a = (tmp_path / "out1" / "dir-test" / n).read_bytes()
        b = (tmp_path / "out2" / "dir-test" / n).read_bytes()
        assert a == b, n
    assert {"strip-machine-loop.gif", "strip-machine-loop.apng", "strip-branch-pip.gif", "strip-completion-pop.apng", "still-reduced-motion.png", "triptych.png",
            "phone-portrait.png", "phone-portrait-hub.png", "phone-portrait-text200.png", "report.json"} <= set(names) | {"report.json"}


def test_sheet_sizes_and_strip_frames(tmp_path):
    mockup.run_direction(mockup.load_direction(make_direction(tmp_path)), GEO, tmp_path / "out", PALETTE)
    out = tmp_path / "out" / "dir-test"
    sizes = {s: Image.open(out / f"{s}.png").size for s, _, _ in mockup.SHEETS}
    assert sizes == {"phone-portrait": (1080, 2400), "phone-landscape": (2400, 1080), "phone-landscape-640x360-200": (1280, 720), "tablet-portrait": (1536, 2048)}
    for ext in ("gif", "apng"):
        for strip in ("machine-loop", "branch-pip", "completion-pop"):
            im = Image.open(out / f"strip-{strip}.{ext}")
            assert getattr(im, "n_frames", 1) == mockup.FRAMES


def test_sprites_land_on_exact_device_pixels(tmp_path):
    direction = mockup.load_direction(make_direction(tmp_path))
    row = next(r for r in GEO["devices"] if r["name"] == "phone-portrait 1080x2400 420dpi")
    lat = mockup.sheet_lattice(row, GEO, direction)
    img, tf = mockup.render_scene(row, lat, direction, PALETTE, frame=0)
    n = tf.n
    assert n == row["nFit"] and isinstance(n, int)
    # the depot body (copper_1, 20 x 13 art px) covers a whole number of device pixels at the integer scale
    copper = PALETTE.rgb("copper_1") + (255,)
    px = img.load()
    pts = [(x, y) for y in range(img.height) for x in range(img.width) if px[x, y] == copper]
    assert pts, "the depot is at least partly on the sheet"
    x0, x1 = min(p[0] for p in pts), max(p[0] for p in pts)
    y0, y1 = min(p[1] for p in pts), max(p[1] for p in pts)
    assert (x1 - x0 + 1, y1 - y0 + 1) == (20 * n, 13 * n)
    # and it sits where the lattice transform puts it: sprite pivot (40, 15 from the bottom), body 10 px left of it, 36 rows below the top
    dx, dy = tf.pt(*lat.depot.pivot)
    assert x0 == dx - 10 * n
    assert y0 == dy - (64 - 15) * n + 36 * n


def test_framing_keeps_the_extent_visible_and_reports_the_clipped_margin(tmp_path):
    direction = mockup.load_direction(make_direction(tmp_path))
    for _sheet, section, name in mockup.SHEETS:
        row = next(r for r in GEO[section] if r["name"] == name)
        lat = mockup.sheet_lattice(row, GEO, direction)
        info = mockup.framing(row, lat, mockup.make_transform(row, lat))
        assert info["extentVisible"], name
        assert info["n"] == row["nFit"] >= row["nMin"]
        assert 0.0 <= info["depotFraction"] <= 1.0 and 0.0 <= info["depositFraction"] <= 1.0


def test_at_least_eight_machines_in_mixed_states(lattices):
    act = mockup.activities(lattices["3x4"])
    assert len(act) >= 8 and len(set(act.values())) >= 3 and "running" in act.values()


def test_reduced_motion_has_no_pips_and_normal_has(tmp_path):
    direction = mockup.load_direction(make_direction(tmp_path))
    row = next(r for r in GEO["devices"] if r["name"] == "phone-portrait 1080x2400 420dpi")
    lat = mockup.sheet_lattice(row, GEO, direction)
    gold = PALETTE.rgb("res_gold") + (255,)

    def count(img):
        px = img.load()
        return sum(1 for y in range(img.height) for x in range(img.width) if px[x, y] == gold)

    moving, _ = mockup.render_scene(row, lat, direction, PALETTE, frame=2)
    still, _ = mockup.render_scene(row, lat, direction, PALETTE, reduced=True)
    assert count(moving) > 0 and count(still) == 0
    teal = PALETTE.rgb("teal_1") + (255,)
    assert any(still.getpixel((x, y)) == teal for y in range(0, still.height, 2) for x in range(0, still.width, 2)), "static active branch tile"


def test_gates_run_on_the_inputs(tmp_path):
    # an off-palette sprite fails the palette gate
    path = make_direction(tmp_path, "bad-palette")
    sprite = tmp_path / "bad-palette" / "depot.png"
    _save(sprite, Image.new("RGBA", (80, 64), (1, 2, 3, 255)))
    report = mockup.run_direction(mockup.load_direction(path), GEO, tmp_path / "out", PALETTE)
    assert not report["passed"]
    assert any(g["name"] == "palette" and not g["passed"] and g["role"] == "depot" for g in report["gates"])
    # a flashing machine loop fails the flash gate (frames alternate between black-ish and white-ish)
    flash = [("ink_0" if i % 2 == 0 else "steel_3") for i in range(6)]
    path = make_direction(tmp_path, "flashy", machine_colors=flash)
    report = mockup.run_direction(mockup.load_direction(path), GEO, tmp_path / "out", PALETTE)
    assert any(g["name"] == "flash" and not g["passed"] for g in report["gates"])


def test_direction_requires_the_core_roles(tmp_path):
    path = make_direction(tmp_path)
    doc = json.loads(path.read_text())
    del doc["sprites"]["machine"]
    path.write_text(json.dumps(doc))
    with pytest.raises(ValueError):
        mockup.load_direction(path)


def test_cli_returns_the_gate_status(tmp_path):
    ok = make_direction(tmp_path, "cli-ok")
    assert cli_main(["mockup", "--direction", str(ok), "--out", str(tmp_path / "o")]) == 0
    bad = make_direction(tmp_path, "cli-bad")
    _save(tmp_path / "cli-bad" / "depot.png", Image.new("RGBA", (80, 64), (1, 2, 3, 255)))
    assert cli_main(["mockup", "--direction", str(bad), "--out", str(tmp_path / "o")]) == 1


# ------------------------------------------------------------------------------------------------------- T507 additions

def make_decor_direction(tmp_path, name="dir-decor"):
    """The placeholder direction plus the optional T507 roles: edge, two props and a selection frame."""
    path = make_direction(tmp_path, name)
    d = path.parent
    _save(d / "edge.png", _diamond((70, 66), "steel_1"))
    _save(d / "prop.png", _box((30, 36), "copper_1"))
    _save(d / "sel.png", _plate("lit_1"))
    doc = json.loads(path.read_text())
    doc["sprites"].update({"edge": {"file": "edge.png", "pivot": [35, 34]}, "prop_1": {"file": "prop.png", "pivot": [15, 7]},
                           "prop_2": {"file": "prop.png", "pivot": [15, 7]}, "selection": "sel.png"})
    doc["stages"] = [{}, {"edge": {"file": "edge.png", "pivot": [35, 34]}}, {}]
    doc["stageLabels"] = ["early", "partial", "established"]
    path.write_text(json.dumps(doc))
    return path


@pytest.mark.parametrize("name", list(mg.TEST_LAYOUTS))
def test_edge_and_props_never_stand_on_a_location_or_hit_diamond(lattices, name):
    lat = lattices[name]
    decor = mg.decor_cells(lat)
    assert mg.decor_violations(lat, decor) == []
    assert decor["prop_1"] is not None and decor["prop_2"] is not None and decor["edge"]
    assert all(c.kind == "margin" for c in decor["edge"] + [decor["prop_1"], decor["prop_2"]])
    # the free margin cells are all used: edge + props + depot + deposit = the whole ring
    ring = sum(c.kind == "margin" for c in lat.cells)
    assert len(decor["edge"]) + 2 + 2 == ring


def test_decor_violation_detection(lattices):
    lat = lattices["3x4"]
    decor = mg.decor_cells(lat)
    on_location = dict(decor, prop_1=lat.location(1, 1))
    problems = mg.decor_violations(lat, on_location)
    assert any("location" in p for p in problems) and any("plate" in p for p in problems)
    on_depot = dict(decor, prop_2=lat.depot)
    assert any("depot" in p for p in mg.decor_violations(lat, on_depot))


def test_decor_run_is_deterministic_and_the_edge_shows(tmp_path):
    path = make_decor_direction(tmp_path)
    reports = [mockup.run_direction(mockup.load_direction(path), GEO, tmp_path / f"o{i}", PALETTE) for i in (1, 2)]
    assert reports[0]["passed"], [g for g in reports[0]["gates"] if not g["passed"]]
    for n in reports[0]["files"]:
        assert (tmp_path / "o1" / "dir-decor" / n).read_bytes() == (tmp_path / "o2" / "dir-decor" / n).read_bytes(), n
    plain = mockup.run_direction(mockup.load_direction(make_direction(tmp_path, "dir-plain")), GEO, tmp_path / "o3", PALETTE)
    assert reports[0]["files"]["phone-landscape.png"] != plain["files"]["phone-landscape.png"]
    assert all(not v for v in reports[0]["decorViolations"].values())
    # the three stage panels differ and carry the stage footer
    tri = Image.open(tmp_path / "o1" / "dir-decor" / "triptych.png")
    third = tri.width // 3
    assert Image.open(tmp_path / "o1" / "dir-decor" / "triptych.png").crop((0, 0, third, tri.height)).tobytes() != tri.crop((third, 0, 2 * third, tri.height)).tobytes()


def test_docked_card_stays_in_the_safe_area_and_never_covers_the_selected_machine(tmp_path):
    direction = mockup.load_direction(make_decor_direction(tmp_path))
    report = mockup.run_direction(direction, GEO, tmp_path / "o", PALETTE)
    for sheet, section, name in mockup.SHEETS:
        row = next(r for r in GEO[section] if r["name"] == name)
        lat = mockup.sheet_lattice(row, GEO, direction)
        sel = mockup.selected_machine(sheet, lat)
        for suffix in ("", "-hub", "-text200"):
            key = sheet + suffix
            plan, frame = report["card"][key], report["framing"][key]
            tf = mockup.Transform(frame["n"], *frame["originPx"])
            x, y, w, h = plan["cardPx"]
            assert x >= 0 and y >= 0 and x + w <= row["widthPx"] and y + h <= row["heightPx"], key
            boxes = mockup.occupied_boxes(lat, tf)
            selbox = boxes[f"machine ({sel[0]},{sel[1]})"]
            assert not mockup._overlap((x, y, x + w, y + h), selbox), key
            assert f"machine ({sel[0]},{sel[1]})" not in plan["covers"], key
            # the report lists exactly what the card covers
            listed = sorted(k for k, b in boxes.items() if mockup._overlap((x, y, x + w, y + h), b))
            if plan["position"] != "side-panel":
                assert listed == plan["covers"], key
            else:
                assert plan["covers"] == []
            assert frame["extentVisible"], key


def test_card_text_wraps_inside_the_card_and_grows_with_the_text_scale():
    ppp = 2.625
    small = mockup.card_metrics(308, *mockup.card_text("running"), ppp, 1.0)
    big = mockup.card_metrics(308, *mockup.card_text("running"), ppp, 2.0)
    assert big["h"] > small["h"] and big["bodyH"] > small["bodyH"]
    for scale, card in ((1.0, small), (2.0, big)):
        card = dict(card, x=10, y=100)
        items = [i for i in mockup.ui_layout("CompactLandscape", 914, 411, False, "Works", "Meridian Works", card=card, text_scale=scale, ppp=ppp) if i.get("role") == "card"]
        frame = next(i for i in items if i["kind"] == "rect")
        for it in items:
            assert it["y"] + it["h"] <= frame["y"] + frame["h"] + 0.01, (scale, it["text"] if "text" in it else it)
        # every wrapped line of the body fits its box width
        body = next(i for i in items if i["kind"] == "text" and i["text"] == card["body"])
        font = mockup._font(mockup.FONT_PT["body"] * ppp * scale)
        for line in mockup._wrap(mockup._measure_draw(), body["text"], font, round(body["w"] * ppp) - 2):
            assert mockup._measure_draw().textlength(line, font=font) <= body["w"] * ppp
        assert round(body["h"] * ppp) >= len(mockup._wrap(mockup._measure_draw(), body["text"], font, round(body["w"] * ppp) - 2)) * font.size * mockup.LINE - 1


def test_header_separates_the_site_name_from_the_credits():
    items = mockup.ui_layout("CompactLandscape", 914, 411, False, "Works", "Meridian Works", ppp=2.625)
    texts = [i for i in items if i["kind"] == "text" and i["y"] < 48 and i["text"] in ("Meridian Works", "Credits 12,480")]
    assert [t["text"] for t in texts] == ["Meridian Works", "Credits 12,480"]
    assert texts[0]["x"] + texts[0]["w"] <= texts[1]["x"] + 0.01 and texts[1].get("align") == "right"
    stacked = [i for i in mockup.ui_layout("CompactPortrait", 411, 914, False, "Works", "Meridian Works", text_scale=2.0, ppp=2.625) if i["kind"] == "text" and i["text"] in ("Meridian Works", "Credits 12,480")]
    assert stacked[1]["y"] >= stacked[0]["y"] + 24


def test_selection_frame_falls_back_to_a_palette_outline(tmp_path):
    direction = mockup.load_direction(make_direction(tmp_path))   # no `selection` role
    row = next(r for r in GEO["devices"] if r["name"] == "phone-portrait 1080x2400 420dpi")
    lat = mockup.sheet_lattice(row, GEO, direction)
    plain, tf = mockup.render_scene(row, lat, direction, PALETTE, frame=1)
    framed, _ = mockup.render_scene(row, lat, direction, PALETTE, frame=1, selected=(3, 0), tf=tf)
    assert plain.tobytes() != framed.tobytes()
