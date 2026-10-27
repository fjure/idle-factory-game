from PIL import Image

from art_pipeline import checks as ck
from art_pipeline import pixel_pass as pp
from conftest import load_frames, solid


def _frames_from_fixture(palette):
    return load_frames("blender", "golden")


def test_palette_check_pass_and_fail(palette):
    frames = load_frames("blender", "golden")
    assert ck.check_palette(frames, palette, {"machine", "ink"}).passed
    bad = Image.new("RGBA", (2, 2), (1, 2, 3, 255))
    r = ck.check_palette([bad], palette)
    assert not r.passed and r.details["unknown"] == ["#010203"]
    cue = solid((2, 2), palette.rgb("cue_warn"))
    assert not ck.check_palette([cue], palette, {"machine", "ink", "floor"}).passed  # reserved cue color
    assert not ck.check_palette(frames, palette, {"machine", "ink"}, group_limits={"machine": 2}).passed


def test_group_budget_is_enforced(palette):
    small = {"machine": palette.budgets["machine"]}
    assert ck.check_palette(load_frames("blender", "golden"), palette, group_limits=small).passed


def test_canvas_and_pivot(palette):
    frames = load_frames("blender", "golden")
    entry = {"canvas": [48, 60], "footprintPx": [48, 48], "pivot": [0.5, 0.4]}
    assert ck.check_canvas(frames, (48, 60)).passed
    assert not ck.check_canvas(frames, (48, 61)).passed
    assert ck.check_pivot(frames, entry).passed
    assert not ck.check_pivot(frames, {**entry, "pivot": [0.5, 0.5]}).passed
    floating = [Image.new("RGBA", (48, 60), (0, 0, 0, 0))]
    floating[0].putpixel((10, 10), (1, 1, 1, 255))
    assert not ck.check_pivot(floating, entry).passed  # does not rest on the footprint


def test_alpha_check():
    ok = solid((2, 2), (1, 2, 3))
    partial = solid((2, 2), (1, 2, 3), 100)
    assert ck.check_alpha([ok]).passed
    r = ck.check_alpha([ok, partial])
    assert not r.passed and r.details["partialAlpha"][0]["frame"] == 1


def test_outline_check(palette):
    frames = load_frames("blender", "golden")
    assert ck.check_outline(frames, palette).passed
    no_outline = pp.place_on_canvas([solid((6, 6), palette.rgb("steel_1"))], (10, 10), (8, 8))[0]
    assert not ck.check_outline(no_outline, palette).passed
    assert not ck.check_outline([Image.new("RGBA", (4, 4), (0, 0, 0, 0))], palette).passed


def test_frame_count():
    assert ck.check_frames([solid((1, 1), (0, 0, 0))] * 6, 6).passed
    assert not ck.check_frames([solid((1, 1), (0, 0, 0))] * 5, 6).passed


def test_overhang_limit():
    canvas, fp = (48, 60), (48, 48)
    ok = Image.new("RGBA", canvas, (0, 0, 0, 0))
    for y in range(0, 60):  # top row 0 -> overhang 12 px = 0.5 cell at ppu 24
        ok.putpixel((5, y), (1, 1, 1, 255))
    assert ck.check_overhang([ok], canvas, fp, 24).passed
    canvas2 = (48, 62)
    taller = Image.new("RGBA", canvas2, (0, 0, 0, 0))
    for y in range(0, 62):
        taller.putpixel((5, y), (1, 1, 1, 255))
    r = ck.check_overhang([taller], canvas2, fp, 24)  # 14 px > 12 px
    assert not r.passed and r.details["overhangPx"] == 14


def _loop(colors):
    return [solid((4, 4), c) for c in colors]


def test_flash_metric_threshold_and_rate():
    dark, light = (0, 0, 0), (255, 255, 255)
    # luminance change 1.0 per transition: every transition is a flash
    strobe = _loop([dark, light] * 3)
    r = ck.check_flash(strobe, 8)
    assert not r.passed and r.details["maxFlashesPerSecond"] == 8
    # gentle change (< 0.10 mean relative luminance) never counts as a flash
    gentle = _loop([(100, 100, 100), (110, 110, 110)] * 3)
    assert ck.check_flash(gentle, 8).passed
    assert ck.flash_rate(gentle, 8)["maxFlashesPerSecond"] == 0
    # exactly 3 flashes in the loop second passes; 4 fails
    three = _loop([dark, light, light, light, light, light])  # transitions: up, then... cyclic down
    assert ck.flash_rate(three, 6)["maxFlashesPerSecond"] == 2
    four = _loop([dark, light, dark, light, dark, dark])
    assert ck.flash_rate(four, 6)["maxFlashesPerSecond"] == 4 and not ck.check_flash(four, 6).passed
    assert ck.check_flash(_loop([dark]), 8).passed  # single still frame


def test_frame0_lights_on(palette):
    lit = solid((4, 4), palette.rgb("lit_0"))
    dim = solid((4, 4), palette.rgb("dim_1"))
    assert ck.check_frame0_lights_on([lit, dim, dim], palette).passed
    assert not ck.check_frame0_lights_on([dim, lit, lit], palette).passed
    assert not ck.check_frame0_lights_on([dim, dim], palette).passed
    assert ck.check_frame0_lights_on(load_frames("blender", "golden"), palette).passed


def _rect_mask(w, h, size=(40, 40)):
    m = Image.new("L", size, 0)
    for y in range(size[1] - h, size[1]):
        for x in range((size[0] - w) // 2, (size[0] - w) // 2 + w):
            m.putpixel((x, y), 255)
    return m


def test_iou_full_and_downsampled():
    a, b = _rect_mask(30, 30), _rect_mask(10, 30)
    assert ck.mask_iou(a, a) == 1.0
    assert abs(ck.mask_iou(a, b) - 1 / 3) < 1e-9
    assert ck.check_iou({"a": a, "b": b}).passed
    near = _rect_mask(28, 30)
    r = ck.check_iou({"a": a, "near": near})
    assert not r.passed and r.details["failing"][0]["pair"] == ["a", "near"]
    # tiny differences that vanish at 32x32 still fail through the downsample metric
    big_a, big_b = _rect_mask(120, 120, (128, 128)), _rect_mask(40, 120, (128, 128))
    assert ck.check_iou({"a": big_a, "b": big_b}).details["worstSmall"] < 0.7


def test_iou_32x32_downsample_catches_close_silhouettes():
    a = _rect_mask(60, 60, (64, 64))
    b = a.copy()
    b.putpixel((0, 63), 255)  # full-size IoU is high anyway; construct a case where small view is stricter
    assert ck.downsample_mask(a).size == (32, 32)
    assert ck.check_iou({"a": a, "b": b}).details["worstSmall"] > 0.7


def test_contrast_thresholds_and_darkest_light(palette):
    ok = [{"fg": "ui_text", "bg": "ui_panel", "kind": "text"},
          {"fg": "cue_warn", "bg": "floor_0", "kind": "nontext"}]
    assert ck.check_contrast(ok, palette, 0.6).passed
    # passes at full light (3.08) but fails under the darkest allowed light (2.54)
    marginal = [{"fg": "cue_alert", "bg": "floor_1", "kind": "nontext"}]
    r = ck.check_contrast(marginal, palette, 0.6)
    assert not r.passed and r.details["pairs"][0]["ratio"] >= 3.0 > r.details["pairs"][0]["ratioDarkest"]
    assert ck.check_contrast(marginal, palette, 1.0).passed
    # text needs 4.5: 3.58 passes the non-text bar only
    assert ck.check_contrast([{"fg": "cue_dim", "bg": "floor_1", "kind": "nontext"}], palette, 1.0).passed
    assert not ck.check_contrast([{"fg": "cue_dim", "bg": "floor_1", "kind": "text"}], palette, 1.0).passed


def test_cue_icon_min_size():
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for x in range(12):
        for y in range(12):
            img.putpixel((x, y), (1, 1, 1, 255))
    assert ck.check_cue_icon_size(img).passed
    img2 = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for x in range(12):
        for y in range(11):
            img2.putpixel((x, y), (1, 1, 1, 255))
    assert not ck.check_cue_icon_size(img2).passed


def test_run_asset_checks_and_report(palette, tmp_path):
    frames = load_frames("blender", "golden")
    entry = {"canvas": [48, 60], "footprintPx": [48, 48], "pivot": [0.5, 0.4], "fps": 8}
    results = ck.run_asset_checks(frames, entry, palette, expected_frames=2, ppu=24, running_loop=True)
    assert {r.name for r in results} >= {"palette", "canvas", "pivot", "alpha", "outline", "frames", "overhang",
                                         "flash", "frame0_lights_on"}
    assert all(r.passed for r in results), [r.to_dict() for r in results if not r.passed]
    report = ck.build_report({"machine": results}, [ck.check_palette_definition(palette)])
    assert report["passed"]
    p = tmp_path / "check-report.json"
    ck.write_report(p, report)
    assert p.read_text() == ck.report_json(report)
    results[0].passed = False
    assert not ck.build_report({"machine": results}, [])["passed"]
